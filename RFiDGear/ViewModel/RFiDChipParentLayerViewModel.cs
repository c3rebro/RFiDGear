using ByteArrayHelper.Extensions;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using LibLogicalAccess;
using MvvmDialogs.ViewModels;
using RFiDGear.DataAccessLayer;
using RFiDGear.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml.Serialization;

namespace RFiDGear.ViewModel
{
	/// <summary>
	/// Description of MifareClassic1KContentViewModel.
	/// </summary>
	[XmlRootAttribute("TreeViewParentNode", IsNullable = false)]
	public class RFiDChipParentLayerViewModel : ViewModelBase
	{
		private SettingsReaderWriter settings;

		private static object _selectedItem;

		private ObservableCollection<IDialogViewModel> dialogs;

		//private readonly CARD_TYPE _CardType;
		private readonly List<MenuItem> ContextMenuItems;

		private readonly RelayCommand _cmdReadAllSectorsWithDefaultKeys;
		private readonly RelayCommand _cmdReadAllPages;
		private readonly RelayCommand _cmdDeleteThisNode;

		private readonly RelayCommand _cmdCreateApp;
		private readonly RelayCommand _cmdEraseDesfireCard;
		//private readonly string[] _constCardType = { "Mifare1K", "Mifare2K", "Mifare4K", "DESFireEV1" };

		private MifareClassicChipModel mifareClassicUidModel;
		private MifareDesfireChipModel mifareDesfireUidModel;
		private MifareUltralightChipModel mifareUltralightUidModel;

		#region Constructors

		public RFiDChipParentLayerViewModel()
		{
			ID = new Random().Next();

			mifareClassicUidModel = new MifareClassicChipModel();
			mifareDesfireUidModel = new MifareDesfireChipModel();
		}

		public RFiDChipParentLayerViewModel(string _text)
		{
			ID = new Random().Next();

			mifareClassicUidModel = new MifareClassicChipModel();
			mifareDesfireUidModel = new MifareDesfireChipModel();
			mifareUltralightUidModel = new MifareUltralightChipModel();
			
			ParentNodeHeader = _text;
		}
		
		public RFiDChipParentLayerViewModel(MifareClassicChipModel _uidModel, ObservableCollection<IDialogViewModel> _dialogs, bool _isTask = false)
		{
			ID = new Random().Next();

			if (_dialogs != null)
				dialogs = _dialogs;

			isTask = _isTask;
			settings = new SettingsReaderWriter();
			mifareClassicUidModel = _uidModel;
			CardType = mifareClassicUidModel.CardType;

			_cmdReadAllSectorsWithDefaultKeys = new RelayCommand(MifareClassicQuickCheck);
			_cmdDeleteThisNode = new RelayCommand(DeleteMeCommand);

			ContextMenuItems = new List<MenuItem>();
			ContextMenuItems.Add(new MenuItem()
			                     {
			                     	Header = ResourceLoader.getResource("menuItemContextMenuParentNodeQuickCheck"),
			                     	Command = _cmdReadAllSectorsWithDefaultKeys,
			                     	ToolTip = new ToolTip()
			                     	{
			                     		Content = ResourceLoader.getResource("toolTipContextMenuParentNodeMifareClassicQuickCheck")
			                     	}
			                     });

			ContextMenuItems.Add(new MenuItem()
			                     {
			                     	Header = "Delete Node",
			                     	Command = _cmdDeleteThisNode
			                     });

			_children = new ObservableCollection<RFiDChipChildLayerViewModel>();

			if (!isTask)
				LoadChildren();

			IsSelected = true;

			if (mifareClassicUidModel != null)
				ParentNodeHeader = String.Format("ChipType: {1}\nUid: {0}", mifareClassicUidModel.UidNumber, Enum.GetName(typeof(CARD_TYPE), CardType));
//			else if(mifareDesfireUidModel != null)
//				ParentNodeHeader = String.Format("ChipType: {1}\nUid: {0}", mifareDesfireUidModel.uidNumber, Enum.GetName(typeof(CARD_TYPE), CardType));

		}

