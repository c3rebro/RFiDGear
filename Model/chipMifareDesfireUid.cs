/*
 * Created by SharpDevelop.
 * User: C3rebro
 * Date: 11/24/2016
 * Time: 23:04
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.ComponentModel;

namespace RFiDGear.Model
{
	/// <summary>
	/// Description of chipUid.
	/// </summary>
	public class chipMifareDesfireUid
	{
		readonly List<chipMifareDesfireAppID> _appList = new List<chipMifareDesfireAppID>();

		public List<chipMifareDesfireAppID> AppList {
			get { return _appList; }
		}
				
		public chipMifareDesfireUid(string uid)
		{
			uidNumber = uid;
		}
		
		public string uidNumber{ get; set;		}
	}
}
