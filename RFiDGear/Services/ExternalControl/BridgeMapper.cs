#nullable enable
using System.Linq;

using RFiDGear.Bridge;
using RFiDGear.Contracts;

namespace RFiDGear.Services.ExternalControl
{
    /// <summary>
    /// Turns what the host knows (<c>RFiDGear.Contracts</c>) into the types of the wire (<c>RFiDGear.Bridge</c>). The wire
    /// never uses the types of the host directly: a member that the host gains does not reach a client until it is mapped on
    /// purpose, and a value the mapper does not know becomes <c>unknown</c> instead of an error.
    /// </summary>
    internal static class BridgeMapper
    {
        /// <summary>
        /// Reads the picture the reader shows now and maps it. A reader provider that fails when it is asked (it is somebody
        /// else's code) must not take the notice or the status down with it: the answer is a reader in the state
        /// <see cref="ReaderStates.Unknown"/>, and the log has the type of the failure.
        /// </summary>
        /// <param name="reader">The reader service of the host.</param>
        /// <returns>The reader as the wire has it.</returns>
        internal static ReaderDto ReadReader(IReaderHost reader)
        {
            System.ArgumentNullException.ThrowIfNull(reader);

            try
            {
                return ToDto(reader.CurrentSnapshot);
            }
            catch (System.Exception e)
            {
                Serilog.Log.ForContext(typeof(BridgeMapper)).Debug("External control: the reader could not be read ({ExceptionType})", e.GetType().FullName);

                return new ReaderDto { State = ReaderStates.Unknown };
            }
        }

        /// <summary>Maps the picture of the reader.</summary>
        /// <param name="snapshot">What the reader showed last.</param>
        /// <returns>The reader as the wire has it.</returns>
        internal static ReaderDto ToDto(ReaderSnapshot snapshot)
        {
            System.ArgumentNullException.ThrowIfNull(snapshot);

            return new ReaderDto
            {
                Name = string.IsNullOrEmpty(snapshot.ReaderName) ? null : snapshot.ReaderName,
                State = ToWire(snapshot.State),
                HasCard = snapshot.HasCard,
                Credentials = snapshot.Credentials
                    .Select(credential => new CredentialDto
                    {
                        Uid = credential.Uid,
                        TypeCode = credential.TypeCode,
                        DisplayName = credential.DisplayName,
                        IsPrimary = credential.IsPrimary,
                    })
                    .ToList(),
            };
        }

        /// <summary>Maps the state of a reader scan to a constant of <see cref="ReaderStates"/>.</summary>
        /// <param name="state">The state.</param>
        /// <returns>The wire name; <see cref="ReaderStates.Unknown"/> for a state the mapper does not know.</returns>
        internal static string ToWire(ReaderScanState state) => state switch
        {
            ReaderScanState.NoReader => ReaderStates.NoReader,
            ReaderScanState.Ready => ReaderStates.Ready,
            ReaderScanState.ReadFailed => ReaderStates.ReadFailed,
            ReaderScanState.Busy => ReaderStates.Busy,
            _ => ReaderStates.Unknown,
        };
    }
}
