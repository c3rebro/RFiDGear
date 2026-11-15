#nullable enable
using System;
using System.Threading.Tasks;

using Elatec.NET.Interfaces;
using Elatec.NET.Sam;

namespace RFiDGear.Services.Sam
{
    /// <summary>
    /// The way <see cref="SamAv3HostSession"/> talks to a SAM in a PC/SC reader: it makes a short APDU of a
    /// <see cref="SamApdu"/>, hands it to the transport and splits the status word off the answer.
    /// </summary>
    /// <remarks>
    /// An error status word is returned to the caller and never thrown: the host protocol needs <c>90AF</c> (go on) and the
    /// activation check needs <c>6985</c> (not activated). Nothing is repeated, whatever goes wrong: a command that the SAM
    /// may have executed must never be sent a second time.
    /// </remarks>
    internal sealed class LlaSamApduChannel : ISamApduChannel
    {
        private readonly ISamTransport _transport;

        /// <summary>Creates a channel over a transport.</summary>
        /// <param name="transport">The transport to the card.</param>
        internal LlaSamApduChannel(ISamTransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }

        /// <inheritdoc />
        public async Task<SamApduResponse> TransmitAsync(SamApdu command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            var apdu = Encode(command);
            byte[] answer;

            try
            {
                // The transport blocks (SCardTransmit): not on the thread of the caller, which may be the one of the window.
                answer = await Task.Run(() => _transport.Exchange(apdu)).ConfigureAwait(false);
            }
            catch (Exception e) when (e is not SamException && e is not OperationCanceledException)
            {
                // The text of the exception of the reader library is left out on purpose: the caller logs the type of this one.
                throw new SamReaderException("The reader could not exchange the APDU with the SAM.");
            }

            return Decode(answer);
        }

        /// <summary>
        /// Writes a command as a short APDU: <c>CLA INS P1 P2</c>, then <c>Lc</c> and the data field when there is data, then
        /// <c>Le</c> when the command has one.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>The bytes to send.</returns>
        internal static byte[] Encode(SamApdu command)
        {
            var data = command.Data;
            var hasData = data.Length > 0;
            var apdu = new byte[4 + (hasData ? 1 + data.Length : 0) + (command.Le.HasValue ? 1 : 0)];

            apdu[0] = command.Cla;
            apdu[1] = command.Ins;
            apdu[2] = command.P1;
            apdu[3] = command.P2;

            var next = 4;

            if (hasData)
            {
                apdu[next++] = (byte)data.Length;
                Array.Copy(data, 0, apdu, next, data.Length);
                next += data.Length;
            }

            if (command.Le.HasValue)
            {
                apdu[next] = command.Le.Value;
            }

            return apdu;
        }

        /// <summary>Splits an answer into its data field and the status word.</summary>
        /// <param name="answer">What the card answered, status word last.</param>
        /// <returns>The response.</returns>
        /// <exception cref="SamReaderException">The answer is shorter than a status word.</exception>
        internal static SamApduResponse Decode(byte[]? answer)
        {
            if (answer == null || answer.Length < 2)
            {
                throw new SamReaderException("The SAM answered with less than a status word.");
            }

            var data = new byte[answer.Length - 2];
            Array.Copy(answer, data, data.Length);

            return new SamApduResponse(data, (ushort)((answer[answer.Length - 2] << 8) | answer[answer.Length - 1]));
        }
    }
}
