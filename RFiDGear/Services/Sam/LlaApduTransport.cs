#nullable enable
using LibLogicalAccess;

namespace RFiDGear.Services.Sam
{
    /// <summary>
    /// Sends command APDUs through the data transport of LibLogicalAccess, for the SAM and for the card that is relayed to it. It is not the
    /// adapter's <c>sendCommand</c>: that one checks the result and throws for every status word but <c>9000</c>, which breaks the three-pass
    /// authentications (<c>90AF</c>, <c>91AF</c>) and every answer that is a refusal by the card. Like <see cref="LlaSamReaderSource"/> this is
    /// a place that touches the native library and holds no logic.
    /// </summary>
    internal sealed class LlaApduTransport : ISamTransport
    {
        // The time a command gets: the key entry of a SAM is written to its EEPROM.
        private const int CommandTimeoutMilliseconds = 5000;

        private readonly DataTransport _transport;

        /// <summary>Creates the transport.</summary>
        /// <param name="transport">The data transport of the reader card adapter of a connected unit.</param>
        internal LlaApduTransport(DataTransport transport)
        {
            _transport = transport ?? throw new System.ArgumentNullException(nameof(transport));
        }

        /// <inheritdoc />
        public byte[] Exchange(byte[] apdu)
        {
            var command = new ByteVector();

            foreach (var b in apdu)
            {
                command.Add(b);
            }

            return _transport.sendCommand(command, CommandTimeoutMilliseconds).ToArray();
        }
    }
}