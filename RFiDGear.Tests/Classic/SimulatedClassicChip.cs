using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Classic;

namespace RFiDGear.Tests.Classic
{
    /// <summary>
    /// A MIFARE Classic chip behind an <see cref="IClassicPort"/>, with the behaviour that a real 4K and a PC/SC reader showed: the access
    /// bits of every sector are enforced as the data sheet says, a login is answered with success and judged at the next access (the reader
    /// does not know whether the chip accepted the key), key B cannot log in while a key may read it, and a refused access halts the chip,
    /// so that the next command fails once unless the chip was woken (<see cref="ReselectAsync"/>) first. It records every call.
    /// </summary>
    internal sealed class SimulatedClassicChip : IClassicPort
    {
        private static readonly byte[] FactoryKey = Enumerable.Repeat((byte)0xFF, 6).ToArray();

        private readonly byte[][] memory;
        private readonly ClassicCardSize size;
        private readonly Dictionary<int, int> readFaults = new Dictionary<int, int>();
        private readonly Dictionary<int, int> readCounts = new Dictionary<int, int>();
        private readonly HashSet<int> writeFaults = new HashSet<int>();

        private int? loggedSector;
        private ClassicKeyType loggedType;
        private bool loginIsGood;
        private bool halted;

        public SimulatedClassicChip(ClassicCardSize size = ClassicCardSize.K4)
        {
            this.size = size;
            memory = new byte[ClassicLayout.BlockCount(size)][];

            for (var block = 0; block < memory.Length; block++)
            {
                memory[block] = new byte[ClassicLayout.BytesPerBlock];
            }

            // Block 0 holds the UID and the manufacturer data: never zero.
            Array.Copy(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x04, 0x08, 0x04, 0x00, 0x62, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69 }, memory[0], 16);

            for (var sector = 0; sector < ClassicLayout.SectorCount(size); sector++)
            {
                SetTrailer(sector, FactoryKey, ClassicAccessBits.Transport, sector == 39 ? (byte)0xBC : (byte)0x69, FactoryKey);
            }
        }

        public bool CardPresent { get; set; } = true;

        /// <summary>Gets or sets a value that lets block 0 be written (some copies of chips do; a genuine chip does not).</summary>
        public bool Block0IsWritable { get; set; }

        /// <summary>Gets or sets a value that makes the chip store a byte of what it is told to write wrong, so that a block does not read back as it was written.</summary>
        public bool CorruptsWrites { get; set; }

        /// <summary>Gets or sets which byte of a block <see cref="CorruptsWrites"/> gets wrong (0 is the first; 9 is the general purpose byte of a trailer).</summary>
        public int CorruptedByte { get; set; }

        /// <summary>Gets or sets a value that takes the chip away as soon as a block was written (the write itself worked).</summary>
        public bool GoesAwayAfterAWrite { get; set; }

        /// <summary>Gets or sets a value that makes the chip answer a login at once with what it thinks of the key (the TWN4 does; a PC/SC reader says yes and the chip judges at the next access).</summary>
        public bool JudgesLoginAtOnce { get; set; }

        /// <summary>Gets or sets a value that makes every call give up the thread before it is answered, so that two operations that are not held apart interleave their calls.</summary>
        public bool YieldsOnEveryCall { get; set; }

        public List<string> Log { get; } = new List<string>();

        public int Logins { get; private set; }

        public int Reads { get; private set; }

        public int Writes { get; private set; }

        public int Reselects { get; private set; }

        public ClassicCardSize? Size => size;

        /// <summary>Puts a trailer on the chip (the setup of a test; no access is checked).</summary>
        public void SetTrailer(int sector, byte[] keyA, ClassicAccessBits bits, byte gpb, byte[] keyB) =>
            memory[ClassicLayout.TrailerBlock(sector)] = bits.BuildTrailer(keyA, gpb, keyB);

        /// <summary>Puts raw bytes in a block (the setup of a test; no access is checked).</summary>
        public void Poke(int block, byte[] data) => memory[block] = (byte[])data.Clone();

        /// <summary>Gives the real content of a block, as no key could read it.</summary>
        public byte[] Peek(int block) => (byte[])memory[block].Clone();

        /// <summary>Makes the next read of a block fail once, as if the chip had refused it.</summary>
        public void FailNextReadOf(int block) => FailReadOf(block, 1);

        /// <summary>Makes one read of a block fail, as if the chip had refused it: the n-th read of that block (1 is the first).</summary>
        public void FailReadOf(int block, int occurrence) => readFaults[block] = occurrence;

        /// <summary>Makes the next write of a block fail once, as if the chip had refused it although the access bits allow it.</summary>
        public void FailNextWriteOf(int block) => writeFaults.Add(block);

        public async Task<ClassicPortResult> LoginAsync(int block, ClassicKeyType keyType, byte[] key)
        {
            await PauseAsync();

            return Login(block, keyType, key);
        }

        public async Task<ClassicBlockResult> ReadBlockAsync(int block)
        {
            await PauseAsync();

            return Read(block);
        }

        public async Task<ClassicPortResult> WriteBlockAsync(int block, byte[] data)
        {
            await PauseAsync();
            Log.Add("write " + block + " (" + data.Length + " bytes)");

            return Write(block, data);
        }