		public RFiDChipParentLayerViewModel(MifareDesfireChipModel _uidModel, ObservableCollection<IDialogViewModel> _dialogs, bool _isTask = false)
		{
			ID = new Random().Next();

			if (_dialogs != null)
				dialogs = _dialogs;

			isTask = _isTask;
			settings = new SettingsReaderWriter();

			mifareDesfireUidModel = _uidModel;
			CardType = mifareDesfireUidModel.CardType;

			RelayCommand _cmdReadAppIds = new RelayCommand(MifareDesfireQuickCheck);
			
			_cmdCreateApp = new RelayCommand(CreateApp);
			_cmdEraseDesfireCard = new RelayCommand(EraseDesfireCard);

			ContextMenuItems = new List<MenuItem>();

			ContextMenuItems.Add(new MenuItem()
			                     {
			                     	Header = ResourceLoader.getResource("menuItemContextMenuParentNodeQuickCheck"),
			                     	Command = _cmdReadAppIds,
			                     	ToolTip = new ToolTip()
			                     	{
			                     		Content = ResourceLoader.getResource("toolTipContextMenuParentNodeMifareDesfireQuickCheck")
			                     	}
			                     });

			_children = new ObservableCollection<RFiDChipChildLayerViewModel>();

			if (!isTask)
				LoadChildren();

			IsSelected = true;

			if (mifareDesfireUidModel != null)
				ParentNodeHeader = String.Format("ChipType: {1}\nUid: {0}", mifareDesfireUidModel.UidNumber, Enum.GetName(typeof(CARD_TYPE), CardType));
		}

		public RFiDChipParentLayerViewModel(MifareUltralightChipModel _uidModel, ObservableCollection<IDialogViewModel> _dialogs, bool _isTask = false)
		{
			ID = new Random().Next();

			if (_dialogs != null)
				dialogs = _dialogs;

			isTask = _isTask;
			settings = new SettingsReaderWriter();
			mifareUltralightUidModel = _uidModel;
			CardType = mifareUltralightUidModel.CardType;

			_cmdReadAllPages = new RelayCommand(MifareUltralightQuickCheck);
			_cmdDeleteThisNode = new RelayCommand(DeleteMeCommand);

			ContextMenuItems = new List<MenuItem>();
			ContextMenuItems.Add(new MenuItem()
			                     {
			                     	Header = ResourceLoader.getResource("menuItemContextMenuParentNodeQuickCheck"),
			                     	Command = _cmdReadAllPages,
			                     	ToolTip = new ToolTip()
			                     	{
			                     		Content = ResourceLoader.getResource("toolTipContextMenuParentNodeMifareClassicQuickCheck")
			                     	}
			                     });

			ContextMenuItems.Add(new MenuItem()
			                     {
			                     	Header = "Delete Node",
			                     	Command = _cmdDeleteThisNode
			                     });

			_children = new ObservableCollection<RFiDChipChildLayerViewModel>();

			if (!isTask)
				LoadChildren();

			IsSelected = true;

			if (mifareUltralightUidModel != null)
				ParentNodeHeader = String.Format("ChipType: {1}\nUid: {0}", mifareUltralightUidModel.UidNumber, Enum.GetName(typeof(CARD_TYPE), CardType));

		}
		
		#endregion Constructors

		#region Context Menu Items

		/// <summary>
		///
		/// </summary>
		[XmlIgnore]
		public List<MenuItem> ContextMenu
		{
			get { return ContextMenuItems; }
		}

		private void MifareClassicQuickCheck()
		{
			if (!isTask)
			{
				using (RFiDDevice device = new RFiDDevice(settings.DefaultSpecification.DefaultReaderProvider))
				{
					Mouse.OverrideCursor = Cursors.Wait;

					foreach (RFiDChipChildLayerViewModel cnVM in this.Children)
					{
						foreach (string key in settings.DefaultSpecification.MifareClassicDefaultQuickCheckKeys)
						{
							//TODO Try all Keys and add the result somewhere in the treeview

							if (device.ReadMiFareClassicSingleSector(cnVM.SectorNumber, key, key) == ERROR.NoError)
							{
								cnVM.Children.Add(new RFiDChipGrandChildLayerViewModel(string.Format("Key: {0}", key)));
								cnVM.IsAuthenticated = true;
							}
							else
							{
								cnVM.IsAuthenticated = false;
								continue;
							}
							break;
						}

						foreach (RFiDChipGrandChildLayerViewModel gcVM in cnVM.Children.Where(x => x.MifareClassicDataBlock != null))
						{
							if (device.Sector.DataBlock.Any(x => x.DataBlockNumberSectorBased == gcVM.DataBlockNumber)) // (gcVM.DataBlockContent != null)
							{
								if (device.Sector.DataBlock.First(x => x.DataBlockNumberSectorBased == gcVM.DataBlockNumber).Data != null)
								{
									gcVM.MifareClassicDataBlock.Data = device.Sector.DataBlock.First(x => x.DataBlockNumberSectorBased == gcVM.DataBlockNumber).Data; //device.currentSector[gcVM.DataBlockNumber];
									gcVM.IsAuthenticated = true;
								}
								else
								{
									gcVM.IsAuthenticated = false;
								}
							}
							else
							{
								gcVM.IsAuthenticated = false;
							}
						}
					}

					this.IsExpanded = true;

					Mouse.OverrideCursor = null;
				}
			}
		}

