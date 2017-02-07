using System;
using System.Linq;

namespace RFiDGear
{
	
	
	//**************************************************************************
	//class for Hexidecimal to Byte and Byte to Hexidecimal conversion
	//**************************************************************************
	public class CustomConverter
	{
		
		public string desFireKeyToEdit { get; set; }
		public string classicKeyToEdit { get; set; }
		// 0 = 3DES, 1 = AES, 2 = DES
		public int[] libLogicalAccessKeyTypeEnumConverter = { 64, 128, 0 };
		public string[] _constDesfireCardKeyType = { "3DES", "AES", "DES" };
		public string[] _constCardType = { "Mifare1K", "Mifare2K", "Mifare4K", "DESFireEV1" };
		
		public bool KeyFormatQuickCheck(string keyToCheck)
		{
			foreach(char c in keyToCheck.ToCharArray()){
				if(c == ' ')
					return true;
			}
			return false;
		}
		
		public int GetByteCount(string hexString)
		{
			int numHexChars = 0;
			char c;
			// remove all none A-F, 0-9, characters
			for (int i = 0; i < hexString.Length; i++) {
				c = hexString[i];
				if (IsHexDigit(c))
					numHexChars++;
			}
			// if odd number of characters, discard last character
			if (numHexChars % 2 != 0) {
				numHexChars--;
			}
			return numHexChars / 2; // 2 characters per byte
		}
		
		public byte[] GetBytes(string hexString, out int discarded)
		{
			discarded = 0;
			string newString = "";
			char c;
			// remove all none A-F, 0-9, characters
			for (int i = 0; i < hexString.Length; i++) {
				c = hexString[i];
				if (IsHexDigit(c))
					newString += c;
				else
					discarded++;
			}
			// if odd number of characters, discard last character
			if (newString.Length % 2 != 0) {
				discarded++;
				newString = newString.Substring(0, newString.Length - 1);
			}

			int byteLength = newString.Length / 2;
			byte[] bytes = new byte[byteLength];
			string hex;
			int j = 0;
			for (int i = 0; i < bytes.Length; i++) {
				hex = new String(new Char[] { newString[j], newString[j + 1] });
				bytes[i] = HexToByte(hex);
				j = j + 2;
			}
			return bytes;
		}
		
		public string HexToString(byte[] bytes)
		{
			string hexString = "";
			for (int i = 0; i < bytes.Length; i++) {
				hexString += bytes[i].ToString("X2");
			}
			return hexString;
		}
		
		public string HexToString(byte bytes)
		{
			string hexString = "";
			{
				hexString += bytes.ToString("X2");
			}
			return hexString;
		}
		
		public bool InHexFormat(string hexString)
		{
			bool hexFormat = true;

			foreach (char digit in hexString) {
				if (!IsHexDigit(digit)) {
					hexFormat = false;
					break;
				}
			}
			return hexFormat;
		}

		public bool IsHexDigit(Char c)
		{
			int numChar;
			int numA = Convert.ToInt32('A');
			int num1 = Convert.ToInt32('0');
			c = Char.ToUpper(c);
			numChar = Convert.ToInt32(c);
			if (numChar >= numA && numChar < (numA + 6))
				return true;
			if (numChar >= num1 && numChar < (num1 + 10))
				return true;
			return false;
		}
		
		public string FormatMifareClassicKeyWithSpacesEachByte(string Str)
		{
			string temp = Str;
			
			if (string.IsNullOrEmpty(temp))
				return "error 0";
			if (!InHexFormat(temp))
				return "error 1";
			if (temp.Length != 12) {
				return "error 2";
			} else {
				for (int i = (Str.Length) - 2; i > 0; i -= 2)
					temp = temp.Insert(i, " ");
				
				return temp.ToLower();
			}
		}
		
		public KEY_ERROR FormatMifareDesfireKeyStringWithSpacesEachByte(string Str)
		{
			string temp = Str;
			
			if (string.IsNullOrEmpty(temp))
				return KEY_ERROR.KEY_IS_EMPTY;
			if (!InHexFormat(temp))
				return KEY_ERROR.KEY_HAS_WRONG_FORMAT;
			if (temp.Length != 32)
				return KEY_ERROR.KEY_HAS_WRONG_LENGTH;
			
			for (int i = (Str.Length) - 2; i > 0; i -= 2)
				temp = temp.Insert(i, " ");
			
			desFireKeyToEdit = temp.ToUpper();
			
			return KEY_ERROR.NO_ERROR;
		}

		public KEY_ERROR FormatMifareClassicKeyStringWithSpacesEachByte(string Str)
		{
			string temp = Str;
			
			if (string.IsNullOrEmpty(temp))
				return KEY_ERROR.KEY_IS_EMPTY;
			if (!InHexFormat(temp))
				return KEY_ERROR.KEY_HAS_WRONG_FORMAT;
			if (temp.Length != 12)
				return KEY_ERROR.KEY_HAS_WRONG_LENGTH;
			
			for (int i = (Str.Length) - 2; i > 0; i -= 2)
				temp = temp.Insert(i, " ");
			
			classicKeyToEdit = temp.ToUpper();
			
			return KEY_ERROR.NO_ERROR;
		}	

		public string NormalizeKey(string keyToNormalize)
		{
			char[] c = keyToNormalize.ToCharArray();
			
			for(int i=0; i< keyToNormalize.Length; i++)
				if(c[i] == ' ')
					c[i] = '\0';
			return new string(c);
		}
		
		byte HexToByte(string hex)
		{
			if (hex.Length > 2 || hex.Length <= 0)
				throw new ArgumentException("hex must be 1 or 2 characters in length");
			byte newByte = byte.Parse(hex, System.Globalization.NumberStyles.HexNumber);
			return newByte;
		}
		
	}
}
