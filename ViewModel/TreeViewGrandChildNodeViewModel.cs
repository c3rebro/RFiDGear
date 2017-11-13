using RFiDGear.DataAccessLayer;
using RFiDGear.Model;

using GalaSoft.MvvmLight;

using System;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Xml.Serialization;


namespace RFiDGear.ViewModel
{
	/// <summary>
	/// Description of TreeViewGrandChildNodeViewModel.
	/// </summary>
	[XmlRootAttribute("TreeViewGrandChildNode", IsNullable = false)]
	public class TreeViewGrandChildNodeViewModel : ViewModelBase
	{

		#region Constructors
		
		public TreeViewGrandChildNodeViewModel()
		{
			dataBlockContent = new MifareClassicDataBlockModel();
			dataBlockContent.Data = new byte[16];
		}
		
		public TreeViewGrandChildNodeViewModel(MifareClassicDataBlockModel dataBlock, TreeViewChildNodeViewModel parentSector, CARD_TYPE cardType, int sectorNumber, bool _isDataBlock)
		{
			if (dataBlock != null)
				dataBlockContent = dataBlock;
			else {
				dataBlockContent = new MifareClassicDataBlockModel();
				dataBlockContent.Data = new byte[16];
			}
			
			isDataBlock = _isDataBlock;
			IsVisible = true;
			
			parent = parentSector;
			dataBlockContent.dataBlockNumber = dataBlock.dataBlockNumber;
			
			DataBlockAsHexString = "0000000000000000";
			DataBlockAsCharString = "\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0";
			
			IsValidDataBlockContent = null;
			
			tag = String.Format("{0}:{1}", parentSector.ParentUid, parentSector.SectorNumber);
			
		}
		
		public TreeViewGrandChildNodeViewModel(string _displayItem)
		{
			grandChildNodeHeader = _displayItem;
			IsDataBlock = false;
			isVisible = false;
		}
		
		#endregion
		
		#region SelectedItem
		
		[XmlIgnore]
		public object SelectedItem {
			get { return selectedItem; }
			set {
				selectedItem = value;
			}
		}
		private object selectedItem;
		
		#endregion

		#region Parent

		
		public TreeViewChildNodeViewModel Parent {
			get { return parent; }
		}
		private readonly TreeViewChildNodeViewModel parent;

		#endregion

		#region (Dependency) Properties
		
		/// <summary>
		/// 
		/// </summary>
		[XmlIgnore]
		public string DataBlockAsCharString {
			get {
				if (DataBlockContent.Length == 16 && dataBlockAsCharString.Length == 16) {
					char[] tempString = new char[DataBlockContent.Length];
					for (int i = 0; i < DataBlockContent.Length; i++) {
						if (DataBlockContent[i] < 27 | DataBlockContent[i] > 127)
							tempString[i] = (char)248;
						else
							tempString[i] = (char)DataBlockContent[i];
					}
					
					dataBlockAsCharString = new string(tempString);
				}
				return dataBlockAsCharString;
			}
			set {
				dataBlockAsCharString = value;
				
				if (dataBlockAsCharString.Length == 16) {
					char[] tempString = value.ToCharArray();
					
					try {
						for (int i = 0; i < DataBlockContent.Length; i++) {
							if (
								((char)DataBlockContent[i] != value[i])
								&& (
								    (!((char)DataBlockContent[i] < 27 | (char)DataBlockContent[i] > 127))//do not perform overwrite datablockat position 'i' if non printable character...
								    || (value[i] > 27 && value[i] < 127) //..except if a printable character was entered at the same position
								)) {
								DataBlockContent[i] = (byte)value[i];
								//tempString[i] = (char)DataBlockContent[i];
							}
							
						}
						//dataBlockAsCharString = new string(tempString);
						//DataBlockContent = Encoding.UTF8.GetBytes(dataBlockAsCharString);
					} catch {
						IsValidDataBlockContent = false;
						IsTask = false;
						return;
					}
					IsValidDataBlockContent = null;
					IsTask = true;

				} else {
					IsTask = false;
					IsValidDataBlockContent = false;
				}
				
				RaisePropertyChanged("DataBlockAsCharString");
				RaisePropertyChanged("DataBlockAsHexString");
				RaisePropertyChanged("DataBlockContent");
			}
		}
		private string dataBlockAsCharString;
		
