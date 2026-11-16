#nullable enable
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

using LibLogicalAccess;
using LibLogicalAccess.Card;

using RFiDGear.Infrastructure.Classic;

namespace RFiDGear.Infrastructure.ReaderProviders
{
    /// <summary>
    /// <see cref="IClassicPort"/> on the commands of LibLogicalAccess: four calls and nothing else. It holds no logic (the decisions are in
    /// <see cref="ClassicOperations"/>), so what it maps is only the faults: a refusal of the chip (<see cref="CardException"/>: security status not
    /// satisfied, block not found) is <see cref="ERROR.AuthFailure"/>, everything else that the library throws (no reader, no chip, no ATR) is
    /// <see cref="ERROR.TransportError"/>. The hardware cases of <c>RFiDGear.HIL.Tests</c> cover this class with a real reader.
    /// </summary>
    internal sealed class LlaClassicPort : IClassicPort
    {
        // The slot of the reader that the keys are loaded into (the same one for both key types).
        private const byte KeySlot = 0;

        private const string FactoryKeyText = "ff ff ff ff ff ff";

        private readonly MifareCommands commands;

        /// <summary>Creates the port.</summary>
        /// <param name="commands">The commands of the chip that was connected to.</param>
        /// <param name="size">The size of the chip, or <c>null</c> when it is not known.</param>
        internal LlaClassicPort(MifareCommands commands, ClassicCardSize? size)
        {
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
            Size = size;
        }

        /// <inheritdoc />
        public ClassicCardSize? Size { get; }

        /// <summary>Maps the card type that the library gives a chip to a size.</summary>
        /// <param name="cardType">For example <c>Mifare1K</c> or <c>Mifare4K</c>.</param>
        /// <returns>The size, or <c>null</c> for a type that is not known.</returns>
        internal static ClassicCardSize? SizeOf(string? cardType)
        {
            if (string.IsNullOrEmpty(cardType))
            {
                return null;
            }

            if (cardType.Contains("Mini", StringComparison.OrdinalIgnoreCase))
            {
                return ClassicCardSize.Mini;
            }

            if (cardType.Contains("4K", StringComparison.OrdinalIgnoreCase))
            {
                return ClassicCardSize.K4;
            }

            if (cardType.Contains("2K", StringComparison.OrdinalIgnoreCase))
            {
                return ClassicCardSize.K2;
            }

            return cardType.Contains("1K", StringComparison.OrdinalIgnoreCase) ? ClassicCardSize.K1 : null;
        }

        /// <inheritdoc />
        public Task<ClassicPortResult> LoginAsync(int block, ClassicKeyType keyType, byte[] key) =>
            Task.Run(() => Guard(() =>
            {
                var type = keyType == ClassicKeyType.KeyA ? MifareKeyType.KT_KEY_A : MifareKeyType.KT_KEY_B;

                // The library takes a key as text, bytes separated by spaces.
                var text = string.Join(" ", key.Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));

                commands.loadKey(KeySlot, type, new MifareKey(text));
                commands.authenticate((byte)block, KeySlot, type);
            }));

        /// <inheritdoc />
        public Task<ClassicBlockResult> ReadBlockAsync(int block) =>
            Task.Run(() =>
            {
                byte[]? data = null;
                var result = Guard(() => data = commands.readBinary((byte)block, ClassicLayout.BytesPerBlock).ToArray());

                if (!result.Succeeded)
                {
                    return new ClassicBlockResult(result.Error, null);
                }

                // A short answer is no block.
                return data is { Length: ClassicLayout.BytesPerBlock }
                    ? new ClassicBlockResult(ERROR.NoError, data)
                    : new ClassicBlockResult(ERROR.AuthFailure, null);
            });

        /// <inheritdoc />
        public Task<ClassicPortResult> WriteBlockAsync(int block, byte[] data) =>
            Task.Run(() => Guard(() => commands.updateBinary((byte)block, new ByteVector(data))));

        /// <summary>
        /// Wakes the chip. A refused access halts it, and neither a reconnect nor a fresh connection to the reader wakes it again (probed on a
        /// MIFARE Classic 4K in an OMNIKEY 5422, with the three ways of reconnecting that the library offers): the first command after the
        /// refusal fails, and that failure is what wakes the chip. So the wake is a login that is expected to fail, with the factory key on
        /// block 0 (a login changes nothing on the chip). A failure of the chip is the expected outcome; any other fault means that the chip is gone.
        /// </summary>
        /// <returns>The outcome.</returns>
        public Task<ClassicPortResult> ReselectAsync() =>
            Task.Run(() =>
            {
                try
                {
                    commands.loadKey(KeySlot, MifareKeyType.KT_KEY_A, new MifareKey(FactoryKeyText));
                    commands.authenticate(0, KeySlot, MifareKeyType.KT_KEY_A);
                }
                catch (CardException)
                {
                    // The failure that wakes the chip.
                }
                catch (Exception)
                {
                    return new ClassicPortResult(ERROR.TransportError);
                }

                return ClassicPortResult.Ok;
            });

        private static ClassicPortResult Guard(Action call)
        {
            try
            {
                call();

                return ClassicPortResult.Ok;
            }
            catch (CardException)
            {
                return new ClassicPortResult(ERROR.AuthFailure);
            }
            catch (Exception)
            {
                return new ClassicPortResult(ERROR.TransportError);
            }
        }
    }
}
