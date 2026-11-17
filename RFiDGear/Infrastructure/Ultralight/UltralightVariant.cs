using System;

namespace RFiDGear.Infrastructure.Ultralight
{
    /// <summary>
    /// The chips that share the page commands of MIFARE Ultralight: 4-byte pages, a READ that returns four pages, a WRITE of one
    /// page. Each has its own memory layout (see <see cref="UltralightMemoryMap"/>). A chip that is not in this list is refused,
    /// never guessed at: NTAG210 and NTAG212 are not in it until their layouts are confirmed on a card.
    /// </summary>
    public enum UltralightVariant
    {
        /// <summary>MIFARE Ultralight (MF0ICU1): 16 pages, no authentication.</summary>
        Plain = 1,

        /// <summary>MIFARE Ultralight C (MF0ICU2): 48 pages, 3DES authentication.</summary>
        C = 2,

        /// <summary>MIFARE Ultralight EV1 MF0UL11: 20 pages, password authentication.</summary>
        Ev1Mf0ul11 = 3,

        /// <summary>MIFARE Ultralight EV1 MF0UL21: 41 pages, password authentication.</summary>
        Ev1Mf0ul21 = 4,

        /// <summary>NTAG213: 45 pages, password authentication.</summary>
        Ntag213 = 5,

        /// <summary>NTAG215: 135 pages, password authentication.</summary>
        Ntag215 = 6,

        /// <summary>NTAG216: 231 pages, password authentication.</summary>
        Ntag216 = 7
    }

    /// <summary>
    /// The chip a task expects, stored in the project by name: the values are the numbers of <see cref="UltralightVariant"/>, and
    /// <see cref="Auto"/> (the default) takes whatever chip is found.
    /// </summary>
    public enum UltralightExpectation
    {
        /// <summary>Any chip. Only what is true for every chip can be checked before the card is asked.</summary>
        Auto = 0,

        /// <summary>Expects <see cref="UltralightVariant.Plain"/>.</summary>
        Plain = 1,

        /// <summary>Expects <see cref="UltralightVariant.C"/>.</summary>
        C = 2,

        /// <summary>Expects <see cref="UltralightVariant.Ev1Mf0ul11"/>.</summary>
        Ev1Mf0ul11 = 3,

        /// <summary>Expects <see cref="UltralightVariant.Ev1Mf0ul21"/>.</summary>
        Ev1Mf0ul21 = 4,

        /// <summary>Expects <see cref="UltralightVariant.Ntag213"/>.</summary>
        Ntag213 = 5,

        /// <summary>Expects <see cref="UltralightVariant.Ntag215"/>.</summary>
        Ntag215 = 6,

        /// <summary>Expects <see cref="UltralightVariant.Ntag216"/>.</summary>
        Ntag216 = 7
    }

    /// <summary>What an <see cref="UltralightExpectation"/> says about a chip that was found.</summary>
    public static class UltralightExpectations
    {
        /// <summary>
        /// The variant an expectation names.
        /// </summary>
        /// <param name="expectation">The expectation.</param>
        /// <param name="variant">The variant, when the expectation names one.</param>
        /// <returns><c>false</c> for <see cref="UltralightExpectation.Auto"/> (and for a number that is no expectation).</returns>
        public static bool TryGetVariant(this UltralightExpectation expectation, out UltralightVariant variant)
        {
            // The values of the two enums are the same by design (and by a test); Auto and a number that is no expectation name nothing.
            if (expectation != UltralightExpectation.Auto && Enum.IsDefined(typeof(UltralightExpectation), expectation))
            {
                variant = (UltralightVariant)(int)expectation;

                return true;
            }

            variant = default;

            return false;
        }

        /// <summary>
        /// Whether the chip that was found is one the expectation accepts.
        /// </summary>
        /// <param name="expectation">What the task expects.</param>
        /// <param name="found">The chip that was found.</param>
        /// <returns><c>true</c> for <see cref="UltralightExpectation.Auto"/> and for the variant the expectation names.</returns>
        public static bool Accepts(this UltralightExpectation expectation, UltralightVariant found)
        {
            return expectation == UltralightExpectation.Auto
                || (expectation.TryGetVariant(out var named) && named == found);
        }
    }
}
