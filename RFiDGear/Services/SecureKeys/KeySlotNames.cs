#nullable enable
using System;
using System.Collections.Generic;

namespace RFiDGear.Services.SecureKeys
{
    /// <summary>The names a person reads for the key slots, in the language of the application.</summary>
    internal static class KeySlotNames
    {
        // The resource key of the name of every slot. A slot that is added to SecureKeySlots needs an entry here, which a test checks.
        private static readonly Dictionary<string, string> ResourceKeys = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [SecureKeySlots.DesfirePiccCurrent] = "keySlotNameDesfirePiccCurrent",
            [SecureKeySlots.DesfirePiccNew] = "keySlotNameDesfirePiccNew",
            [SecureKeySlots.DesfireAppCurrent] = "keySlotNameDesfireAppCurrent",
            [SecureKeySlots.DesfireAppOld] = "keySlotNameDesfireAppOld",
            [SecureKeySlots.DesfireAppNew] = "keySlotNameDesfireAppNew",
            [SecureKeySlots.DesfireRead] = "keySlotNameDesfireRead",
            [SecureKeySlots.DesfireWrite] = "keySlotNameDesfireWrite",
            [SecureKeySlots.ClassicACurrent] = "keySlotNameClassicACurrent",
            [SecureKeySlots.ClassicBCurrent] = "keySlotNameClassicBCurrent",
            [SecureKeySlots.ClassicATarget] = "keySlotNameClassicATarget",
            [SecureKeySlots.ClassicBTarget] = "keySlotNameClassicBTarget",
            [SecureKeySlots.ClassicMadACurrent] = "keySlotNameClassicMadACurrent",
            [SecureKeySlots.ClassicMadBCurrent] = "keySlotNameClassicMadBCurrent",
            [SecureKeySlots.ClassicMadATarget] = "keySlotNameClassicMadATarget",
            [SecureKeySlots.ClassicMadBTarget] = "keySlotNameClassicMadBTarget",
        };

        /// <summary>Gets the resource key of the name of a slot.</summary>
        /// <param name="slotId">The slot id; see <see cref="SecureKeySlots"/>.</param>
        /// <returns>The resource key, or <see langword="null"/> for a slot this version does not know.</returns>
        internal static string? ResourceKeyOf(string slotId) => ResourceKeys.TryGetValue(slotId, out var key) ? key : null;

        /// <summary>Gives the name of a slot.</summary>
        /// <param name="slotId">The slot id.</param>
        /// <param name="text">Gives the text of a resource key.</param>
        /// <returns>The name in the language of the application; the id itself for a slot this version does not know.</returns>
        internal static string For(string slotId, Func<string, string> text)
        {
            ArgumentNullException.ThrowIfNull(text);

            var key = ResourceKeyOf(slotId);

            return key == null ? slotId : text(key);
        }
    }
}
