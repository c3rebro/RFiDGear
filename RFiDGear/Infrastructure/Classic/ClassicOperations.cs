#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RFiDGear.Infrastructure.Classic
{
    /// <summary>One block of a sector as it was read.</summary>
    public sealed class ClassicBlockRead
    {
        internal ClassicBlockRead(int block, int blockInSector, byte[]? data)
        {
            Block = block;
            BlockInSector = blockInSector;
            Data = data;
        }

        /// <summary>Gets the number of the block on the chip.</summary>
        public int Block { get; }

        /// <summary>Gets the position of the block in its sector (0 is the first).</summary>
        public int BlockInSector { get; }

        /// <summary>Gets the 16 bytes that were read, or <c>null</c> when the block could not be read with the keys that there were. For a trailer, key A is read as zeros and key B only when the access bits let a key read it.</summary>
        public byte[]? Data { get; }

        /// <summary>Gets a value that says whether the block was read.</summary>
        public bool WasRead => Data != null;
    }

    /// <summary>The result of <see cref="ClassicOperations.ReadSectorAsync(int, ClassicSectorKeys)"/>.</summary>
    public sealed class ClassicSectorRead
    {
        internal ClassicSectorRead(
            int sector,
            ERROR error,
            IReadOnlyList<ClassicBlockRead> blocks,
            ClassicAccessBits? accessBits,
            ClassicKeyType? loginKey)
        {
            Sector = sector;
            Error = error;
            Blocks = blocks;
            AccessBits = accessBits;
            LoginKey = loginKey;
        }

        /// <summary>Gets the sector number.</summary>
        public int Sector { get; }

        /// <summary>
        /// Gets how it went: <see cref="ERROR.NoError"/> when a key logged in and the trailer was read (single blocks may still be
        /// unreadable, see <see cref="ClassicBlockRead.WasRead"/>), <see cref="ERROR.AuthFailure"/> when no key that there was did,
        /// <see cref="ERROR.TransportError"/> when the chip went away, <see cref="ERROR.ProtocolConstraint"/> when the chip has no such sector.
        /// </summary>
        public ERROR Error { get; }

        /// <summary>Gets a value that says whether a key logged in to the sector.</summary>
        public bool IsAuthenticated => Error == ERROR.NoError;

        /// <summary>Gets every block of the sector in order, the trailer last; empty when no key logged in.</summary>
        public IReadOnlyList<ClassicBlockRead> Blocks { get; }

        /// <summary>Gets the access bits of the sector, or <c>null</c> when the trailer could not be read or its bits are not consistent.</summary>
        public ClassicAccessBits? AccessBits { get; }

        /// <summary>Gets the key that logged in first, or <c>null</c> when none did.</summary>
        public ClassicKeyType? LoginKey { get; }

        internal static ClassicSectorRead Failed(int sector, ERROR error) =>
            new ClassicSectorRead(sector, error, Array.Empty<ClassicBlockRead>(), null, null);
    }

    /// <summary>
    /// The operations on a MIFARE Classic chip, once, for every reader: they use only the four primitives of <see cref="IClassicPort"/>
    /// and decide everything else here. A login is only believed after the first access (a PC/SC reader says yes to a key that the chip
    /// refuses), a chip that failed is woken before the next try, and the access bits of the sector decide which key is used for which
    /// block, so that a block that a key may not read is not asked for (every refusal halts the chip).
    /// <para>
    /// This class never writes: the writing operations come with the guards that they need.
    /// </para>
    /// </summary>
    public sealed class ClassicOperations
    {
        private readonly IClassicPort port;

        /// <summary>Creates the operations on a port.</summary>
        /// <param name="port">The reader.</param>
        public ClassicOperations(IClassicPort port)
        {
            this.port = port ?? throw new ArgumentNullException(nameof(port));
        }

        private enum BlockOutcome
        {
            Read,
            Refused,
            SessionLost,
            Gone
        }

        private ClassicCardSize Size => port.Size ?? ClassicCardSize.K4;

        /// <summary>
        /// Reads all blocks of a sector, the trailer included. It tries key A first and then key B, one attempt each, and reads every
        /// block with a key that the access bits allow to read it (so a sector in which key A reads one block and key B another costs a second login).
        /// </summary>
        /// <param name="sector">The sector number.</param>
        /// <param name="keys">The keys that there are.</param>
        /// <returns>The sector; see <see cref="ClassicSectorRead.Error"/> for how it went.</returns>
        public async Task<ClassicSectorRead> ReadSectorAsync(int sector, ClassicSectorKeys keys)
        {
            ArgumentNullException.ThrowIfNull(keys);

            if (!ClassicLayout.HasSector(Size, sector))
            {
                return ClassicSectorRead.Failed(sector, ERROR.ProtocolConstraint);
            }

            var state = new SectorState(sector, keys);

            var opened = await OpenFirstSessionAsync(state);

            if (opened != ERROR.NoError)
            {
                return ClassicSectorRead.Failed(sector, opened);
            }

            foreach (var type in state.PassKeys())
            {
                if (await ReadPassAsync(state, type) == BlockOutcome.Gone)
                {
                    return state.ToResult(ERROR.TransportError);
                }
            }

            return state.ToResult(ERROR.NoError);
        }

        // Key A first, then key B, one attempt each: the first that logs in (and proves it by reading the trailer) is the session.
        private async Task<ERROR> OpenFirstSessionAsync(SectorState state)
        {
            foreach (var type in new[] { ClassicKeyType.KeyA, ClassicKeyType.KeyB }.Where(state.Keys.Has))
            {
                var trailer = await OpenSessionAsync(state, type, wakeFirst: false);

                if (trailer.Error == ERROR.TransportError)
                {
                    return ERROR.TransportError;
                }

                if (trailer.Succeeded)
                {
                    state.StartSession(type, trailer.Data!);

                    return ERROR.NoError;
                }
            }

            return ERROR.AuthFailure;
        }

        // The blocks that one key reads: logs in again when it is not the key that is logged in, then reads one block after the other.
        private async Task<BlockOutcome> ReadPassAsync(SectorState state, ClassicKeyType type)
        {
            var wanted = state.Wanted(type);

            if (wanted.Count == 0)
            {
                return BlockOutcome.Read;
            }

            // The first pass is the key that logged in; the second pass is the other key, and it logs in first.
            if (type != state.Session)
            {
                // The chip is awake here (the reads before this one worked, or the last login that failed was followed by a wake): a wake is a
                // login that is expected to fail, and on a chip that is awake it would halt it.
                var opened = await OpenSessionAsync(state, type, wakeFirst: false);

                if (opened.Error == ERROR.TransportError)
                {
                    return BlockOutcome.Gone;
                }

                if (!opened.Succeeded)
                {
                    return BlockOutcome.Refused;
                }
            }

            foreach (var block in wanted)
            {
                var outcome = await ReadOneAsync(state, type, block);

                if (outcome == BlockOutcome.Gone || outcome == BlockOutcome.SessionLost)
                {
                    return outcome;
                }
            }

            return BlockOutcome.Read;
        }

        private async Task<BlockOutcome> ReadOneAsync(SectorState state, ClassicKeyType type, int block)
        {
            var read = await port.ReadBlockAsync(block);

            if (read.Error == ERROR.TransportError)
            {
                return BlockOutcome.Gone;
            }

            if (read.Succeeded)
            {
                state.Set(block, read.Data!);

                return BlockOutcome.Read;
            }

            // The chip is halted by the refusal: wake it and log in again before the next block.
            var reopened = await OpenSessionAsync(state, type, wakeFirst: true);

            if (reopened.Error == ERROR.TransportError)
            {
                return BlockOutcome.Gone;
            }

            return reopened.Succeeded ? BlockOutcome.Refused : BlockOutcome.SessionLost;
        }

        // The login is proved by reading the trailer: every key that can log in may read it, so the answer is the trailer or a reason.
        private async Task<ClassicBlockResult> OpenSessionAsync(SectorState state, ClassicKeyType type, bool wakeFirst)
        {
            if (wakeFirst && (await port.ReselectAsync()).Error == ERROR.TransportError)
            {
                return new ClassicBlockResult(ERROR.TransportError, null);
            }

            var login = await port.LoginAsync(state.First, type, state.Keys.Get(type));

            if (login.Error == ERROR.TransportError)
            {
                return new ClassicBlockResult(ERROR.TransportError, null);
            }

            if (login.Succeeded)
            {
                var read = await port.ReadBlockAsync(state.TrailerBlock);

                if (read.Succeeded || read.Error == ERROR.TransportError)
                {
                    return read;
                }
            }

            // The key was refused: the chip is halted, and the next attempt must find it awake.
            var reselected = await port.ReselectAsync();

            return new ClassicBlockResult(reselected.Error == ERROR.TransportError ? ERROR.TransportError : ERROR.AuthFailure, null);
        }

        // What is known about the sector while it is read.
        private sealed class SectorState
        {
            private readonly byte[]?[] data;
            private readonly List<int> pending;

            public SectorState(int sector, ClassicSectorKeys keys)
            {
                Sector = sector;
                Keys = keys;
                First = ClassicLayout.FirstBlock(sector);
                TrailerBlock = ClassicLayout.TrailerBlock(sector);
                data = new byte[]?[TrailerBlock - First + 1];
                pending = Enumerable.Range(First, TrailerBlock - First).ToList();
            }

            public int Sector { get; }

            public ClassicSectorKeys Keys { get; }

            public int First { get; }

            public int TrailerBlock { get; }

            public ClassicAccessBits? Bits { get; private set; }

            // The key that logged in first.
            public ClassicKeyType Session { get; private set; }

            public void StartSession(ClassicKeyType type, byte[] trailer)
            {
                Session = type;
                data[^1] = trailer;
                Bits = ClassicAccessBits.TryFromTrailer(trailer, out var parsed) ? parsed : null;
            }

            // The key that logged in reads first; the other one only when there is one.
            public IEnumerable<ClassicKeyType> PassKeys()
            {
                yield return Session;

                var other = Session == ClassicKeyType.KeyA ? ClassicKeyType.KeyB : ClassicKeyType.KeyA;

                if (Keys.Has(other))
                {
                    yield return other;
                }
            }

            // With the access bits known a key reads only what they let it read; without them the key that logged in is tried for every block.
            public List<int> Wanted(ClassicKeyType type) =>
                pending.Where(block => MayRead(block, type)).ToList();

            public void Set(int block, byte[] bytes)
            {
                data[block - First] = bytes;
                pending.Remove(block);
            }

            public ClassicSectorRead ToResult(ERROR error) =>
                new ClassicSectorRead(
                    Sector,
                    error,
                    Enumerable.Range(0, data.Length).Select(i => new ClassicBlockRead(First + i, i, data[i])).ToList(),
                    Bits,
                    Session);

            private bool MayRead(int block, ClassicKeyType type)
            {
                if (Bits == null)
                {
                    return type == Session;
                }

                return Bits.GetDataBlockRights(ClassicLayout.AccessGroup(block)).Read.Allows(type) && Bits.AllowsLoginWith(type);
            }
        }
    }
}
