/*
 * Created by SharpDevelop.
 * Date: 10/11/2017
 * Time: 22:15
 *
 */

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
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Xml.Serialization;

namespace RFiDGear.ViewModel
{
	/// <summary>
	/// Description of MifareDesfireSetupViewModel.
	/// </summary>
	public class MifareDesfireSetupViewModel : ViewModelBase, IUserDialogViewModel
	{
		private SettingsReaderWriter settings = new SettingsReaderWriter();

		private MifareDesfireChipModel chip;
		private MifareDesfireAppModel app;
		private DESFireAccessRights accessRights;

		public ERROR TaskErr { get; set; }

		/// <summary>
		///
		/// </summary>
		public MifareDesfireSetupViewModel()
		{
			accessRights = new DESFireAccessRights();
		}

		/// <summary>
		///
		/// </summary>
		/// <param name="_selectedSetupViewModel"></param>
		/// <param name="_dialogs"></param>
		public MifareDesfireSetupViewModel(object _selectedSetupViewModel, ObservableCollection<IDialogViewModel> _dialogs)
		{
			
			if(_selectedSetupViewModel is MifareDesfireSetupViewModel)
			{
				PropertyInfo[] properties = typeof(MifareDesfireSetupViewModel).GetProperties(BindingFlags.Public | BindingFlags.Instance);

				foreach (PropertyInfo p in properties)
				{
					// If not writable then cannot null it; if not readable then cannot check it's value
					if (!p.CanWrite || !p.CanRead) { continue; }

					MethodInfo mget = p.GetGetMethod(false);
					MethodInfo mset = p.GetSetMethod(false);

					// Get and set methods have to be public
					if (mget == null) { continue; }
					if (mset == null) { continue; }

					p.SetValue(this, p.GetValue(_selectedSetupViewModel));
				}
			}
			
			else
			{
				DesfireMasterKeyCurrent = settings.DefaultSpecification.MifareDesfireDefaultSecuritySettings.First(x => x.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardCardMasterKey).Key;
				SelectedDesfireMasterKeyEncryptionTypeCurrent = settings.DefaultSpecification.MifareDesfireDefaultSecuritySettings.First(x => x.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardCardMasterKey).EncryptionType;

				DesfireMasterKeyTarget = settings.DefaultSpecification.MifareDesfireDefaultSecuritySettings.First(x => x.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardApplicationMasterKey).Key;
				DesfireMasterKeyTargetRetyped = settings.DefaultSpecification.MifareDesfireDefaultSecuritySettings.First(x => x.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardApplicationMasterKey).Key;
				SelectedDesfireMasterKeyEncryptionTypeTarget = settings.DefaultSpecification.MifareDesfireDefaultSecuritySettings.First(x => x.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardCardMasterKey).EncryptionType;

				DesfireAppKeyCurrent = settings.DefaultSpecification.MifareDesfireDefaultSecuritySettings.First(x => x.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardApplicationMasterKey).Key;
				SelectedDesfireAppKeyEncryptionTypeCurrent = settings.DefaultSpecification.MifareDesfireDefaultSecuritySettings.First(x => x.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardApplicationMasterKey).EncryptionType;

				DesfireAppKeyTarget = settings.DefaultSpecification.MifareDesfireDefaultSecuritySettings.First(x => x.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardApplicationMasterKey).Key;
				SelectedDesfireAppKeyEncryptionTypeTarget = settings.DefaultSpecification.MifareDesfireDefaultSecuritySettings.First(x => x.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardApplicationMasterKey).EncryptionType;

				DesfireAppKeyTargetRetyped = settings.DefaultSpecification.MifareDesfireDefaultSecuritySettings.First(x => x.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardApplicationMasterKey).Key;

				accessRights = new DESFireAccessRights();

				AppNumberNew = "1";
				AppNumberCurrent = "0";
				AppNumberTarget = "0";

				SelectedDesfireAppKeyNumberTarget = MifareDesfireKeyNumber.MifareDesfireKey01;
				SelectedDesfireAppMaxNumberOfKeys = MifareDesfireKeyNumber.MifareDesfireKey01;

				IsValidDesfireMasterKeyCurrent = null;
				IsValidDesfireMasterKeyTarget = null;
				IsValidDesfireMasterKeyTargetRetyped = null;

				IsValidDesfireAppKeyCurrent = null;
				IsValidDesfireAppKeyTarget = null;
				IsValidDesfireAppKeyTargetRetyped = null;

				IsValidAppNumberCurrent = null;
				IsValidAppNumberTarget = null;
				IsValidAppNumberNew = null;

				SelectedTaskIndex = "0";
				SelectedTaskDescription = "Enter a Description";

				MifareDesfireChipModel chip = new MifareDesfireChipModel(string.Format("Task Description: {0}", SelectedTaskDescription), CARD_TYPE.DESFire);
			}

		}

		#region Key Properties Card Master

		public DESFireKeyType SelectedDesfireMasterKeyEncryptionTypeCurrent
		{
			get { return selectedDesfireMasterKeyEncryptionTypeCurrent; }
			set
			{
				selectedDesfireMasterKeyEncryptionTypeCurrent = value;
				RaisePropertyChanged("SelectedDesfireMasterKeyEncryptionTypeCurrent");
			}
		}

		private DESFireKeyType selectedDesfireMasterKeyEncryptionTypeCurrent;

		public string DesfireMasterKeyCurrent
		{
			get { return desfireMasterKeyCurrent; }
			set
			{
				try
				{
					desfireMasterKeyCurrent = value.ToUpper().Remove(32);
				}
				catch
				{
					desfireMasterKeyCurrent = value.ToUpper();
				}
				IsValidDesfireMasterKeyCurrent = (CustomConverter.IsInHexFormat(desfireMasterKeyCurrent) && desfireMasterKeyCurrent.Length == 32);
				RaisePropertyChanged("DesfireMasterKeyCurrent");
			}
		}

		private string desfireMasterKeyCurrent;

		/// <summary>
		///
		/// </summary>
		public bool? IsValidDesfireMasterKeyCurrent
		{
			get
			{
				return isValidDesfireMasterKeyCurrent;
			}
			set
			{
				isValidDesfireMasterKeyCurrent = value;
				RaisePropertyChanged("IsValidDesfireMasterKeyCurrent");
			}
		}

		private bool? isValidDesfireMasterKeyCurrent;

		public DESFireKeyType SelectedDesfireMasterKeyEncryptionTypeTarget
		{
			get { return selectedDesfireMasterKeyEncryptionTypeTarget; }
			set
			{
				selectedDesfireMasterKeyEncryptionTypeTarget = value;
				RaisePropertyChanged("SelectedDesfireMasterKeyEncryptionTypeCurrent");
			}
		}

