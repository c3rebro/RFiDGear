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

    /// <summary>The result of <see cref="ClassicOperations.WriteBlockAsync(int, byte[], ClassicSectorKeys, ClassicWriteOptions)"/> and <see cref="ClassicOperations.WriteSectorAsync(int, byte[], ClassicSectorKeys, ClassicWriteOptions)"/>.</summary>
    public sealed class ClassicWriteResult
    {
        internal ClassicWriteResult(ERROR error, ClassicWriteViolation violation, int blocksWritten, int? failedBlock)
        {
            Error = error;
            Violation = violation;
            BlocksWritten = blocksWritten;
            FailedBlock = failedBlock;
        }

        /// <summary>
        /// Gets how it went: <see cref="ERROR.NoError"/> when every block was written and read back as written, <see cref="ERROR.ProtocolConstraint"/>
        /// for a request that was refused before the chip (see <see cref="Violation"/>), <see cref="ERROR.PermissionDenied"/> when no key that there is
        /// may write a block (nothing was written), <see cref="ERROR.AuthFailure"/> when no key logged in or the chip refused the write,
        /// <see cref="ERROR.TransportError"/> when the chip went away, and <see cref="ERROR.Unknown"/> when a block did not read back as it was written.
        /// </summary>
        public ERROR Error { get; }

        /// <summary>Gets why the request was refused before the chip, or <see cref="ClassicWriteViolation.None"/>.</summary>
        public ClassicWriteViolation Violation { get; }

        /// <summary>Gets how many blocks were written and read back as written (a sector write that stopped has written the blocks before the one that failed).</summary>
        public int BlocksWritten { get; }

        /// <summary>Gets the block that ended the call, or <c>null</c> when nothing failed or the request was refused before the chip.</summary>
        public int? FailedBlock { get; }

        /// <summary>Gets a value that says whether everything that was asked for was written.</summary>
        public bool Succeeded => Error == ERROR.NoError;

        internal static ClassicWriteResult Success(int blocksWritten) =>
            new ClassicWriteResult(ERROR.NoError, ClassicWriteViolation.None, blocksWritten, null);

        internal static ClassicWriteResult Refused(ClassicWriteViolation violation) =>
            new ClassicWriteResult(ERROR.ProtocolConstraint, violation, 0, null);

        internal static ClassicWriteResult Failed(ERROR error, int block, int blocksWritten) =>
            new ClassicWriteResult(error, ClassicWriteViolation.None, blocksWritten, block);
    }

    /// <summary>
    /// The operations on a MIFARE Classic chip, once, for every reader: they use only the four primitives of <see cref="IClassicPort"/>
    /// and decide everything else here. A login is only believed after the first access (a PC/SC reader says yes to a key that the chip
    /// refuses), a chip that failed is woken before the next try, and the access bits of the sector decide which key is used for which
    /// block, so that a block that a key may not read or write is not asked for (every refusal halts the chip).
    /// <para>
    /// A write makes one attempt per block and never repeats it: a chip that is taken away in the middle of a write must not be written twice.
    /// What may be written at all is decided first by <see cref="ClassicWritePolicy"/>, without the chip, and every block is read back.
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

        /// <summary>
        /// Writes one block (one attempt, never repeated) and reads it back. The key is the one that the access bits of the sector let write
        /// it (key A when both may); a trailer needs a key that may write every part of it that changes: key A is never readable, so it counts as
        /// changed, the access bits and key B are compared with what the chip shows (key B is not shown to a key that may not read it, and counts as
        /// changed then). Nothing is written when the options do not allow the block, when no key that there is may write it, or when the sector is unreadable.
        /// </summary>
        /// <param name="block">The block, as the chip numbers it.</param>
        /// <param name="data">The 16 bytes.</param>
        /// <param name="keys">The keys that there are.</param>
        /// <param name="options">What may be written (data blocks only unless the options say more).</param>
        /// <returns>The result; see <see cref="ClassicWriteResult.Error"/> for how it went.</returns>
        public async Task<ClassicWriteResult> WriteBlockAsync(int block, byte[] data, ClassicSectorKeys keys, ClassicWriteOptions options)
        {
            ArgumentNullException.ThrowIfNull(keys);
            ArgumentNullException.ThrowIfNull(options);

            // The bytes are copied before they are checked: a caller's array can change while the call waits for the reader (the editor of a
            // dialog does), and what was approved is what is written and read back.
            var approved = data?.ToArray();
            var decision = ClassicWritePolicy.CheckBlock(Size, block, approved, options);

            if (!decision.IsAllowed)
            {
                return ClassicWriteResult.Refused(decision.Violation);
            }

            var snapshot = approved!;
            var state = new SectorState(ClassicLayout.SectorOf(block), keys);
            var opened = await OpenFirstSessionAsync(state);

            if (opened != ERROR.NoError)
            {
                return ClassicWriteResult.Failed(opened, block, 0);
            }

            if (state.Bits == null)
            {
                return ClassicWriteResult.Refused(ClassicWriteViolation.AccessBitsUnknown);
            }

            if (!state.TryChooseWriteKey(block, snapshot, out var key))
            {
                return ClassicWriteResult.Failed(ERROR.PermissionDenied, block, 0);
            }

            return await WriteOneAsync(state, block, snapshot, key, 0);
        }

        /// <summary>
        /// Writes the data blocks of one sector, in order (never the trailer, and block 0 only when the options allow it), one attempt each,
        /// every block read back. Before the first block is written it is checked that a key that there is may write every block: if one
        /// cannot be written, none is. The first block that fails ends the call, and the blocks after it are not touched.
        /// </summary>
        /// <param name="sector">The sector number.</param>
        /// <param name="data">The bytes of the blocks to write (see <see cref="ClassicWritePolicy.SectorDataLength(int, ClassicWriteOptions)"/>).</param>
        /// <param name="keys">The keys that there are.</param>
        /// <param name="options">What may be written.</param>
        /// <returns>The result; <see cref="ClassicWriteResult.BlocksWritten"/> says how far it came.</returns>
        public async Task<ClassicWriteResult> WriteSectorAsync(int sector, byte[] data, ClassicSectorKeys keys, ClassicWriteOptions options)
        {
            ArgumentNullException.ThrowIfNull(keys);
            ArgumentNullException.ThrowIfNull(options);

            // Copied before it is checked, as in WriteBlockAsync.
            var approved = data?.ToArray();
            var decision = ClassicWritePolicy.CheckSector(Size, sector, approved, options);

            if (!decision.IsAllowed)
            {
                return ClassicWriteResult.Refused(decision.Violation);
            }

            var snapshot = approved!;
            var state = new SectorState(sector, keys);
            var opened = await OpenFirstSessionAsync(state);

            if (opened != ERROR.NoError)
            {
                return ClassicWriteResult.Failed(opened, ClassicLayout.FirstBlock(sector), 0);
            }

            if (state.Bits == null)
            {
                return ClassicWriteResult.Refused(ClassicWriteViolation.AccessBitsUnknown);
            }

            var plan = state.PlanDataWrites(sector == 0 && !options.AllowManufacturerBlock ? 1 : 0);

            if (plan == null)
            {
                return ClassicWriteResult.Failed(ERROR.PermissionDenied, ClassicLayout.FirstBlock(sector), 0);
            }

            return await WritePlanAsync(state, plan, snapshot);
        }

        private async Task<ClassicWriteResult> WritePlanAsync(SectorState state, IReadOnlyList<(int Block, ClassicKeyType Key)> plan, byte[] data)
        {
            var written = 0;

            foreach (var (block, key) in plan)
            {
                var chunk = data.AsSpan(written * ClassicLayout.BytesPerBlock, ClassicLayout.BytesPerBlock).ToArray();
                var result = await WriteOneAsync(state, block, chunk, key, written);

                if (!result.Succeeded)
                {
                    return result;
                }

                written = result.BlocksWritten;
            }

            return ClassicWriteResult.Success(written);
        }

        // One block: log in with the key that may write it (when it is not the key that is logged in), one write, a read back. A refused write
        // is looked at once (is the chip still there?) and never repeated.
        private async Task<ClassicWriteResult> WriteOneAsync(SectorState state, int block, byte[] data, ClassicKeyType key, int writtenBefore)
        {
            var switched = await SwitchKeyAsync(state, key);

            if (switched != ERROR.NoError)
            {
                return ClassicWriteResult.Failed(switched, block, writtenBefore);
            }

            var wrote = await port.WriteBlockAsync(block, data);

            if (!wrote.Succeeded)
            {
                return ClassicWriteResult.Failed(await ClassifyRefusedWriteAsync(wrote.Error), block, writtenBefore);
            }

            var verified = await VerifyWrittenAsync(state, block, data);

            return verified == ERROR.NoError
                ? ClassicWriteResult.Success(writtenBefore + 1)
                : ClassicWriteResult.Failed(verified, block, writtenBefore);
        }

        // The chip awake and logged in with the key that is needed: nothing to do when it is, a login (proved by the trailer) when it is not.
        private async Task<ERROR> SwitchKeyAsync(SectorState state, ClassicKeyType key)
        {
            if (key == state.LoggedIn)
            {
                return ERROR.NoError;
            }

            var opened = await OpenSessionAsync(state, key, wakeFirst: false);

            if (opened.Error == ERROR.TransportError)
            {
                return ERROR.TransportError;
            }

            if (!opened.Succeeded)
            {
                return ERROR.AuthFailure;
            }

            state.LoggedIn = key;

            return ERROR.NoError;
        }

        // A write that the chip refused: a look at the chip, which is the one thing that is done about it (it is no second attempt). A chip that
        // is gone is a transport error and not a refusal.
        private async Task<ERROR> ClassifyRefusedWriteAsync(ERROR refused)
        {
            if (refused == ERROR.TransportError)
            {
                return ERROR.TransportError;
            }

            var look = await port.ReselectAsync();

            return look.Error == ERROR.TransportError ? ERROR.TransportError : ERROR.AuthFailure;
        }

        // A data block reads back as it was written. A trailer is read back with the key A that was written (the old keys are gone): its access
        // bits and general purpose byte must read back as written.
        private async Task<ERROR> VerifyWrittenAsync(SectorState state, int block, byte[] data)
        {
            if (ClassicLayout.IsTrailer(block))
            {
                var login = await port.LoginAsync(state.First, ClassicKeyType.KeyA, data.AsSpan(0, ClassicKey.Length).ToArray());

                if (login.Error == ERROR.TransportError)
                {
                    return ERROR.TransportError;
                }

                if (!login.Succeeded)
                {
                    return ERROR.Unknown;
                }

                state.LoggedIn = ClassicKeyType.KeyA;
            }

            var read = await port.ReadBlockAsync(block);

            if (read.Error == ERROR.TransportError)
            {
                return ERROR.TransportError;
            }

            if (!read.Succeeded)
            {
                return ERROR.Unknown;
            }

            return ReadsBackAsWritten(block, data, read.Data!) ? ERROR.NoError : ERROR.Unknown;
        }

        // A data block reads back whole. A trailer reads back its access bits and its general purpose byte, and its key B when the access bits that
        // were written let key A read it (key A itself is never shown, and a key B that is hidden reads as zeros: there is nothing to compare).
        private static bool ReadsBackAsWritten(int block, byte[] written, byte[] read)
        {
            if (!ClassicLayout.IsTrailer(block))
            {
                return read.AsSpan(0, ClassicLayout.BytesPerBlock).SequenceEqual(written.AsSpan(0, ClassicLayout.BytesPerBlock));
            }

            var comparedLength = KeyBIsShown(written) ? ClassicLayout.BytesPerBlock : ClassicKey.Length + 4;

            return read.AsSpan(ClassicKey.Length, comparedLength - ClassicKey.Length).SequenceEqual(written.AsSpan(ClassicKey.Length, comparedLength - ClassicKey.Length));
        }

        private static bool KeyBIsShown(byte[] trailer) =>
            ClassicAccessBits.TryFromTrailer(trailer, out var bits) && bits.TrailerRights.KeyBRead.Allows(ClassicKeyType.KeyA);

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

                // The chip refused this key: a write does not try it again with another block.
                state.Reject(type);
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
            private readonly HashSet<ClassicKeyType> rejected = new HashSet<ClassicKeyType>();

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

            // The key that logged in first, and the key that is logged in now (a write may switch keys from block to block).
            public ClassicKeyType Session { get; private set; }

            public ClassicKeyType LoggedIn { get; set; }

            public void StartSession(ClassicKeyType type, byte[] trailer)
            {
                Session = type;
                LoggedIn = type;
                data[^1] = trailer;
                Bits = ClassicAccessBits.TryFromTrailer(trailer, out var parsed) ? parsed : null;
            }

            // A key that the chip refused when the session was opened is not a key to write with, even if its access bits would allow it: the
            // key that logged in is.
            public void Reject(ClassicKeyType type) => rejected.Add(type);

            // The key that may write a block, if one of the keys that there are may. For a trailer the rights of every part that changes are needed.
            public bool TryChooseWriteKey(int block, byte[] newData, out ClassicKeyType key) =>
                Bits!.TryChooseKey(WriteRights(block, newData), Usable(ClassicKeyType.KeyA), Usable(ClassicKeyType.KeyB), out key);

            /// <summary>Plans the write of the data blocks of the sector: each block, from the given position on and without the trailer, with the key that may write it.</summary>
            /// <param name="firstPosition">The position in the sector of the first block (1 skips block 0 of sector 0).</param>
            /// <returns>The plan, or <c>null</c> when a key that there is cannot write one of the blocks.</returns>
            public List<(int Block, ClassicKeyType Key)>? PlanDataWrites(int firstPosition)
            {
                var plan = new List<(int Block, ClassicKeyType Key)>();

                for (var block = First + firstPosition; block < TrailerBlock; block++)
                {
                    if (!TryChooseWriteKey(block, Array.Empty<byte>(), out var key))
                    {
                        return null;
                    }

                    plan.Add((block, key));
                }

                return plan;
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

            private ClassicKeyAccess WriteRights(int block, byte[] newData)
            {
                if (!ClassicLayout.IsTrailer(block))
                {
                    return Bits!.GetDataBlockRights(ClassicLayout.AccessGroup(block)).Write;
                }

                var rights = Bits!.TrailerRights;
                var current = data[^1]!;

                // Key A is never readable, so it counts as changed. The access bits and key B count as changed when they differ from what the
                // chip showed; key B is only shown to a key that may read it, and counts as changed when it was not shown.
                var needed = rights.KeyAWrite;

                if (!current.AsSpan(6, 4).SequenceEqual(newData.AsSpan(6, 4)))
                {
                    needed &= rights.AccessBitsWrite;
                }

                if (!rights.KeyBRead.Allows(Session) || !current.AsSpan(10, 6).SequenceEqual(newData.AsSpan(10, 6)))
                {
                    needed &= rights.KeyBWrite;
                }

                return needed;
            }

            private bool Usable(ClassicKeyType type) => Keys.Has(type) && !rejected.Contains(type);

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
