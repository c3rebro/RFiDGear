/*
 * Created by SharpDevelop.
 * User: c3rebro
 * Date: 02.03.2018
 * Time: 23:21
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */

using RFiDGear.Infrastructure;
using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace RFiDGear.Models
{
    /// <summary>
    /// Description of Checkpoint.
    /// </summary>
    public class Checkpoint
    {
        public int UUID { get; set; }

        public Checkpoint()
        {
            var rnd = new Random();
            UUID = rnd.Next();
            ErrorLevel = ERROR.Empty;
        }

        public string CheckpointIndex { get; set; }

        public int CheckpointIndexAsInt
        {
        get {
                int res;
                if (int.TryParse(CheckpointIndex, out res))
                {
                    return res;
                }
                else
                {
                    return 0;
                }
            }
        }

        [XmlIgnore]
        public ERROR ErrorLevel { get; set; }

        /// <summary>
        /// XML-facing surrogate that accepts the legacy "AuthenticationError" name in addition to current enum names.
        /// </summary>
        [XmlElement("ErrorLevel")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public string ErrorLevelSerialized
        {
            get => ErrorLevel.ToString();
            set
            {
                var normalized = value == "AuthenticationError" ? nameof(ERROR.AuthFailure) : value;
                if (Enum.TryParse<ERROR>(normalized, out var parsed))
                    ErrorLevel = parsed;
            }
        }

        public string TaskIndex { get; set; }

        public string Content { get; set; }

        public string CompareValue { get; set; }

        public string TemplateField { get; set; }
    }
}