		private DESFireKeyType selectedDesfireMasterKeyEncryptionTypeTarget;

		/// <summary>
		///
		/// </summary>
		public string DesfireMasterKeyTarget
		{
			get { return desfireMasterKeyTarget; }
			set
			{
				try
				{
					desfireMasterKeyTarget = value.ToUpper().Remove(32);
				}
				catch
				{
					desfireMasterKeyTarget = value.ToUpper();
				}

				IsValidDesfireMasterKeyTarget = (
					CustomConverter.IsInHexFormat(desfireMasterKeyTarget) &&
					desfireMasterKeyTarget.Length == 32);
				IsValidDesfireMasterKeyTargetRetyped = (desfireMasterKeyTarget == desfireMasterKeyTargetRetyped);
				RaisePropertyChanged("DesfireMasterKeyTarget");
			}
		}

		private string desfireMasterKeyTarget;

		/// <summary>
		///
		/// </summary>
		public string DesfireMasterKeyTargetRetyped
		{
			get { return desfireMasterKeyTargetRetyped; }
			set
			{
				try
				{
					desfireMasterKeyTargetRetyped = value.ToUpper().Remove(32);
				}
				catch
				{
					desfireMasterKeyTargetRetyped = value.ToUpper();
				}

				IsValidDesfireMasterKeyTargetRetyped = ((
					CustomConverter.IsInHexFormat(desfireMasterKeyTargetRetyped) &&
					desfireMasterKeyTargetRetyped.Length == 32) &&
				                                        (IsValidDesfireMasterKeyTarget == true) &&
				                                        desfireMasterKeyTarget.ToUpper() == desfireMasterKeyTargetRetyped.ToUpper());
				RaisePropertyChanged("DesfireMasterKeyTargetRetyped");
			}
		}

		private string desfireMasterKeyTargetRetyped;

		/// <summary>
		///
		/// </summary>
		public bool? IsValidDesfireMasterKeyTarget
		{
			get
			{
				return isValidDesfireMasterKeyTarget;
			}
			set
			{
				isValidDesfireMasterKeyTarget = value;
				RaisePropertyChanged("IsValidDesfireMasterKeyTarget");
			}
		}

		private bool? isValidDesfireMasterKeyTarget;

		/// <summary>
		///
		/// </summary>
		public bool? IsValidDesfireMasterKeyTargetRetyped
		{
			get
			{
				return isValidDesfireMasterKeyTargetRetyped;
			}
			set
			{
				isValidDesfireMasterKeyTargetRetyped = value;
				RaisePropertyChanged("IsValidDesfireMasterKeyTargetRetyped");
			}
		}

		private bool? isValidDesfireMasterKeyTargetRetyped;

		#endregion Key Properties Card Master

		#region Key Properties App Master

		#region App Creation

		/// <summary>
		///
		/// </summary>
		public DESFireKeyType SelectedDesfireAppKeyEncryptionTypeCreateNewApp
		{
			get { return selectedCreateDesfireAppKeyEncryptionTypeCurrent; }
			set
			{
				selectedCreateDesfireAppKeyEncryptionTypeCurrent = value;
				RaisePropertyChanged("SelectedDesfireAppKeyEncryptionTypeCreateNewApp");
			}
		}

		private DESFireKeyType selectedCreateDesfireAppKeyEncryptionTypeCurrent;

		/// <summary>
		///
		/// </summary>
		public MifareDesfireKeyNumber SelectedDesfireAppMaxNumberOfKeys
		{
			get { return selectedDesfireAppMaxNumberOfKeys; }
			set
			{
				selectedDesfireAppMaxNumberOfKeys = value;
				RaisePropertyChanged("SelectedDesfireAppMaxNumberOfKeys");
			}
		}

		private MifareDesfireKeyNumber selectedDesfireAppMaxNumberOfKeys;

		/// <summary>
		///
		/// </summary>
		public AccessCondition_MifareDesfireAppCreation SelectedDesfireAppKeySettingsCreateNewApp
		{
			get { return selectedDesfireAppKeySettingsTarget; }
			set
			{
				selectedDesfireAppKeySettingsTarget = value;
				//selectedDesfireAppKeySettingsTarget |= DESFireKeySettings.KS_ALLOW_CHANGE_MK;
				RaisePropertyChanged("SelectedDesfireAppKeySettingsCreateNewApp");
			}
		}

		private AccessCondition_MifareDesfireAppCreation selectedDesfireAppKeySettingsTarget;

		/// <summary>
		///
		/// </summary>
		public string AppNumberNew
		{
			get { return appNumberNew; }
			set
			{
				try
				{
					appNumberNew = value.ToUpper().Remove(32);
				}
				catch
				{
					appNumberNew = value.ToUpper();
				}
				IsValidAppNumberNew = (int.TryParse(value, out appNumberNewAsInt) && appNumberNewAsInt <= 65535);
				RaisePropertyChanged("AppNumberNew");
			}
		}

		private string appNumberNew;

		/// <summary>
		///
		/// </summary>
		public int AppNumberNewAsInt
		{ get { return appNumberNewAsInt; } }

		private int appNumberNewAsInt;

		/// <summary>
		///
		/// </summary>
		public bool? IsValidAppNumberNew
		{
			get
			{
				return isValidAppNumberNew;
			}
			set
			{
				isValidAppNumberNew = value;
				RaisePropertyChanged("IsValidAppNumberNew");
			}
		}

		private bool? isValidAppNumberNew;

		/// <summary>
		///
		/// </summary>
		public bool IsAllowChangeMKChecked
		{
			get { return isAllowChangeMKChecked; }
			set
			{
				isAllowChangeMKChecked = value;
				RaisePropertyChanged("IsAllowChangeMKChecked");
			}
		}

		private bool isAllowChangeMKChecked;

		/// <summary>
		///
		/// </summary>
		public bool IsAllowListingWithoutMKChecked
		{
			get { return isAllowListingWithoutMKChecked; }
			set
			{
				isAllowListingWithoutMKChecked = value;
				RaisePropertyChanged("IsAllowListingWithoutMKChecked");
			}
		}

		private bool isAllowListingWithoutMKChecked;

		/// <summary>
		///
		/// </summary>
		public bool IsAllowCreateDelWithoutMKChecked
		{
			get { return isAllowCreateDelWithoutMKChecked; }
			set
			{
				isAllowCreateDelWithoutMKChecked = value;
				RaisePropertyChanged("IsAllowCreateDelWithoutMKChecked");
			}
		}

		private bool isAllowCreateDelWithoutMKChecked;

		/// <summary>
		///
		/// </summary>
		public bool IsAllowConfigChangableChecked
		{
			get { return isAllowConfigChangableChecked; }
			set
			{
				isAllowConfigChangableChecked = value;
				RaisePropertyChanged("IsAllowConfigChangableChecked");
			}
		}

