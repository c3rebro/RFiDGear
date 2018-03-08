/*
 * Created by SharpDevelop.
 * User: rotts
 * Date: 02.03.2018
 * Time: 23:21
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using VCNEditor.DataAccessLayer;
 
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
		
		public string ProfileDesc { get { return string.Format("{0}:{1}:{2}",ProfileType, CustomConverter.HexToString(AccessProfileAsBytes), CustomConverter.HexToString(MainListWords)); }}
	}

//	// Collection of Person objects. This class
//	// implements IEnumerable so that it can be used
//	// with ForEach syntax.
//	public class AccessProfile : IEnumerable
//	{
//		private Profile[] profile;
//		
//		public AccessProfile(Profile[] pArray = null)
//		{
//			profile = new Profile[pArray.Length];
//
//			for (int i = 0; i < pArray.Length; i++)
//			{
//				profile[i] = pArray[i];
//			}
//		}
//
//		// Implementation for the GetEnumerator method.
//		IEnumerator IEnumerable.GetEnumerator()
//		{
//			return (IEnumerator) GetEnumerator();
//		}
//
//		public ProfileEnum GetEnumerator()
//		{
//			return new ProfileEnum(profile);
//		}
//	}
//
//	// When you implement IEnumerable, you must also implement IEnumerator.
//	public class ProfileEnum : IEnumerator
//	{
//		public Profile[] profile;
//
//		// Enumerators are positioned before the first element
//		// until the first MoveNext() call.
//		int position = -1;
//
//		public ProfileEnum(Profile[] list)
//		{
//			profile = list;
//		}
//
//		public bool MoveNext()
//		{
//			position++;
//			return (position < profile.Length);
//		}
//
//		public void Reset()
//		{
//			position = -1;
//		}
//
//		object IEnumerator.Current
//		{
//			get
//			{
//				return Current;
//			}
//		}
//
//		public Profile Current
//		{
//			get
//			{
//				try
//				{
//					return profile[position];
//				}
//				catch (IndexOutOfRangeException)
//				{
//					throw new InvalidOperationException();
//				}
//			}
//		}
//	}
}
