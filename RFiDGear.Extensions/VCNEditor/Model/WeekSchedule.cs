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

namespace RFiDGear.Extensions.VCNEditor.Model
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
            weekDays = Enum.GetNames(typeof(DayOfWeek));
        }

        public WeekSchedule(DateTime _begin, DateTime _end)
        {
            weekDays = Enum.GetNames(typeof(DayOfWeek));

            Begin = _begin;
            End = _end;
        }

        /// <summary>
        /// 
        /// </summary>
        public string FormattedBegin
        {
            get { return string.Format("{0:dddd HH\\:mm\\:ss}", Begin); }
        }

        /// <summary>
        /// 
        /// </summary>
        public string FormattedEnd
        {
            get { return string.Format("{0:dddd HH\\:mm\\:ss}", End); }
        }

        /// <summary>
        /// 
        /// </summary>
        public DateTime Begin { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public DateTime End { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string[] WeekDays
        {
            get
            {
                return weekDays;
            }
        }
        private string[] weekDays;

        /// <summary>
        /// minute of day 0 .. 1439 (00:00 … 23:59)
        /// </summary>
        public int Duration { get; set; }

        /// <summary>
        /// in minutes 0 .. 1439 (can span midnight!)
        /// </summary>
        public int StartMinute { get; set; }

        public byte[] WeekScheduleAsBytes { get; set; }
    }
}
