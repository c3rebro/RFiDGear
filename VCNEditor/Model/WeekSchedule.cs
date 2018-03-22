/*
 * Created by SharpDevelop.
 * Date: 08/31/2017
 * Time: 22:45
 * 
 */

using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace VCNEditor.Model
{
	/// <summary>
	/// Description of Period.
	/// </summary>
	public class WeekSchedule
	{		
		/// <summary>
		/// 
		/// </summary>
		public WeekSchedule()
		{
			weekDays = new string[]{"Monday","Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"};
		}
		
		/// <summary>
		/// 
		/// </summary>
		public string[] WeekDays
		{
			get
			{
				return weekDays;
			}
		} private string[] weekDays;
		
		/// <summary>
		/// minute of day 0 .. 1439 (00:00 … 23:59)
		/// </summary>
		public int Duration { get; set; }
		
		/// <summary>
		/// in minutes 0 .. 1439 (can span midnight!)
		/// </summary>
		public int StartMinute { get; set; }
	}
}
