/*
 * Created by SharpDevelop.
 * Date: 09/05/2017
 * Time: 22:46
 * 
 */
using System;

namespace RFiDGear.Extensions.VCNEditor.Model
{
    /// <summary>
    /// Description of Period.
    /// </summary>
    public class Period
    {
        /// <summary>
        /// 
        /// </summary>
        public Period()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="_begin"></param>
        /// <param name="_end"></param>
        public Period(DateTime _begin, DateTime _end)
        {
            Begin = _begin;
            End = _end;
        }

        /// <summary>
        /// 
        /// </summary>
        public string FormattedBegin
        {
            get { return string.Format("{0:dddd dd\\.MM\\.yyyy\\ HH\\:mm\\:ss}", Begin); }
        }

        /// <summary>
        /// 
        /// </summary>
        public string FormattedEnd
        {
            get { return string.Format("{0:dddd dd\\.MM\\.yyyy\\ HH\\:mm\\:ss}", End); }
        }

        /// <summary>
        /// 
        /// </summary>
        public DateTime Begin { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public DateTime End { get; set; }

    }
}
