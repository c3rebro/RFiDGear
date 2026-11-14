using System;
using System.Collections.Generic;

namespace RFiDGear.Infrastructure.Ultralight
{
    /// <summary>
    /// The memory layout of one <see cref="UltralightVariant"/>: how many 4-byte pages it has, which of them are user memory and
    /// which are not (the header with the UID, the lock bytes and the one-time-programmable page, and after the user memory the
    /// dynamic lock bytes, the configuration, the password and the key). A page that is in the chip and not user memory is a
    /// <em>protected</em> page: a wrong write to it can lock the chip or its data for good.
    /// </summary>
    /// <remarks>
    /// The layouts are those of the NXP data sheets (MF0ICU1, MF0ICU2, MF0ULx1, NTAG213/215/216). The NTAG215 was checked on a card.
    /// </remarks>
    public sealed class UltralightMemoryMap
    {
        /// <summary>The size of a page in bytes. Every variant has 4-byte pages.</summary>
        public const int PageSize = 4;

        /// <summary>The first user page. Pages 0 to 3 are the UID, the internal byte, the lock bytes and the OTP bytes in every variant.</summary>
        public const int FirstUserPage = 4;

        /// <summary>The number of pages of the largest variant (NTAG216): no chip has a page at or above this number.</summary>
        public const int MaxPageCount = 231;

        // Pages, last user page. After the user memory come, per chip: Ultralight C the lock bytes, the counter, AUTH0, AUTH1 and the
        // key (pages 40-47); MF0UL11 CFG0, CFG1, PWD and PACK (16-19); MF0UL21 the dynamic lock bytes and the same four (36-40); the
        // NTAGs the dynamic lock bytes and the same four (40-44, 130-134, 226-230). The plain Ultralight has nothing after page 15.
        private static readonly UltralightMemoryMap[] Maps =
        {
            new UltralightMemoryMap(UltralightVariant.Plain, 16, 15),
            new UltralightMemoryMap(UltralightVariant.C, 48, 39),
            new UltralightMemoryMap(UltralightVariant.Ev1Mf0ul11, 20, 15),
            new UltralightMemoryMap(UltralightVariant.Ev1Mf0ul21, 41, 35),
            new UltralightMemoryMap(UltralightVariant.Ntag213, 45, 39),
            new UltralightMemoryMap(UltralightVariant.Ntag215, 135, 129),
            new UltralightMemoryMap(UltralightVariant.Ntag216, 231, 225)
        };

        private UltralightMemoryMap(UltralightVariant variant, int pageCount, int lastUserPage)
        {
            Variant = variant;
            PageCount = pageCount;
            LastUserPage = lastUserPage;
        }

        /// <summary>Gets the variant this layout belongs to.</summary>
        public UltralightVariant Variant { get; }

        /// <summary>Gets the number of pages of the chip, the first being page 0.</summary>
        public int PageCount { get; }

        /// <summary>Gets the last user page. The pages after it up to <see cref="PageCount"/> are protected.</summary>
        public int LastUserPage { get; }

        /// <summary>Gets the number of user pages.</summary>
        public int UserPageCount => LastUserPage - FirstUserPage + 1;

        /// <summary>Gets the number of protected pages (the four first ones and those after the user memory).</summary>
        public int ProtectedPageCount => PageCount - UserPageCount;

        /// <summary>
        /// The layout of a variant.
        /// </summary>
        /// <param name="variant">The variant.</param>
        /// <returns>Its layout.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The value is no <see cref="UltralightVariant"/>.</exception>
        public static UltralightMemoryMap For(UltralightVariant variant)
        {
            foreach (var map in Maps)
            {
                if (map.Variant == variant)
                {
                    return map;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(variant), variant, "No layout is known for this variant.");
        }

        /// <summary>
        /// The layout that belongs to the answer of the GET_VERSION command, which tells the chips apart that the ATQA and the SAK
        /// cannot (an NTAG and an Ultralight EV1 both answer with the SAK 0x00): byte 2 is the product type, byte 6 the storage size.
        /// </summary>
        /// <param name="answer">The 8 bytes of the answer, or what came back instead.</param>
        /// <param name="map">The layout, when the answer names a chip this class knows.</param>
        /// <returns>
        /// <c>false</c> for anything else, including an answer that is not 8 bytes, a vendor other than NXP, and a chip of the same
        /// family whose layout is not confirmed (NTAG210, NTAG212): such a chip is refused, not guessed at.
        /// </returns>
        public static bool TryFromGetVersion(byte[] answer, out UltralightMemoryMap map)
        {
            map = null;

            // 0x00, vendor 0x04 (NXP), product type, product subtype, major, minor, storage size, protocol type 0x03 (ISO 14443-3).
            if (answer == null || answer.Length != 8 || answer[0] != 0x00 || answer[1] != 0x04 || answer[7] != 0x03)
            {
                return false;
            }

            // The storage size byte holds n in its upper 7 bits; the user memory is 2^n bytes if the lowest bit is 0 and more than 2^n
            // and less than 2^(n+1) if it is 1. So 48 bytes are 0x0B, 128 bytes 0x0E, 144 bytes 0x0F, 504 bytes 0x11, 888 bytes 0x13.
            UltralightVariant variant;

            switch (answer[2])
            {
                case 0x03: // Ultralight EV1
                    switch (answer[6])
                    {
                        case 0x0B: variant = UltralightVariant.Ev1Mf0ul11; break;
                        case 0x0E: variant = UltralightVariant.Ev1Mf0ul21; break;
                        default: return false;
                    }

                    break;

                case 0x04: // NTAG. NTAG210 (0x0B) and NTAG212 (0x0E) are the same family and are not here: their layouts are not confirmed.
                    switch (answer[6])
                    {
                        case 0x0F: variant = UltralightVariant.Ntag213; break;
                        case 0x11: variant = UltralightVariant.Ntag215; break;
                        case 0x13: variant = UltralightVariant.Ntag216; break;
                        default: return false;
                    }

                    break;

                default:
                    return false;
            }

            map = For(variant);

            return true;
        }

        /// <summary>Whether the page is a page of the chip.</summary>
        /// <param name="page">The page number.</param>
        /// <returns><c>true</c> from page 0 to the last page.</returns>
        public bool Contains(int page)
        {
            return page >= 0 && page < PageCount;
        }

        /// <summary>Whether all pages of a range are pages of the chip.</summary>
        /// <param name="startPage">The first page.</param>
        /// <param name="pageCount">The number of pages, at least 1.</param>
        /// <returns><c>false</c> for an empty or negative range and for one that ends after the last page.</returns>
        public bool ContainsRange(int startPage, int pageCount)
        {
            // Written so that nothing can overflow: the range ends in the chip when its length fits in what is left after its start.
            return pageCount >= 1 && startPage >= 0 && startPage < PageCount && pageCount <= PageCount - startPage;
        }

        /// <summary>Whether the page is user memory.</summary>
        /// <param name="page">The page number.</param>
        /// <returns><c>true</c> from <see cref="FirstUserPage"/> to <see cref="LastUserPage"/>.</returns>
        public bool IsUserPage(int page)
        {
            return page >= FirstUserPage && page <= LastUserPage;
        }

        /// <summary>Whether the page is in the chip but not user memory.</summary>
        /// <param name="page">The page number.</param>
        /// <returns><c>true</c> for pages 0 to 3 and for the pages after the user memory; <c>false</c> for a page that is not in the chip.</returns>
        public bool IsProtectedPage(int page)
        {
            return Contains(page) && !IsUserPage(page);
        }
    }
}
