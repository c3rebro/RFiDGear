/*
 * Created by SharpDevelop.
 * User: rotts
 * Date: 02.03.2018
 * Time: 23:21
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using ByteArrayHelper.Extensions;
 
using System;
using System.Collections;
using System.Collections.Generic;

namespace VCNEditor.Model
{
	/// <summary>
	/// Description of AccessProfile.
	/// </summary>
	public class AccessProfile
	{
		public AccessProfile()
		{
			AccessProfileAsBytes = new byte[4];
			MainListWords = new byte[16];
		}
		
		public bool IsLastProfile { get; set; }
		
		public int ProfileType { get; set; }
		
		public byte[] AccessProfileAsBytes { get; set; }
		
		public byte[] MainListWords { get; set; }
		
		public string ProfileDesc { get { return string.Format("{0}:{1}:{2}",ProfileType, ByteConverter.HexToString(AccessProfileAsBytes), ByteConverter.HexToString(MainListWords)); }}
	}
}
