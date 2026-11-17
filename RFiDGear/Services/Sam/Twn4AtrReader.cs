#nullable enable
namespace RFiDGear.Services.Sam
{
    /// <summary>
    /// Takes the values that the contact interface of a TWN4 needs for T=1 out of the ATR of a SAM (ISO 7816-3): the extra guard time (TC1), and
    /// the size of the information field (TA3) and the waiting integers (TB3) of the first interface bytes of T=1. What the ATR does not say
    /// gets the default of the standard. A card that does not offer T=1 gets none.
    /// </summary>
    internal static class Twn4AtrReader
    {
        private const byte DefaultIfsc = 0x20;

        // BWI 4 and CWI 13: the defaults of ISO 7816-3 for T=1.
        private const byte DefaultWaitingIntegers = 0x4D;

        /// <summary>Reads the T=1 settings out of an ATR.</summary>
        /// <param name="atr">The ATR, from the initial character to the check byte.</param>
        /// <returns>The settings, or <see langword="null"/> when the ATR is no ATR, is cut off, or does not offer T=1.</returns>
        internal static Twn4SamSettings? SettingsOf(byte[]? atr)
        {
            if (atr == null || atr.Length < 2 || (atr[0] != 0x3B && atr[0] != 0x3F))
            {
                return null;
            }

            var index = 2;
            var historicalBytes = atr[1] & 0x0F;
            var present = atr[1] >> 4;
            var group = 1;
            var protocolOfGroup = -1;
            var offersT1 = false;
            byte? guardTime = null;
            byte? ifsc = null;
            byte? waitingIntegers = null;

            while (true)
            {
                byte? ta = null;
                byte? tb = null;
                byte? tc = null;
                byte? td = null;

                if (!TakeIf(atr, present, 0x1, ref index, ref ta) || !TakeIf(atr, present, 0x2, ref index, ref tb)
                    || !TakeIf(atr, present, 0x4, ref index, ref tc) || !TakeIf(atr, present, 0x8, ref index, ref td))
                {
                    return null;
                }

                if (group == 1)
                {
                    guardTime = tc;
                }
                else if (group >= 3 && protocolOfGroup == 1 && ifsc == null && waitingIntegers == null)
                {
                    // The first interface bytes of T=1 (TA3 and TB3 when TD2 says T=1).
                    ifsc = ta;
                    waitingIntegers = tb;
                }

                if (td == null)
                {
                    break;
                }

                protocolOfGroup = td.Value & 0x0F;
                offersT1 |= protocolOfGroup == 1;
                present = td.Value >> 4;
                group++;
            }

            // What is left must hold the historical bytes (and the check byte, which a card that offers T=1 has).
            if (!offersT1 || atr.Length < index + historicalBytes)
            {
                return null;
            }

            return new Twn4SamSettings(guardTime ?? 0, waitingIntegers ?? DefaultWaitingIntegers, ifsc ?? DefaultIfsc);
        }

        private static bool TakeIf(byte[] atr, int present, int bit, ref int index, ref byte? value)
        {
            if ((present & bit) == 0)
            {
                return true;
            }

            if (index >= atr.Length)
            {
                return false;
            }

            value = atr[index++];

            return true;
        }
    }
}
