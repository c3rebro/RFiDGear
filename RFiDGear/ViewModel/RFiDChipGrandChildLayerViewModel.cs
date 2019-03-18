using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using MvvmDialogs.ViewModels;
using RFiDGear.DataAccessLayer;
using RFiDGear.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Xml.Serialization;

namespace RFiDGear.ViewModel
{
	/// <summary>
	/// Description of RFiDChipGrandChildLayerViewModel.
	/// </summary>
	[XmlRootAttribute("TreeViewGrandChildNode", IsNullable = false)]
	public class RFiDChipGrandChildLayerViewModel : ViewModelBase, IUserDialogViewModel
	{
		private MifareClassicSetupViewModel setupViewModel;

		#region Constructors

		public RFiDChipGrandChildLayerViewModel()
		{
			children = new ObservableCollection<RFiDChipGrandGrandChildLayerViewModel>();
		}

		/// <summary>
		/// Task Constructor
		/// </summary>
		/// <param name="_mifareClassicDataBlock"></param>
		/// <param name="_setupViewModel"></param>
		public RFiDChipGrandChildLayerViewModel(MifareClassicDataBlockModel _mifareClassicDataBlock, MifareClassicSetupViewModel _setupViewModel)
		{
			if (_mifareClassicDataBlock != null && _mifareClassicDataBlock.Data != null)
				mifareClassicDataBlock = _mifareClassicDataBlock;
			else
			{
				mifareClassicDataBlock = new MifareClassicDataBlockModel();
				mifareClassicDataBlock.Data = new byte[16];
			}

			setupViewModel = _setupViewModel;

			IsVisible = true;

			mifareClassicDataBlock.DataBlockNumberChipBased = _mifareClassicDataBlock.DataBlockNumberChipBased;

			DataAsHexString = "00000000000000000000000000000000";
			DataAsCharString = "\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0";

			IsValidDataContent = null;

			children = new ObservableCollection<RFiDChipGrandGrandChildLayerViewModel>();
			
			IsTask = true;
		}