		private void MifareDesfireQuickCheck()
		{
			if (!isTask)
			{
				using (RFiDDevice device = RFiDDevice.Instance)
				{
					if (device != null && device.GetMiFareDESFireChipAppIDs() == ERROR.NoError)
					{
						Mouse.OverrideCursor = Cursors.Wait;

						var appIDs = device.AppIDList;

						Children.Clear();

						Children.Add(
							new RFiDChipChildLayerViewModel(
								string.Format("Available Space: {0}Byte", device.FreeMemory)));

						Children.Add(
							new RFiDChipChildLayerViewModel(
								new MifareDesfireAppModel(0), this, CardType, dialogs));

						foreach (uint appID in appIDs)
						{
							if(appID == 0)
								continue;
							
							Children.Add(new RFiDChipChildLayerViewModel(new MifareDesfireAppModel(appID), this, CardType, dialogs));

							if (device.GetMifareDesfireAppSettings(settings.DefaultSpecification.MifareDesfireDefaultSecuritySettings.First(x => x.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardApplicationMasterKey).Key,
							                                       settings.DefaultSpecification.MifareDesfireDefaultSecuritySettings.First(x => x.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardApplicationMasterKey).EncryptionType,
							                                       0, (int)appID) == ERROR.NoError)
							{
								Children.First(x => x.AppID == appID).Children.Add(new RFiDChipGrandChildLayerViewModel(string.Format("Available Keys: {0}", device.MaxNumberOfAppKeys)));
								Children.First(x => x.AppID == appID).Children.Add(new RFiDChipGrandChildLayerViewModel(string.Format("Key Settings: {0}", Enum.GetName(typeof(DESFireKeySettings), (device.DesfireAppKeySetting & (DESFireKeySettings)0xF0)))));

								Children.First(x => x.AppID == appID).Children.Add(new RFiDChipGrandChildLayerViewModel(string.Format("Allow Change MK: {0}", (device.DesfireAppKeySetting & (DESFireKeySettings)0x01) == (DESFireKeySettings)0x01 ? "yes" : "no")));
								Children.First(x => x.AppID == appID).Children.Add(new RFiDChipGrandChildLayerViewModel(string.Format("Allow Listing without MK: {0}", (device.DesfireAppKeySetting & (DESFireKeySettings)0x02) == (DESFireKeySettings)0x02 ? "yes" : "no")));
								Children.First(x => x.AppID == appID).Children.Add(new RFiDChipGrandChildLayerViewModel(string.Format("Allow Create/Delete without MK: {0}", (device.DesfireAppKeySetting & (DESFireKeySettings)0x04) == (DESFireKeySettings)0x04 ? "yes" : "no")));
								Children.First(x => x.AppID == appID).Children.Add(new RFiDChipGrandChildLayerViewModel(string.Format("Allow Change Config: {0}", (device.DesfireAppKeySetting & (DESFireKeySettings)0x08) == (DESFireKeySettings)0x08 ? "yes" : "no")));
							}

							//TODO add grandchild fileid
							if (device.GetMifareDesfireFileList(
								settings.DefaultSpecification.MifareDesfireDefaultSecuritySettings.First(x => x.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardApplicationMasterKey).Key,
								settings.DefaultSpecification.MifareDesfireDefaultSecuritySettings.First(x => x.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardApplicationMasterKey).EncryptionType,
								0, (int)appID) == ERROR.NoError)
							{
								foreach (byte fileID in device.FileIDList)
								{
									Children.First(x => x.AppID == appID).Children.Add(new RFiDChipGrandChildLayerViewModel(new MifareDesfireFileModel(null, fileID),Children.First(x => x.AppID == appID)));

									if (device.GetMifareDesfireFileSettings(settings.DefaultSpecification.MifareDesfireDefaultSecuritySettings.First(x => x.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardApplicationMasterKey).Key,
									                                        settings.DefaultSpecification.MifareDesfireDefaultSecuritySettings.First(x => x.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardApplicationMasterKey).EncryptionType,
									                                        0, (int)appID, fileID) == ERROR.NoError)
									{
										RFiDChipGrandChildLayerViewModel grandChild = Children.First(x => x.AppID == appID).Children.First(y => (y.DesfireFile != null ? y.DesfireFile.FileID : -1) == fileID);

										grandChild.Children.Add(new RFiDChipGrandGrandChildLayerViewModel(string.Format("FileType: {0}", Enum.GetName(typeof(FileType_MifareDesfireFileType), device.DesfireFileSetting.FileType)),grandChild));
										grandChild.Children.Add(new RFiDChipGrandGrandChildLayerViewModel(string.Format("FileSize: {0}Bytes", device.DesfireFileSetting.dataFile.fileSize.ToString()),grandChild));
										grandChild.Children.Add(new RFiDChipGrandGrandChildLayerViewModel(string.Format("EncryptionMode: {0}", Enum.GetName(typeof(EncryptionMode), device.DesfireFileSetting.comSett)),grandChild));
										grandChild.Children.Add(new RFiDChipGrandGrandChildLayerViewModel(string.Format("Read: {0}", Enum.GetName(typeof(TaskAccessRights), ((device.DesfireFileSetting.accessRights[1] & 0xF0) >> 4))),grandChild));
										grandChild.Children.Add(new RFiDChipGrandGrandChildLayerViewModel(string.Format("Write: {0}", Enum.GetName(typeof(TaskAccessRights), device.DesfireFileSetting.accessRights[1] & 0x0F)),grandChild));
										grandChild.Children.Add(new RFiDChipGrandGrandChildLayerViewModel(string.Format("RW: {0}", Enum.GetName(typeof(TaskAccessRights), ((device.DesfireFileSetting.accessRights[0] & 0xF0) >> 4))),grandChild)); //lsb, upper nibble
										grandChild.Children.Add(new RFiDChipGrandGrandChildLayerViewModel(string.Format("Change: {0}", Enum.GetName(typeof(TaskAccessRights), device.DesfireFileSetting.accessRights[0] & 0x0F)),grandChild)); //lsb , lower nibble
									}
								}
							}
						}

						if (device.AuthToMifareDesfireApplication("00000000000000000000000000000000", DESFireKeyType.DF_KEY_AES, 0) == ERROR.NoError ||
						    device.AuthToMifareDesfireApplication("00000000000000000000000000000000", DESFireKeyType.DF_KEY_3K3DES, 0) == ERROR.NoError ||
						    device.AuthToMifareDesfireApplication("00000000000000000000000000000000", DESFireKeyType.DF_KEY_DES, 0) == ERROR.NoError)
						{
							Children.First(x => x.AppID == 0).Children.Add(new RFiDChipGrandChildLayerViewModel("PICC MasterKey NOT Set"));
						}
						else
						{
							Children.First(x => x.AppID == 0).Children.Add(new RFiDChipGrandChildLayerViewModel("PICC SECURED, MasterKey Unkown"));
						}

						IsExpanded = true;

						Mouse.OverrideCursor = null;
					}
				}
			}
		}