		private bool isAllowConfigChangableChecked;

		#endregion App Creation

		#region Key Properties App Master Current

		/// <summary>
		///
		/// </summary>
		public DESFireKeyType SelectedDesfireAppKeyEncryptionTypeCurrent
		{
			get { return selectedDesfireAppKeyEncryptionTypeCurrent; }
			set
			{
				selectedDesfireAppKeyEncryptionTypeCurrent = value;
				RaisePropertyChanged("SelectedDesfireAppKeyEncryptionTypeCurrent");
			}
		}

		private DESFireKeyType selectedDesfireAppKeyEncryptionTypeCurrent;

		/// <summary>
		///
		/// </summary>
		public MifareDesfireKeyNumber SelectedDesfireAppKeyNumberCurrent
		{
			get { return selectedDesfireAppKeyNumberCurrent; }
			set
			{
				selectedDesfireAppKeyNumberCurrent = value;
				RaisePropertyChanged("SelectedDesfireAppKeyNumberCurrent");
			}
		}

		private MifareDesfireKeyNumber selectedDesfireAppKeyNumberCurrent;

		/// <summary>
		///
		/// </summary>
		public string DesfireAppKeyCurrent
		{
			get { return desfireAppKeyCurrent; }
			set
			{
				try
				{
					desfireAppKeyCurrent = value.ToUpper().Remove(32);
				}
				catch
				{
					desfireAppKeyCurrent = value.ToUpper();
				}
				IsValidDesfireAppKeyCurrent = (CustomConverter.IsInHexFormat(desfireAppKeyCurrent) && desfireAppKeyCurrent.Length == 32);
				RaisePropertyChanged("DesfireAppKeyCurrent");
			}
		}

		private string desfireAppKeyCurrent;

		/// <summary>
		///
		/// </summary>
		public bool? IsValidDesfireAppKeyCurrent
		{
			get
			{
				return isValidDesfireAppKeyCurrent;
			}
			set
			{
				isValidDesfireAppKeyCurrent = value;
				RaisePropertyChanged("IsValidDesfireAppKeyCurrent");
			}
		}

		private bool? isValidDesfireAppKeyCurrent;

		/// <summary>
		///
		/// </summary>
		public string AppNumberCurrent
		{
			get { return appNumberCurrent; }
			set
			{
				try
				{
					appNumberCurrent = value.ToUpper().Remove(32);
				}
				catch
				{
					appNumberCurrent = value.ToUpper();
				}
				IsValidAppNumberCurrent = (int.TryParse(value, out appNumberCurrentAsInt) && appNumberCurrentAsInt <= 65535);
				RaisePropertyChanged("AppNumberCurrent");
			}
		}

		private string appNumberCurrent;

		/// <summary>
		///
		/// </summary>
		public int AppNumberCurrentAsInt
		{ get { return appNumberCurrentAsInt; } }

		private int appNumberCurrentAsInt;

		/// <summary>
		///
		/// </summary>
		public bool? IsValidAppNumberCurrent
		{
			get
			{
				return isValidAppNumberCurrent;
			}
			set
			{
				isValidAppNumberCurrent = value;
				RaisePropertyChanged("IsValidAppNumberCurrent");
			}
		}

		private bool? isValidAppNumberCurrent;

		#endregion Key Properties App Master Current

		#region Key Properties App Master Target

		/// <summary>
		///
		/// </summary>
		public DESFireKeyType SelectedDesfireAppKeyEncryptionTypeTarget
		{
			get { return selectedDesfireAppKeyEncryptionTypeTarget; }
			set
			{
				selectedDesfireAppKeyEncryptionTypeTarget = value;
				RaisePropertyChanged("SelectedDesfireAppKeyEncryptionTypeTarget");
			}
		}

		private DESFireKeyType selectedDesfireAppKeyEncryptionTypeTarget;

		/// <summary>
		///
		/// </summary>
		public MifareDesfireKeyNumber SelectedDesfireAppKeyNumberTarget
		{
			get { return selectedDesfireAppKeyNumberTarget; }
			set
			{
				selectedDesfireAppKeyNumberTarget = value;
				RaisePropertyChanged("SelectedDesfireAppKeyNumberTarget");
			}
		}

		private MifareDesfireKeyNumber selectedDesfireAppKeyNumberTarget;

		/// <summary>
		///
		/// </summary>
		public string DesfireAppKeyTarget
		{
			get { return desfireAppKeyTarget; }
			set
			{
				try
				{
					desfireAppKeyTarget = value.ToUpper().Remove(32);
				}
				catch
				{
					desfireAppKeyTarget = value.ToUpper();
				}

				IsValidDesfireAppKeyTarget = (
					CustomConverter.IsInHexFormat(desfireAppKeyTarget) &&
					desfireAppKeyTarget.Length == 32);
				IsValidDesfireAppKeyTargetRetyped = (desfireAppKeyTarget == desfireAppKeyTargetRetyped);
				RaisePropertyChanged("DesfireAppKeyTarget");
			}
		}

		private string desfireAppKeyTarget;

		/// <summary>
		///
		/// </summary>
		public string DesfireAppKeyTargetRetyped
		{
			get { return desfireAppKeyTargetRetyped; }
			set
			{
				try
				{
					desfireAppKeyTargetRetyped = value.ToUpper().Remove(32);
				}
				catch
				{
					desfireAppKeyTargetRetyped = value.ToUpper();
				}

				IsValidDesfireAppKeyTargetRetyped = ((
					CustomConverter.IsInHexFormat(desfireAppKeyTargetRetyped) &&
					desfireAppKeyTargetRetyped.Length == 32) &&
				                                     (IsValidDesfireAppKeyTarget == true) &&
				                                     desfireAppKeyTarget.ToUpper() == desfireAppKeyTargetRetyped.ToUpper());
				RaisePropertyChanged("DesfireAppKeyTargetRetyped");
			}
		}

		private string desfireAppKeyTargetRetyped;

		/// <summary>
		///
		/// </summary>
		public bool? IsValidDesfireAppKeyTarget
		{
			get
			{
				return isValidDesfireAppKeyTarget;
			}
			set
			{
				isValidDesfireAppKeyTarget = value;
				RaisePropertyChanged("IsValidDesfireAppKeyTarget");
			}
		}

		private bool? isValidDesfireAppKeyTarget;

		/// <summary>
		///
		/// </summary>
		public bool? IsValidDesfireAppKeyTargetRetyped
		{
			get
			{
				return isValidDesfireAppKeyTargetRetyped;
			}
			set
			{
				isValidDesfireAppKeyTargetRetyped = value;
				RaisePropertyChanged("IsValidDesfireAppKeyTargetRetyped");
			}
		}

		private bool? isValidDesfireAppKeyTargetRetyped;