		/// <summary>
		/// TreeView Constructor
		/// </summary>
		/// <param name="_dataBlock"></param>
		/// <param name="_parentSector"></param>
		public RFiDChipGrandChildLayerViewModel(MifareClassicDataBlockModel _dataBlock, RFiDChipChildLayerViewModel _parentSector)
		{
			if (_dataBlock != null )
			{
				mifareClassicDataBlock = _dataBlock;
				if(mifareClassicDataBlock.Data == null)
					mifareClassicDataBlock.Data = new byte[16];
			}
			
			
			else
			{
				mifareClassicDataBlock = new MifareClassicDataBlockModel();
				mifareClassicDataBlock.Data = new byte[16];
			}

			IsVisible = true;

			parent = _parentSector;
			mifareClassicDataBlock.DataBlockNumberChipBased = _dataBlock.DataBlockNumberChipBased;

			DataAsHexString = "00000000000000000000000000000000";
			DataAsCharString = "\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0";

			IsValidDataContent = null;

			tag = String.Format("{0}:{1}", _parentSector.ParentUid, _parentSector.SectorNumber);

			children = new ObservableCollection<RFiDChipGrandGrandChildLayerViewModel>();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="_desfireFile"></param>
		/// <param name="_parentSector"></param>
		public RFiDChipGrandChildLayerViewModel(MifareDesfireFileModel _desfireFile, RFiDChipChildLayerViewModel _parentSector)
		{
			desfireFile = _desfireFile;
			children = new ObservableCollection<RFiDChipGrandGrandChildLayerViewModel>();
			
			parent = _parentSector;
			
			if (_desfireFile == null)
			{
				desfireFile = new MifareDesfireFileModel();
				desfireFile.Data = new byte[16];
			}
			
			DataAsHexString = "00000000000000000000000000000000";
			DataAsCharString = "\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0";
			
			IsValidDataContent = null;
			
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="_madApp"></param>
		/// <param name="_setupViewModel"></param>
		public RFiDChipGrandChildLayerViewModel(MifareClassicMADModel _madApp, MifareClassicSetupViewModel _setupViewModel)
		{
			mifareClassicMAD = _madApp;
			children = new ObservableCollection<RFiDChipGrandGrandChildLayerViewModel>();
			
			setupViewModel = _setupViewModel;
			
			if (mifareClassicMAD == null)
			{
				mifareClassicMAD = new MifareClassicMADModel();
			}
			
			mifareClassicMAD.Data = new byte[16];
			DataAsHexString = "00000000000000000000000000000000";
			DataAsCharString = "\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0";
			
			IsValidDataContent = null;
			
		}

		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="_displayItem"></param>
		public RFiDChipGrandChildLayerViewModel(string _displayItem)
		{
			children = new ObservableCollection<RFiDChipGrandGrandChildLayerViewModel>();
			grandChildNodeHeader = _displayItem;
			isVisible = false;
		}

		#endregion Constructors

		#region Dialogs

		//private ObservableCollection<IDialogViewModel> dialogs;

		#endregion Dialogs

		#region SelectedItem

		[XmlIgnore]
		public object SelectedItem { get; set; }

		#endregion SelectedItem

		#region Parent

		public RFiDChipChildLayerViewModel Parent
		{
			get { return parent; }
		} private readonly RFiDChipChildLayerViewModel parent;

		#endregion Parent

		/// <summary>
		///
		/// </summary>
		public ObservableCollection<RFiDChipGrandGrandChildLayerViewModel> Children
		{
			get { return children; }
			set
			{
				children = value;
				RaisePropertyChanged("Children");
			}
		} private ObservableCollection<RFiDChipGrandGrandChildLayerViewModel> children;

		#region (Dependency) Properties

		/// <summary>
		///
		/// </summary>
		[XmlIgnore]
		public string DataAsCharString
		{
			get
			{
				if (mifareClassicDataBlock != null && mifareClassicDataBlock.Data.Length == 16 && dataBlockAsCharString.Length == 16)
				{
					char[] tempString = new char[mifareClassicDataBlock.Data.Length];
					for (int i = 0; i < mifareClassicDataBlock.Data.Length; i++)
					{
						if (mifareClassicDataBlock.Data[i] < 27 | mifareClassicDataBlock.Data[i] > 127)
							tempString[i] = (char)248;
						else
							tempString[i] = (char)mifareClassicDataBlock.Data[i];
					}

					dataBlockAsCharString = new string(tempString);
				}
				
				else if(desfireFile != null)
				{
					char[] tempString = new char[desfireFile.Data.Length];
					for (int i = 0; i < desfireFile.Data.Length; i++)
					{
						if (desfireFile.Data[i] < 27 | desfireFile.Data[i] > 127)
							tempString[i] = (char)248;
						else
							tempString[i] = (char)desfireFile.Data[i];
					}

					dataBlockAsCharString = new string(tempString);
				}

				else if(mifareClassicMAD != null)
				{
					char[] tempString = new char[mifareClassicMAD.Data.Length];
					for (int i = 0; i < mifareClassicMAD.Data.Length; i++)
					{
						if (mifareClassicMAD.Data[i] < 27 | mifareClassicMAD.Data[i] > 127)
							tempString[i] = (char)248;
						else
							tempString[i] = (char)mifareClassicMAD.Data[i];
					}

					dataBlockAsCharString = new string(tempString);
				}
				
				return dataBlockAsCharString;
			}
			set
			{
				
				dataBlockAsCharString = value;

				if (dataBlockAsCharString.Length == 16 || desfireFile != null || mifareClassicMAD != null)
				{
					char[] tempString = value.ToCharArray();

					try
					{
						if (mifareClassicDataBlock != null)
						{
							for (int i = 0; i < mifareClassicDataBlock.Data.Length; i++)
							{
								if ( mifareClassicDataBlock.Data != null &&
								    ((char)mifareClassicDataBlock.Data[i] != value[i])
								    && (
								    	(!((char)mifareClassicDataBlock.Data[i] < 27 | (char)mifareClassicDataBlock.Data[i] > 127))//do not perform overwrite datablockat position 'i' if non printable character...
								    	|| (value[i] > 27 && value[i] < 127) //..except if a printable character was entered at the same position
								    ))
								{
									mifareClassicDataBlock.Data[i] = (byte)value[i];
								}
							}
						}
						
						else if (desfireFile != null)
						{
							for (int i = 0; i < desfireFile.Data.Length; i++)
							{
								if ((char)desfireFile.Data[i] != value[i]
								    && (!((char)desfireFile.Data[i] < 27 | (char)desfireFile.Data[i] > 127))//do not perform overwrite datablockat position 'i' if non printable character...
								    || (value[i] > 27 && value[i] < 127) //..except if a printable character was entered at the same position
								   )
								{
									desfireFile.Data[i] = (byte)value[i];
								}
							}
						}
						
						else if (mifareClassicMAD != null)
						{
							for (int i = 0; i < mifareClassicMAD.Data.Length; i++)
							{
								if ((char)mifareClassicMAD.Data[i] != value[i]
								    && (!((char)mifareClassicMAD.Data[i] < 27 | (char)mifareClassicMAD.Data[i] > 127))//do not perform overwrite datablockat position 'i' if non printable character...
								    || (value[i] > 27 && value[i] < 127) //..except if a printable character was entered at the same position
								   )
								{
									mifareClassicMAD.Data[i] = (byte)value[i];
								}
							}
						}
					}
					catch
					{
						IsValidDataContent = false;
						IsTask = false;
						return;
					}
					IsValidDataContent = null;
					IsTask = true;
				}
				else
				{
					IsTask = false;
					IsValidDataContent = false;
				}

				RaisePropertyChanged("DataAsCharString");
				RaisePropertyChanged("DataAsHexString");
				RaisePropertyChanged("DataBlockContent");
			}
		} private string dataBlockAsCharString;

		/// <summary>
		/// DependencyProperty
		/// </summary>

		public string DataAsHexString
		{
			get
			{
				if (mifareClassicDataBlock != null &&
				    mifareClassicDataBlock.Data.Length == 16 &&
				    dataBlockAsHexString.Length == 32)
				{
					dataBlockAsHexString = CustomConverter.HexToString(mifareClassicDataBlock.Data);
				}
				else if(desfireFile != null && (dataBlockAsHexString.Length % 2 == 0))
				{
					dataBlockAsHexString = CustomConverter.HexToString(desfireFile.Data);
				}
				
				else if(mifareClassicMAD != null && (dataBlockAsHexString.Length % 2 == 0))
				{
					dataBlockAsHexString = CustomConverter.HexToString(mifareClassicMAD.Data);
				}
				return dataBlockAsHexString;
			}
			set
			{
				int discardedChars = 0;
				
				dataBlockAsHexString = value;
				
				if(mifareClassicDataBlock != null && value.Length == 32 && CustomConverter.IsInHexFormat(value))
				{
					mifareClassicDataBlock.Data = CustomConverter.GetBytes(value, out discardedChars);
					IsValidDataContent = null;
				}

				else if(mifareClassicMAD != null && (dataBlockAsHexString.Length % 2 == 0))
				{
					mifareClassicMAD.Data = CustomConverter.GetBytes(value, out discardedChars);
					IsValidDataContent = null;
				}
				
				else if(desfireFile != null && (dataBlockAsHexString.Length % 2 == 0))
				{
					desfireFile.Data = CustomConverter.GetBytes(value, out discardedChars);
					IsValidDataContent = null;
				}
				
				else
				{
					IsValidDataContent = false;
					return;
				}


				RaisePropertyChanged("DataAsHexString");
				RaisePropertyChanged("DataAsCharString");
			}
		} private string dataBlockAsHexString;

		/// <summary>
		///
		/// </summary>
		[XmlIgnore]
		public bool? IsValidDataContent
		{
			get { return isValidDataBlockContent; }
			set
			{
				isValidDataBlockContent = value;

				RaisePropertyChanged("IsValidDataContent");
			}
		}	private bool? isValidDataBlockContent;

		/// <summary>
		/// 
		/// </summary>
		public MifareClassicMADModel MifareClassicMAD
		{
			get { return mifareClassicMAD; }
			set { mifareClassicMAD = value; }
		} private MifareClassicMADModel mifareClassicMAD;
		
		/// <summary>
		/// 
		/// </summary>
		public MifareClassicDataBlockModel MifareClassicDataBlock
		{
			get { return mifareClassicDataBlock; }
			set { mifareClassicDataBlock = value; }
		} private MifareClassicDataBlockModel mifareClassicDataBlock;

		/// <summary>
		/// 
		/// </summary>
		public MifareDesfireFileModel DesfireFile
		{
			get { return desfireFile; }
			set
			{
				desfireFile = value;
				RaisePropertyChanged("DesfireFile");
				RaisePropertyChanged("DataAsHexString");
				RaisePropertyChanged("DataAsCharString");
			}
		} private MifareDesfireFileModel desfireFile;
		
		/// <summary>
		/// 
		/// </summary>
		[XmlIgnore]
		public string Tag
		{
			get { return tag; }
		} private readonly string tag;

		/// <summary>
		/// 
		/// </summary>
		[XmlIgnore]
		public string GrandChildNodeHeader
		{
			get
			{
				if (mifareClassicDataBlock != null)
					grandChildNodeHeader = string.Format("Block: [{0}; ({1})]", mifareClassicDataBlock.DataBlockNumberSectorBased, mifareClassicDataBlock.DataBlockNumberChipBased);
				else if(mifareClassicMAD != null)
					grandChildNodeHeader = string.Format("MAD ID: [{0}]", mifareClassicMAD.MADApp.ToString("D3"));
				else if (desfireFile != null)
					grandChildNodeHeader = string.Format("File No.: [{0}]", DesfireFile.FileID.ToString("D3")); //dataBlockContent.dataBlockNumber.ToString("D3"), dataBlockContent.dataBlockNumber+16.ToString("D3")

				return grandChildNodeHeader;
			}
		} private string grandChildNodeHeader;

		/// <summary>
		///
		/// </summary>
		[XmlIgnore]
		public int SelectedDataLength
		{
			get { return selectedDataLength; }
			set
			{
				selectedDataLength = value;

				if (value % 2 == 0)
				{
					SelectedDataLengthInBytes = value/2;
					IsValidSelectedDataIndexAndLength = (bool)(selectedDataIndexStart % 2 == 0);
				}
				else
					IsValidSelectedDataIndexAndLength = false;
				
				RaisePropertyChanged("SelectedDataLength");
			}
		} private int selectedDataLength;

		/// <summary>
		///
		/// </summary>
		[XmlIgnore]
		public int SelectedDataIndexStart
		{
			get { return selectedDataIndexStart; }
			set
			{
				selectedDataIndexStart = value;
				if (value % 2 == 0)
				{
					SelectedDataIndexStartInBytes = value/2;
					IsValidSelectedDataIndexAndLength = true;
				}
				else
					IsValidSelectedDataIndexAndLength = false;
				
				RaisePropertyChanged("SelectedDataIndexStart");
			}
		} private int selectedDataIndexStart;

		/// <summary>
		///
		/// </summary>
		[XmlIgnore]
		public int SelectedDataLengthInBytes
		{
			get { return selectedDataLengthInBytes; }
			set
			{
				selectedDataLengthInBytes = value;
				RaisePropertyChanged("SelectedDataLengthInBytes");
			}
		} private int selectedDataLengthInBytes;

		/// <summary>
		///
		/// </summary>
		[XmlIgnore]
		public int SelectedDataIndexStartInBytes
		{
			get { return selectedDataIndexStartInBytes; }
			set
			{
				selectedDataIndexStartInBytes = value;
				RaisePropertyChanged("SelectedDataIndexStartInBytes");
			}
		} private int selectedDataIndexStartInBytes;
		
		/// <summary>
		///
		/// </summary>
		public int DataBlockNumber
		{
			get { return mifareClassicDataBlock != null ? mifareClassicDataBlock.DataBlockNumberSectorBased : 0; }
			set
			{
				if(mifareClassicDataBlock != null)
					mifareClassicDataBlock.DataBlockNumberSectorBased = value;
				RaisePropertyChanged("DataBlockNumber");
				RaisePropertyChanged("GrandChildNodeHeader");
			}
		}

		#endregion (Dependency) Properties

		#region View Switches

		[XmlIgnore]
		public bool IsExpanded
		{
			get { return isExpanded; }
			set
			{
				if (value != isExpanded)
				{
					isExpanded = value;
					this.RaisePropertyChanged("IsExpanded");
				}

				// Expand all the way up to the root.
				if (parent != null)
					parent.IsExpanded = true;
			}
		} private bool isExpanded;

		[XmlIgnore]
		public bool IsSelected
		{
			get { return isSelected; }
			set
			{
				if (value != isSelected)
				{
					isSelected = value;
					RaisePropertyChanged("IsSelected");
				}
			}
		} private bool isSelected;

		public bool? IsValidSelectedDataIndexAndLength
		{
			get { return isValidSelectedDataIndexAndLength; }
			set
			{
				isValidSelectedDataIndexAndLength = value;
				RaisePropertyChanged("IsValidSelectedDataIndexAndLength");
			}
		} private bool? isValidSelectedDataIndexAndLength;

		[XmlIgnore]
		public bool IsFocused
		{
			get { return isFocused; }
			set
			{
				if (value != isFocused)
				{
					isFocused = value;
					RaisePropertyChanged("IsFocused");
				}
			}
		} private bool isFocused;

		public bool? IsAuthenticated
		{
			get { return isAuth; }
			set
			{
				isAuth = value;
				RaisePropertyChanged("IsAuthenticated");
			}
		} private bool? isAuth;

		public bool? IsTask
		{
			get { return isTask; }
			set
			{
				isTask = value;
				RaisePropertyChanged("IsTask");
			}
		} private bool? isTask;

		[XmlIgnore]
		public bool? IsVisible
		{
			get { return isVisible; }
			set
			{
				isVisible = value;
				RaisePropertyChanged("IsVisible");
			}
		} private bool? isVisible;

		#endregion View Switches

		#region IUserDialogViewModel Implementation

		[XmlIgnore]
		public bool IsModal { get; private set; }

		public virtual void RequestRefresh()
		{
			RaisePropertyChanged("DataAsHexString");
			RaisePropertyChanged("DataAsCharString");
			RaisePropertyChanged("GrandChildNodeHeader");
		}
		
		public virtual void RequestClose()
		{
			if (this.OnCloseRequest != null)
				OnCloseRequest(this);
			else
				Close();
		}

		public event EventHandler DialogClosing;

		public ICommand OKCommand { get { return new RelayCommand(Ok); } }

		protected virtual void Ok()
		{
			if (this.OnOk != null)
				this.OnOk(this);
			else
				Close();
		}

		/// <summary>
		///
		/// </summary>
		public ICommand CancelCommand { get { return new RelayCommand(Cancel); } }

		protected virtual void Cancel()
		{
			if (this.OnCancel != null)
				this.OnCancel(this);
			else
				Close();
		}

		/// <summary>
		///
		/// </summary>
		[XmlIgnore]
		public Action<RFiDChipGrandChildLayerViewModel> OnOk { get; set; }

		/// <summary>
		///
		/// </summary>
		[XmlIgnore]
		public Action<RFiDChipGrandChildLayerViewModel> OnCancel { get; set; }

		/// <summary>
		///
		/// </summary>
		[XmlIgnore]
		public Action<RFiDChipGrandChildLayerViewModel> OnCloseRequest { get; set; }

		/// <summary>
		///
		/// </summary>
		public void Close()
		{
			if (this.DialogClosing != null)
				this.DialogClosing(this, new EventArgs());
		}

		/// <summary>
		///
		/// </summary>
		/// <param name="collection"></param>
		public void Show(IList<IDialogViewModel> collection)
		{
			collection.Add(this);
		}

		#endregion IUserDialogViewModel Implementation
	}
}