		private void MifareUltralightQuickCheck()
		{
			if (!isTask)
			{
				using (RFiDDevice device = new RFiDDevice(settings.DefaultSpecification.DefaultReaderProvider))
				{
					Mouse.OverrideCursor = Cursors.Wait;

					foreach (RFiDChipChildLayerViewModel cnVM in this.Children)
					{

						if (device.ReadMifareUltralightSinglePage(cnVM.PageNumber) == ERROR.NoError)
						{
							string dataToShow = ByteConverter.HexToString(device.MifareUltralightPageData);
							
							for (int i = (ByteConverter.HexToString(device.MifareUltralightPageData).Length) - 2; i > 0; i -= 2)
								dataToShow = dataToShow.Insert(i, " ");
							
							cnVM.Children.Add(
								new RFiDChipGrandChildLayerViewModel(
									string.Format("Data: {0}", dataToShow)));
									
									cnVM.IsAuthenticated = true;
								}
						else
						{
							cnVM.IsAuthenticated = false;
							continue;
						}

						foreach (RFiDChipGrandChildLayerViewModel gcVM in cnVM.Children.Where(x => x.MifareClassicDataBlock != null))
						{
							if (device.Sector.DataBlock.Any(x => x.DataBlockNumberSectorBased == gcVM.DataBlockNumber)) // (gcVM.DataBlockContent != null)
							{
								if (device.Sector.DataBlock.First(x => x.DataBlockNumberSectorBased == gcVM.DataBlockNumber).Data != null)
								{
									gcVM.MifareClassicDataBlock.Data = device.Sector.DataBlock.First(x => x.DataBlockNumberSectorBased == gcVM.DataBlockNumber).Data; //device.currentSector[gcVM.DataBlockNumber];
									gcVM.IsAuthenticated = true;
								}
								else
								{
									gcVM.IsAuthenticated = false;
								}
							}
							else
							{
								gcVM.IsAuthenticated = false;
							}
						}
					}

					this.IsExpanded = true;

					Mouse.OverrideCursor = null;
				}
			}
		}
		
