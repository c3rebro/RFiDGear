namespace RFiDGear.Infrastructure.Ultralight
{
    /// <summary>
    /// What the chip types of the application say about the chips of the Ultralight family (the Ultralight, the Ultralight C, the
    /// Ultralight EV1 and the NTAG21x, which are Type 2 tags with 4-byte pages): which chip types are of the family, and which have
    /// a layout in <see cref="UltralightMemoryMap"/>. The chip tree builds its pages from this, and a chip that has no layout has no
    /// pages in it: its pages are not guessed at.
    /// </summary>
    internal static class UltralightChipTypes
    {
        /// <summary>
        /// Whether a chip type is a chip of the family: the Ultralight values 0x90 to 0x94 and the NTAG21x values 0xA0 to 0xA6 (the
        /// high byte of the type). An NTAG 424 DNA and an NTAG 426 are no Type 2 tags and are not of the family.
        /// </summary>
        /// <param name="type">The chip type.</param>
        /// <returns><c>true</c> when the chip is of the family.</returns>
        internal static bool IsOfTheFamily(CARD_TYPE type)
        {
            var value = (int)type;

            if ((value & 0xFF) != 0)
            {
                return false;
            }

            var high = (value >> 8) & 0xFF;

            return (high >= 0x90 && high <= 0x94) || (high >= 0xA0 && high <= 0xA6);
        }

        /// <summary>
        /// Gets the layout variant of a chip type.
        /// </summary>
        /// <param name="type">The chip type.</param>
        /// <param name="variant">The variant, when the chip type has a layout.</param>
        /// <returns>
        /// <c>false</c> for a chip of another family and for a chip of the family without a confirmed layout (an Ultralight EV1 of
        /// another size, the NTAG210, NTAG211, NTAG212 and NTAG214).
        /// </returns>
        internal static bool TryGetVariant(CARD_TYPE type, out UltralightVariant variant)
        {
            switch (type)
            {
                case CARD_TYPE.MifareUltralight:
                    variant = UltralightVariant.Plain;
                    return true;

                case CARD_TYPE.MifareUltralightC:
                    variant = UltralightVariant.C;
                    return true;

                case CARD_TYPE.MifareUltralight_EV1_MF0UL11:
                    variant = UltralightVariant.Ev1Mf0ul11;
                    return true;

                case CARD_TYPE.MifareUltralight_EV1_MF0UL21:
                    variant = UltralightVariant.Ev1Mf0ul21;
                    return true;

                case CARD_TYPE.NTAG_213:
                    variant = UltralightVariant.Ntag213;
                    return true;

                case CARD_TYPE.NTAG_215:
                    variant = UltralightVariant.Ntag215;
                    return true;

                case CARD_TYPE.NTAG_216:
                    variant = UltralightVariant.Ntag216;
                    return true;

                default:
                    variant = default;
                    return false;
            }
        }

        /// <summary>
        /// The chip a call expects when the chip on the reader is of this type.
        /// </summary>
        /// <param name="type">The chip type.</param>
        /// <returns>The expectation that names the chip, or <see cref="UltralightExpectation.Auto"/> when the type has no layout.</returns>
        internal static UltralightExpectation ExpectationOf(CARD_TYPE type) =>
            TryGetVariant(type, out var variant) ? (UltralightExpectation)(int)variant : UltralightExpectation.Auto;
    }
}