		/// <summary>
		///
		/// </summary>
		public string AppNumberTarget
		{
			get { return appNumberTarget; }
			set
			{
				try
				{
					appNumberTarget = value.ToUpper().Remove(32);
				}
				catch
				{
					appNumberTarget = value.ToUpper();
				}
				IsValidAppNumberTarget = (int.TryParse(value, out appNumberTargetAsInt) && appNumberTargetAsInt <= 65535);
				RaisePropertyChanged("AppNumberTarget");
			}
		}

		private string appNumberTarget;

		/// <summary>
		///
		/// </summary>
		public int AppNumberTargetAsInt
		{ get { return appNumberTargetAsInt; } }

		private int appNumberTargetAsInt;

		/// <summary>
		///
		/// </summary>
		public bool? IsValidAppNumberTarget
		{
			get
			{
				return isValidAppNumberTarget;
			}
			set
			{
				isValidAppNumberTarget = value;
				RaisePropertyChanged("IsValidAppNumberTarget");
			}
		}

		private bool? isValidAppNumberTarget;

		#endregion Key Properties App Master Target

		#endregion Key Properties App Master

		#region Key Properties File Master

		#region File Creation

		/// <summary>
		///
		/// </summary>
		public TaskAccessRights SelectedDesfireFileAccessRightReadWrite
		{
			get { return selectedDesfireFileAccessRightReadWrite; }
			set
			{
				selectedDesfireFileAccessRightReadWrite = value;
				RaisePropertyChanged("SelectedDesfireFileAccessRightReadWrite");
			}
		}

		private TaskAccessRights selectedDesfireFileAccessRightReadWrite;

		/// <summary>
		///
		/// </summary>
		public TaskAccessRights SelectedDesfireFileAccessRightChange
		{
			get { return selectedDesfireFileAccessRightChange; }
			set
			{
				selectedDesfireFileAccessRightChange = value;
				RaisePropertyChanged("SelectedDesfireFileAccessRightChange");
			}
		}

		private TaskAccessRights selectedDesfireFileAccessRightChange;

		/// <summary>
		///
		/// </summary>
		public TaskAccessRights SelectedDesfireFileAccessRightRead
		{
			get { return selectedDesfireFileAccessRightRead; }
			set
			{
				selectedDesfireFileAccessRightRead = value;
				RaisePropertyChanged("SelectedDesfireFileAccessRightRead");
			}
		}

		private TaskAccessRights selectedDesfireFileAccessRightRead;

		/// <summary>
		///
		/// </summary>
		public TaskAccessRights SelectedDesfireFileAccessRightWrite
		{
			get { return selectedDesfireFileAccessRightWrite; }
			set
			{
				selectedDesfireFileAccessRightWrite = value;
				RaisePropertyChanged("SelectedDesfireFileAccessRightWrite");
			}
		}

		private TaskAccessRights selectedDesfireFileAccessRightWrite;

		/// <summary>
		///
		/// </summary>
		public EncryptionMode SelectedDesfireFileCryptoMode
		{
			get { return selectedDesfireFileCryptoMode; }
			set
			{
				selectedDesfireFileCryptoMode = value;
				RaisePropertyChanged("SelectedDesfireFileCryptoMode");
			}
		}

		private EncryptionMode selectedDesfireFileCryptoMode;

		/// <summary>
		///
		/// </summary>
		public FileType_MifareDesfireFileType SelectedDesfireFileType
		{
			get { return selectedDesfireFileType; }
			set
			{
				selectedDesfireFileType = value;
				RaisePropertyChanged("SelectedDesfireFileType");
			}
		}

		private FileType_MifareDesfireFileType selectedDesfireFileType;

		/// <summary>
		///
		/// </summary>
		public string FileNumberCurrent
		{
			get { return fileNumberCurrent; }
			set
			{
				fileNumberCurrent = value;
				IsValidFileNumberCurrent = (int.TryParse(value, out fileNumberCurrentAsInt) && fileNumberCurrentAsInt <= 8000);
				RaisePropertyChanged("FileNumberCurrent");
			}
		}

		private string fileNumberCurrent;

		/// <summary>
		///
		/// </summary>
		public int FileNumberCurrentAsInt
		{ get { return fileNumberCurrentAsInt; } }

		private int fileNumberCurrentAsInt;

		/// <summary>
		///
		/// </summary>
		public bool? IsValidFileNumberCurrent
		{
			get
			{
				return isValidFileNumberCurrent;
			}
			set
			{
				isValidFileNumberCurrent = value;
				RaisePropertyChanged("IsValidFileNumberCurrent");
			}
		}

		private bool? isValidFileNumberCurrent;

		/// <summary>
		///
		/// </summary>
		public string FileSizeCurrent
		{
			get { return fileSizeCurrent; }
			set
			{
				fileSizeCurrent = value;
				IsValidFileSizeCurrent = (int.TryParse(value, out fileSizeCurrentAsInt) && fileSizeCurrentAsInt <= 8000);
				RaisePropertyChanged("FileSizeCurrent");
			}
		}

		private string fileSizeCurrent;

		/// <summary>
		///
		/// </summary>
		public int FileSizeCurrentAsInt
		{ get { return fileSizeCurrentAsInt; } }

		private int fileSizeCurrentAsInt;

		/// <summary>
		///
		/// </summary>
		public bool? IsValidFileSizeCurrent
		{
			get
			{
				return isValidFileSizeCurrent;
			}
			set
			{
				isValidFileSizeCurrent = value;
				RaisePropertyChanged("IsValidFileSizeCurrent");
			}
		}

		private bool? isValidFileSizeCurrent;

		#endregion File Creation

		#endregion Key Properties File Master

		#region general props

		public SettingsReaderWriter Settings
		{
			get { return settings; }
		}

		//		public TreeViewParentNodeViewModel ParentNodeViewModel
		//		{
		//			get { return parentNodeViewModel; }
		//			set { parentNodeViewModel = value; }
		//		} private TreeViewParentNodeViewModel parentNodeViewModel;

		//		public ObservableCollection<TreeViewChildNodeViewModel> DataAsByteArray
		//		{
		//			get { return parentNodeViewModel != null ? parentNodeViewModel.Children : null; }
		//		}

		/// <summary>
		///
		/// </summary>
		public TaskType_MifareDesfireTask SelectedTaskType
		{
			get
			{
				//classicKeyAKeyCurrent = SectorTrailer.Split(',')[0];
				return selectedAccessBitsTaskType;
			}
			set
			{
				selectedAccessBitsTaskType = value;
				switch (value)
				{
					case TaskType_MifareDesfireTask.None:

						break;

					case TaskType_MifareDesfireTask.ReadData:

						break;

					case TaskType_MifareDesfireTask.WriteData:

						break;

					case TaskType_MifareDesfireTask.FormatDesfireCard:

						break;

					case TaskType_MifareDesfireTask.ChangeDefault:

						break;
				}
				RaisePropertyChanged("SelectedTaskType");
			}
		}