		private void EraseDesfireCard()
		{
			if (!isTask)
			{
				using (RFiDDevice device = new RFiDDevice(settings.DefaultSpecification.DefaultReaderProvider))
				{
					Mouse.OverrideCursor = Cursors.Wait;

					this.IsExpanded = true;

					Mouse.OverrideCursor = null;
				}
			}
		}

		private void CreateApp()
		{
		}

		private void DeleteMeCommand()
		{
		}

		#endregion Context Menu Items

		#region Items Sources

		/// <summary>
		///
		/// </summary>
		public int ID { get; set; }

		/// <summary>
		///
		/// </summary>
		public ObservableCollection<RFiDChipChildLayerViewModel> Children
		{
			get { return _children; }
			set
			{
				_children = value;
				RaisePropertyChanged("Children");
			}
		}

		private ObservableCollection<RFiDChipChildLayerViewModel> _children;

		#endregion Items Sources

		#region Selected Items

		[XmlIgnore]
		public object SelectedItem
		{
			get { return _selectedItem; }
			set
			{
				if (_selectedItem != value)
				{
					_selectedItem = value;
					RaisePropertyChanged("SelectedItem");
				}
			}
		}

		#endregion Selected Items

		#region Properties

		/// <summary>
		///
		/// </summary>
		public string ParentNodeHeader
		{
			get { return parentNodeHeader; }
			set
			{
				parentNodeHeader = value;
				RaisePropertyChanged("ParentNodeHeader");
			}
		} private string parentNodeHeader;

		/// <summary>
		///
		/// </summary>
		public int TaskIndex { get; set; }

		/// <summary>
		///
		/// </summary>
		public string UidNumber
		{
			get
			{
				if (mifareClassicUidModel != null)
					return mifareClassicUidModel.UidNumber;
				else if(mifareDesfireUidModel != null)
					return mifareDesfireUidModel.UidNumber;
				else if(mifareUltralightUidModel != null)
					return mifareUltralightUidModel.UidNumber;
				else
					return "";
			}
		}

		/// <summary>
		///
		/// </summary>

		public CARD_TYPE CardType
		{
			get
			{
				return mifareClassicUidModel != null ? mifareClassicUidModel.CardType :
					(mifareDesfireUidModel != null ? mifareDesfireUidModel.CardType :
					 (mifareUltralightUidModel != null ? mifareUltralightUidModel.CardType : CARD_TYPE.Unspecified)
					);}
			set
			{
				if (mifareClassicUidModel != null)
					mifareClassicUidModel.CardType = value;
				else if (mifareDesfireUidModel != null)
					mifareDesfireUidModel.CardType = value;
				else if (mifareUltralightUidModel != null)
					mifareUltralightUidModel.CardType = value;
				RaisePropertyChanged("CardType");
			}
		}

		#endregion Properties

		#region View Switches

		/// <summary>
		///
		/// </summary>
		public bool IsExpanded
		{
			get { return _isExpanded; }
			set
			{
				_isExpanded = value;
				RaisePropertyChanged("IsExpanded");
			}
		}

		private bool _isExpanded;