		/// <summary>
		/// DependencyProperty
		/// </summary>
		
		public string DataBlockAsHexString {
			get {
				if (DataBlockContent.Length == 16) {
					dataBlockAsHexString = CustomConverter.HexToString(DataBlockContent);
				}

				return dataBlockAsHexString;
			}
			set {
				int discardedChars = 0;
				DataBlockContent = CustomConverter.GetBytes(value, out discardedChars);
				
				if (discardedChars == 0 && value.Length == 32) {
					IsValidDataBlockContent = null;
					IsTask = true;
				} else {
					IsValidDataBlockContent = false;
					IsTask = false;
				}
					
				
				dataBlockAsHexString = value;
				
				RaisePropertyChanged("DataBlockAsHexString");
				RaisePropertyChanged("DataBlockAsCharString");
			}
		}
		private string dataBlockAsHexString;
		
		/// <summary>
		/// 
		/// </summary>
		[XmlIgnore]
		public bool? IsValidDataBlockContent {
			get { return isValidDataBlockContent; }
			set {
				isValidDataBlockContent = value;
				
				RaisePropertyChanged("IsValidDataBlockContent");
			}
		}
		private bool? isValidDataBlockContent;
		
		[XmlIgnore]
		public byte[] DataBlockContent {
			get { return dataBlockContent.Data; }		//_dataBlock != null ? _dataBlock.Data : new byte[16]; }
			set {
				dataBlockContent.Data = value;
				RaisePropertyChanged("DataBlockContent");
				RaisePropertyChanged("DataBlockAsHexString");
				RaisePropertyChanged("DataBlockAsCharString");
			}
		} private MifareClassicDataBlockModel dataBlockContent;
		
		
		[XmlIgnore]
		public string Tag {
			get { return tag; }
		} private readonly string tag;
		
		
		[XmlIgnore]
		public string GrandChildNodeHeader {
			get {
				if (dataBlockContent != null)
					return String.Format("Block: [{0}]", dataBlockContent.dataBlockNumber);
				return grandChildNodeHeader;
			}
		} private string grandChildNodeHeader;
		
		
		public int DataBlockNumber {
			get { return dataBlockContent != null ? dataBlockContent.dataBlockNumber : 0; }
			set { dataBlockContent.dataBlockNumber = value; }
		}
		
		#endregion
		
		#region View Switches
		
		public bool IsExpanded {
			get { return isExpanded; }
			set {
				if (value != isExpanded) {
					isExpanded = value;
					this.RaisePropertyChanged("IsExpanded");
				}

				// Expand all the way up to the root.
				if (parent != null)
					parent.IsExpanded = true;
			}
		}
		private bool isExpanded;

		public bool IsSelected {
			get { return isSelected; }
			set {
				if (value != isSelected) {
					isSelected = value;
					RaisePropertyChanged("IsSelected");
				}
			}
		}
		private bool isSelected;
			
		public bool? IsAuthenticated {
			get { return isAuth; }
			set {
				isAuth = value;
				RaisePropertyChanged("IsAuthenticated");
			}
		}
		private bool? isAuth;
		
		public bool? IsTask {
			get { return isTask; }
			set {
				isTask = value;
				RaisePropertyChanged("IsTask");
			}
		}
		private bool? isTask;
		
		public bool? IsVisible {
			get { return isVisible; }
			set {
				isVisible = value;
				RaisePropertyChanged("IsVisible");
			}
		}
		private bool? isVisible;
		
		public bool IsDataBlock {
			get { return isDataBlock; }
			set {
				isDataBlock = value;
				RaisePropertyChanged("IsDataBlock");
			}
		}
		private bool isDataBlock;
		#endregion
	}
}