		private TaskType_MifareDesfireTask selectedAccessBitsTaskType;

		/// <summary>
		///
		/// </summary>
		public string SelectedTaskIndex
		{
			get
			{
				//classicKeyAKeyCurrent = SectorTrailer.Split(',')[0];
				return selectedAccessBitsTaskIndex;
			}
			set
			{
				selectedAccessBitsTaskIndex = value;
				IsValidSelectedTaskIndex = int.TryParse(value, out selectedTaskIndexAsInt);
			}
		}

		private string selectedAccessBitsTaskIndex;

		/// <summary>
		///
		/// </summary>
		public int SelectedTaskIndexAsInt
		{ get { return selectedTaskIndexAsInt; } }

		private int selectedTaskIndexAsInt;

		/// <summary>
		///
		/// </summary>
		public bool? IsValidSelectedTaskIndex
		{
			get
			{
				return isValidSelectedTaskIndex;
			}
			set
			{
				isValidSelectedTaskIndex = value;
				RaisePropertyChanged("IsValidSelectedTaskIndex");
			}
		}

		private bool? isValidSelectedTaskIndex;

		/// <summary>
		///
		/// </summary>
		public string SelectedTaskDescription
		{
			get
			{
				//classicKeyAKeyCurrent = SectorTrailer.Split(',')[0];
				return selectedAccessBitsTaskDescription;
			}
			set
			{
				selectedAccessBitsTaskDescription = value;
			}
		}

		private string selectedAccessBitsTaskDescription;

		/// <summary>
		///
		/// </summary>
		public bool? IsTaskCompletedSuccessfully
		{
			get { return isTaskCompletedSuccessfully; }
			set
			{
				isTaskCompletedSuccessfully = value;
				RaisePropertyChanged("IsTaskCompletedSuccessfully");
			}
		}

		private bool? isTaskCompletedSuccessfully;

		/// <summary>
		///
		/// </summary>
		public bool IsValidSelectedAccessBitsTaskIndex
		{
			get
			{
				//classicKeyAKeyCurrent = SectorTrailer.Split(',')[0];
				return isValidSelectedAccessBitsTaskIndex;
			}
			set
			{
				isValidSelectedAccessBitsTaskIndex = value;
				RaisePropertyChanged("IsValidSelectedAccessBitsTaskIndex");
			}
		}

		private bool isValidSelectedAccessBitsTaskIndex;

		/// <summary>
		///
		/// </summary>
		[XmlIgnore]
		public string StatusText
		{
			get { return statusText; }
			set
			{
				statusText = value;
				RaisePropertyChanged("StatusText");
			}
		}

		private string statusText;

		#endregion general props

		#region Commands

		/// <summary>
		/// return new RelayCommand<RFiDDevice>((_device) => OnNewCreateAppCommand(_device));
		/// </summary>
		public ICommand CreateAppCommand { get { return new RelayCommand(OnNewCreateAppCommand); } }

		private void OnNewCreateAppCommand()
		{
			TaskErr = ERROR.Empty;

			Task desfireTask =
				new Task(() =>
				         {
				         	using (RFiDDevice device = RFiDDevice.Instance)
				         	{
				         		if (device != null)
				         		{
				         			StatusText = string.Format("{0}: Connection to Reader successfully established\n", DateTime.Now);

				         			//Mouse.OverrideCursor = Cursors.Wait;

				         			if (CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireAppKeyCurrent) == KEY_ERROR.NO_ERROR)
				         			{
				         				if (IsValidAppNumberNew != false &&
				         				    device.AuthToMifareDesfireApplication(
				         				    	DesfireMasterKeyCurrent,
				         				    	SelectedDesfireMasterKeyEncryptionTypeCurrent,
				         				    	MifareDesfireKeyNumber.MifareDesfireKey00) == ERROR.NoError)
				         				{
				         					StatusText += string.Format("{0}: Successfully Authenticated to App 0\n", DateTime.Now);

				         					DESFireKeySettings keySettings = DESFireKeySettings.KS_ALLOW_CHANGE_MK;
				         					keySettings = (DESFireKeySettings)SelectedDesfireAppKeySettingsCreateNewApp;

				         					keySettings |= IsAllowChangeMKChecked ? (DESFireKeySettings)1 : (DESFireKeySettings)0;
				         					keySettings |= IsAllowListingWithoutMKChecked ? (DESFireKeySettings)2 : (DESFireKeySettings)0;
				         					keySettings |= IsAllowCreateDelWithoutMKChecked ? (DESFireKeySettings)4 : (DESFireKeySettings)0;
				         					keySettings |= IsAllowConfigChangableChecked ? (DESFireKeySettings)8 : (DESFireKeySettings)0;

				         					if (device.CreateMifareDesfireApplication(
				         						DesfireMasterKeyCurrent,
				         						keySettings,
				         						SelectedDesfireMasterKeyEncryptionTypeCurrent,
				         						SelectedDesfireAppKeyEncryptionTypeCreateNewApp,
				         						(int)SelectedDesfireAppMaxNumberOfKeys,
				         						AppNumberNewAsInt) == ERROR.NoError)
				         					{
				         						StatusText += string.Format("{0}: Successfully Created AppID {1}\n", DateTime.Now, AppNumberNewAsInt);
				         						TaskErr = ERROR.NoError;
				         						return;
				         					}
				         					else
				         					{
				         						StatusText += "Unable to Create App";
				         						TaskErr = ERROR.AuthenticationError;
				         						return;
				         					}
				         				}
				         				else
				         				{
				         					StatusText += "Unable to Auth";
				         					TaskErr = ERROR.AuthenticationError;
				         					return;
				         				}
				         			}

				         			//RaisePropertyChanged("DataAsByteArray");

				         			//Mouse.OverrideCursor = null;
				         		}
				         		else
				         		{
				         			TaskErr = ERROR.DeviceNotReadyError;
				         			return;
				         		}
				         	}
				         });

			if (TaskErr == ERROR.Empty)
			{
				TaskErr = ERROR.DeviceNotReadyError;

				desfireTask.ContinueWith((x) =>
				                         {
				                         	if (TaskErr == ERROR.NoError)
				                         	{
				                         		IsTaskCompletedSuccessfully = true;
				                         	}
				                         	else
				                         	{
				                         		IsTaskCompletedSuccessfully = false;
				                         	}
				                         });

				desfireTask.Start();
			}

			return;
		}

		/// <summary>
		/// return new RelayCommand<RFiDDevice>((_device) => OnNewCreateAppCommand(_device));
		/// </summary>
		public ICommand CreateFileCommand { get { return new RelayCommand(OnNewCreateFileCommand); } }

