/*
 * Created by SharpDevelop.
 * User: C3rebro
 * Date: 12.03.2018
 * Time: 21:02
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ByteArrayHelper
{

    public class ByteArray
    {
        public int Length => Data.Length;

        public byte[] Data { get; set; }

        public ByteArray()
        {

        }

        public ByteArray(int length)
        {
            Data = new byte[length];
        }

        public ByteArray(byte[] _data)
        {
            Data = _data != null ? (byte[])_data.Clone() : Array.Empty<byte>();
        }

        public ByteArray Or(byte[] source, bool isLittleEndian = true)
        {

            if (!isLittleEndian) // or-ing from right to left
            {
                int targetIndex = Data.Length - 1;
                for (int sourceIndex = source.Length - 1; sourceIndex >= 0 && targetIndex >= 0; sourceIndex--, targetIndex--)
                {
                    Data[targetIndex] |= source[sourceIndex];
                }
            }
            else // the other way around
            {
                for (int i = 0; i < Data.Length; i++)
                {
                    if (source.Length > i)
                    {
                        Data[i] |= source[i];
                    }
                    else
                    {
                        break;
                    }
                }
            }
            return this;
        }
    }

    namespace Extensions
    {
        /// <summary>
        /// Convert byte and byte[] to and from other types
        /// </summary>
        public static class ByteArrayConverter
        {
            /// <summary>
            /// Reverses an Array of Bytes from Little Endian to Big Endian or Vice Versa
            /// </summary>
            /// <param name="arrToReverse">The byte[] Array to be reversed</param>
            /// <returns>The byte[] Array in Reversed Order</returns>
            public static byte[] Reverse(byte[] arrToReverse)
            {
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(arrToReverse);
                }

                return arrToReverse;
            }

            /// <summary>
            /// Reverses the Bit Order in a Single Byte
            /// </summary>
            /// <param name="byteToPutInReverseOrder">The byte to be reversed</param>
            /// <returns>The byte in reversed Order</returns>
            public static byte Reverse(byte byteToPutInReverseOrder)
            {
                byte byteInReversedOrder = 0;
                for (byte i = 0; i < 8; ++i)
                {
                    byteInReversedOrder <<= 1;
                    byteInReversedOrder |= (byte)(byteToPutInReverseOrder & 1);
                    byteToPutInReverseOrder >>= 1;
                }
                return byteInReversedOrder;
            }

            /// <summary>
            /// Gets the Amount of bytes in a string that contains one or more bytes in the format "00" to "FF" each byte
            /// </summary>
            /// <param name="hexString">A string containing one or more bytes in the format "00" to "FF" each byte</param>
            /// <returns>The Amount of Bytes in the string</returns>
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

            /// <summary>
            /// Converts a string with hexadecimal chars to a byte array (e.g.: "FF00FF" -> byte[3]{0xFF,0x00,0xFF})
            /// </summary>
            /// <param name="hexString">The string to convert</param>
            /// <param name="discarded">The Number of characters in the string that could not be converted to a byte</param>
            /// <returns>The Array that contains all converted values. Reliably only if <param name="discarded"/> is returned as 0</returns>
            public static byte[] GetBytesFrom(string hexString)
            {

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
                }
                // if odd number of characters, discard last character
                if (newString.Length % 2 != 0)
                {
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

            /// <summary>
            /// Converts a string with hexadecimal chars to a byte array (e.g.: "FF00FF" -> byte[3]{0xFF,0x00,0xFF})
            /// </summary>
            /// <param name="hexString">The string to convert</param>
            /// <param name="discarded">The Number of characters in the string that could not be converted to a byte</param>
            /// <returns>The Array that contains all converted values. Reliably only if <param name="discarded"/> is returned as 0</returns>
            public static byte[] GetBytesFrom(string hexString, bool reverse)
            {
                byte[] bytes = new byte[GetBytesFrom(hexString).Length];

                bytes = GetBytesFrom(hexString);

                if (reverse)
                {
                    Array.Reverse(bytes);
                }

                return bytes;
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="bytes"></param>
            /// <returns></returns>
            public static string GetStringFrom(byte[] bytes)
            {
                string hexString = "";
                if (bytes != null)
                {
                    for (int i = 0; i < bytes.Length; i++)
                    {
                        hexString += bytes[i].ToString("X2");
                    }
                }
                return hexString;
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="bytes"></param>
            /// <returns></returns>
            public static string GetStringFrom(UInt32 int32Value)
            {
                string hexString = int32Value.ToString("X8");

                return hexString;
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="bytes"></param>
            /// <returns></returns>
            public static string GetStringFrom(byte[] bytes, bool reverse)
            {
                if(reverse)
                {
                    Array.Reverse(bytes);
                }

                string hexString = "";
                if (bytes != null)
                {
                    for (int i = 0; i < bytes.Length; i++)
                    {
                        hexString += bytes[i].ToString("X2");
                    }
                }
                return hexString;
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="bytes"></param>
            /// <returns></returns>
            public static string GetStringFrom(byte[] bytes, int offset)
            {
                string hexString = "";
                if (bytes != null)
                {
                    for (int i = offset; i < bytes.Length; i++)
                    {
                        hexString += bytes[i].ToString("X2");
                    }
                }
                return hexString;
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="bytes"></param>
            /// <returns></returns>
            public static string GetStringFrom(byte bytes)
            {
                string hexString = "";
                {
                    hexString += bytes.ToString("X2");
                }
                return hexString;
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="hexString"></param>
            /// <returns></returns>
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

            public static int SearchBytePattern(byte[] src, byte[] pattern)
            {
                int maxFirstCharSlot = src.Length - pattern.Length + 1;
                for (int i = 0; i < maxFirstCharSlot; i++)
                {
                    if (src[i] != pattern[0]) // compare only first byte
                    {
                        continue;
                    }

                    // found a match on first byte, now try to match rest of the pattern
                    for (int j = pattern.Length - 1; j >= 1; j--)
                    {
                        if (src[i + j] != pattern[j])
                        {
                            break;
                        }

                        if (j == 1)
                        {
                            return i;
                        }
                    }
                }
                return 0;
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="c"></param>
            /// <returns></returns>
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

            /// <summary>
            /// 
            /// </summary>
            /// <param name="hex"></param>
            /// <returns></returns>
            private static byte HexToByte(string hex)
            {
                byte newByte = 0x00;

                try
                {
                    if (!(hex.Length > 2 || hex.Length <= 0))
                    {
                        newByte = byte.Parse(hex, System.Globalization.NumberStyles.HexNumber);
                    }
                }
                catch
                {
                    return 0x00;
                }

                return newByte;
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="Source"></param>
            /// <param name="Add"></param>
            /// <returns></returns>
            private static byte[] AddByteArray(byte[] Source, byte[] Add)
            {
                // Is Source = null
                if (Source == null)
                {
                    // Yes, copy Add in Source
                    Source = Add;
                    // Return source
                    return Source;
                }
                // Initialize buffer array, with the length of Source and Add
                byte[] buffer = new byte[Source.Length + Add.Length];
                // Copy Source in buffer
                for (int i = 0; i < Source.Length; i++)
                {
                    // Copy source bytes to buffer
                    buffer[i] = Source[i];
                }
                // Add the secound array to buffer
                for (int i = Source.Length; i < buffer.Length; i++)
                {
                    // Copy Add bytes after the Source bytes in buffer
                    buffer[i] = Add[i - Source.Length];
                }
                // Return the combined array buffer
                return buffer;
            }// End of AddByteArray

            /// <summary>
            /// 
            /// </summary>
            /// <param name="Source"></param>
            /// <param name="Add"></param>
            /// <returns></returns>
            private static byte[] AddByte2Array(byte[] Source, byte Add)
            {
                if (Source == null)
                {
                    return new byte[] { Add };
                }
                // Initialize buffer with the length of Source + 1
                byte[] buffer = new byte[Source.Length + 1];
                // Copy Source in buffer
                for (int i = 0; i < Source.Length; i++)
                {
                    // Copy Source bytes in buffer array
                    buffer[i] = Source[i];
                }
                // Add byte behind the Source
                buffer[Source.Length] = Add;
                // Return the buffer
                return buffer;
            }// End of AddByte2Array

            /// <summary>
            /// 
            /// </summary>
            /// <param name="Source"></param>
            /// <param name="index"></param>
            /// <param name="count"></param>
            /// <returns></returns>
            public static byte[] Trim(byte[] Source, int index, int count)
            {
                // Initialize buffer with the segment size
                byte[] buffer = new byte[count];
                // Copy bytes from index until count
                for (int i = index; i < (index + count); i++)
                {
                    // Copy in segment buffer
                    buffer[i - index] = Source[i];
                }
                // Return segment buffer
                return buffer;
            }// End of GetSegmentFromByteArray

            /// <summary>
            /// 
            /// </summary>
            /// <param name="Array1"></param>
            /// <param name="index1"></param>
            /// <param name="Array2"></param>
            /// <param name="index2"></param>
            /// <param name="count"></param>
            /// <returns></returns>
            private static bool CompareArraysSegments(byte[] Array1, int index1, byte[] Array2, int index2, int count)
            {
                // Plausibility check, is index + count longer than arran
                if (((index1 + count) > Array1.Length) || ((index2 + count) > Array2.Length))
                {
                    // Yes, return false
                    return false;
                }
                // Compare segments of count
                for (int i = 0; i < count; i++)
                {
                    // Is byte in Array1 == byte in Array2?
                    if (Array1[i + index1] != Array2[i + index2])
                    {
                        // No, return flase
                        return false;
                    }
                }
                // Return true
                return true;
            }// End of CompareArraysSegment
        }
    }

    namespace CRCTool
    {
        // based on implementation from here
        // http://svn.jimblackler.net/jimblackler/trunk/Visual%20Studio%202005/Projects/PersistentObjects/CRCTool.cs
        // Original Copyright:
        /// Copyright (c) 2003 Thoraxcentrum, Erasmus MC, The Netherlands.
        /// 
        /// Written by Marcel de Wijs with help from a lot of others, 
        /// especially Stefan Nelwan
        /// 
        /// This code is for free. I ported it from several different sources to C#.
        /// 
        /// JB mods: made private functions that are not used externally, March 07.
        /// 
        /// For comments: Marcel_de_Wijs@hotmail.com

        public class CRC
        {
            // this holds all neccessary parameters for the various CRC algorithms
            public class CRCParameters
            {
                public string[] Names
                {
                    get; private set;
                }
                public int Width
                {
                    get; private set;
                }
                public ulong Polynom
                {
                    get; private set;
                }
                public ulong Init
                {
                    get; private set;
                }
                public bool ReflectIn
                {
                    get; private set;
                }
                public bool ReflectOut
                {
                    get; private set;
                }
                public ulong XOROut
                {
                    get; private set;
                }

                public ulong CheckValue
                {
                    get; private set;
                }

                public CRCParameters(int width, ulong poly, ulong init, bool refIn, bool refOut, ulong xorOut, ulong check, params string[] names)
                {
                    Names = names;
                    Width = width;
                    Polynom = poly;
                    Init = init;
                    ReflectIn = refIn;
                    ReflectOut = refOut;
                    XOROut = xorOut;
                    CheckValue = check;
                }
            }

            // source: http://reveng.sourceforge.net/crc-catalogue
            private static CRCParameters[] s_CRCParams = new CRCParameters[]  {
            // CRC 3
            new CRCParameters(3, 0x3, 0x7, true, true, 0x0, 0x6,
                "CRC-3/ROHC"),

            // CRC 4
            new CRCParameters(4, 0x3, 0x0, true, true, 0x0, 0x7,
                "CRC-4/ITU"),

            // CRC 5
            new CRCParameters(5, 0x09, 0x09, false, false, 0x00, 0x00,
                "CRC-5/EPC"),
            new CRCParameters(5, 0x15, 0x00, true, true, 0x00, 0x07,
                "CRC-5/ITU"),
            new CRCParameters(5, 0x05, 0x1f, true, true, 0x1f, 0x19,
                "CRC-5/USB"),

            // CRC 6
            new CRCParameters(6, 0x19, 0x00, true, true, 0x00, 0x26,
                "CRC-6/DARC"),
            new CRCParameters(6, 0x03, 0x00, true, true, 0x00, 0x06,
                "CRC-6/ITU"),

            // CRC 7
            new CRCParameters(7, 0x09, 0x00, false, false, 0x00, 0x75,
                "CRC-7"),
            new CRCParameters(7, 0x4f, 0x7f, true, true, 0x00, 0x53,
                "CRC-7/ROHC"),

            // CRC 8
            new CRCParameters(8, 0x07, 0x00, false, false, 0x00, 0xf4,
                "CRC-8"),
            new CRCParameters(8, 0x39, 0x00, true, true, 0x00, 0x15,
                "CRC-8/DARC"),
            new CRCParameters(8, 0x1d, 0xff, true, true, 0x00, 0x97,
                "CRC-8/EBU"),
            new CRCParameters(8, 0x1d, 0xfd, false, false, 0x00, 0x7e,
                "CRC-8/I-CODE"),
            new CRCParameters(8, 0x07, 0x00, false, false, 0x55, 0xa1,
                "CRC-8/ITU"),
            new CRCParameters(8, 0x31, 0x00, true, true, 0x00, 0xa1,
                "CRC-8/MAXIM", "DOW-CRC"),
            new CRCParameters(8, 0x07, 0xff, true, true, 0x00, 0xd0,
                "CRC-8/ROHC"),
            new CRCParameters(8, 0x9b, 0x00, true, true, 0x00, 0x25,
                "CRC-8/WCDMA"),

            // CRC 10
            new CRCParameters(10, 0x233, 0x000, false, false, 0x000, 0x199,
                "CRC-10"),

            // CRC 11
            new CRCParameters(11, 0x385, 0x01a, false, false, 0x000, 0x5a3,
                "CRC-11"),

            // CRC 12
            new CRCParameters(12, 0x80f, 0x000, false, true, 0x000, 0xdaf,
                "CRC-12/3GPP"),
            new CRCParameters(12, 0x80f, 0x000, false, false, 0x000, 0xf5b,
                "CRC-12/DECT", "X-CRC-12"),

            // CRC 14
            new CRCParameters(14, 0x0805, 0x0000, true, true, 0x0000, 0x082d,
                "CRC-14/DARC"),

            // CRC 15
            new CRCParameters(15, 0x4599, 0x0000, false, false, 0x0000, 0x059e,
                "CRC-15"),
            new CRCParameters(15, 0x6815, 0x0000, false, false, 0x0001, 0x2566,
                "CRC-15/MPT1327"),

            // CRC 16
            new CRCParameters(16, 0x8005 , 0x0000, true, true, 0x0000, 0xbb3d,
                "CRC-16", "ARC", "CRC-IBM", "CRC-16/ARC", "CRC-16/LHA"),
            new CRCParameters(16, 0x1021, 0x1d0f, false, false, 0x0000, 0xe5cc,
                "CRC-16/AUG-CCITT", "CRC-16/SPI-FUJITSU"),
            new CRCParameters(16, 0x8005, 0x0000, false, false, 0x0000, 0xfee8,
                "CRC-16/BUYPASS", "CRC-16/VERIFONE"),
            new CRCParameters(16, 0x1021, 0xffff, false, false, 0x0000, 0x29b1,
                "CRC-16/CCITT-FALSE"),
            new CRCParameters(16, 0x8005, 0x800d, false, false, 0x0000, 0x9ecf,
                "CRC-16/DDS-110"),
            new CRCParameters(16, 0x0589, 0x0000, false, false, 0x0001, 0x007e,
                "CRC-16/DECT-R", "R-CRC-16"),
            new CRCParameters(16, 0x0589, 0x0000, false, false, 0x0000, 0x007f,
                "CRC-16/DECT-X", "X-CRC-16"),
            new CRCParameters(16, 0x3d65, 0x0000, true, true, 0xffff, 0xea82,
                "CRC-16/DNP"),
            new CRCParameters(16, 0x3d65, 0x0000, false, false, 0xffff, 0xc2b7,
                "CRC-16/EN-13757"),
            new CRCParameters(16, 0x1021, 0xffff, false, false, 0xffff, 0xd64e,
                "CRC-16/GENIBUS", "CRC-16/EPC", "CRC-16/I-CODE", "CRC-16/DARC"),
            new CRCParameters(16, 0x8005, 0x0000, true, true, 0xffff, 0x44c2,
                "CRC-16/MAXIM"),
            new CRCParameters(16, 0x1021, 0xffff, true, true, 0x0000, 0x6f91,
                "CRC-16/MCRF4XX"),
            new CRCParameters(16, 0x1021, 0xb2aa, true, true, 0x0000, 0x63d0,
                "CRC-16/RIELLO"),
            new CRCParameters(16, 0x8bb7, 0x0000, false, false, 0x0000, 0xd0db,
                "CRC-16/T10-DIF"),
            new CRCParameters(16, 0x1021, 0x89ec, true, true, 0x0000, 0x26b1,
                "CRC-16/TMS37157"),
            new CRCParameters(16, 0x8005, 0xffff, true, true, 0xffff, 0xb4c8,
                "CRC-16/USB"),
            new CRCParameters(16, 0x1021, 0xc6c6, true, true, 0x0000, 0xbf05,
                "CRC-A"),
            new CRCParameters(16, 0x1021, 0x0000, true, true, 0x0000, 0x2189,
                "KERMIT", "CRC-16/CCITT", "CRC-16/CCITT-TRUE", "CRC-CCITT"),
            new CRCParameters(16, 0x8005, 0xffff, true, true, 0x0000, 0x4b37,
                "MODBUS"),
            new CRCParameters(16, 0x1021, 0xffff, true, true, 0xffff, 0x906e,
                "X-25", "CRC-16/IBM-SDLC", "CRC-16/ISO-HDLC", "CRC-B"),
            new CRCParameters(16, 0x1021, 0x0000, false, false, 0x0000, 0x31c3,
                "XMODEM", "ZMODEM", "CRC-16/ACORN"),

            // CRC 24
            new CRCParameters(24, 0x864cfb, 0xb704ce, false, false, 0x000000, 0x21cf02,
                "CRC-24", "CRC-24/OPENPGP"),
            new CRCParameters(24, 0x5d6dcb, 0xfedcba, false, false, 0x000000, 0x7979bd,
                "CRC-24/FLEXRAY-A"),
            new CRCParameters(24, 0x5d6dcb, 0xabcdef, false, false, 0x000000, 0x1f23b8,
                "CRC-24/FLEXRAY-B"),

            // CRC 31
            new CRCParameters(31, 0x04c11db7, 0x7fffffff, false, false, 0x7fffffff, 0x0ce9e46c,
                "CRC-31/PHILLIPS"),

            // CRC 32
            new CRCParameters(32, 0x04c11db7, 0xffffffff, true, true, 0xffffffff, 0xcbf43926,
                "CRC-32", "CRC-32/ADCCP", "PKZIP"),
            new CRCParameters(32, 0x04c11db7, 0xffffffff, false, false, 0xffffffff, 0xfc891918,
                "CRC-32/BZIP2", "CRC-32/AAL5", "CRC-32/DECT-B", "B-CRC-32"),
            new CRCParameters(32, 0x1edc6f41, 0xffffffff, true, true, 0xffffffff, 0xe3069283,
                "CRC-32C", "CRC-32/ISCSI", "CRC-32/CASTAGNOLI"),
            new CRCParameters(32, 0xa833982b, 0xffffffff, true, true, 0xffffffff, 0x87315576,
                "CRC-32D"),
            new CRCParameters(32, 0x04c11db7, 0xffffffff, false, false, 0x00000000, 0x0376e6e7,
                "CRC-32/MPEG-2"),
            new CRCParameters(32, 0x814141ab, 0x00000000, false, false, 0x00000000, 0x3010bf7f,
                "CRC-32Q"),
            new CRCParameters(32, 0x04c11db7, 0xffffffff, true, true, 0x00000000, 0x340bc6d9,
                "JAMCRC"),
            new CRCParameters(32, 0x000000af, 0x00000000, false, false, 0x00000000, 0xbd0be338,
                "XFER"),

            // CRC 40
            new CRCParameters(40, 0x0004820009, 0x0000000000, false, false, 0x0000000000, 0x2be9b039b9,
                "CRC-40/GSM"),
        };

            public static void DoCRCTests()
            {
                byte[] checkdata = Encoding.ASCII.GetBytes("123456789");

                foreach (CRCParameters p in s_CRCParams)
                {
                    CRC foo = new CRC(p);

                    if (p.Width > 7)
                    {
                        // do some additional sanity checks with random data, to check if direct and table-driven algorithms match
                        Random rnd = new Random();
                        for (int i = 0; i < 1000; i++)
                        {
                            int len = rnd.Next(256);
                            byte[] buf = new byte[len];
                            for (int j = 0; j < len; j++)
                            {
                                buf[j] = (byte)(rnd.Next(256) & 0xff);
                            }
                            ulong crc1 = foo.CalculateCRCbyTable(buf, len);
                            ulong crc2 = foo.CalculateCRCdirect(buf, len);
                            if (crc1 != crc2)
                            {
                                Console.WriteLine("CRC '{0}': Table-driven and direct algorithm mismatch: table=0x{0:x8}, direct=0x{0:x8}", crc1, crc2);
                                break;
                            }
                        }
                    }

                    ulong crc = foo.CalculateCRC("123456789");
                    if (crc != p.CheckValue)
                        Console.WriteLine("CRC '{0}': failed sanity check, expected {1:x8}, got {2:x8}", p.Names[0], p.CheckValue, crc);
                    else
                        Console.WriteLine("CRC '{0}': passed", p.Names[0]);
                }
            }

            // create a well-known CRC Algorithm
            public static CRC Create(string name)
            {
                foreach (CRCParameters param in s_CRCParams)
                {
                    if (param.Names.Contains(name.ToUpper()))
                        return new CRC(param);
                }

                return null;
            }

            // enumerate all CRC methods
            public static IEnumerable<string> AllCRCMethods
            {
                get
                {
                    foreach (CRCParameters p in s_CRCParams)
                    {
                        yield return p.Names[0];
                    }
                }
            }

            private ulong m_CRCMask;
            private ulong m_CRCHighBitMask;
            private CRCParameters m_Params;
            private ulong[] m_CRCTable;

            // Construct a new CRC algorithm object
            public CRC(CRCParameters param)
            {
                m_Params = param;

                // initialize some bitmasks
                m_CRCMask = ((((ulong)1 << (m_Params.Width - 1)) - 1) << 1) | 1;
                m_CRCHighBitMask = (ulong)1 << (m_Params.Width - 1);

                if (m_Params.Width > 7)
                {
                    GenerateTable();
                }
            }

            public static ulong Reflect(ulong value, int width)
            {
                // reflects the lower 'width' bits of 'value'

                ulong j = 1;
                ulong result = 0;

                for (ulong i = 1UL << (width - 1); i != 0; i >>= 1)
                {
                    if ((value & i) != 0)
                    {
                        result |= j;
                    }
                    j <<= 1;
                }
                return result;
            }

            private void GenerateTable()
            {
                ulong bit;
                ulong crc;

                m_CRCTable = new ulong[256];

                for (int i = 0; i < 256; i++)
                {
                    crc = (ulong)i;
                    if (m_Params.ReflectIn)
                    {
                        crc = Reflect(crc, 8);
                    }
                    crc <<= m_Params.Width - 8;

                    for (int j = 0; j < 8; j++)
                    {
                        bit = crc & m_CRCHighBitMask;
                        crc <<= 1;
                        if (bit != 0) crc ^= m_Params.Polynom;
                    }

                    if (m_Params.ReflectIn)
                    {
                        crc = Reflect(crc, m_Params.Width);
                    }
                    crc &= m_CRCMask;
                    m_CRCTable[i] = crc;
                }
            }

            // tables work only for 8, 16, 24, 32 bit CRC
            private ulong CalculateCRCbyTable(byte[] data, int length)
            {
                ulong crc = m_Params.Init;

                if (m_Params.ReflectIn)
                    crc = Reflect(crc, m_Params.Width);

                if (m_Params.ReflectIn)
                {
                    for (int i = 0; i < length; i++)
                    {
                        crc = (crc >> 8) ^ m_CRCTable[(crc & 0xff) ^ data[i]];
                    }
                }
                else
                {
                    for (int i = 0; i < length; i++)
                    {
                        crc = (crc << 8) ^ m_CRCTable[((crc >> (m_Params.Width - 8)) & 0xff) ^ data[i]];
                    }
                }

                if (m_Params.ReflectIn ^ m_Params.ReflectOut)
                {
                    crc = Reflect(crc, m_Params.Width);
                }

                crc ^= m_Params.XOROut;
                crc &= m_CRCMask;

                return crc;
            }

            private ulong CalculateCRCdirect(byte[] data, int length)
            {
                // fast bit by bit algorithm without augmented zero bytes.
                // does not use lookup table, suited for polynom orders between 1...32.
                ulong c, bit;
                ulong crc = m_Params.Init;

                for (int i = 0; i < length; i++)
                {
                    c = (ulong)data[i];
                    if (m_Params.ReflectIn)
                    {
                        c = Reflect(c, 8);
                    }

                    for (ulong j = 0x80; j > 0; j >>= 1)
                    {
                        bit = crc & m_CRCHighBitMask;
                        crc <<= 1;
                        if ((c & j) > 0) bit ^= m_CRCHighBitMask;
                        if (bit > 0) crc ^= m_Params.Polynom;
                    }
                }

                if (m_Params.ReflectOut)
                {
                    crc = Reflect(crc, m_Params.Width);
                }
                crc ^= m_Params.XOROut;
                crc &= m_CRCMask;

                return crc;
            }

            public ulong CalculateCRC(byte[] data, int length)
            {
                // table driven CRC reportedly only works for 8, 16, 24, 32 bits
                // HOWEVER, it seems to work for everything > 7 bits, so use it
                // accordingly

                if (m_Params.Width > 7)
                    return CalculateCRCbyTable(data, length);
                else
                    return CalculateCRCdirect(data, length);
            }

            public ulong CalculateCRC(string data)
            {
                return CalculateCRC(Encoding.ASCII.GetBytes(data), data.Length);
            }
        }
    }
}