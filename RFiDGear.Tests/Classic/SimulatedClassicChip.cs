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
        private readonly HashSet<int> readFaults = new HashSet<int>();

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
        public void FailNextReadOf(int block) => readFaults.Add(block);

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
            Writes++;
            Log.Add("write " + block + " (" + data.Length + " bytes)");

            return new ClassicPortResult(ERROR.ProtocolConstraint);
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

            // The reader says yes whatever the chip thinks.
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

            if (halted)
            {
                halted = false;

                return new ClassicBlockResult(ERROR.AuthFailure, null);
            }

            var sector = ClassicLayout.SectorOf(block);

            if (loggedSector != sector || !loginIsGood || readFaults.Remove(block))
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
