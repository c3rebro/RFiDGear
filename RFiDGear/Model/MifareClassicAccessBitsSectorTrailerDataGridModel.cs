using System;
using System.ComponentModel;

namespace RFiDGear.DataSource
{
	/// <summary>
	/// Description of MifareClassicAccessBitsSectorTrailerDataGridModel.
	/// </summary>
	public class MifareClassicAccessBitsSectorTrailerDataGridModel
	{
		readonly string readKeyA;
		readonly string writeKeyA;
		readonly string readAccessCond;
		readonly string writeAccessCond;
		readonly string readKeyB;
		readonly string writeKeyB;
		
		readonly string accessBits;
		
		MifareClassicAccessBitsBaseModel abModel;
		
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

		public MifareClassicAccessBitsSectorTrailerDataGridModel(string _accessBits)
		{
			abModel = new MifareClassicAccessBitsBaseModel();
			
			accessBits = _accessBits;
			
			string[] temp = accessBits.Split(',');
			
			for(int i=0; i<temp.Length; i++){
				switch(i){
					case 0:
						readKeyA=convertCondToHumanReadableFormat(temp[i]);
						continue;
					case 1:
						writeKeyA=convertCondToHumanReadableFormat(temp[i]);
						continue;
					case 2:
						readAccessCond=convertCondToHumanReadableFormat(temp[i]);
						continue;
					case 3:
						writeAccessCond=convertCondToHumanReadableFormat(temp[i]);
						continue;
					case 4:
						readKeyB=convertCondToHumanReadableFormat(temp[i]);
						continue;
					case 5:
						writeKeyB=convertCondToHumanReadableFormat(temp[i]);
						continue;
				}
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
		
		public MifareClassicAccessBitsBaseModel ProcessSectorTrailerEncoding(string block0AB, string block1AB, string block2AB){
			abModel.DecodedDataBlock0AccessBits = block0AB;
			abModel.encodeSectorTrailer(block0AB, 0);
			abModel.DecodedDataBlock1AccessBits = block1AB;
			abModel.encodeSectorTrailer(block1AB, 1);
			abModel.DecodedDataBlock2AccessBits = block2AB;
			abModel.encodeSectorTrailer(block2AB, 2);
			abModel.encodeSectorTrailer(accessBits, 3);
			abModel.encodeSectorTrailer(null, 4);
			return this.abModel;
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
