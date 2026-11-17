#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace RFiDGear.Services.SecureKeys
{
    /// <summary>What a command does with a slot that is bound to a key of the SAM.</summary>
    internal enum SamKeyPolicy
    {
        /// <summary>
        /// The command cannot use a key of the SAM: a slot that is bound to one ends the acquisition, before any card call. This is what
        /// every command is, unless it says otherwise, so a project that names a key of the SAM in a slot of an older command (or of a
        /// Classic or Ultralight task) stops it and does not fall back to anything.
        /// </summary>
        Refuse,

        /// <summary>
        /// The command can use keys of the SAM, but only if all its slots are keys of the SAM: the SAM builds the cryptogram of a key
        /// change from the key that authenticated and the new key, so it needs both of them.
        /// </summary>
        AllOrNothing,
    }

    /// <summary>The slots in which a key of the SAM is offered to the person: where a command can use one.</summary>
    internal static class SamKeySlots
    {
        /// <summary>
        /// Gets the slots in which the key manager offers the SAM as a source of the key: the keys of the authentication of a PICC or application
        /// key and of the change of those keys. A command that uses one of them says so (<see cref="SamKeyPolicy.AllOrNothing"/>); every other
        /// command that uses such a slot refuses a key of the SAM before it touches a card. The slots of the read and write keys of a file are not
        /// in it: no command can use a key of the SAM for a file yet.
        /// </summary>
        internal static IReadOnlyCollection<string> Offered { get; } = new[]
        {
            SecureKeySlots.DesfirePiccCurrent,
            SecureKeySlots.DesfirePiccNew,
            SecureKeySlots.DesfireAppCurrent,
            SecureKeySlots.DesfireAppOld,
            SecureKeySlots.DesfireAppNew,
        };

        /// <summary>Tells whether the SAM is offered as a source of the key of a slot.</summary>
        /// <param name="slotId">The slot.</param>
        /// <returns><see langword="true"/> when it is.</returns>
        internal static bool Offers(string slotId) => Offers(slotId, Offered);

        /// <summary>Tells whether a slot is one of the offered ones.</summary>
        /// <param name="slotId">The slot.</param>
        /// <param name="offered">The offered slots.</param>
        /// <returns><see langword="true"/> when it is.</returns>
        internal static bool Offers(string slotId, IReadOnlyCollection<string> offered) =>
            offered.Contains(slotId, StringComparer.Ordinal);

        /// <summary>The providers a slot can be bound to: the SAM only where it is offered.</summary>
        /// <param name="slotId">The slot.</param>
        /// <param name="providers">All the providers.</param>
        /// <param name="offered">The slots in which the SAM is offered.</param>
        /// <returns>The providers of the slot, in the order they came.</returns>
        internal static IReadOnlyList<SecureKeyProviderInfo> ProvidersFor(string slotId, IReadOnlyList<SecureKeyProviderInfo> providers, IReadOnlyCollection<string> offered) =>
            Offers(slotId, offered)
                ? providers
                : providers.Where(provider => !string.Equals(provider.Id, SamKeyProvider.ProviderId, StringComparison.Ordinal)).ToList();
    }
}