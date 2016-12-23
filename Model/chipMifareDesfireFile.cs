﻿/*
 * Created by SharpDevelop.
 * User: rotts
 * Date: 11/24/2016
 * Time: 14:14
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;

namespace RFiDGear
{
	/// <summary>
	/// Description of DesfireDataContent.
	/// </summary>
	public class DesfireDataContent
	{
		helperClass converter = new helperClass();
		
		byte data;
		int discarded;
		
		public DesfireDataContent(byte[] cardContent, int arIndex)
		{
			data = cardContent[arIndex];
		}
		
		public byte singleByteAsByte {
			get { return data; }
			set { data = value; }
		}
		
		public string singleByteAsString {
			get { return data.ToString("X2"); }
			set { data = converter.GetBytes(value, out discarded)[0]; }
		}

		public char singleByteAsChar {
			get {
				if (data < 32 | data > 126)
					return '.';
				else
					return (char)data;
			}
			
			set {
				if ((byte)value < 32 | (byte)value > 126)
					data |= 0;
				else
					data = (byte)value;
			}
		}
	}
}