		/// <summary>
		///
		/// </summary>
		public bool IsSelected
		{
			get { return _isSelected; }
			set
			{
				_isSelected = value;
				RaisePropertyChanged("IsSelected");
			}
		}

		private bool _isSelected;

		/// <summary>
		///
		/// </summary>
		public bool IsTask
		{
			get { return isTask; }
			set { isTask = value; }
		}

		private bool isTask;

		/// <summary>
		///
		/// </summary>
		public bool? IsProgrammingCompletedSuccessfully
		{
			get { return isProgrammingCompletedSuccessfully; }
			set
			{
				isProgrammingCompletedSuccessfully = value;
				RaisePropertyChanged("IsProgrammingCompletedSuccessfully");
			}
		}

		private bool? isProgrammingCompletedSuccessfully;

		/// <summary>
		///
		/// </summary>
		public bool? IsBeingProgrammed
		{
			get { return isBeingProgrammed; }
			set
			{
				isBeingProgrammed = value;
				RaisePropertyChanged("IsBeingProgrammed");
			}
		}

		private bool? isBeingProgrammed;

		#endregion View Switches

		#region Commands
		
		/// <summary>
		/// 
		/// </summary>
		public ICommand ExecuteDesfireQuickCheckCommand { get { return new RelayCommand(MifareDesfireQuickCheck); } }
		
		/// <summary>
		/// 
		/// </summary>
		public ICommand ExecuteClassicQuickCheckCommand { get { return new RelayCommand(MifareClassicQuickCheck); } }
		
		#endregion
		private void LoadChildren()
		{
			switch (CardType)
			{
				case CARD_TYPE.Mifare1K:
					{
						for (int i = 0; i < 16; i++)
						{
							_children.Add(
								new RFiDChipChildLayerViewModel(
									new MifareClassicSectorModel(i), this, CardType, dialogs));
						}
						
						for(int i = 0; i < _children.Count; i++)
						{
							for(int j = 0; j < _children[i].Children.Count(); j++)
							{
								_children[i].Children[j].MifareClassicDataBlock.DataBlockNumberChipBased = CustomConverter.GetChipBasedDataBlockNumber(CARD_TYPE.Mifare1K, i, _children[i].Children[j].MifareClassicDataBlock.DataBlockNumberSectorBased);
							}
						}

					}
					break;

				case CARD_TYPE.Mifare2K:
					{
						for (int i = 0; i < 32; i++)
						{
							_children.Add(
								new RFiDChipChildLayerViewModel(
									new MifareClassicSectorModel(i), this, CardType, dialogs));
						}
						
						for(int i = 0; i < _children.Count; i++)
						{
							for(int j = 0; j < _children[i].Children.Count(); j++)
							{
								_children[i].Children[j].MifareClassicDataBlock.DataBlockNumberChipBased = CustomConverter.GetChipBasedDataBlockNumber(CARD_TYPE.Mifare1K, i, _children[i].Children[j].MifareClassicDataBlock.DataBlockNumberSectorBased);
							}
						}
					}
					break;

				case CARD_TYPE.Mifare4K:
					{
						for (int i = 0; i < 40; i++)
						{
							_children.Add(
								new RFiDChipChildLayerViewModel(
									new MifareClassicSectorModel(i), this, CardType, dialogs));
						}
						
						for(int i = 0; i < _children.Count; i++)
						{
							for(int j = 0; j < _children[i].Children.Count(); j++)
							{
								_children[i].Children[j].MifareClassicDataBlock.DataBlockNumberChipBased = CustomConverter.GetChipBasedDataBlockNumber(CARD_TYPE.Mifare1K, i, _children[i].Children[j].MifareClassicDataBlock.DataBlockNumberSectorBased);
							}
						}
					}
					break;

				case CARD_TYPE.DESFire:
				case CARD_TYPE.DESFireEV1:
				case CARD_TYPE.DESFireEV2:
					{
						_children.Add(
							new RFiDChipChildLayerViewModel(
								new MifareDesfireAppModel(0), this, CardType, dialogs));
					}
					break;
					
				case CARD_TYPE.MifareUltralight:
					
					for(int i = 0; i < 15; i++)
					{
						_children.Add(
							new RFiDChipChildLayerViewModel(
								new MifareUltralightPageModel(new byte[4], i),this, CardType, dialogs));
					}

					break;
			}
		}
	}
}