using System;
using System.ComponentModel;

namespace RFiDGear.DataSource
{
	/// <summary>
	/// Description of SourceForSectorTrailerDataGrid.
	/// </summary>
	public class SourceForSectorTrailerDataGrid : MifareClassicAccessBitsModel
	{
		readonly string readKeyA;
		readonly string writeKeyA;
		readonly string readAccessCond;
		readonly string writeAccessCond;
		readonly string readKeyB;
		readonly string writeKeyB;

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

		public SourceForSectorTrailerDataGrid()
		{
			
		}
		
		public SourceForSectorTrailerDataGrid(string accessBits)
		{
			string[] temp = accessBits.Split(',');
			
			for(int i=0; i<temp.Length; i++){
				switch(i){
					case 0:
						readKeyA=convertCondToHumanReadableFormat(temp[i]);
						break;
					case 1:
						writeKeyA=convertCondToHumanReadableFormat(temp[i]);
						break;
					case 2:
						readAccessCond=convertCondToHumanReadableFormat(temp[i]);
						break;
					case 3:
						writeAccessCond=convertCondToHumanReadableFormat(temp[i]);
						break;
					case 4:
						readKeyB=convertCondToHumanReadableFormat(temp[i]);
						break;
					case 5:
						writeKeyB=convertCondToHumanReadableFormat(temp[i]);
						break;
				}
				
				encodeSectorTrailer(accessBits, 3);
				encodeSectorTrailer(null,4);
			}
		}
		
		public string GetSectorAccessBitsFromHumanReadableFormat(){
			return String.Format("{0},{1},{2},{3},{4},{5}",
			                     convertCondFromHumanReadableFormat(readKeyA),
			                     convertCondFromHumanReadableFormat(writeKeyA),
			                     convertCondFromHumanReadableFormat(readAccessCond),
			                     convertCondFromHumanReadableFormat(writeAccessCond),
			                     convertCondFromHumanReadableFormat(readKeyB),
			                     convertCondFromHumanReadableFormat(writeKeyB));
		}
		
		[DisplayName("ReadKey A")]
		public string getReadKeyA {
			get { return readKeyA; }
		}
		
		[DisplayName("WriteKey A")]
		public string getWriteKeyA {
			get { return writeKeyA; }
		}
		
		[DisplayName("Read\nAccess\nCondition")]
		public string getReadAccessCond {
			get { return readAccessCond; }
		}
		
		[DisplayName("Write\nAccess\nCondition")]
		public string getWriteAccessCond {
			get { return writeAccessCond; }
		}
		
		[DisplayName("ReadKey B")]
		public string getReadKeyB {
			get { return readKeyB; }
		}
		
		[DisplayName("WriteKey B")]
		public string getWriteKeyB {
			get { return writeKeyB; }
		}
	}
}
