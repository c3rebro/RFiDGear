using System;
using System.ComponentModel;

namespace RFiDGear.DataSource
{
	/// <summary>
	/// Description of SourceForLongDataBlockDataGrid.
	/// </summary>
	public class SourceForLongDataBlockDataGrid
	{
		readonly string read;
		readonly string write;
		readonly string inc;
		readonly string dec;

		private string convertCondToHumanReadableFormat(string cond){
			switch(cond){
				case "N":
					return "not Allowed";
				case "A":
					return "using Key A";
				case "B":
					return "using Key B";
				case "AB":
					return "Key A or B";
				default:
					return null;
			}
		}
	
		private string convertCondFromHumanReadableFormat(string cond){
			switch(cond){
				case "not Allowed":
					return "N";
				case "using Key A":
					return "A";
				case "using Key B":
					return "B";
				case "Key A or B":
					return "AB";
				default:
					return null;
			}
		}
		
		public SourceForLongDataBlockDataGrid(string accessBits)
		{
			string[] temp = accessBits.Split(',');
			
			for(int i=0; i<temp.Length; i++){
				switch(i){
					case 0:
						read=convertCondToHumanReadableFormat(temp[i]);
						break;
					case 1:
						write=convertCondToHumanReadableFormat(temp[i]);
						break;
					case 2:
						inc=convertCondToHumanReadableFormat(temp[i]);
						break;
					case 3:
						dec=convertCondToHumanReadableFormat(temp[i]);
						break;
				}
			}
		}

		public string GetLongDataBlockAccessBitsFromHumanReadableFormat(){
			return String.Format("{0},{1},{2},{3}",
			                     convertCondFromHumanReadableFormat(read),
			                     convertCondFromHumanReadableFormat(write),
			                     convertCondFromHumanReadableFormat(inc),
			                     convertCondFromHumanReadableFormat(dec));
		}
		
		[DisplayName("Read")]
		public string getReadKeyA {
			get { return read; }
		}
		
		[DisplayName("Write")]
		public string getWriteKeyA {
			get { return write; }
		}
		
		[DisplayName("Incr.")]
		public string getReadAccessCond {
			get { return inc; }
		}
		
		[DisplayName("Decr.\nTransf.\nRestore")]
		public string getWriteAccessCond {
			get { return dec; }
		}
	}
}
