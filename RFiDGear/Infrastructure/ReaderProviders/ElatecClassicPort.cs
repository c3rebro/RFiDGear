#nullable enable
using System;
using System.IO;
using System.Threading.Tasks;

using Elatec.NET;

using RFiDGear.Infrastructure.Classic;

namespace RFiDGear.Infrastructure.ReaderProviders
{
    /// <summary>
    /// <see cref="IClassicPort"/> on the TWN4: four calls and nothing else. It holds no logic (the decisions are in
    /// <see cref="ClassicOperations"/>). The TWN4 judges a login at once, so a login that worked is a login that worked (a PC/SC reader says
    /// yes to every key). It numbers the sector of a login by the block that starts it divided by four, which for the sectors 32 to 39 of a
    /// 4K is <c>32 + 4 * (sector - 32)</c>, and not the number of the sector.
    /// <para>
    /// What the library throws when the chip refuses is not documented. An answer of the reader that is no fault of the port (the chip did not
    /// take the key, the block, the write) is taken as a refusal of the chip (<see cref="ERROR.AuthFailure"/>); a port that is not open, a timeout
    /// and an I/O fault are the reader gone (<see cref="ERROR.TransportError"/>). The hardware cases of <c>RFiDGear.HIL.Tests</c> cover this class
    /// with a real reader.
    /// </para>
    /// </summary>
    internal sealed class ElatecClassicPort : IClassicPort
    {
        private const byte KeyAType = 0;
        private const byte KeyBType = 1;

        private readonly TWN4ReaderDevice reader;

        /// <summary>Creates the port.</summary>
        /// <param name="reader">The reader, connected.</param>
        /// <param name="size">The size of the chip, or <c>null</c> when it is not known.</param>
        internal ElatecClassicPort(TWN4ReaderDevice reader, ClassicCardSize? size)
        {
            this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
            Size = size;
        }

        /// <inheritdoc />
        public ClassicCardSize? Size { get; }

        /// <summary>The number that the TWN4 calls a sector: the block that starts the sector divided by four.</summary>
        /// <param name="block">Any block of the sector.</param>
        /// <returns>The number for the login.</returns>
        internal static byte SectorNumberOf(int block) => (byte)(ClassicLayout.FirstBlock(ClassicLayout.SectorOf(block)) / 4);

        /// <summary>The key type that the TWN4 takes for a key: 0 for key A, 1 for key B.</summary>
        /// <param name="keyType">The key.</param>
        /// <returns>The byte for the login.</returns>
        internal static byte KeyTypeOf(ClassicKeyType keyType) => keyType == ClassicKeyType.KeyA ? KeyAType : KeyBType;

        /// <summary>Makes the answer of a block read: a short answer is no block.</summary>
        /// <param name="data">What the reader returned.</param>
        /// <returns>The block when there are 16 bytes, otherwise a refusal.</returns>
        internal static ClassicBlockResult ToBlockResult(byte[]? data) =>
            data is { Length: ClassicLayout.BytesPerBlock }
                ? new ClassicBlockResult(ERROR.NoError, data)
                : new ClassicBlockResult(ERROR.AuthFailure, null);

        /// <summary>Tells what a fault of the library means for a Classic call.</summary>
        /// <param name="fault">What the library threw.</param>
        /// <returns><see cref="ERROR.TransportError"/> when the reader is not there (closed, timed out, I/O), otherwise <see cref="ERROR.AuthFailure"/>.</returns>
        internal static ERROR Map(Exception fault)
        {
            ArgumentNullException.ThrowIfNull(fault);

            switch (fault)
            {
                case ReaderException readerFault when readerFault.ErrorNumber is ReaderError.NotOpen or ReaderError.NotInit or ReaderError.IsAlreadyOpen or ReaderError.Timeout:
                case IOException:
                case TimeoutException:
                case ObjectDisposedException:
                case InvalidOperationException:
                case UnauthorizedAccessException:
                    return ERROR.TransportError;
                default:
                    return ERROR.AuthFailure;
            }
        }

        /// <inheritdoc />
        public async Task<ClassicPortResult> LoginAsync(int block, ClassicKeyType keyType, byte[] key)
        {
            try
            {
                await reader.MifareClassic_LoginAsync(
                    Convert.ToHexString(key),
                    KeyTypeOf(keyType),
                    SectorNumberOf(block));

                return ClassicPortResult.Ok;
            }
            catch (Exception fault)
            {
                return new ClassicPortResult(Map(fault));
            }
        }

        /// <inheritdoc />
        public async Task<ClassicBlockResult> ReadBlockAsync(int block)
        {
            try
            {
                return ToBlockResult(await reader.MifareClassic_ReadBlockAsync((byte)block));
            }
            catch (Exception fault)
            {
                return new ClassicBlockResult(Map(fault), null);
            }
        }

        /// <inheritdoc />
        public async Task<ClassicPortResult> WriteBlockAsync(int block, byte[] data)
        {
            try
            {
                await reader.MifareClassic_WriteBlockAsync(data, (byte)block);

                return ClassicPortResult.Ok;
            }
            catch (Exception fault)
            {
                return new ClassicPortResult(Map(fault));
            }
        }

        /// <summary>Wakes the chip: the TWN4 searches for it again, which selects it anew after a refusal.</summary>
        /// <returns>The outcome; <see cref="ERROR.TransportError"/> when the reader finds no chip.</returns>
        public async Task<ClassicPortResult> ReselectAsync()
        {
            try
            {
                return await reader.SearchTagAsync() != null
                    ? ClassicPortResult.Ok
                    : new ClassicPortResult(ERROR.TransportError);
            }
            catch (Exception)
            {
                return new ClassicPortResult(ERROR.TransportError);
            }
        }
    }
}
