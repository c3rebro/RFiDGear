/*
 * Created by SharpDevelop.
 * User: c3rebro
 * Date: 02.03.2018
 * Time: 23:21
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */

using RFiDGear.DataAccessLayer;

namespace RFiDGear.Model
{
    /// <summary>
    /// Description of Checkpoint.
    /// </summary>
    public class Checkpoint
	{
		public Checkpoint()
		{
			ErrorLevel = ERROR.Empty;
		}
		
		public ERROR ErrorLevel { get; set; }

		public string TaskIndex { get; set; }

		public string Content { get; set; }

		public string TemplateField { get; set; }
	}
}