        public async Task<ClassicPortResult> ReselectAsync()
        {
            await PauseAsync();
            Log.Add("reselect");

            if (!CardPresent)
            {
                return new ClassicPortResult(ERROR.TransportError);
            }

            Reselects++;
            halted = false;
            loggedSector = null;

            return ClassicPortResult.Ok;
        }

        private async Task PauseAsync()
        {
            if (YieldsOnEveryCall)
            {
                await Task.Yield();
            }
        }

        private ClassicPortResult Login(int block, ClassicKeyType keyType, byte[] key)
        {
            var sector = ClassicLayout.SectorOf(block);

            Log.Add("login " + sector + " " + keyType);

            if (!CardPresent)
            {
                return new ClassicPortResult(ERROR.TransportError);
            }

            Logins++;

            if (halted)
            {
                halted = false;

                return new ClassicPortResult(ERROR.AuthFailure);
            }

            var trailer = memory[ClassicLayout.TrailerBlock(sector)];
            var stored = keyType == ClassicKeyType.KeyA ? trailer.Take(6) : trailer.Skip(10);
            var bits = Bits(sector);

            loggedSector = sector;
            loggedType = keyType;
            loginIsGood = stored.SequenceEqual(key) && bits != null && bits.AllowsLoginWith(keyType);

            if (JudgesLoginAtOnce && !loginIsGood)
            {
                halted = true;
                loggedSector = null;

                return new ClassicPortResult(ERROR.AuthFailure);
            }

            // A PC/SC reader says yes whatever the chip thinks.
            return ClassicPortResult.Ok;
        }

        private ClassicBlockResult Read(int block)
        {
            Log.Add("read " + block);

            if (!CardPresent)
            {
                return new ClassicBlockResult(ERROR.TransportError, null);
            }

            Reads++;
            readCounts[block] = readCounts.TryGetValue(block, out var seen) ? seen + 1 : 1;

            if (halted)
            {
                halted = false;

                return new ClassicBlockResult(ERROR.AuthFailure, null);
            }

            var sector = ClassicLayout.SectorOf(block);
            var faulty = readFaults.TryGetValue(block, out var occurrence) && occurrence == readCounts[block];

            if (loggedSector != sector || !loginIsGood || faulty)
            {
                return Refuse();
            }

            var bits = Bits(sector);

            if (bits == null)
            {
                return Refuse();
            }

            var data = (byte[])memory[block].Clone();

            if (ClassicLayout.IsTrailer(block))
            {
                var rights = bits.TrailerRights;

                Array.Clear(data, 0, 6);

                if (!rights.KeyBRead.Allows(loggedType))
                {
                    Array.Clear(data, 10, 6);
                }

                if (!rights.AccessBitsRead.Allows(loggedType))
                {
                    Array.Clear(data, 6, 3);
                }

                return new ClassicBlockResult(ERROR.NoError, data);
            }

            return bits.GetDataBlockRights(ClassicLayout.AccessGroup(block)).Read.Allows(loggedType)
                ? new ClassicBlockResult(ERROR.NoError, data)
                : Refuse();
        }

        private ClassicPortResult Write(int block, byte[] data)
        {
            if (!CardPresent)
            {
                return new ClassicPortResult(ERROR.TransportError);
            }

            Writes++;

            if (halted)
            {
                halted = false;

                return new ClassicPortResult(ERROR.AuthFailure);
            }

            var sector = ClassicLayout.SectorOf(block);
            var bits = Bits(sector);

            if (loggedSector != sector || !loginIsGood || bits == null || writeFaults.Remove(block) || !MayWrite(block, data, bits))
            {
                halted = true;
                loggedSector = null;

                return new ClassicPortResult(ERROR.AuthFailure);
            }

            memory[block] = (byte[])data.Clone();

            if (CorruptsWrites)
            {
                memory[block][CorruptedByte] ^= 0x01;
            }

            if (GoesAwayAfterAWrite)
            {
                CardPresent = false;
            }

            return ClassicPortResult.Ok;
        }

        // What the data sheet lets the key that is logged in write. A trailer is written as a whole: every part that changes needs the right.
        private bool MayWrite(int block, byte[] data, ClassicAccessBits bits)
        {
            if (block == 0)
            {
                return Block0IsWritable;
            }

            if (!ClassicLayout.IsTrailer(block))
            {
                return bits.GetDataBlockRights(ClassicLayout.AccessGroup(block)).Write.Allows(loggedType);
            }

            var rights = bits.TrailerRights;
            var current = memory[block];

            return (current.AsSpan(0, 6).SequenceEqual(data.AsSpan(0, 6)) || rights.KeyAWrite.Allows(loggedType))
                && (current.AsSpan(6, 4).SequenceEqual(data.AsSpan(6, 4)) || rights.AccessBitsWrite.Allows(loggedType))
                && (current.AsSpan(10, 6).SequenceEqual(data.AsSpan(10, 6)) || rights.KeyBWrite.Allows(loggedType));
        }

        private ClassicAccessBits? Bits(int sector) =>
            ClassicAccessBits.TryFromTrailer(memory[ClassicLayout.TrailerBlock(sector)], out var bits) ? bits : null;

        private ClassicBlockResult Refuse()
        {
            halted = true;
            loggedSector = null;

            return new ClassicBlockResult(ERROR.AuthFailure, null);
        }
    }
}
