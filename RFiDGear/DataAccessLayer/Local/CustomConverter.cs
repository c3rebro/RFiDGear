using RFiDGear.DataAccessLayer;

using Elatec.NET;

using System;
using System.Collections.Generic;

namespace RFiDGear
{
    //**************************************************************************
    // Hexidecimal to Byte and Byte to Hexadecimal conversion
    //**************************************************************************
    public static class CustomConverter
    {
        public static string DesfireKeyToCheck { get; private set; }
        public static string ClassicKeyToCheck { get; private set; }

        #region parser

        public static bool KeyFormatQuickCheck(string keyToCheck)
        {
            foreach (char c in keyToCheck.ToCharArray())
            {
                if (c == ' ')
                {
                    return true;
                }
            }
            return false;
        }

        public static int GetByteCount(string hexString)
        {
            int numHexChars = 0;
            char c;
            // remove all none A-F, 0-9, characters
            for (int i = 0; i < hexString.Length; i++)
            {
                c = hexString[i];
                if (IsHexDigit(c))
                {
                    numHexChars++;
                }
            }
            // if odd number of characters, discard last character
            if (numHexChars % 2 != 0)
            {
                numHexChars--;
            }
            return numHexChars / 2; // 2 characters per byte
        }

        public static byte[] GetBytes(string hexString, out int discarded)
        {
            discarded = 0;
            string newString = "";
            char c;
            // remove all none A-F, 0-9, characters
            for (int i = 0; i < hexString.Length; i++)
            {
                c = hexString[i];
                if (IsHexDigit(c))
                {
                    newString += c;
                }
                else
                {
                    discarded++;
                }
            }
            // if odd number of characters, discard last character
            if (newString.Length % 2 != 0)
            {
                discarded++;
                newString = newString.Substring(0, newString.Length - 1);
            }

            int byteLength = newString.Length / 2;
            byte[] bytes = new byte[byteLength];
            string hex;
            int j = 0;
            for (int i = 0; i < bytes.Length; i++)
            {
                hex = new String(new Char[] { newString[j], newString[j + 1] });
                bytes[i] = HexToByte(hex);
                j = j + 2;
            }
            return bytes;
        }

        public static string HexToString(byte[] bytes)
        {
            string hexString = "";
            for (int i = 0; i < bytes.Length; i++)
            {
                hexString += bytes[i].ToString("X2");
            }
            return hexString;
        }

        public static string HexToString(byte bytes)
        {
            string hexString = "";
            {
                hexString += bytes.ToString("X2");
            }
            return hexString;
        }

        public static bool IsInHexFormat(string hexString)
        {
            bool hexFormat = true;

            foreach (char digit in hexString)
            {
                if (!IsHexDigit(digit))
                {
                    hexFormat = false;
                    break;
                }
            }
            return hexFormat;
        }

        public static bool IsHexDigit(Char c)
        {
            int numChar;
            int numA = Convert.ToInt32('A');
            int num1 = Convert.ToInt32('0');
            c = Char.ToUpper(c);
            numChar = Convert.ToInt32(c);
            if (numChar >= numA && numChar < (numA + 6))
            {
                return true;
            }

            if (numChar >= num1 && numChar < (num1 + 10))
            {
                return true;
            }

            return false;
        }

        public static string FormatMifareClassicKeyWithSpacesEachByte(string Str)
        {
            string temp = Str;

            if (string.IsNullOrEmpty(temp))
            {
                return "error 0";
            }

            if (!IsInHexFormat(temp))
            {
                return "error 1";
            }

            if (temp.Length != 12)
            {
                return "error 2";
            }
            else
            {
                for (int i = (Str.Length) - 2; i > 0; i -= 2)
                {
                    temp = temp.Insert(i, " ");
                }

                return temp.ToLower();
            }
        }

        public static KEY_ERROR FormatMifareDesfireKeyStringWithSpacesEachByte(string Str)
        {
            string temp = Str;

            if (string.IsNullOrEmpty(temp))
            {
                return KEY_ERROR.KEY_IS_EMPTY;
            }

            if (!IsInHexFormat(temp))
            {
                return KEY_ERROR.KEY_HAS_WRONG_FORMAT;
            }

            if (temp.Length != 32)
            {
                return KEY_ERROR.KEY_HAS_WRONG_LENGTH;
            }

            for (int i = (Str.Length) - 2; i > 0; i -= 2)
            {
                temp = temp.Insert(i, " ");
            }

            DesfireKeyToCheck = temp.ToUpper();

            return KEY_ERROR.NO_ERROR;
        }

        public static KEY_ERROR FormatMifareClassicKeyStringWithSpacesEachByte(string Str)
        {
            string temp = Str;

            if (string.IsNullOrEmpty(temp))
            {
                return KEY_ERROR.KEY_IS_EMPTY;
            }

            if (!IsInHexFormat(temp))
            {
                return KEY_ERROR.KEY_HAS_WRONG_FORMAT;
            }

            if (temp.Length != 12)
            {
                return KEY_ERROR.KEY_HAS_WRONG_LENGTH;
            }

            for (int i = (Str.Length) - 2; i > 0; i -= 2)
            {
                temp = temp.Insert(i, " ");
            }

            ClassicKeyToCheck = temp.ToUpper();

            return KEY_ERROR.NO_ERROR;
        }

