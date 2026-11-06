using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>
    /// The decisions the check makes from what the first report says: how much memory the chip has, which verdict
    /// that gives, and which templates fit. Pure functions of their arguments; the culture of the machine never
    /// changes a result.
    /// </summary>
    public static class TemplateRules
    {
        /// <summary>
        /// The least free memory a DESFire chip must report to be programmable. The old assistant used this gate
        /// and it is kept, although the smallest template needs only 224 bytes: a chip with exactly 224 bytes free
        /// is turned away.
        /// </summary>
        internal const int DesfireMinimumFreeBytes = 225;

        private const string DefaultDesfireTemplateName = "MD4000L_AV";
        private const string DefaultClassicTemplateName = "MC1000L_AV";

        /// <summary>
        /// Reads the free-memory field of a report. The text is trimmed first. If its first word (up to the first
        /// blank) is a whole number, the chip is a DESFire chip and the number is its free bytes; a minus sign is
        /// allowed and the result is then too small to program. Otherwise a comma-separated list of free sectors
        /// is expected and the entries that contain a digit (0-9) are counted; empty entries are not sectors. Text without a digit, an
        /// <c>N/A</c> marker, an empty field and a number too large for an <see cref="int"/> are
        /// <see cref="MemoryKind.Unknown"/>.
        /// </summary>
        /// <param name="field">The text of the report field; may be <c>null</c>.</param>
        /// <returns>The figure; never <c>null</c>.</returns>
        public static FreeMemory ParseFreeMemory(string? field)
        {
            var text = field?.Trim();

            if (string.IsNullOrEmpty(text))
            {
                return FreeMemory.Unknown;
            }

            var firstWord = text.Split(' ')[0];

            if (LooksLikeWholeNumber(firstWord))
            {
                return int.TryParse(firstWord, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var bytes)
                    ? new FreeMemory(MemoryKind.Desfire, bytes)
                    : FreeMemory.Unknown;
            }

            // An entry counts only if it has a digit, so empty entries (a trailing comma) are not sectors.
            var sectors = text.Split(',').Count(HasAsciiDigit);

            return sectors == 0 ? FreeMemory.Unknown : new FreeMemory(MemoryKind.Classic, sectors);
        }

        /// <summary>
        /// Whether the chip has memory for programming at all. A DESFire chip needs at least
        /// <see cref="DesfireMinimumFreeBytes"/> free bytes, a Classic chip at least one free sector, and a chip whose
        /// memory is unknown is turned away: no template can be chosen for it.
        /// </summary>
        /// <param name="memory">The figure from <see cref="ParseFreeMemory"/>.</param>
        /// <returns><c>true</c> when at least one template can be offered.</returns>
        public static bool HasEnoughMemory(FreeMemory memory)
        {
            if (memory == null)
            {
                throw new ArgumentNullException(nameof(memory));
            }

            return memory.Kind switch
            {
                MemoryKind.Desfire => memory.Amount >= DesfireMinimumFreeBytes,
                MemoryKind.Classic => memory.Amount >= 1,
                _ => false,
            };
        }

        /// <summary>
        /// The templates that fit the chip, in display order: for a DESFire chip those that need no more bytes than
        /// it has, for a Classic chip those that need no more sectors than it has. Nothing fits a chip without enough
        /// memory.
        /// </summary>
        /// <param name="memory">The figure from <see cref="ParseFreeMemory"/>.</param>
        /// <returns>The fitting templates; empty when none fits.</returns>
        public static IReadOnlyList<LsmTemplate> AvailableTemplates(FreeMemory memory)
        {
            if (!HasEnoughMemory(memory))
            {
                return Array.Empty<LsmTemplate>();
            }

            return memory.Kind == MemoryKind.Desfire
                ? LsmTemplateCatalog.All.Where(t => t.Family == TemplateFamily.Desfire && t.SizeInBytes <= memory.Amount).ToList()
                : LsmTemplateCatalog.All.Where(t => t.Family == TemplateFamily.Classic && t.SizeInFreeSectors <= memory.Amount).ToList();
        }

        /// <summary>
        /// The template that is preselected: <c>MD4000L_AV</c> if it fits, otherwise <c>MC1000L_AV</c> if it fits,
        /// otherwise the first one in display order.
        /// </summary>
        /// <param name="available">The list from <see cref="AvailableTemplates"/>.</param>
        /// <returns>The template, or <c>null</c> when the list is empty.</returns>
        public static LsmTemplate? DefaultTemplate(IReadOnlyList<LsmTemplate> available)
        {
            if (available == null)
            {
                throw new ArgumentNullException(nameof(available));
            }

            return available.FirstOrDefault(t => t.Name == DefaultDesfireTemplateName)
                ?? available.FirstOrDefault(t => t.Name == DefaultClassicTemplateName)
                ?? available.FirstOrDefault();
        }

        /// <summary>
        /// Turns what the report says into a verdict. A chip that cannot be programmed is not supported, whatever the
        /// supported flag says. A programmable chip that is not marked supported is a contradiction and is
        /// <see cref="ChipVerdict.Indeterminate"/> instead of being guessed at. A supported, programmable chip is
        /// <see cref="ChipVerdict.Programmable"/> if it has memory for a template and
        /// <see cref="ChipVerdict.SupportedOnly"/> if it has not. Every combination has a verdict.
        /// </summary>
        /// <param name="supported">Whether the report marks the chip as supported.</param>
        /// <param name="programmable">Whether the report marks the chip as usable for programming.</param>
        /// <param name="memory">The figure from <see cref="ParseFreeMemory"/>.</param>
        /// <returns>The verdict.</returns>
        public static ChipVerdict Classify(bool supported, bool programmable, FreeMemory memory)
        {
            if (!programmable)
            {
                return ChipVerdict.NotSupported;
            }

            if (!supported)
            {
                return ChipVerdict.Indeterminate;
            }

            return HasEnoughMemory(memory) ? ChipVerdict.Programmable : ChipVerdict.SupportedOnly;
        }

        // A sign and digits only. A long digit string that does not fit an int is still a number, not a sector list.
        private static bool LooksLikeWholeNumber(string word)
        {
            var digits = word.Length > 0 && (word[0] == '-' || word[0] == '+') ? word.Substring(1) : word;

            return digits.Length > 0 && digits.All(IsAsciiDigit);
        }

        private static bool HasAsciiDigit(string entry) => entry.Any(IsAsciiDigit);

        // char.IsDigit also accepts digits of other scripts, which the number parser does not.
        private static bool IsAsciiDigit(char c) => c >= '0' && c <= '9';
    }
}