		private void OnNewCreateFileCommand()
		{
			TaskErr = ERROR.Empty;

			accessRights.changeAccess = SelectedDesfireFileAccessRightChange;
			accessRights.readAccess = SelectedDesfireFileAccessRightRead;
			accessRights.writeAccess = SelectedDesfireFileAccessRightWrite;
			accessRights.readAndWriteAccess = SelectedDesfireFileAccessRightReadWrite;

			Task desfireTask =
				new Task(() =>
				         {
				         	using (RFiDDevice device = RFiDDevice.Instance)
				         	{
				         		if (device != null)
				         		{
				         			StatusText = string.Format("{0}: Connection to Reader successfully established\n", DateTime.Now);

				         			//Mouse.OverrideCursor = Cursors.Wait;

				         			if (CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireAppKeyCurrent) == KEY_ERROR.NO_ERROR)
				         			{
				         				if (IsValidAppNumberNew != false &&
				         				    device.AuthToMifareDesfireApplication(
				         				    	DesfireMasterKeyCurrent,
				         				    	SelectedDesfireMasterKeyEncryptionTypeCurrent,
				         				    	MifareDesfireKeyNumber.MifareDesfireKey00) == ERROR.NoError)
				         				{
				         					StatusText += string.Format("{0}: Successfully Authenticated to App 0\n", DateTime.Now);

				         					if (device.CreateMifareDesfireFile(DesfireAppKeyCurrent, SelectedDesfireAppKeyEncryptionTypeCurrent, SelectedDesfireFileType,
				         					                                   accessRights, SelectedDesfireFileCryptoMode, AppNumberNewAsInt, FileNumberCurrentAsInt, FileSizeCurrentAsInt) == ERROR.NoError)
				         					{
				         						StatusText += string.Format("{0}: Successfully Created FileNo: {1} with Size: {2} in AppID: {3}\n", DateTime.Now, FileNumberCurrentAsInt, FileSizeCurrentAsInt, AppNumberNewAsInt);
				         						TaskErr = ERROR.NoError;
				         						return;
				         					}
				         					else
				         					{
				         						StatusText += "Unable to Create File";
				         						TaskErr = ERROR.AuthenticationError;
				         						return;
				         					}
				         				}
				         				else
				         				{
				         					StatusText += "Unable to Auth";
				         					TaskErr = ERROR.AuthenticationError;
				         					return;
				         				}
				         			}

				         			//RaisePropertyChanged("DataAsByteArray");

				         			//Mouse.OverrideCursor = null;
				         		}
				         		else
				         		{
				         			TaskErr = ERROR.DeviceNotReadyError;
				         			return;
				         		}
				         	}
				         });

			if (TaskErr == ERROR.Empty)
			{
				TaskErr = ERROR.DeviceNotReadyError;

				desfireTask.ContinueWith((x) =>
				                         {
				                         	if (TaskErr == ERROR.NoError)
				                         	{
				                         		IsTaskCompletedSuccessfully = true;
				                         	}
				                         	else
				                         	{
				                         		IsTaskCompletedSuccessfully = false;
				                         	}
				                         });

				desfireTask.Start();
			}

