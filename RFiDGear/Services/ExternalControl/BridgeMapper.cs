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
