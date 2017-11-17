/*
 * Created by SharpDevelop.
 * Date: 10/11/2017
 * Time: 22:15
 * 
 */
using LibLogicalAccess;

using RFiDGear.DataAccessLayer;
using RFiDGear.Model;

using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

using MvvmDialogs.ViewModels;

using System;
using System.Security;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Threading.Tasks;
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
		private MifareDesfireAppIdTreeViewModel app;
		
		public ERROR taskErr { get; set; }
		
		/// <summary>
		/// 
		/// </summary>
		public MifareDesfireSetupViewModel()
		{
			
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="_parentNodeViewModel"></param>
		/// <param name="_dialogs"></param>
		public MifareDesfireSetupViewModel(object _parentNodeViewModel, ObservableCollection<IDialogViewModel> _dialogs)
		{
			
			//.Where(x => x.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardCardMasterKey)
			//SelectedDesfireMasterKeyEncryptionTypeCurrent = DESFireKeyType.DF_KEY_DES;
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
			
			
			if(_parentNodeViewModel is TreeViewParentNodeViewModel && (_parentNodeViewModel as TreeViewParentNodeViewModel).IsTask)
			{
				chip = new MifareDesfireChipModel(string.Format("Task Description: {0}",SelectedTaskDescription), CARD_TYPE.Unspecified);
				app = new MifareDesfireAppIdTreeViewModel(0);
				
				//TreeViewChildNodeViewModel taskChild = new TreeViewChildNodeViewModel(appID: appModel, parent: _parentNodeViewModel,cardType: CARD_TYPE.Unspecified, _dialogs: _dialogs, _isTask: true);
				parentNodeViewModel = new TreeViewParentNodeViewModel(chip, _dialogs, true);
				
				
			}
			else
			{
				MifareDesfireChipModel chip = new MifareDesfireChipModel(string.Format("Task Description: {0}",SelectedTaskDescription), CARD_TYPE.DESFire);
				parentNodeViewModel = new TreeViewParentNodeViewModel(chip,_dialogs,true);
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
		} private DESFireKeyType selectedDesfireMasterKeyEncryptionTypeCurrent;
		
		public string DesfireMasterKeyCurrent
		{
			get { return desfireMasterKeyCurrent; }
			set
			{
				try{
					desfireMasterKeyCurrent = value.ToUpper().Remove(32);
				}
				catch{
					desfireMasterKeyCurrent = value.ToUpper();
				}
				IsValidDesfireMasterKeyCurrent = (CustomConverter.IsInHexFormat(desfireMasterKeyCurrent) && desfireMasterKeyCurrent.Length == 32);
				RaisePropertyChanged("DesfireMasterKeyCurrent");
			}
		} private string desfireMasterKeyCurrent;
		
		/// <summary>
		/// 
		/// </summary>
		public bool? IsValidDesfireMasterKeyCurrent {
			get
			{
				return isValidDesfireMasterKeyCurrent;
			}
			set
			{
				isValidDesfireMasterKeyCurrent = value;
				RaisePropertyChanged("IsValidDesfireMasterKeyCurrent");
			}
		} private bool? isValidDesfireMasterKeyCurrent;
		

		public DESFireKeyType SelectedDesfireMasterKeyEncryptionTypeTarget
		{
			get { return selectedDesfireMasterKeyEncryptionTypeTarget; }
			set
			{
				selectedDesfireMasterKeyEncryptionTypeTarget = value;
				RaisePropertyChanged("SelectedDesfireMasterKeyEncryptionTypeCurrent");
			}
		} private DESFireKeyType selectedDesfireMasterKeyEncryptionTypeTarget;
		
		/// <summary>
		/// 
		/// </summary>
		public string DesfireMasterKeyTarget
		{
			get { return desfireMasterKeyTarget; }
			set
			{
				try{
					desfireMasterKeyTarget = value.ToUpper().Remove(32);
				}
				catch{
					desfireMasterKeyTarget = value.ToUpper();
				}
				
				IsValidDesfireMasterKeyTarget = (
					CustomConverter.IsInHexFormat(desfireMasterKeyTarget) &&
					desfireMasterKeyTarget.Length == 32);
				IsValidDesfireMasterKeyTargetRetyped = (desfireMasterKeyTarget == desfireMasterKeyTargetRetyped);
				RaisePropertyChanged("DesfireMasterKeyTarget");
			}
		} private string desfireMasterKeyTarget;
		
		/// <summary>
		/// 
		/// </summary>
		public string DesfireMasterKeyTargetRetyped
		{
			get { return desfireMasterKeyTargetRetyped; }
			set
			{
				try{
					desfireMasterKeyTargetRetyped = value.ToUpper().Remove(32);
				}
				catch{
					desfireMasterKeyTargetRetyped = value.ToUpper();
				}
				
				IsValidDesfireMasterKeyTargetRetyped = ((
					CustomConverter.IsInHexFormat(desfireMasterKeyTargetRetyped) &&
					desfireMasterKeyTargetRetyped.Length == 32) &&
				                                        (IsValidDesfireMasterKeyTarget == true) &&
				                                        desfireMasterKeyTarget.ToUpper() == desfireMasterKeyTargetRetyped.ToUpper());
				RaisePropertyChanged("DesfireMasterKeyTargetRetyped");
			}
		} private string desfireMasterKeyTargetRetyped;
		
		/// <summary>
		/// 
		/// </summary>
		public bool? IsValidDesfireMasterKeyTarget {
			get
			{
				return isValidDesfireMasterKeyTarget;
			}
			set
			{
				isValidDesfireMasterKeyTarget = value;
				RaisePropertyChanged("IsValidDesfireMasterKeyTarget");
			}
		} private bool? isValidDesfireMasterKeyTarget;
		
		/// <summary>
		/// 
		/// </summary>
		public bool? IsValidDesfireMasterKeyTargetRetyped {
			get
			{
				return isValidDesfireMasterKeyTargetRetyped;
			}
			set
			{
				isValidDesfireMasterKeyTargetRetyped = value;
				RaisePropertyChanged("IsValidDesfireMasterKeyTargetRetyped");
			}
		} private bool? isValidDesfireMasterKeyTargetRetyped;
		
		#endregion
		
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
		} private DESFireKeyType selectedCreateDesfireAppKeyEncryptionTypeCurrent;
		
		/// <summary>
		/// 
		/// </summary>
		public MifareDesfireKeyNumber SelectedDesfireAppMaxNumberOfKeys
		{
			get { return selectedDesfireAppMaxNumberOfKeys; }
			set {
				selectedDesfireAppMaxNumberOfKeys = value;
				RaisePropertyChanged("SelectedDesfireAppMaxNumberOfKeys");
			}
		} private MifareDesfireKeyNumber selectedDesfireAppMaxNumberOfKeys;

		/// <summary>
		/// 
		/// </summary>
		public DESFireKeySettings SelectedDesfireAppKeySettingsCreateNewApp
		{
			get { return selectedDesfireAppKeySettingsTarget; }
			set {
				selectedDesfireAppKeySettingsTarget = value;
				RaisePropertyChanged("SelectedDesfireAppKeySettingsCreateNewApp");
			}
		} private DESFireKeySettings selectedDesfireAppKeySettingsTarget;
		
		/// <summary>
		/// 
		/// </summary>
		public string AppNumberNew
		{
			get { return appNumberNew; }
			set
			{
				try{
					appNumberNew = value.ToUpper().Remove(32);
				}
				catch{
					appNumberNew = value.ToUpper();
				}
				IsValidAppNumberNew = (int.TryParse(value, out appNumberNewAsInt) && appNumberNewAsInt <= 65535);
				RaisePropertyChanged("AppNumberNew");
			}
		} private string appNumberNew;
		
		/// <summary>
		/// 
		/// </summary>
		public int AppNumberNewAsInt
		{ get { return appNumberNewAsInt;} } private int appNumberNewAsInt;
		
		/// <summary>
		/// 
		/// </summary>
		public bool? IsValidAppNumberNew {
			get
			{
				return isValidAppNumberNew;
			}
			set
			{
				isValidAppNumberNew = value;
				RaisePropertyChanged("IsValidAppNumberNew");
			}
		} private bool? isValidAppNumberNew;
		
		#endregion
		
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
		} private DESFireKeyType selectedDesfireAppKeyEncryptionTypeCurrent;
		
		/// <summary>
		/// 
		/// </summary>
		public MifareDesfireKeyNumber SelectedDesfireAppKeyNumberCurrent
		{
			get { return selectedDesfireAppKeyNumberCurrent; }
			set {
				selectedDesfireAppKeyNumberCurrent = value;
				RaisePropertyChanged("SelectedDesfireAppKeyNumberCurrent");
			}
		} private MifareDesfireKeyNumber selectedDesfireAppKeyNumberCurrent;
		
		/// <summary>
		/// 
		/// </summary>
		public string DesfireAppKeyCurrent
		{
			get { return desfireAppKeyCurrent; }
			set
			{
				try{
					desfireAppKeyCurrent = value.ToUpper().Remove(32);
				}
				catch{
					desfireAppKeyCurrent = value.ToUpper();
				}
				IsValidDesfireAppKeyCurrent = (CustomConverter.IsInHexFormat(desfireAppKeyCurrent) && desfireAppKeyCurrent.Length == 32);
				RaisePropertyChanged("DesfireAppKeyCurrent");
			}
		} private string desfireAppKeyCurrent;
		
		/// <summary>
		/// 
		/// </summary>
		public bool? IsValidDesfireAppKeyCurrent {
			get
			{
				return isValidDesfireAppKeyCurrent;
			}
			set
			{
				isValidDesfireAppKeyCurrent = value;
				RaisePropertyChanged("IsValidDesfireAppKeyCurrent");
			}
		} private bool? isValidDesfireAppKeyCurrent;
		
		/// <summary>
		/// 
		/// </summary>
		public string AppNumberCurrent
		{
			get { return appNumberCurrent; }
			set
			{
				try{
					appNumberCurrent = value.ToUpper().Remove(32);
				}
				catch{
					appNumberCurrent = value.ToUpper();
				}
				IsValidAppNumberCurrent = (int.TryParse(value, out appNumberCurrentAsInt) && appNumberCurrentAsInt <= 65535);
				RaisePropertyChanged("AppNumberCurrent");
			}
		} private string appNumberCurrent;
		
		/// <summary>
		/// 
		/// </summary>
		public int AppNumberCurrentAsInt
		{ get { return appNumberCurrentAsInt;} } private int appNumberCurrentAsInt;
		
		/// <summary>
		/// 
		/// </summary>
		public bool? IsValidAppNumberCurrent {
			get
			{
				return isValidAppNumberCurrent;
			}
			set
			{
				isValidAppNumberCurrent = value;
				RaisePropertyChanged("IsValidAppNumberCurrent");
			}
		} private bool? isValidAppNumberCurrent;
		
		#endregion
		
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
		} private DESFireKeyType selectedDesfireAppKeyEncryptionTypeTarget;
		
		/// <summary>
		/// 
		/// </summary>
		public MifareDesfireKeyNumber SelectedDesfireAppKeyNumberTarget
		{
			get { return selectedDesfireAppKeyNumberTarget; }
			set {
				selectedDesfireAppKeyNumberTarget = value;
				RaisePropertyChanged("SelectedDesfireAppKeyNumberTarget");
			}
		} private MifareDesfireKeyNumber selectedDesfireAppKeyNumberTarget;
		
		/// <summary>
		/// 
		/// </summary>
		public string DesfireAppKeyTarget
		{
			get { return desfireAppKeyTarget; }
			set
			{
				try{
					desfireAppKeyTarget = value.ToUpper().Remove(32);
				}
				catch{
					desfireAppKeyTarget = value.ToUpper();
				}
				
				IsValidDesfireAppKeyTarget = (
					CustomConverter.IsInHexFormat(desfireAppKeyTarget) &&
					desfireAppKeyTarget.Length == 32);
				IsValidDesfireAppKeyTargetRetyped = (desfireAppKeyTarget == desfireAppKeyTargetRetyped);
				RaisePropertyChanged("DesfireAppKeyTarget");
			}
		} private string desfireAppKeyTarget;
		
		/// <summary>
		/// 
		/// </summary>
		public string DesfireAppKeyTargetRetyped
		{
			get { return desfireAppKeyTargetRetyped; }
			set
			{
				try{
					desfireAppKeyTargetRetyped = value.ToUpper().Remove(32);
				}
				catch{
					desfireAppKeyTargetRetyped = value.ToUpper();
				}
				
				IsValidDesfireAppKeyTargetRetyped = ((
					CustomConverter.IsInHexFormat(desfireAppKeyTargetRetyped) &&
					desfireAppKeyTargetRetyped.Length == 32) &&
				                                     (IsValidDesfireAppKeyTarget == true) &&
				                                     desfireAppKeyTarget.ToUpper() == desfireAppKeyTargetRetyped.ToUpper());
				RaisePropertyChanged("DesfireAppKeyTargetRetyped");
			}
		} private string desfireAppKeyTargetRetyped;
		
		/// <summary>
		/// 
		/// </summary>
		public bool? IsValidDesfireAppKeyTarget {
			get
			{
				return isValidDesfireAppKeyTarget;
			}
			set
			{
				isValidDesfireAppKeyTarget = value;
				RaisePropertyChanged("IsValidDesfireAppKeyTarget");
			}
		} private bool? isValidDesfireAppKeyTarget;
		
		/// <summary>
		/// 
		/// </summary>
		public bool? IsValidDesfireAppKeyTargetRetyped {
			get
			{
				return isValidDesfireAppKeyTargetRetyped;
			}
			set
			{
				isValidDesfireAppKeyTargetRetyped = value;
				RaisePropertyChanged("IsValidDesfireAppKeyTargetRetyped");
			}
		} private bool? isValidDesfireAppKeyTargetRetyped;
		
		
		/// <summary>
		/// 
		/// </summary>
		public string AppNumberTarget
		{
			get { return appNumberTarget; }
			set
			{
				try{
					appNumberTarget = value.ToUpper().Remove(32);
				}
				catch{
					appNumberTarget = value.ToUpper();
				}
				IsValidAppNumberTarget = (int.TryParse(value, out appNumberTargetAsInt) && appNumberTargetAsInt <= 65535);
				RaisePropertyChanged("AppNumberTarget");
			}
		} private string appNumberTarget;
		
		/// <summary>
		/// 
		/// </summary>
		public int AppNumberTargetAsInt
		{ get { return appNumberTargetAsInt;} } private int appNumberTargetAsInt;
		
		/// <summary>
		/// 
		/// </summary>
		public bool? IsValidAppNumberTarget {
			get
			{
				return isValidAppNumberTarget;
			}
			set
			{
				isValidAppNumberTarget = value;
				RaisePropertyChanged("IsValidAppNumberTarget");
			}
		} private bool? isValidAppNumberTarget;
		
		#endregion
		
		#endregion
		
		#region general props

		public SettingsReaderWriter Settings
		{
			get { return settings; }
		}
		
		public TreeViewParentNodeViewModel ParentNodeViewModel
		{
			get { return parentNodeViewModel; }
			set { parentNodeViewModel = value; }
		} private TreeViewParentNodeViewModel parentNodeViewModel;
		
		public ObservableCollection<TreeViewChildNodeViewModel> DataAsByteArray
		{
			get { return parentNodeViewModel != null ? parentNodeViewModel.Children : null; }
		}
		
		/// <summary>
		/// 
		/// </summary>
		public TaskType_MifareDesfireTask SelectedTaskType {
			get
			{
				//classicKeyAKeyCurrent = SectorTrailer.Split(',')[0];
				return selectedAccessBitsTaskType;
			}
			set
			{
				selectedAccessBitsTaskType = value;
				switch(value)
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
		} private TaskType_MifareDesfireTask selectedAccessBitsTaskType;
		
		/// <summary>
		/// 
		/// </summary>
		public string SelectedTaskIndex {
			get
			{
				//classicKeyAKeyCurrent = SectorTrailer.Split(',')[0];
				return selectedAccessBitsTaskIndex;
			}
			set
			{
				selectedAccessBitsTaskIndex = value;
				try{
					parentNodeViewModel.TaskIndex = int.Parse(value);
				}
				catch{
					
				}
			}
		} private string selectedAccessBitsTaskIndex;
		
		/// <summary>
		/// 
		/// </summary>
		public string SelectedTaskDescription {
			get
			{
				//classicKeyAKeyCurrent = SectorTrailer.Split(',')[0];
				return selectedAccessBitsTaskDescription;
			}
			set
			{
				selectedAccessBitsTaskDescription = value;
			}
		} private string selectedAccessBitsTaskDescription;
		
		/// <summary>
		/// 
		/// </summary>
		public bool? IsTaskCompletedSuccessfully
		{
			get { return isTaskCompletedSuccessfully; }
			set{
				isTaskCompletedSuccessfully = value;
				RaisePropertyChanged("IsTaskCompletedSuccessfully");
			}
		} private bool? isTaskCompletedSuccessfully;
		
		/// <summary>
		/// 
		/// </summary>
		public bool IsValidSelectedAccessBitsTaskIndex {
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
		} private bool isValidSelectedAccessBitsTaskIndex;
		
		public string StatusText
		{
			get { return statusText; }
			set {
				statusText = value;
				RaisePropertyChanged("StatusText");
			}
		} private string statusText;
		
		#endregion
		
		
		#region Commands

		/// <summary>
		/// return new RelayCommand<RFiDDevice>((_device) => OnNewCreateAppCommand(_device));
		/// </summary>
		public ICommand CreateAppCommand { get { return new RelayCommand(OnNewCreateAppCommand);}}
		private void OnNewCreateAppCommand()
		{
			using ( RFiDDevice device = RFiDDevice.Instance)
			{

				if(device != null)
				{
					StatusText = string.Format("{0}: Connection to Reader successfully established\n", DateTime.Now);
					
					Mouse.OverrideCursor = Cursors.Wait;

					if(CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireAppKeyCurrent) == KEY_ERROR.NO_ERROR)
					{
						if(IsValidAppNumberNew != false &&
						   device.AuthToMifareDesfireApplication(
						   	DesfireMasterKeyCurrent,
						   	SelectedDesfireMasterKeyEncryptionTypeCurrent,
						   	MifareDesfireKeyNumber.MifareDesfireKey00) == ERROR.NoError)
						{
							
							StatusText += string.Format("{0}: Successfully Authenticated to App 0\n", DateTime.Now);
							
							if(device.CreateMifareDesfireApplication(
								DesfireMasterKeyCurrent,
								SelectedDesfireAppKeySettingsCreateNewApp,
								SelectedDesfireMasterKeyEncryptionTypeCurrent,
								SelectedDesfireAppKeyEncryptionTypeCreateNewApp,
								(int)SelectedDesfireAppMaxNumberOfKeys,
								AppNumberNewAsInt) == ERROR.NoError)
							{
								StatusText += string.Format("{0}: Successfully Created AppID {1}\n", DateTime.Now, AppNumberNewAsInt);
							}
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
		public ICommand ChangeAppKeyCommand { get { return new RelayCommand(OnNewChangeAppKeyCommand); }}
		private void OnNewChangeAppKeyCommand()
		{
			using ( RFiDDevice device = RFiDDevice.Instance)
			{

				if(device != null)
				{
					StatusText = string.Format("{0}: Connection to Reader successfully established\n", DateTime.Now);
					
					Mouse.OverrideCursor = Cursors.Wait;

					if(CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireAppKeyCurrent) == KEY_ERROR.NO_ERROR)
					{
						if(IsValidAppNumberCurrent != false &&
						   IsValidAppNumberTarget != false &&
						   IsValidDesfireAppKeyTarget != false &&
						   device.AuthToMifareDesfireApplication(
						   	DesfireAppKeyCurrent,
						   	SelectedDesfireAppKeyEncryptionTypeCurrent,
						   	SelectedDesfireAppKeyNumberCurrent,
						   	AppNumberCurrentAsInt) == ERROR.NoError)
						{
							StatusText += string.Format("{0}: Successfully Authenticated to AppID {1}\n", DateTime.Now, AppNumberCurrentAsInt);
							
							if(device.ChangeMifareDesfireApplicationKey(DesfireAppKeyCurrent,
							                                            (int)SelectedDesfireAppKeyNumberCurrent,
							                                            SelectedDesfireAppKeyEncryptionTypeCurrent,
							                                            DesfireAppKeyTarget,
							                                            SelectedDesfireAppKeyEncryptionTypeTarget,
							                                            AppNumberCurrentAsInt, AppNumberTargetAsInt) == ERROR.NoError)
							{
								StatusText += string.Format("{0}: Successfully Changed Key {1} of AppID {2}\n", DateTime.Now, SelectedDesfireAppKeyNumberCurrent, AppNumberCurrentAsInt);
							}
							else
							{
								StatusText += string.Format("{0}: Unable to Change Key {1} of AppID {2}\n", DateTime.Now, SelectedDesfireAppKeyNumberCurrent, AppNumberCurrentAsInt);
							}
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
		public ICommand DeleteSignleCardApplicationCommand { get { return new RelayCommand(OnNewDeleteSignleCardApplicationCommand); }}
		private void OnNewDeleteSignleCardApplicationCommand()
		{
			using ( RFiDDevice device = RFiDDevice.Instance)
			{

				if(device != null)
				{
					StatusText = string.Format("{0}: Connection to Reader successfully established\n", DateTime.Now);
					
					Mouse.OverrideCursor = Cursors.Wait;

					if(CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireAppKeyCurrent) == KEY_ERROR.NO_ERROR)
					{
						if(IsValidAppNumberCurrent != false &&
						   device.AuthToMifareDesfireApplication(
						   	DesfireMasterKeyCurrent,
						   	SelectedDesfireMasterKeyEncryptionTypeCurrent,
						   	MifareDesfireKeyNumber.MifareDesfireKey00) == ERROR.NoError)
						{
							StatusText += string.Format("{0}: Successfully Authenticated to PICC Master App 0\n", DateTime.Now);
							
							if(device.DeleteMifareDesfireApplication(
								DesfireMasterKeyCurrent,
								SelectedDesfireMasterKeyEncryptionTypeCurrent,
								AppNumberNewAsInt) == ERROR.NoError)
							{
								StatusText += string.Format("{0}: Successfully Deleted AppID {1}\n", DateTime.Now, AppNumberCurrentAsInt);
							}
							else
								StatusText += string.Format("{0}: Unable to Remove AppID {1}\n", DateTime.Now, AppNumberCurrentAsInt);
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
		/// public ICommand FormatDesfireCardCommand { get { return new RelayCommand<RFiDDevice>((_device) => OnNewFormatDesfireCardCommand(_device)); }}
		/// </summary>
		public ICommand FormatDesfireCardCommand { get { return new RelayCommand(OnNewFormatDesfireCardCommand); }}
		private void OnNewFormatDesfireCardCommand()
		{
			Task thread = new Task(() =>
			                       {
			                       	using ( RFiDDevice device = RFiDDevice.Instance) //?? _device
			                       	{
			                       		
			                       		if(device != null)
			                       		{
			                       			StatusText = string.Format("{0}: Connection to Reader successfully established\n", DateTime.Now);
			                       			
			                       			//Mouse.OverrideCursor = Cursors.Wait;
			                       			//Thread.Sleep(10000);
			                       			if(CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireAppKeyCurrent) == KEY_ERROR.NO_ERROR)
			                       			{
			                       				if(IsValidAppNumberCurrent != false &&
			                       				   device.AuthToMifareDesfireApplication(
			                       				   	DesfireMasterKeyCurrent,
			                       				   	SelectedDesfireMasterKeyEncryptionTypeCurrent,
			                       				   	MifareDesfireKeyNumber.MifareDesfireKey00) == ERROR.NoError)
			                       				{
			                       					StatusText += string.Format("{0}: Successfully Authenticated to PICC Master App 0\n", DateTime.Now);
			                       					
			                       					foreach(uint appID in device.GetAppIDList)
			                       					{
			                       						StatusText += string.Format("{0}: FoundAppID {1}\n", DateTime.Now, appID);
			                       						
			                       						if(device.DeleteMifareDesfireApplication(
			                       							DesfireMasterKeyCurrent,
			                       							SelectedDesfireMasterKeyEncryptionTypeCurrent,
			                       							(int)appID) == ERROR.NoError)
			                       						{
			                       							StatusText += string.Format("{0}: Successfully Deleted AppID {1}\n", DateTime.Now, appID);
			                       						}
			                       						else
			                       						{
			                       							StatusText += string.Format("{0}: Unable to Remove AppID {1}\n", DateTime.Now, appID);
			                       							taskErr = ERROR.AuthenticationError;
			                       							return;
			                       						}
			                       						
			                       					}

			                       					taskErr = device.FormatDesfireCard(DesfireMasterKeyCurrent,SelectedDesfireMasterKeyEncryptionTypeCurrent);
			                       				}

			                       				else
			                       				{
			                       					StatusText += "Unable to Auth";
			                       					taskErr = ERROR.AuthenticationError;
			                       					return;
			                       				}
			                       				
			                       			}

			                       			
			                       			//RaisePropertyChanged("DataAsByteArray");
			                       			
			                       			//Mouse.OverrideCursor = null;
			                       		}
			                       		else
			                       		{
			                       			taskErr = ERROR.DeviceNotReadyError;
			                       			return;
			                       		}
			                       	}
			                       	return;
			                       });
			
			
			
			if(taskErr == ERROR.Empty)
			{
				taskErr = ERROR.DeviceNotReadyError;
				
				thread.ContinueWith((x) => {
				                    	
				                    	if(taskErr == ERROR.NoError)
				                    	{
				                    		IsTaskCompletedSuccessfully = true;
				                    	}
				                    	else
				                    	{
				                    		IsTaskCompletedSuccessfully = false;
				                    	}
				                    });
				
				thread.Start();
			}
			

			
			
			return;
		}
		
		/// <summary>
		/// 
		/// </summary>
		public ICommand AuthenticateToCardApplicationCommand { get { return new RelayCommand(OnNewAuthenticateToCardApplicationCommand); }}
		private void OnNewAuthenticateToCardApplicationCommand()
		{
			using ( RFiDDevice device = RFiDDevice.Instance)
			{

				if(device != null)
				{
					StatusText = string.Format("{0}: Connection to Reader successfully established\n", DateTime.Now);
					
					Mouse.OverrideCursor = Cursors.Wait;

					if(CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireAppKeyCurrent) == KEY_ERROR.NO_ERROR)
					{
						if(IsValidAppNumberCurrent != false &&
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
		
		public ICommand ChangeMasterCardKeyCommand { get { return new RelayCommand(OnNewChangeMasterCardKeyCommand); }}
		private void OnNewChangeMasterCardKeyCommand()
		{
			using ( RFiDDevice device = RFiDDevice.Instance)
			{
				if(device != null)
				{
					StatusText = string.Format("{0}: Connection to Reader successfully established\n", DateTime.Now);
					
					Mouse.OverrideCursor = Cursors.Wait;

					if(CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireMasterKeyCurrent) == KEY_ERROR.NO_ERROR)
					{
						if(device.AuthToMifareDesfireApplication(
							CustomConverter.desFireKeyToEdit,
							SelectedDesfireMasterKeyEncryptionTypeCurrent,
							MifareDesfireKeyNumber.MifareDesfireKey00) == ERROR.NoError)
						{
							StatusText += string.Format("{0}: Successfully Authenticated to App 0\n", DateTime.Now);
							
							if(CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireMasterKeyCurrent) == KEY_ERROR.NO_ERROR &&
							   CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireMasterKeyTarget) == KEY_ERROR.NO_ERROR &&
							   CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireMasterKeyTargetRetyped) == KEY_ERROR.NO_ERROR &&
							   DesfireMasterKeyTarget == DesfireMasterKeyTargetRetyped && (IsValidDesfireMasterKeyTarget == true))
							{
								if(device.ChangeMifareDesfireApplicationKey(
									DesfireMasterKeyCurrent,
									0,
									SelectedDesfireMasterKeyEncryptionTypeCurrent,
									DesfireMasterKeyTarget,
									SelectedDesfireMasterKeyEncryptionTypeTarget) == ERROR.NoError)
								{
									StatusText += string.Format("{0}: Keychange Successful\n", DateTime.Now);
								}
								else
								{
									StatusText += string.Format("{0}: Unable to Change Key\n", DateTime.Now);
								}
							}
							
							else
							{
								StatusText += string.Format("{0}: Key Error: Wrong Format\n", DateTime.Now);
							}
						}

						else
							StatusText += "Unable to Auth";
					}

					
					//RaisePropertyChanged("DataAsByteArray");
					
					Mouse.OverrideCursor = null;
				}

			}
		}
		
		public ICommand AuthenticateToCardMasterApplicationCommand { get { return new RelayCommand(OnNewAuthenticateToCardMasterApplicationCommand); }}
		private void OnNewAuthenticateToCardMasterApplicationCommand()
		{
			using ( RFiDDevice device = RFiDDevice.Instance)
			{

				if(device != null)
				{
					StatusText = string.Format("{0}: Connection to Reader successfully established\n", DateTime.Now);
					
					Mouse.OverrideCursor = Cursors.Wait;

					if(CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireMasterKeyCurrent) == KEY_ERROR.NO_ERROR)
					{
						if(device.AuthToMifareDesfireApplication(
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
		
		
		#endregion
		
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
		public string Caption {
			get { return _Caption; }
			set {
				_Caption = value;
				RaisePropertyChanged("Caption");
			}
		}
		
		#endregion
	}
}