			return;
		}

		/// <summary>
		///
		/// </summary>
		public ICommand ChangeAppKeyCommand { get { return new RelayCommand(OnNewChangeAppKeyCommand); } }

		private void OnNewChangeAppKeyCommand()
		{
			TaskErr = ERROR.Empty;

			Task desfireTask = new Task(() =>
			                            {
			                            	using (RFiDDevice device = RFiDDevice.Instance)
			                            	{
			                            		if (device != null)
			                            		{
			                            			StatusText = string.Format("{0}: Connection to Reader successfully established\n", DateTime.Now);

			                            			//Mouse.OverrideCursor = Cursors.Wait;

			                            			if (CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireAppKeyCurrent) == KEY_ERROR.NO_ERROR)
			                            			{
			                            				if (IsValidAppNumberCurrent != false &&
			                            				    IsValidAppNumberTarget != false &&
			                            				    IsValidDesfireAppKeyTarget != false &&
			                            				    device.AuthToMifareDesfireApplication(
			                            				    	DesfireAppKeyCurrent,
			                            				    	SelectedDesfireAppKeyEncryptionTypeCurrent,
			                            				    	SelectedDesfireAppKeyNumberCurrent,
			                            				    	AppNumberCurrentAsInt) == ERROR.NoError)
			                            				{
			                            					StatusText += string.Format("{0}: Successfully Authenticated to AppID {1}\n", DateTime.Now, AppNumberCurrentAsInt);

			                            					if (device.ChangeMifareDesfireApplicationKey(DesfireAppKeyCurrent,
			                            					                                             (int)SelectedDesfireAppKeyNumberCurrent,
			                            					                                             SelectedDesfireAppKeyEncryptionTypeCurrent,
			                            					                                             DesfireAppKeyTarget,
			                            					                                             (int)SelectedDesfireAppKeyNumberTarget,
			                            					                                             SelectedDesfireAppKeyEncryptionTypeTarget,
			                            					                                             AppNumberCurrentAsInt, AppNumberTargetAsInt) == ERROR.NoError)
			                            					{
			                            						StatusText += string.Format("{0}: Successfully Changed Key {1} of AppID {2}\n", DateTime.Now, SelectedDesfireAppKeyNumberCurrent, AppNumberTargetAsInt);
			                            						TaskErr = ERROR.NoError;
			                            						return;
			                            					}
			                            					else
			                            					{
			                            						StatusText += string.Format("{0}: Unable to Change Key {1} of AppID {2}\n", DateTime.Now, SelectedDesfireAppKeyNumberCurrent, AppNumberTargetAsInt);
			                            						TaskErr = ERROR.AuthenticationError;
			                            						return;
			                            					}
			                            				}
			                            				else
			                            				{
			                            					StatusText += "Unable to Auth";
			                            					TaskErr = ERROR.AuthenticationError;
			                            					return;
			                            				}
			                            			}

			                            			//RaisePropertyChanged("DataAsByteArray");

			                            			//Mouse.OverrideCursor = null;
			                            		}
			                            		else
			                            		{
			                            			TaskErr = ERROR.DeviceNotReadyError;
			                            			return;
			                            		}
			                            	}
			                            });

			if (TaskErr == ERROR.Empty)
			{
				TaskErr = ERROR.DeviceNotReadyError;

				desfireTask.ContinueWith((x) =>
				                         {
				                         	if (TaskErr == ERROR.NoError)
				                         	{
				                         		IsTaskCompletedSuccessfully = true;
				                         	}
				                         	else
				                         	{
				                         		IsTaskCompletedSuccessfully = false;
				                         	}
				                         });

				desfireTask.Start();
			}

			return;
		}

		/// <summary>
		///
		/// </summary>
		public ICommand DeleteSignleCardApplicationCommand { get { return new RelayCommand(OnNewDeleteSignleCardApplicationCommand); } }

		private void OnNewDeleteSignleCardApplicationCommand()
		{
			TaskErr = ERROR.Empty;

			Task desfireTask = new Task(() =>
			                            {
			                            	using (RFiDDevice device = RFiDDevice.Instance)
			                            	{
			                            		if (device != null)
			                            		{
			                            			StatusText = string.Format("{0}: Connection to Reader successfully established\n", DateTime.Now);

			                            			//Mouse.OverrideCursor = Cursors.Wait;

			                            			if (CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireAppKeyCurrent) == KEY_ERROR.NO_ERROR)
			                            			{
			                            				if (IsValidAppNumberCurrent != false &&
			                            				    device.AuthToMifareDesfireApplication(
			                            				    	DesfireMasterKeyCurrent,
			                            				    	SelectedDesfireMasterKeyEncryptionTypeCurrent,
			                            				    	MifareDesfireKeyNumber.MifareDesfireKey00) == ERROR.NoError)
			                            				{
			                            					StatusText += string.Format("{0}: Successfully Authenticated to PICC Master App 0\n", DateTime.Now);

			                            					if (device.DeleteMifareDesfireApplication(
			                            						DesfireMasterKeyCurrent,
			                            						SelectedDesfireMasterKeyEncryptionTypeCurrent,
			                            						AppNumberNewAsInt) == ERROR.NoError)
			                            					{
			                            						StatusText += string.Format("{0}: Successfully Deleted AppID {1}\n", DateTime.Now, AppNumberNewAsInt);
			                            						TaskErr = ERROR.NoError;
			                            						return;
			                            					}
			                            					else
			                            					{
			                            						StatusText += string.Format("{0}: Unable to Remove AppID {1}\n", DateTime.Now, AppNumberNewAsInt);
			                            						TaskErr = ERROR.AuthenticationError;
			                            						return;
			                            					}
			                            				}
			                            				else
			                            				{
			                            					StatusText += "Unable to Auth";
			                            					TaskErr = ERROR.AuthenticationError;
			                            				}
			                            			}

			                            			//RaisePropertyChanged("DataAsByteArray");

			                            			//Mouse.OverrideCursor = null;
			                            		}
			                            		else
			                            		{
			                            			TaskErr = ERROR.DeviceNotReadyError;
			                            			return;
			                            		}
			                            	}
			                            });

			if (TaskErr == ERROR.Empty)
			{
				TaskErr = ERROR.DeviceNotReadyError;

				desfireTask.ContinueWith((x) =>
				                         {
				                         	if (TaskErr == ERROR.NoError)
				                         	{
				                         		IsTaskCompletedSuccessfully = true;
				                         	}
				                         	else
				                         	{
				                         		IsTaskCompletedSuccessfully = false;
				                         	}
				                         });

				desfireTask.Start();
			}

			return;
		}

		/// <summary>
		/// public ICommand FormatDesfireCardCommand { get { return new RelayCommand<RFiDDevice>((_device) => OnNewFormatDesfireCardCommand(_device)); }}
		/// </summary>
		public ICommand FormatDesfireCardCommand { get { return new RelayCommand(OnNewFormatDesfireCardCommand); } }

		private void OnNewFormatDesfireCardCommand()
		{
			TaskErr = ERROR.Empty;

			Task desfireTask = new Task(() =>
			                            {
			                            	using (RFiDDevice device = RFiDDevice.Instance) //?? _device
			                            	{
			                            		if (device != null && device.GetMiFareDESFireChipAppIDs() == ERROR.NoError)
			                            		{
			                            			StatusText = string.Format("{0}: Connection to Reader successfully established\n", DateTime.Now);

			                            			//Mouse.OverrideCursor = Cursors.Wait;
			                            			//Thread.Sleep(10000);
			                            			if (CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireAppKeyCurrent) == KEY_ERROR.NO_ERROR)
			                            			{
			                            				if (IsValidAppNumberCurrent != false &&
			                            				    device.AuthToMifareDesfireApplication(
			                            				    	DesfireMasterKeyCurrent,
			                            				    	SelectedDesfireMasterKeyEncryptionTypeCurrent,
			                            				    	MifareDesfireKeyNumber.MifareDesfireKey00) == ERROR.NoError)
			                            				{
			                            					StatusText += string.Format("{0}: Successfully Authenticated to PICC Master App 0\n", DateTime.Now);

			                            					foreach (uint appID in device.AppIDList)
			                            					{
			                            						StatusText += string.Format("{0}: FoundAppID {1}\n", DateTime.Now, appID);

			                            						if (device.DeleteMifareDesfireApplication(
			                            							DesfireMasterKeyCurrent,
			                            							SelectedDesfireMasterKeyEncryptionTypeCurrent,
			                            							(int)appID) == ERROR.NoError)
			                            						{
			                            							StatusText += string.Format("{0}: Successfully Deleted AppID {1}\n", DateTime.Now, appID);
			                            						}
			                            						else
			                            						{
			                            							StatusText += string.Format("{0}: Unable to Remove AppID {1}\n", DateTime.Now, appID);
			                            							TaskErr = ERROR.AuthenticationError;
			                            							return;
			                            						}
			                            					}

			                            					TaskErr = device.FormatDesfireCard(DesfireMasterKeyCurrent, SelectedDesfireMasterKeyEncryptionTypeCurrent);
			                            				}
			                            				else
			                            				{
			                            					StatusText += "Unable to Auth";
			                            					TaskErr = ERROR.AuthenticationError;
			                            					return;
			                            				}
			                            			}

			                            			//RaisePropertyChanged("DataAsByteArray");

			                            			//Mouse.OverrideCursor = null;
			                            		}
			                            		else
			                            		{
			                            			TaskErr = ERROR.DeviceNotReadyError;
			                            			return;
			                            		}
			                            	}
			                            	return;
			                            });

			if (TaskErr == ERROR.Empty)
			{
				TaskErr = ERROR.DeviceNotReadyError;

				desfireTask.ContinueWith((x) =>
				                         {
				                         	if (TaskErr == ERROR.NoError)
				                         	{
				                         		IsTaskCompletedSuccessfully = true;
				                         	}
				                         	else
				                         	{
				                         		IsTaskCompletedSuccessfully = false;
				                         	}
				                         });

				desfireTask.Start();
			}

			return;
		}

		/// <summary>
		///
		/// </summary>
		public ICommand AuthenticateToCardApplicationCommand { get { return new RelayCommand(OnNewAuthenticateToCardApplicationCommand); } }

		private void OnNewAuthenticateToCardApplicationCommand()
		{
			using (RFiDDevice device = RFiDDevice.Instance)
			{
				if (device != null)
				{
					StatusText = string.Format("{0}: Connection to Reader successfully established\n", DateTime.Now);

					Mouse.OverrideCursor = Cursors.Wait;

					if (CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireAppKeyCurrent) == KEY_ERROR.NO_ERROR)
					{
						if (IsValidAppNumberCurrent != false &&
						    device.AuthToMifareDesfireApplication(
						    	DesfireAppKeyCurrent,
						    	SelectedDesfireAppKeyEncryptionTypeCurrent,
						    	SelectedDesfireAppKeyNumberCurrent,
						    	AppNumberCurrentAsInt) == ERROR.NoError)
						{
							StatusText += string.Format("{0}: Successfully Authenticated to App {1}\n", DateTime.Now, AppNumberCurrentAsInt);
						}
						else
							StatusText += "Unable to Auth";
					}

					//RaisePropertyChanged("DataAsByteArray");

					Mouse.OverrideCursor = null;
				}
			}
		}

		/// <summary>
		///
		/// </summary>
		public ICommand ChangeMasterCardKeyCommand { get { return new RelayCommand(OnNewChangeMasterCardKeyCommand); } }

		private void OnNewChangeMasterCardKeyCommand()
		{
			TaskErr = ERROR.Empty;

			Task desfireTask = new Task(
				() =>
				{
					using (RFiDDevice device = RFiDDevice.Instance)
					{
						if (device != null)
						{
							StatusText = string.Format("{0}: Connection to Reader successfully established\n", DateTime.Now);

							//Mouse.OverrideCursor = Cursors.Wait;

							if (CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireMasterKeyCurrent) == KEY_ERROR.NO_ERROR)
							{
								if (device.AuthToMifareDesfireApplication(
									CustomConverter.desFireKeyToEdit,
									SelectedDesfireMasterKeyEncryptionTypeCurrent,
									MifareDesfireKeyNumber.MifareDesfireKey00) == ERROR.NoError)
								{
									StatusText += string.Format("{0}: Successfully Authenticated to App 0\n", DateTime.Now);

									if (IsValidDesfireMasterKeyCurrent != false &&
									    IsValidDesfireMasterKeyTarget != false &&
									    IsValidDesfireMasterKeyTargetRetyped != false &&
									    DesfireMasterKeyTarget == DesfireMasterKeyTargetRetyped && (IsValidDesfireMasterKeyTarget != false))
									{
										if (device.ChangeMifareDesfireApplicationKey(
											DesfireMasterKeyCurrent,
											0,
											SelectedDesfireMasterKeyEncryptionTypeCurrent,
											DesfireMasterKeyTarget,
											(int)SelectedDesfireAppKeyNumberTarget,
											SelectedDesfireMasterKeyEncryptionTypeTarget) == ERROR.NoError)
										{
											StatusText += string.Format("{0}: Keychange Successful\n", DateTime.Now);
											TaskErr = ERROR.NoError;
											return;
										}
										else
										{
											StatusText += string.Format("{0}: Unable to Change Key\n", DateTime.Now);
											TaskErr = ERROR.AuthenticationError;
											return;
										}
									}
									else
									{
										StatusText += string.Format("{0}: Key Error: Wrong Format\n", DateTime.Now);
									}
								}
								else
								{
									StatusText += "Unable to Auth";
									TaskErr = ERROR.AuthenticationError;
									return;
								}
							}
							//RaisePropertyChanged("DataAsByteArray");
							//Mouse.OverrideCursor = null;
						}
						else
						{
							TaskErr = ERROR.DeviceNotReadyError;
							return;
						}
					}
				});

			if (TaskErr == ERROR.Empty)
			{
				TaskErr = ERROR.DeviceNotReadyError;

				desfireTask.ContinueWith((x) =>
				                         {
				                         	if (TaskErr == ERROR.NoError)
				                         	{
				                         		IsTaskCompletedSuccessfully = true;
				                         	}
				                         	else
				                         	{
				                         		IsTaskCompletedSuccessfully = false;
				                         	}
				                         });

				desfireTask.Start();
			}

			return;
		}

		/// <summary>
		///
		/// </summary>
		public ICommand AuthenticateToCardMasterApplicationCommand { get { return new RelayCommand(OnNewAuthenticateToCardMasterApplicationCommand); } }

		private void OnNewAuthenticateToCardMasterApplicationCommand()
		{
			using (RFiDDevice device = RFiDDevice.Instance)
			{
				if (device != null)
				{
					StatusText = string.Format("{0}: Connection to Reader successfully established\n", DateTime.Now);

					Mouse.OverrideCursor = Cursors.Wait;

					if (CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireMasterKeyCurrent) == KEY_ERROR.NO_ERROR)
					{
						if (device.AuthToMifareDesfireApplication(
							DesfireMasterKeyCurrent,
							SelectedDesfireMasterKeyEncryptionTypeCurrent,
							MifareDesfireKeyNumber.MifareDesfireKey00) == ERROR.NoError)
						{
							StatusText += string.Format("{0}: Successfully Authenticated to App 0\n", DateTime.Now);
						}
						else
							StatusText += "Unable to Auth";
					}

					//RaisePropertyChanged("DataAsByteArray");

					Mouse.OverrideCursor = null;
				}
			}
		}

		#endregion Commands

		#region IUserDialogViewModel Implementation

		[XmlIgnore]
		public bool IsModal { get; private set; }

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

		public ICommand CancelCommand { get { return new RelayCommand(Cancel); } }

		protected virtual void Cancel()
		{
			if (this.OnCancel != null)
				this.OnCancel(this);
			else
				Close();
		}

		//		public ICommand CancelCommand { get { return new RelayCommand(Auth); } }
		//		protected virtual void Auth()
		//		{
		//			if (this.OnAuth != null)
		//				this.OnAuth(this);
		//			else
		//				Close();
		//		}

		[XmlIgnore]
		public Action<MifareDesfireSetupViewModel> OnOk { get; set; }

		[XmlIgnore]
		public Action<MifareDesfireSetupViewModel> OnCancel { get; set; }

		//		public Action<MifareDesfireSetupViewModel> OnAuth { get; set; }
		[XmlIgnore]
		public Action<MifareDesfireSetupViewModel> OnCloseRequest { get; set; }

		public void Close()
		{
			if (this.DialogClosing != null)
				this.DialogClosing(this, new EventArgs());
		}

		public void Show(IList<IDialogViewModel> collection)
		{
			collection.Add(this);
		}

		#endregion IUserDialogViewModel Implementation

		#region Localization

		private string _Caption;

		public string Caption
		{
			get { return _Caption; }
			set
			{
				_Caption = value;
				RaisePropertyChanged("Caption");
			}
		}

		#endregion Localization
	}
}