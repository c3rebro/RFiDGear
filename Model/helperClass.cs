/*
 * Created by SharpDevelop.
 * User: rotts
 * Date: 24.09.2013
 * Time: 20:00
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Security.Cryptography;
using System.Text;
using System.Timers;

namespace RFiDGear
{
	
	
	//**************************************************************************
	//class for Hexidecimal to Byte and Byte to Hexidecimal conversion
	//**************************************************************************
	public class helperClass
	{
		
		public string desFireKeyToEdit { get; set; }
		// 0 = 3DES, 1 = AES, 2 = DES
		public int[] libLogicalAccessKeyTypeEnumConverter = { 64, 128, 0 };
		public string[] _constDesfireCardKeyType = { "3DES", "AES", "DES" };
		public string[] _constCardType = { "Mifare1K", "Mifare2K", "Mifare4K", "DESFireEV1" };
		
		public helperClass()
		{
			
			// constructor
			
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
		
		public string FormatSectorStringWithSpacesEachByte(string Str)
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
			
			desFireKeyToEdit = temp;
			
			return KEY_ERROR.NO_ERROR;
		}
		
		public string CalculateMD5Hash(string input)
		{
			
			StringBuilder sb = new StringBuilder();
			// step 1, calculate MD5 hash from input
			string result;
			MD5 md5 = System.Security.Cryptography.MD5.Create();

			byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
			
							inputBytes = md5.ComputeHash(inputBytes);
			do {
				
				sb.Clear();
				result="";
				inputBytes = md5.ComputeHash(inputBytes);

				byte[] hash =  inputBytes; //md5.ComputeHash(inputBytes);
			


				// step 2, convert byte array to hex string

			

				for (int i = 0; i < hash.Length; i++) {

					sb.Append(hash[i].ToString("X2"));
				}
				
				result = sb.ToString().ToLower();
				
			} while(!result.Contains("c7"));


			return sb.ToString();

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