        public static string NormalizeKey(string keyToNormalize)
        {
            char[] c = keyToNormalize.ToCharArray();

            for (int i = 0; i < keyToNormalize.Length; i++)
            {
                if (c[i] == ' ')
                {
                    c[i] = '\0';
                }
            }

            return new string(c);
        }

        #endregion parser

        #region Converter

        public static int GetChipBasedDataBlockNumber(CARD_TYPE _cardType, int _sectorNumber, int _dataBlockNumberSectorBased)
        {
            int blockCount = 0;
            int dataBlockNumberChipBased = 0;

            switch (_cardType)
            {
                case CARD_TYPE.Mifare1K:
                    blockCount = 4;
                    break;

                case CARD_TYPE.Mifare2K:
                    break;

                case CARD_TYPE.Mifare4K:
                    blockCount = (_sectorNumber <= 31 ? 4 : 16);
                    break;

                default:
                    throw new InvalidOperationException("Unexpected Card Type");

            }

            dataBlockNumberChipBased = _sectorNumber <= 31
                ? (((_sectorNumber + 1) * blockCount) - (blockCount - dataBlockNumberChipBased))
                : ((128 + (_sectorNumber - 31) * blockCount) - (blockCount - dataBlockNumberChipBased));

            return dataBlockNumberChipBased + _dataBlockNumberSectorBased;
        }

        public static bool SectorTrailerHasWrongFormat(byte[] st)
        {
            uint C1x, C2x, C3x;
            uint _C1, _C2, _C3;

            _C2 = st[0];
            _C2 &= 0xF0;
            _C2 >>= 4;

            C2x = st[2];
            C2x &= 0x0F;
            C2x |= 0xF0;
            C2x ^= 0xFF;

            if (C2x != _C2)
            {
                return true;
            }
            else
            {
                _C1 = st[0];
                _C1 &= 0x0F;

                C1x = st[1];
                C1x &= 0xF0;
                C1x >>= 4;
                C1x |= 0xF0;
                C1x ^= 0xFF;

                if (C1x != _C1)
                {
                    return true;
                }
                else
                {
                    _C3 = st[1];
                    _C3 &= 0x0F;

                    C3x = st[2];
                    C3x &= 0xF0;
                    C3x >>= 4;
                    C3x |= 0xF0;
                    C3x ^= 0xFF;

                    if (C3x != _C3)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
        }

        public static bool SectorTrailerHasWrongFormat(string stString)
        {
            _ = new byte[255];
            byte[] st = GetBytes(stString, out int _);

            if (!SectorTrailerHasWrongFormat(st))
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        #endregion Converter

        #region Extensions

        public static IEnumerable<string> GenerateStringSequence(int n1, int n2)
        {
            while (n1 <= n2)
            {
                yield return n1++.ToString();
            }
        }

        private static byte HexToByte(string hex)
        {
            if (hex.Length > 2 || hex.Length <= 0)
            {
                throw new ArgumentException("hex must be 1 or 2 characters in length");
            }

            byte newByte = byte.Parse(hex, System.Globalization.NumberStyles.HexNumber);
            return newByte;
        }

        public static byte[] buildSectorTrailerInvNibble(byte[] st)
        {
            uint _C1, _C2, _C3;

            byte[] new_st = new byte[st.Length];

            _C3 = st[2];
            _C3 ^= 0xFF;
            _C3 >>= 4;
            _C3 &= 0x0F;

            new_st[1] = st[1];
            new_st[1] &= 0xF0;
            new_st[1] |= (byte)_C3;

            _C2 = st[2];
            _C2 ^= 0xFF;
            _C2 &= 0x0F;

            new_st[0] = (byte)_C2;
            new_st[0] <<= 4;

            _C1 = st[1];
            _C1 ^= 0xFF;
            _C1 >>= 4;
            _C1 &= 0x0F;

            new_st[0] &= 0xF0;
            new_st[0] |= (byte)_C1;

            new_st[1] |= st[1];
            new_st[2] |= st[2];

            return new_st;
        }

        /// 
        /// Class for calculating CRC8 checksums...
        /// 
        public class CRC8Calc
        {
            private byte[] table = new byte[256];

            public byte Checksum(params byte[] val)
            {
                if (val == null)
                {
                    throw new ArgumentNullException("val");
                }

                byte crc = 0xc7;

                foreach (byte b in val)
                {
                    crc = table[(byte)(crc ^ b)];
                }

                return crc;
            }

            public byte[] Table
            {
                get => table;
                set => table = value;
            }

            public byte[] GenerateTable(CRC8_POLY polynomial)
            {
                byte[] csTable = new byte[256];

                for (int i = 0; i < 256; ++i)
                {
                    int curr = i;

                    for (int j = 0; j < 8; ++j)
                    {
                        if ((curr & 0x80) != 0)
                        {
                            curr = (curr << 1) ^ (byte)polynomial;
                        }
                        else
                        {
                            curr <<= 1;
                        }
                    }

                    csTable[i] = (byte)(curr & 0xFF);
                }

                return csTable;
            }

            public CRC8Calc(CRC8_POLY polynomial)
            {
                table = GenerateTable(polynomial);
            }
        }
        #endregion Extensions
    }
}