#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace RFiDGear.Infrastructure.ReaderProviders
{
    /// <summary>How the PC/SC provider gets its reader unit.</summary>
    internal enum PcscReaderPick
    {
        /// <summary>The reader of that name.</summary>
        Named,

        /// <summary>The library's default reader unit: no name was chosen, and no SAM reader is configured that it might pick.</summary>
        Default,

        /// <summary>No reader: nothing may be picked for the card (a SAM reader is configured and the choice is blank, unknown or the SAM itself).</summary>
        None,
    }

    /// <summary>
    /// Which PC/SC reader is the reader of the card. The library's default reader unit takes the first reader that it finds, and on a reader pair
    /// such as the OMNIKEY 5422 that is the contact slot, where the SAM sits: with a SAM reader configured the application must never fall
    /// back to it, and never take the SAM reader for the card.
    /// </summary>
    internal static class PcscReaderChoice
    {
        /// <summary>Decides which reader is the reader of the card.</summary>
        /// <param name="names">The names of the PC/SC readers that exist.</param>
        /// <param name="selected">The name that was chosen for the card, if any.</param>
        /// <param name="samReader">The name of the reader of the SAM, if one is configured.</param>
        /// <returns>The kind of pick, and the name for <see cref="PcscReaderPick.Named"/>.</returns>
        internal static (PcscReaderPick Kind, string? Name) Pick(IReadOnlyCollection<string> names, string? selected, string? samReader)
        {
            ArgumentNullException.ThrowIfNull(names);

            var samConfigured = !string.IsNullOrWhiteSpace(samReader);

            if (!string.IsNullOrWhiteSpace(selected))
            {
                var match = names.FirstOrDefault(n => n == selected);

                if (match != null)
                {
                    return IsSamReader(match, samReader) ? (PcscReaderPick.None, null) : (PcscReaderPick.Named, match);
                }
            }

            // Blank or unknown: the library's default, unless a SAM reader exists that it might pick.
            return samConfigured ? (PcscReaderPick.None, null) : (PcscReaderPick.Default, null);
        }

        /// <summary>Tells whether a reader is the reader of the SAM (names are compared without case and without the spaces around them).</summary>
        /// <param name="name">The name of a reader.</param>
        /// <param name="samReader">The name of the reader of the SAM, if one is configured.</param>
        /// <returns><see langword="true"/> when they are the same reader.</returns>
        internal static bool IsSamReader(string? name, string? samReader) => SameReader(name, samReader);

        /// <summary>
        /// Tells whether a unit that was chosen earlier is not the reader that was confirmed for the card: a reader was named for the card, and it
        /// is another one. A blank name confirms nothing, so the unit that is held stays.
        /// </summary>
        /// <param name="unitName">The name of the reader the held unit is bound to.</param>
        /// <param name="cardReader">The reader that was confirmed for the card.</param>
        /// <returns><see langword="true"/> when the unit has to be chosen again.</returns>
        internal static bool IsOtherReader(string? unitName, string? cardReader) =>
            !string.IsNullOrWhiteSpace(cardReader) && !SameReader(unitName, cardReader);

        private static bool SameReader(string? a, string? b) =>
            !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b)
            && string.Equals(a!.Trim(), b!.Trim(), StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Tells whether the provider that exists is told the readers when the dialog is confirmed: only for the PC/SC provider, only when the reader
        /// of the card or of the SAM (or the provider) changed, and only when the settings were accepted. A save that was refused (a Classic key
        /// that is no key) writes nothing, and the provider of the application must not change for it: it would run with a SAM reader that is not
        /// the one that is saved.
        /// </summary>
        /// <param name="providerIsPcsc">Whether the PC/SC provider is chosen.</param>
        /// <param name="samReaderChanged">Whether the reader of the SAM that is saved changed.</param>
        /// <param name="readerChanged">Whether the provider or the reader of the card changed.</param>
        /// <param name="settingsAccepted">Whether the settings passed the check of the dialog and are saved.</param>
        /// <returns><see langword="true"/> when the provider is told.</returns>
        internal static bool ProviderLearnsReaders(bool providerIsPcsc, bool samReaderChanged, bool readerChanged, bool settingsAccepted) =>
            settingsAccepted && providerIsPcsc && (samReaderChanged || readerChanged);

        /// <summary>
        /// The SAM reader to save when the dialog is confirmed: the one that was chosen when the PC/SC provider is the provider, and otherwise the one
        /// that is saved already (the choice is not shown, and it is not lost by trying another provider).
        /// </summary>
        /// <param name="providerIsPcsc">Whether the PC/SC provider is chosen.</param>
        /// <param name="chosen">The SAM reader that the dialog shows.</param>
        /// <param name="saved">The SAM reader that is saved.</param>
        /// <returns>The name to save; never <see langword="null"/>.</returns>
        internal static string SamReaderToSave(bool providerIsPcsc, string? chosen, string? saved) =>
            providerIsPcsc ? (chosen ?? string.Empty) : (saved ?? string.Empty);
    }
}