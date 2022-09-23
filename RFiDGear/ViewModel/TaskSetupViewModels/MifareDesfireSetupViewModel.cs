﻿/*
 * Created by SharpDevelop.
 * Date: 10/11/2017
 * Time: 22:15
 *
 */

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MefMvvm.SharedContracts;
using MefMvvm.SharedContracts.ViewModel;

using MvvmDialogs.ViewModels;

using RFiDGear.DataAccessLayer.Remote.FromIO;
using RFiDGear.DataAccessLayer;
using RFiDGear.Model;
using RFiDGear.ViewModel;

using Log4CSharp;

using System;
using System.ComponentModel.Composition;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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
    public class MifareDesfireSetupViewModel : ObservableObject, IUserDialogViewModel, IGenericTaskModel
    {
        #region Fields
        private static readonly string FacilityName = "RFiDGear";

        private protected SettingsReaderWriter settings = new SettingsReaderWriter();

        public MifareDesfireChipModel MifareDesfireChipModelToUse
        {
            get => chip;
            set
            {
                chip = value;
            }
        }
        private MifareDesfireChipModel chip;
        private protected MifareDesfireAppModel app;
        private DESFireAccessRights accessRights;

        #endregion

        #region Constructors
        /// <summary>
        ///
        /// </summary>
        public MifareDesfireSetupViewModel()
        {
            accessRights = new DESFireAccessRights();
            chip = new MifareDesfireChipModel(string.Format("Task Description: {0}", SelectedTaskDescription), CARD_TYPE.DESFire);

            app = new MifareDesfireAppModel(0);

            childNodeViewModelFromChip = new RFiDChipChildLayerViewModel(app, null, CARD_TYPE.DESFire, new ObservableCollection<IDialogViewModel>(), true);
            childNodeViewModelTemp = new RFiDChipChildLayerViewModel(app, null, CARD_TYPE.DESFire, new ObservableCollection<IDialogViewModel>(), true);

            MifareDesfireKeys = CustomConverter.GenerateStringSequence(0, 16).ToArray();
            MifareDesfireKeyCount = CustomConverter.GenerateStringSequence(1, 16).ToArray();
            KeyVersions = CustomConverter.GenerateStringSequence(0, 16).ToArray();

            KeyVersionCurrent = "0";

            isAllowChangeMKChecked = true;
            isAllowConfigChangableChecked = true;
            isAllowListingWithoutMKChecked = true;

        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="_selectedSetupViewModel"></param>
        /// <param name="_dialogs"></param>
        public MifareDesfireSetupViewModel(object _selectedSetupViewModel, ObservableCollection<IDialogViewModel> _dialogs)
        {
            try
            {
                MefHelper.Instance.Container.ComposeParts(this); //Load Plugins

                chip = new MifareDesfireChipModel(string.Format("Task Description: {0}", ""), CARD_TYPE.DESFire);
                app = new MifareDesfireAppModel(0);

                childNodeViewModelFromChip = new RFiDChipChildLayerViewModel(app, null, CARD_TYPE.DESFire, _dialogs, true);
                childNodeViewModelTemp = new RFiDChipChildLayerViewModel(app, null, CARD_TYPE.DESFire, _dialogs, true);

                MifareDesfireKeys = CustomConverter.GenerateStringSequence(0, 16).ToArray();
                MifareDesfireKeyCount = CustomConverter.GenerateStringSequence(1, 16).ToArray();
                KeyVersions = CustomConverter.GenerateStringSequence(0, 16).ToArray();

                isAllowChangeMKChecked = true;
                isAllowConfigChangableChecked = true;
                isAllowListingWithoutMKChecked = true;

                if (_selectedSetupViewModel is MifareDesfireSetupViewModel)
                {
                    PropertyInfo[] properties = typeof(MifareDesfireSetupViewModel).GetProperties(BindingFlags.Public | BindingFlags.Instance);

                    foreach (PropertyInfo p in properties.Where(x => x.PropertyType != items.GetType()))
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
                    SelectedDesfireMasterKeyEncryptionTypeTarget = settings.DefaultSpecification.MifareDesfireDefaultSecuritySettings.First(x => x.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardCardMasterKey).EncryptionType;

                    DesfireAppKeyCurrent = settings.DefaultSpecification.MifareDesfireDefaultSecuritySettings.First(x => x.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardApplicationMasterKey).Key;
                    SelectedDesfireAppKeyEncryptionTypeCurrent = settings.DefaultSpecification.MifareDesfireDefaultSecuritySettings.First(x => x.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardApplicationMasterKey).EncryptionType;
                    SelectedDesfireAppKeyNumberCurrent = "0";

                    DesfireAppKeyTarget = settings.DefaultSpecification.MifareDesfireDefaultSecuritySettings.First(x => x.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardApplicationMasterKey).Key;
                    SelectedDesfireAppKeyEncryptionTypeTarget = settings.DefaultSpecification.MifareDesfireDefaultSecuritySettings.First(x => x.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardApplicationMasterKey).EncryptionType;


                    DesfireReadKeyCurrent = settings.DefaultSpecification.MifareDesfireDefaultSecuritySettings.First(x => x.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardReadKey).Key;
                    SelectedDesfireReadKeyNumber = "1";

                    DesfireWriteKeyCurrent = settings.DefaultSpecification.MifareDesfireDefaultSecuritySettings.First(x => x.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardWriteKey).Key;
                    SelectedDesfireWriteKeyNumber = "1";

                    KeyVersionCurrent = "0";

                    accessRights = new DESFireAccessRights();

                    AppNumberNew = "1";
                    AppNumberCurrent = "0";
                    AppNumberTarget = "0";

                    SelectedDesfireAppKeyNumberTarget = "1";
                    SelectedDesfireAppMaxNumberOfKeys = "1";

                    IsValidDesfireMasterKeyCurrent = null;
                    IsValidDesfireMasterKeyTarget = null;

                    IsValidDesfireAppKeyCurrent = null;
                    IsValidDesfireAppKeyTarget = null;

                    IsValidAppNumberCurrent = null;
                    IsValidAppNumberTarget = null;
                    IsValidAppNumberNew = null;

                    IsValidDesfireReadKeyCurrent = null;
                    IsValidDesfireWriteKeyCurrent = null;

                    CurrentTaskIndex = "0";
                    SelectedTaskDescription = ResourceLoader.GetResource("textBoxPleaseEnterTaskDescription");

                    FileNumberCurrent = "0";
                    FileSizeCurrent = "16";

                    IsValidFileNumberCurrent = null;
                    IsValidFileSizeCurrent = null;

                    IsDesfireFileAuthoringTabEnabled = false;
                    IsDataExplorerEditTabEnabled = false;
                    IsDesfirePICCAuthoringTabEnabled = false;
                    IsDesfireAuthenticationTabEnabled = false;
                    IsDesfireAppAuthenticationTabEnabled = false;
                    IsDesfireAppAuthoringTabEnabled = false;
                    IsDesfireAppCreationTabEnabled = false;
                }

                HasPlugins = items != null && items.Any();

                if (HasPlugins)
                {
                    SelectedPlugin = Items.FirstOrDefault();
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e, FacilityName);
            }
        }
        #endregion

        #region Dialogs
        [XmlIgnore]
        public ObservableCollection<IDialogViewModel> Dialogs => dialogs;
        private readonly ObservableCollection<IDialogViewModel> dialogs = new ObservableCollection<IDialogViewModel>();
        #region Plugins

        /// <summary>
        /// True if Count of Imported Views is > 0; See constructor
        /// </summary>
        [XmlIgnore]
        public bool HasPlugins
        {
            get => hasPlugins;
            set
            {
                hasPlugins = value;
                OnPropertyChanged(nameof(HasPlugins));
            }
        }
        private bool hasPlugins;

        /// <summary>
        /// Selected Plugin in ComboBox
        /// </summary>
        [XmlIgnore]
        public object SelectedPlugin
        {
            get => selectedPlugin;
            set
            {
                selectedPlugin = value;
                OnPropertyChanged(nameof(SelectedPlugin));
            }
        }
        private object selectedPlugin;

        /// <summary>
        /// Imported Views by URI
        /// </summary>
        [XmlIgnore]
        [ImportMany()]
        public Lazy<IUIExtension, IUIExtensionDetails>[] Items
        {
            get => items;

            set
            {
                items = (from g in value
                         orderby g.Metadata.SortOrder, g.Metadata.Name
                         select g).ToArray();

                OnPropertyChanged(nameof(Items));
            }
        }
        private Lazy<IUIExtension, IUIExtensionDetails>[] items;

        #endregion

        #endregion Dialogs

        #region Dependency Properties

        /// <summary>
        /// The Indexnumber of the ExecuteCondition Task As String
        /// </summary>
        public string SelectedExecuteConditionTaskIndex
        {
            get => selectedExecuteConditionTaskIndex;

            set
            {
                selectedExecuteConditionTaskIndex = value;
                IsValidSelectedExecuteConditionTaskIndex = int.TryParse(value, out selectedExecuteConditionTaskIndexAsInt);
                OnPropertyChanged(nameof(SelectedExecuteConditionTaskIndex));
            }
        }
        private string selectedExecuteConditionTaskIndex;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsValidSelectedExecuteConditionTaskIndex
        {
            get => isValidSelectedExecuteConditionTaskIndex;
            set
            {
                isValidSelectedExecuteConditionTaskIndex = value;
                OnPropertyChanged(nameof(IsValidSelectedExecuteConditionTaskIndex));
            }
        }
        private bool? isValidSelectedExecuteConditionTaskIndex;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public int SelectedExecuteConditionTaskIndexAsInt => selectedExecuteConditionTaskIndexAsInt;
        private int selectedExecuteConditionTaskIndexAsInt;

        /// <summary>
        /// 
        /// </summary>
        public ERROR SelectedExecuteConditionErrorLevel
        {
            get => selectedExecuteConditionErrorLevel;

            set
            {
                selectedExecuteConditionErrorLevel = value;
                OnPropertyChanged(nameof(SelectedExecuteConditionErrorLevel));
            }
        }
        private ERROR selectedExecuteConditionErrorLevel;

        /// <summary>
        /// 
        /// </summary>
        public bool IsDesfireFileAuthoringTabEnabled
        {
            get => isDesfireFileAuthoringTabEnabled;
            set
            {
                isDesfireFileAuthoringTabEnabled = value;
                OnPropertyChanged(nameof(IsDesfireFileAuthoringTabEnabled));
            }
        }
        private bool isDesfireFileAuthoringTabEnabled;

        /// <summary>
        ///
        /// </summary>
        public bool IsDataExplorerEditTabEnabled
        {
            get => isDataExplorerEditTabEnabled;
            set
            {
                isDataExplorerEditTabEnabled = value;
                OnPropertyChanged(nameof(IsDataExplorerEditTabEnabled));
            }
        }
        private bool isDataExplorerEditTabEnabled;

        /// <summary>
        ///
        /// </summary>
        public bool IsDesfirePICCAuthoringTabEnabled
        {
            get => isDesfirePICCAuthoringTabEnabled;
            set
            {
                isDesfirePICCAuthoringTabEnabled = value;
                OnPropertyChanged(nameof(IsDesfirePICCAuthoringTabEnabled));
            }
        }
        private bool isDesfirePICCAuthoringTabEnabled;

        /// <summary>
        ///
        /// </summary>
        public bool IsDesfireAuthenticationTabEnabled
        {
            get => isDesfireAuthenticationTabEnabled;
            set
            {
                isDesfireAuthenticationTabEnabled = value;
                OnPropertyChanged(nameof(IsDesfireAuthenticationTabEnabled));
            }
        }
        private bool isDesfireAuthenticationTabEnabled;

        /// <summary>
        ///
        /// </summary>
        public bool IsDesfireAppAuthenticationTabEnabled
        {
            get => isDesfireAppAuthenticationTabEnabled;
            set
            {
                isDesfireAppAuthenticationTabEnabled = value;
                OnPropertyChanged(nameof(IsDesfireAppAuthenticationTabEnabled));
            }
        }
        private bool isDesfireAppAuthenticationTabEnabled;

        /// <summary>
        ///
        /// </summary>
        public bool IsDesfireAppAuthoringTabEnabled
        {
            get => isDesfireAppAuthoringTabEnabled;
            set
            {
                isDesfireAppAuthoringTabEnabled = value;
                OnPropertyChanged(nameof(IsDesfireAppAuthoringTabEnabled));
            }
        }
        private bool isDesfireAppAuthoringTabEnabled;

        /// <summary>
        /// 
        /// </summary>
        public bool IsDesfireAppCreationTabEnabled
        {
            get => isDesfireAppCreationTabEnabled;
            set
            {
                isDesfireAppCreationTabEnabled = value;
                OnPropertyChanged(nameof(IsDesfireAppCreationTabEnabled));
            }
        }
        private bool isDesfireAppCreationTabEnabled;

        /// <summary>
        /// 
        /// </summary>
        public SettingsReaderWriter Settings => settings;

        /// <summary>
        ///
        /// </summary>
        public TaskType_MifareDesfireTask SelectedTaskType
        {
            get =>
                selectedAccessBitsTaskType;
            set
            {
                selectedAccessBitsTaskType = value;
                switch (value)
                {
                    case TaskType_MifareDesfireTask.None:
                        IsDesfireFileAuthoringTabEnabled = false;
                        IsDataExplorerEditTabEnabled = false;
                        IsDesfirePICCAuthoringTabEnabled = false;
                        IsDesfireAuthenticationTabEnabled = false;
                        IsDesfireAppAuthenticationTabEnabled = false;
                        IsDesfireAppAuthoringTabEnabled = false;
                        IsDesfireAppCreationTabEnabled = false;
                        break;

                    case TaskType_MifareDesfireTask.ReadAppSettings:
                        IsDesfireFileAuthoringTabEnabled = false;
                        IsDataExplorerEditTabEnabled = false;
                        IsDesfirePICCAuthoringTabEnabled = true;
                        IsDesfireAuthenticationTabEnabled = false;
                        IsDesfireAppAuthenticationTabEnabled = false;
                        IsDesfireAppAuthoringTabEnabled = false;
                        IsDesfireAppCreationTabEnabled = false;
                        break;

                    case TaskType_MifareDesfireTask.AppExistCheck:
                        IsDesfireFileAuthoringTabEnabled = false;
                        IsDataExplorerEditTabEnabled = false;
                        IsDesfirePICCAuthoringTabEnabled = false;
                        IsDesfireAuthenticationTabEnabled = false;
                        IsDesfireAppAuthenticationTabEnabled = true;
                        IsDesfireAppAuthoringTabEnabled = false;
                        IsDesfireAppCreationTabEnabled = false;
                        break;

                    case TaskType_MifareDesfireTask.ApplicationKeyChangeover:
                        IsDesfireFileAuthoringTabEnabled = false;
                        IsDataExplorerEditTabEnabled = false;
                        IsDesfirePICCAuthoringTabEnabled = false;
                        IsDesfireAuthenticationTabEnabled = false;
                        IsDesfireAppAuthenticationTabEnabled = true;
                        IsDesfireAppAuthoringTabEnabled = true;
                        IsDesfireAppCreationTabEnabled = true;
                        break;

                    case TaskType_MifareDesfireTask.ChangeDefault:
                        IsDesfireFileAuthoringTabEnabled = true;
                        IsDataExplorerEditTabEnabled = true;
                        IsDesfirePICCAuthoringTabEnabled = true;
                        IsDesfireAuthenticationTabEnabled = true;
                        IsDesfireAppAuthenticationTabEnabled = true;
                        IsDesfireAppAuthoringTabEnabled = true;
                        IsDesfireAppCreationTabEnabled = true;
                        break;

                    case TaskType_MifareDesfireTask.CreateApplication:
                        IsDesfireFileAuthoringTabEnabled = false;
                        IsDataExplorerEditTabEnabled = false;
                        IsDesfirePICCAuthoringTabEnabled = false;
                        IsDesfireAuthenticationTabEnabled = true;
                        IsDesfireAppAuthenticationTabEnabled = false;
                        IsDesfireAppAuthoringTabEnabled = false;
                        IsDesfireAppCreationTabEnabled = true;
                        break;

                    case TaskType_MifareDesfireTask.AuthenticateApplication:
                        IsDesfireFileAuthoringTabEnabled = false;
                        IsDataExplorerEditTabEnabled = false;
                        IsDesfirePICCAuthoringTabEnabled = false;
                        IsDesfireAuthenticationTabEnabled = false;
                        IsDesfireAppAuthenticationTabEnabled = true;
                        IsDesfireAppAuthoringTabEnabled = false;
                        IsDesfireAppCreationTabEnabled = false;
                        break;

                    case TaskType_MifareDesfireTask.CreateFile:
                        IsDesfireFileAuthoringTabEnabled = true;
                        IsDataExplorerEditTabEnabled = false;
                        IsDesfirePICCAuthoringTabEnabled = false;
                        IsDesfireAuthenticationTabEnabled = false;
                        IsDesfireAppAuthenticationTabEnabled = true;
                        IsDesfireAppAuthoringTabEnabled = false;
                        IsDesfireAppCreationTabEnabled = false;
                        break;

                    case TaskType_MifareDesfireTask.DeleteApplication:
                        IsDesfireFileAuthoringTabEnabled = false;
                        IsDataExplorerEditTabEnabled = false;
                        IsDesfirePICCAuthoringTabEnabled = false;
                        IsDesfireAuthenticationTabEnabled = true;
                        IsDesfireAppAuthenticationTabEnabled = false;
                        IsDesfireAppAuthoringTabEnabled = false;
                        IsDesfireAppCreationTabEnabled = true;
                        break;

                    case TaskType_MifareDesfireTask.DeleteFile:
                        IsDesfireFileAuthoringTabEnabled = true;
                        IsDataExplorerEditTabEnabled = false;
                        IsDesfirePICCAuthoringTabEnabled = false;
                        IsDesfireAuthenticationTabEnabled = false;
                        IsDesfireAppAuthenticationTabEnabled = true;
                        IsDesfireAppAuthoringTabEnabled = false;
                        IsDesfireAppCreationTabEnabled = false;
                        break;

                    case TaskType_MifareDesfireTask.FormatDesfireCard:
                        IsDesfireFileAuthoringTabEnabled = false;
                        IsDataExplorerEditTabEnabled = false;
                        IsDesfirePICCAuthoringTabEnabled = false;
                        IsDesfireAuthenticationTabEnabled = true;
                        IsDesfireAppAuthenticationTabEnabled = false;
                        IsDesfireAppAuthoringTabEnabled = false;
                        IsDesfireAppCreationTabEnabled = false;
                        break;

                    case TaskType_MifareDesfireTask.PICCMasterKeyChangeover:
                        IsDesfireFileAuthoringTabEnabled = false;
                        IsDataExplorerEditTabEnabled = false;
                        IsDesfirePICCAuthoringTabEnabled = true;
                        IsDesfireAuthenticationTabEnabled = true;
                        IsDesfireAppAuthenticationTabEnabled = false;
                        IsDesfireAppAuthoringTabEnabled = false;
                        IsDesfireAppCreationTabEnabled = false;
                        break;

                    case TaskType_MifareDesfireTask.ReadData:
                        IsDesfireFileAuthoringTabEnabled = true;
                        IsDataExplorerEditTabEnabled = true;
                        IsDesfirePICCAuthoringTabEnabled = false;
                        IsDesfireAuthenticationTabEnabled = false;
                        IsDesfireAppAuthenticationTabEnabled = true;
                        IsDesfireAppAuthoringTabEnabled = false;
                        IsDesfireAppCreationTabEnabled = false;
                        break;

                    case TaskType_MifareDesfireTask.WriteData:
                        IsDesfireFileAuthoringTabEnabled = true;
                        IsDataExplorerEditTabEnabled = true;
                        IsDesfirePICCAuthoringTabEnabled = false;
                        IsDesfireAuthenticationTabEnabled = false;
                        IsDesfireAppAuthenticationTabEnabled = true;
                        IsDesfireAppAuthoringTabEnabled = false;
                        IsDesfireAppCreationTabEnabled = false;
                        break;
                }
                OnPropertyChanged(nameof(SelectedTaskType));
            }
        }
        private TaskType_MifareDesfireTask selectedAccessBitsTaskType;

        /// <summary>
        ///
        /// </summary>
        public string CurrentTaskIndex
        {
            get => currentTaskIndex;
            set
            {
                currentTaskIndex = value;
                IsValidSelectedTaskIndex = int.TryParse(value, out selectedTaskIndexAsInt);
            }
        }
        private string currentTaskIndex;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public int SelectedTaskIndexAsInt => selectedTaskIndexAsInt;
        private int selectedTaskIndexAsInt;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsValidSelectedTaskIndex
        {
            get => isValidSelectedTaskIndex;
            set
            {
                isValidSelectedTaskIndex = value;
                OnPropertyChanged(nameof(IsValidSelectedTaskIndex));
            }
        }
        private bool? isValidSelectedTaskIndex;

        /// <summary>
        ///
        /// </summary>
        public string SelectedTaskDescription
        {
            get =>
                //classicKeyAKeyCurrent = SectorTrailer.Split(',')[0];
                selectedAccessBitsTaskDescription;
            set => selectedAccessBitsTaskDescription = value;
        }
        private string selectedAccessBitsTaskDescription;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsTaskCompletedSuccessfully
        {
            get => isTaskCompletedSuccessfully;
            set
            {
                isTaskCompletedSuccessfully = value;
                OnPropertyChanged(nameof(IsTaskCompletedSuccessfully));
            }
        }
        private bool? isTaskCompletedSuccessfully;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool IsValidSelectedAccessBitsTaskIndex
        {
            get =>
                //classicKeyAKeyCurrent = SectorTrailer.Split(',')[0];
                isValidSelectedAccessBitsTaskIndex;
            set
            {
                isValidSelectedAccessBitsTaskIndex = value;
                OnPropertyChanged(nameof(IsValidSelectedAccessBitsTaskIndex));
            }
        }
        private bool isValidSelectedAccessBitsTaskIndex;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public string StatusText
        {
            get => statusText;
            set
            {
                statusText = value;
                OnPropertyChanged(nameof(StatusText));
            }
        }
        private string statusText;

        /// <summary>
        /// Result of this Task
        /// </summary>
        [XmlIgnore]
        public ERROR CurrentTaskErrorLevel { get; set; }

        #region Key Properties Card Master

        [XmlIgnore]
        public string[] MifareDesfireKeys { get; set; }

        [XmlIgnore]
        public string[] MifareDesfireKeyCount { get; set; }

        [XmlIgnore]
        public string[] KeyVersions { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string KeyVersionCurrent
        {
            get => keyVersionCurrent;
            set
            {
                keyVersionCurrent = value;
                keyVersionCurrentAsInt = int.Parse(keyVersionCurrent);
                OnPropertyChanged(nameof(KeyVersionCurrent));
            }
        }
        private string keyVersionCurrent;
        private int keyVersionCurrentAsInt;

        /// <summary>
        /// 
        /// </summary>
        public DESFireKeyType SelectedDesfireMasterKeyEncryptionTypeCurrent
        {
            get => selectedDesfireMasterKeyEncryptionTypeCurrent;
            set
            {
                selectedDesfireMasterKeyEncryptionTypeCurrent = value;
                OnPropertyChanged(nameof(SelectedDesfireMasterKeyEncryptionTypeCurrent));
            }
        }
        private DESFireKeyType selectedDesfireMasterKeyEncryptionTypeCurrent;

        /// <summary>
        /// 
        /// </summary>
        public string DesfireMasterKeyCurrent
        {
            get => desfireMasterKeyCurrent;
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
                OnPropertyChanged(nameof(DesfireMasterKeyCurrent));
            }
        }
        private string desfireMasterKeyCurrent;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsValidDesfireMasterKeyCurrent
        {
            get => isValidDesfireMasterKeyCurrent;
            set
            {
                isValidDesfireMasterKeyCurrent = value;
                OnPropertyChanged(nameof(IsValidDesfireMasterKeyCurrent));
            }
        }
        private bool? isValidDesfireMasterKeyCurrent;

        /// <summary>
        /// 
        /// </summary>
        public DESFireKeyType SelectedDesfireMasterKeyEncryptionTypeTarget
        {
            get => selectedDesfireMasterKeyEncryptionTypeTarget;
            set
            {
                selectedDesfireMasterKeyEncryptionTypeTarget = value;
                OnPropertyChanged(nameof(SelectedDesfireMasterKeyEncryptionTypeCurrent));
            }
        }
        private DESFireKeyType selectedDesfireMasterKeyEncryptionTypeTarget;

        /// <summary>
        ///
        /// </summary>
        public string DesfireMasterKeyTarget
        {
            get => desfireMasterKeyTarget;
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
                OnPropertyChanged(nameof(DesfireMasterKeyTarget));
            }
        }
        private string desfireMasterKeyTarget;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsValidDesfireMasterKeyTarget
        {
            get => isValidDesfireMasterKeyTarget;
            set
            {
                isValidDesfireMasterKeyTarget = value;
                OnPropertyChanged(nameof(IsValidDesfireMasterKeyTarget));
            }
        }
        private bool? isValidDesfireMasterKeyTarget;

        #endregion Key Properties Card Master

        #region Key Properties App Master

        #region App Creation

        /// <summary>
        ///
        /// </summary>
        public DESFireKeyType SelectedDesfireAppKeyEncryptionTypeCreateNewApp
        {
            get => selectedCreateDesfireAppKeyEncryptionTypeCurrent;
            set
            {
                selectedCreateDesfireAppKeyEncryptionTypeCurrent = value;
                OnPropertyChanged(nameof(SelectedDesfireAppKeyEncryptionTypeCreateNewApp));
            }
        }
        private DESFireKeyType selectedCreateDesfireAppKeyEncryptionTypeCurrent;

        /// <summary>
        ///
        /// </summary>
        public string SelectedDesfireAppMaxNumberOfKeys
        {
            get => selectedDesfireAppMaxNumberOfKeys;
            set
            {
                if (int.TryParse(value, out selectedDesfireAppMaxNumberOfKeysAsInt))
                {
                    selectedDesfireAppMaxNumberOfKeys = value;
                }
                OnPropertyChanged(nameof(SelectedDesfireAppMaxNumberOfKeys));
            }
        }
        private string selectedDesfireAppMaxNumberOfKeys;
        private int selectedDesfireAppMaxNumberOfKeysAsInt;

        /// <summary>
        ///
        /// </summary>
        public AccessCondition_MifareDesfireAppCreation SelectedDesfireAppKeySettingsCreateNewApp
        {
            get => selectedDesfireAppKeySettingsTarget;
            set
            {
                selectedDesfireAppKeySettingsTarget = value;
                OnPropertyChanged(nameof(SelectedDesfireAppKeySettingsCreateNewApp));
            }
        }
        private AccessCondition_MifareDesfireAppCreation selectedDesfireAppKeySettingsTarget;

        /// <summary>
        ///
        /// </summary>
        public string AppNumberNew
        {
            get => appNumberNew;
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
                IsValidAppNumberNew = (int.TryParse(value, out appNumberNewAsInt) && appNumberNewAsInt <= (int)0xFFFFFF);
                OnPropertyChanged(nameof(AppNumberNew));
            }
        }
        private string appNumberNew;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public int AppNumberNewAsInt => appNumberNewAsInt;
        private int appNumberNewAsInt;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsValidAppNumberNew
        {
            get => isValidAppNumberNew;
            set
            {
                isValidAppNumberNew = value;
                OnPropertyChanged(nameof(IsValidAppNumberNew));
            }
        }
        private bool? isValidAppNumberNew;

        /// <summary>
        ///
        /// </summary>
        public bool IsAllowChangeMKChecked
        {
            get => isAllowChangeMKChecked;
            set
            {
                isAllowChangeMKChecked = value;
                OnPropertyChanged(nameof(IsAllowChangeMKChecked));
            }
        }
        private bool isAllowChangeMKChecked;

        /// <summary>
        ///
        /// </summary>
        public bool IsAllowListingWithoutMKChecked
        {
            get => isAllowListingWithoutMKChecked;
            set
            {
                isAllowListingWithoutMKChecked = value;
                OnPropertyChanged(nameof(IsAllowListingWithoutMKChecked));
            }
        }
        private bool isAllowListingWithoutMKChecked;

        /// <summary>
        ///
        /// </summary>
        public bool IsAllowCreateDelWithoutMKChecked
        {
            get => isAllowCreateDelWithoutMKChecked;
            set
            {
                isAllowCreateDelWithoutMKChecked = value;
                OnPropertyChanged(nameof(IsAllowCreateDelWithoutMKChecked));
            }
        }
        private bool isAllowCreateDelWithoutMKChecked;

        /// <summary>
        ///
        /// </summary>
        public bool IsAllowConfigChangableChecked
        {
            get => isAllowConfigChangableChecked;
            set
            {
                isAllowConfigChangableChecked = value;
                OnPropertyChanged(nameof(IsAllowConfigChangableChecked));
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
            get => selectedDesfireAppKeyEncryptionTypeCurrent;
            set
            {
                selectedDesfireAppKeyEncryptionTypeCurrent = value;
                OnPropertyChanged(nameof(SelectedDesfireAppKeyEncryptionTypeCurrent));
            }
        }
        private DESFireKeyType selectedDesfireAppKeyEncryptionTypeCurrent;

        /// <summary>
        ///
        /// </summary>
        public string SelectedDesfireAppKeyNumberCurrent
        {
            get => selectedDesfireAppKeyNumberCurrent;
            set
            {
                if (int.TryParse(value, out selectedDesfireAppKeyNumberCurrentAsInt))
                {
                    selectedDesfireAppKeyNumberCurrent = value;
                }
                OnPropertyChanged(nameof(SelectedDesfireAppKeyNumberCurrent));
            }
        }
        private string selectedDesfireAppKeyNumberCurrent;
        private int selectedDesfireAppKeyNumberCurrentAsInt;

        /// <summary>
        ///
        /// </summary>
        public string DesfireAppKeyCurrent
        {
            get => desfireAppKeyCurrent;
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
                OnPropertyChanged(nameof(DesfireAppKeyCurrent));
            }
        }
        private string desfireAppKeyCurrent;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsValidDesfireAppKeyCurrent
        {
            get => isValidDesfireAppKeyCurrent;
            set
            {
                isValidDesfireAppKeyCurrent = value;
                OnPropertyChanged(nameof(IsValidDesfireAppKeyCurrent));
            }
        }
        private bool? isValidDesfireAppKeyCurrent;

        /// <summary>
        ///
        /// </summary>
        public string AppNumberCurrent
        {
            get => appNumberCurrent;
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
                IsValidAppNumberCurrent = (int.TryParse(value, out appNumberCurrentAsInt) && appNumberCurrentAsInt <= (int)0xFFFFFF);
                OnPropertyChanged(nameof(AppNumberCurrent));
            }
        }
        private string appNumberCurrent;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public int AppNumberCurrentAsInt => appNumberCurrentAsInt;
        private int appNumberCurrentAsInt;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsValidAppNumberCurrent
        {
            get => isValidAppNumberCurrent;
            set
            {
                isValidAppNumberCurrent = value;
                OnPropertyChanged(nameof(IsValidAppNumberCurrent));
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
            get => selectedDesfireAppKeyEncryptionTypeTarget;
            set
            {
                selectedDesfireAppKeyEncryptionTypeTarget = value;
                OnPropertyChanged(nameof(SelectedDesfireAppKeyEncryptionTypeTarget));
            }
        }
        private DESFireKeyType selectedDesfireAppKeyEncryptionTypeTarget;

        /// <summary>
        ///
        /// </summary>
        public string SelectedDesfireAppKeyNumberTarget
        {
            get => selectedDesfireAppKeyNumberTarget;
            set
            {
                if (int.TryParse(value, out selectedDesfireAppKeyNumberTargetAsInt))
                {
                    selectedDesfireAppKeyNumberTarget = value;
                }
                OnPropertyChanged(nameof(SelectedDesfireAppKeyNumberTarget));
            }
        }
        private string selectedDesfireAppKeyNumberTarget;
        private int selectedDesfireAppKeyNumberTargetAsInt;

        /// <summary>
        ///
        /// </summary>
        public string SelectedDesfireAppKeyVersionTarget
        {
            get
            {
                return selectedDesfireAppKeyVersionTarget;
            }
            set
            {
                if (int.TryParse(value, out selectedDesfireAppKeyVersionTargetAsInt))
                {
                    selectedDesfireAppKeyVersionTarget = value;
                }
                OnPropertyChanged(nameof(SelectedDesfireAppKeyVersionTarget));
            }
        }
        private string selectedDesfireAppKeyVersionTarget;
        private int selectedDesfireAppKeyVersionTargetAsInt;

        /// <summary>
        ///
        /// </summary>
        public string DesfireAppKeyTarget
        {
            get => desfireAppKeyTarget;
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
                OnPropertyChanged(nameof(DesfireAppKeyTarget));
            }
        }
        private string desfireAppKeyTarget;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsValidDesfireAppKeyTarget
        {
            get => isValidDesfireAppKeyTarget;
            set
            {
                isValidDesfireAppKeyTarget = value;
                OnPropertyChanged(nameof(IsValidDesfireAppKeyTarget));
            }
        }
        private bool? isValidDesfireAppKeyTarget;

        /// <summary>
        ///
        /// </summary>
        public string AppNumberTarget
        {
            get => appNumberTarget;
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
                IsValidAppNumberTarget = (int.TryParse(value, out appNumberTargetAsInt) && appNumberTargetAsInt <= (int)0xFFFFFF);
                OnPropertyChanged(nameof(AppNumberTarget));
            }
        }
        private string appNumberTarget;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public int AppNumberTargetAsInt => appNumberTargetAsInt;
        private int appNumberTargetAsInt;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsValidAppNumberTarget
        {
            get => isValidAppNumberTarget;
            set
            {
                isValidAppNumberTarget = value;
                OnPropertyChanged(nameof(IsValidAppNumberTarget));
            }
        }
        private bool? isValidAppNumberTarget;

        #endregion Key Properties App Master Target

        #endregion

        #region Key Properties File Master

        #region File Creation

        /// <summary>
        ///
        /// </summary>
        public TaskAccessRights SelectedDesfireFileAccessRightReadWrite
        {
            get => selectedDesfireFileAccessRightReadWrite;
            set
            {
                selectedDesfireFileAccessRightReadWrite = value;
                OnPropertyChanged(nameof(SelectedDesfireFileAccessRightReadWrite));
            }
        }
        private TaskAccessRights selectedDesfireFileAccessRightReadWrite;

        /// <summary>
        ///
        /// </summary>
        public TaskAccessRights SelectedDesfireFileAccessRightChange
        {
            get => selectedDesfireFileAccessRightChange;
            set
            {
                selectedDesfireFileAccessRightChange = value;
                OnPropertyChanged(nameof(SelectedDesfireFileAccessRightChange));
            }
        }
        private TaskAccessRights selectedDesfireFileAccessRightChange;

        /// <summary>
        ///
        /// </summary>
        public TaskAccessRights SelectedDesfireFileAccessRightRead
        {
            get => selectedDesfireFileAccessRightRead;
            set
            {
                selectedDesfireFileAccessRightRead = value;
                OnPropertyChanged(nameof(SelectedDesfireFileAccessRightRead));
            }
        }
        private TaskAccessRights selectedDesfireFileAccessRightRead;

        /// <summary>
        ///
        /// </summary>
        public TaskAccessRights SelectedDesfireFileAccessRightWrite
        {
            get => selectedDesfireFileAccessRightWrite;
            set
            {
                selectedDesfireFileAccessRightWrite = value;
                OnPropertyChanged(nameof(SelectedDesfireFileAccessRightWrite));
            }
        }
        private TaskAccessRights selectedDesfireFileAccessRightWrite;

        /// <summary>
        ///
        /// </summary>
        public EncryptionMode SelectedDesfireFileCryptoMode
        {
            get => selectedDesfireFileCryptoMode;
            set
            {
                selectedDesfireFileCryptoMode = value;
                OnPropertyChanged(nameof(SelectedDesfireFileCryptoMode));
            }
        }
        private EncryptionMode selectedDesfireFileCryptoMode;

        /// <summary>
        ///
        /// </summary>
        public FileType_MifareDesfireFileType SelectedDesfireFileType
        {
            get => selectedDesfireFileType;
            set
            {
                selectedDesfireFileType = value;
                OnPropertyChanged(nameof(SelectedDesfireFileType));
            }
        }
        private FileType_MifareDesfireFileType selectedDesfireFileType;

        /// <summary>
        ///
        /// </summary>
        public string FileNumberCurrent
        {
            get => fileNumberCurrent;
            set
            {
                fileNumberCurrent = value;
                IsValidFileNumberCurrent = (int.TryParse(value, out fileNumberCurrentAsInt) && fileNumberCurrentAsInt <= (int)0xFFFF);
                OnPropertyChanged(nameof(FileNumberCurrent));
            }
        }
        private string fileNumberCurrent;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public int FileNumberCurrentAsInt => fileNumberCurrentAsInt;
        private int fileNumberCurrentAsInt;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsValidFileNumberCurrent
        {
            get => isValidFileNumberCurrent;
            set
            {
                isValidFileNumberCurrent = value;
                OnPropertyChanged(nameof(IsValidFileNumberCurrent));
            }
        }
        private bool? isValidFileNumberCurrent;

        /// <summary>
        ///
        /// </summary>
        public string FileSizeCurrent
        {
            get => fileSizeCurrent;
            set
            {
                fileSizeCurrent = value;
                IsValidFileSizeCurrent = (int.TryParse(value, out fileSizeCurrentAsInt) && fileSizeCurrentAsInt <= 8000);

                if (IsValidFileSizeCurrent != false)
                {
                    if (childNodeViewModelFromChip.Children.Any(x => x.DesfireFile != null))
                    {
                        try
                        {
                            childNodeViewModelFromChip.Children.Single(y => y.DesfireFile.FileID == FileNumberCurrentAsInt).DesfireFile = new MifareDesfireFileModel(new byte[FileSizeCurrentAsInt], 0);
                            childNodeViewModelTemp.Children.Single(y => y.DesfireFile.FileID == FileNumberCurrentAsInt).DesfireFile = new MifareDesfireFileModel(new byte[FileSizeCurrentAsInt], 0);
                        }
                        catch
                        {

                        }

                    }
                    else
                    {
                        childNodeViewModelFromChip.Children.Add(new RFiDChipGrandChildLayerViewModel(new MifareDesfireFileModel(new byte[FileSizeCurrentAsInt], 0), null));
                        childNodeViewModelTemp.Children.Add(new RFiDChipGrandChildLayerViewModel(new MifareDesfireFileModel(new byte[FileSizeCurrentAsInt], 0), null));
                    }
                }

                OnPropertyChanged(nameof(FileSizeCurrent));
            }
        }
        private string fileSizeCurrent;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public int FileSizeCurrentAsInt => fileSizeCurrentAsInt;
        private int fileSizeCurrentAsInt;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsValidFileSizeCurrent
        {
            get => isValidFileSizeCurrent;
            set
            {
                isValidFileSizeCurrent = value;
                OnPropertyChanged(nameof(IsValidFileSizeCurrent));
            }
        }
        private bool? isValidFileSizeCurrent;

        #endregion File Creation

        #endregion Key Properties File Master

        #region DataExplorer

        /// <summary>
        ///
        /// </summary>
        public RFiDChipChildLayerViewModel ChildNodeViewModelFromChip
        {
            get => childNodeViewModelFromChip;
            set => childNodeViewModelFromChip = value;
        }
        private RFiDChipChildLayerViewModel childNodeViewModelFromChip;

        /// <summary>
        ///
        /// </summary>
        public RFiDChipChildLayerViewModel ChildNodeViewModelTemp
        {
            get => childNodeViewModelTemp;
            set
            {
                childNodeViewModelTemp = value;
                OnPropertyChanged(nameof(ChildNodeViewModelTemp));
            }
        }
        private RFiDChipChildLayerViewModel childNodeViewModelTemp;

        /// <summary>
        ///
        /// </summary>
        public RFiDChipGrandChildLayerViewModel GrandChildNodeViewModel => ChildNodeViewModelTemp.Children.Count > 0 ? ChildNodeViewModelTemp.Children.Single(x => x.DesfireFile != null) : null;

        /// <summary>
        ///
        /// </summary>
        public string DesfireReadKeyCurrent
        {
            get => desfireReadKeyCurrent;
            set
            {
                try
                {
                    desfireReadKeyCurrent = value.ToUpper().Remove(32);
                }
                catch
                {
                    desfireReadKeyCurrent = value.ToUpper();
                }

                IsValidDesfireReadKeyCurrent = (
                    CustomConverter.IsInHexFormat(desfireReadKeyCurrent) &&
                    desfireReadKeyCurrent.Length == 32);

                OnPropertyChanged(nameof(DesfireReadKeyCurrent));
            }
        }
        private string desfireReadKeyCurrent;

        /// <summary>
        ///
        /// </summary>
        public string SelectedDesfireReadKeyNumber
        {
            get => selectedDesfireReadKeyNumber;
            set
            {
                if (int.TryParse(value, out selectedDesfireReadKeyNumberAsInt))
                {
                    selectedDesfireReadKeyNumber = value;
                }
                OnPropertyChanged(nameof(SelectedDesfireReadKeyNumber));
            }
        }
        private string selectedDesfireReadKeyNumber;
        private int selectedDesfireReadKeyNumberAsInt;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsValidDesfireReadKeyCurrent
        {
            get => isValidDesfireReadKeyCurrent;
            set
            {
                isValidDesfireReadKeyCurrent = value;
                OnPropertyChanged(nameof(IsValidDesfireReadKeyCurrent));
            }
        }
        private bool? isValidDesfireReadKeyCurrent;

        /// <summary>
        ///
        /// </summary>
        public string DesfireWriteKeyCurrent
        {
            get => desfireWriteKeyCurrent;
            set
            {
                try
                {
                    desfireWriteKeyCurrent = value.ToUpper().Remove(32);
                }
                catch
                {
                    desfireWriteKeyCurrent = value.ToUpper();
                }

                IsValidDesfireWriteKeyCurrent = (
                    CustomConverter.IsInHexFormat(desfireWriteKeyCurrent) &&
                    desfireWriteKeyCurrent.Length == 32);

                OnPropertyChanged(nameof(DesfireWriteKeyCurrent));
            }
        }
        private string desfireWriteKeyCurrent;

        /// <summary>
        ///
        /// </summary>
        public string SelectedDesfireWriteKeyNumber
        {
            get => selectedDesfireWriteKeyNumber;
            set
            {
                if (int.TryParse(value, out selectedDesfireWriteKeyNumberAsInt))
                {
                    selectedDesfireWriteKeyNumber = value;
                }
                OnPropertyChanged(nameof(SelectedDesfireWriteKeyNumber));
            }
        }
        private string selectedDesfireWriteKeyNumber;
        private int selectedDesfireWriteKeyNumberAsInt;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsValidDesfireWriteKeyCurrent
        {
            get => isValidDesfireWriteKeyCurrent;
            set
            {
                isValidDesfireWriteKeyCurrent = value;
                OnPropertyChanged(nameof(IsValidDesfireWriteKeyCurrent));
            }
        }
        private bool? isValidDesfireWriteKeyCurrent;

        /// <summary>
        /// 
        /// </summary>
        public DESFireKeyType SelectedDesfireReadKeyEncryptionType
        {
            get => selectedDesfireReadKeyEncryptionType;
            set
            {
                selectedDesfireReadKeyEncryptionType = value;
                OnPropertyChanged(nameof(SelectedDesfireReadKeyEncryptionType));
            }
        }
        private DESFireKeyType selectedDesfireReadKeyEncryptionType;

        /// <summary>
        /// 
        /// </summary>
        public DESFireKeyType SelectedDesfireWriteKeyEncryptionType
        {
            get => selectedDesfireWriteKeyEncryptionType;
            set
            {
                selectedDesfireWriteKeyEncryptionType = value;
                OnPropertyChanged(nameof(SelectedDesfireWriteKeyEncryptionType));
            }
        }
        private DESFireKeyType selectedDesfireWriteKeyEncryptionType;

        #endregion

        #endregion

        #region Commands

        public ICommand CommandDelegator => new RelayCommand<TaskType_MifareDesfireTask>((x) => OnNewCommandDelegatorCall(x));
        private void OnNewCommandDelegatorCall(TaskType_MifareDesfireTask desfireTaskType)
        {
            switch (desfireTaskType)
            {
                case TaskType_MifareDesfireTask.AppExistCheck:
                    DoesAppExistCommand(null);
                    break;

                case TaskType_MifareDesfireTask.ApplicationKeyChangeover:
                    OnNewChangeAppKeyCommand();
                    break;

                case TaskType_MifareDesfireTask.AuthenticateApplication:
                    OnNewAuthenticateToCardApplicationCommand();
                    break;

                case TaskType_MifareDesfireTask.CreateApplication:
                    OnNewCreateAppCommand();
                    break;

                case TaskType_MifareDesfireTask.CreateFile:
                    OnNewCreateFileCommand();
                    break;

                case TaskType_MifareDesfireTask.DeleteApplication:
                    OnNewDeleteSignleCardApplicationCommand();
                    break;

                case TaskType_MifareDesfireTask.DeleteFile:
                    OnNewDeleteFileCommand();
                    break;

                case TaskType_MifareDesfireTask.FormatDesfireCard:
                    OnNewFormatDesfireCardCommand();
                    break;

                case TaskType_MifareDesfireTask.PICCMasterKeyChangeover:
                    OnNewChangeMasterCardKeyCommand();
                    break;

                case TaskType_MifareDesfireTask.ReadAppSettings:
                    ReadAppSettingsCommand();
                    break;

                case TaskType_MifareDesfireTask.ReadData:
                    OnNewReadDataCommand();
                    break;

                case TaskType_MifareDesfireTask.WriteData:
                    OnNewWriteDataCommand();
                    break;

                default:
                    break;
            }

        }
        /// <summary>
        /// return new RelayCommand<LibLogicalAccessProvider>((_device) => OnNewCreateAppCommand(_device));
        /// </summary>
        public ICommand CreateAppCommand { get { return new RelayCommand(OnNewCreateAppCommand); } }
        private void OnNewCreateAppCommand()
        {
            CurrentTaskErrorLevel = ERROR.Empty;

            Task desfireTask =
                new Task(() =>
                         {
                             using (ReaderDevice device = ReaderDevice.Instance)
                             {
                                 if (device != null)
                                 {
                                     StatusText = string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxDllLoaded"));

                                     if (CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireAppKeyCurrent) == KEY_ERROR.NO_ERROR)
                                     {
                                         ERROR result = device.AuthToMifareDesfireApplication(
                                                   DesfireMasterKeyCurrent,
                                                   SelectedDesfireMasterKeyEncryptionTypeCurrent,
                                                   0);

                                         if (IsValidAppNumberNew != false && result == ERROR.NoError)
                                         {
                                             StatusText += string.Format("{0}: Successfully Authenticated to App 0\n", DateTime.Now);

                                             DESFireKeySettings keySettings;
                                             keySettings = (DESFireKeySettings)SelectedDesfireAppKeySettingsCreateNewApp;

                                             keySettings |= IsAllowChangeMKChecked ? (DESFireKeySettings)1 : (DESFireKeySettings)0;
                                             keySettings |= IsAllowListingWithoutMKChecked ? (DESFireKeySettings)2 : (DESFireKeySettings)0;
                                             keySettings |= IsAllowCreateDelWithoutMKChecked ? (DESFireKeySettings)4 : (DESFireKeySettings)0;
                                             keySettings |= IsAllowConfigChangableChecked ? (DESFireKeySettings)8 : (DESFireKeySettings)0;

                                             result = device.CreateMifareDesfireApplication(
                                                 DesfireMasterKeyCurrent,
                                                 keySettings,
                                                 SelectedDesfireMasterKeyEncryptionTypeCurrent,
                                                 SelectedDesfireAppKeyEncryptionTypeCreateNewApp,
                                                 selectedDesfireAppMaxNumberOfKeysAsInt,
                                                 AppNumberNewAsInt);

                                             if (result == ERROR.NoError)
                                             {
                                                 StatusText += string.Format("{0}: Successfully Created AppID {1}\n", DateTime.Now, AppNumberNewAsInt);
                                                 CurrentTaskErrorLevel = result;
                                                 return;
                                             }
                                             else
                                             {
                                                 StatusText += string.Format("{0}: Unable to Create App: {1}\n", DateTime.Now, result.ToString());
                                                 CurrentTaskErrorLevel = result;
                                                 return;
                                             }
                                         }
                                         else
                                         {
                                             StatusText += string.Format("{0}: Authentication to PICC failed. Try without Authentication...\n", DateTime.Now);

                                             DESFireKeySettings keySettings;
                                             keySettings = (DESFireKeySettings)SelectedDesfireAppKeySettingsCreateNewApp;

                                             keySettings |= IsAllowChangeMKChecked ? (DESFireKeySettings)1 : (DESFireKeySettings)0;
                                             keySettings |= IsAllowListingWithoutMKChecked ? (DESFireKeySettings)2 : (DESFireKeySettings)0;
                                             keySettings |= IsAllowCreateDelWithoutMKChecked ? (DESFireKeySettings)4 : (DESFireKeySettings)0;
                                             keySettings |= IsAllowConfigChangableChecked ? (DESFireKeySettings)8 : (DESFireKeySettings)0;

                                             result = device.CreateMifareDesfireApplication(
                                                   DesfireMasterKeyCurrent,
                                                   keySettings,
                                                   SelectedDesfireMasterKeyEncryptionTypeCurrent,
                                                   SelectedDesfireAppKeyEncryptionTypeCreateNewApp,
                                                   selectedDesfireAppMaxNumberOfKeysAsInt,
                                                   AppNumberNewAsInt, false);

                                             if (result == ERROR.NoError)
                                             {
                                                 StatusText += string.Format("{0}: Successfully Created AppID {1}\n", DateTime.Now, AppNumberNewAsInt);
                                                 CurrentTaskErrorLevel = result;
                                                 return;
                                             }
                                             else
                                             {
                                                 StatusText += string.Format("{0}: Unable to create App: {1}\n", DateTime.Now, result.ToString());
                                                 CurrentTaskErrorLevel = result;
                                                 return;
                                             }
                                         }
                                     }
                                 }
                                 else
                                 {
                                     CurrentTaskErrorLevel = ERROR.NotReadyError;
                                     return;
                                 }
                             }
                         });

            if (CurrentTaskErrorLevel == ERROR.Empty)
            {
                desfireTask.ContinueWith((x) =>
                                         {
                                             if (CurrentTaskErrorLevel == ERROR.NoError)
                                             {
                                                 IsTaskCompletedSuccessfully = true;
                                             }
                                             else
                                             {
                                                 IsTaskCompletedSuccessfully = false;
                                             }
                                         });

                desfireTask.RunSynchronously();
            }


            return;
        }

        /// <summary>
        /// return new RelayCommand<LibLogicalAccessProvider>((_device) => OnNewCreateAppCommand(_device));
        /// </summary>
        public ICommand CreateFileCommand { get { return new RelayCommand(OnNewCreateFileCommand); } }
        private void OnNewCreateFileCommand()
        {
            CurrentTaskErrorLevel = ERROR.Empty;

            accessRights.changeAccess = SelectedDesfireFileAccessRightChange;
            accessRights.readAccess = SelectedDesfireFileAccessRightRead;
            accessRights.writeAccess = SelectedDesfireFileAccessRightWrite;
            accessRights.readAndWriteAccess = SelectedDesfireFileAccessRightReadWrite;

            Task desfireTask =
                new Task(() =>
                         {
                             using (ReaderDevice device = ReaderDevice.Instance)
                             {
                                 if (device != null)
                                 {
                                     StatusText = string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxDllLoaded"));

                                     if (CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireAppKeyCurrent) == KEY_ERROR.NO_ERROR && IsValidAppNumberNew != false)
                                     {
                                         ERROR result = device.AuthToMifareDesfireApplication(
                                                 DesfireAppKeyCurrent,
                                                 SelectedDesfireAppKeyEncryptionTypeCurrent,
                                                 selectedDesfireAppKeyNumberCurrentAsInt, AppNumberCurrentAsInt);

                                         if (result == ERROR.NoError)
                                         {
                                             StatusText += string.Format("{0}: Successfully Authenticated to App {1}\n", DateTime.Now, AppNumberCurrentAsInt);

                                             result = device.CreateMifareDesfireFile(
                                                DesfireAppKeyCurrent,
                                                SelectedDesfireAppKeyEncryptionTypeCurrent,
                                                SelectedDesfireFileType,
                                                accessRights,
                                                SelectedDesfireFileCryptoMode,
                                                AppNumberCurrentAsInt,
                                                FileNumberCurrentAsInt,
                                                FileSizeCurrentAsInt);

                                             if (result == ERROR.NoError)
                                             {
                                                 StatusText += string.Format("{0}: Successfully Created FileNo: {1} with Size: {2} in AppID: {3}\n", DateTime.Now, FileNumberCurrentAsInt, FileSizeCurrentAsInt, AppNumberNewAsInt);
                                                 CurrentTaskErrorLevel = result;
                                                 return;
                                             }
                                             else
                                             {
                                                 StatusText += string.Format("{0}: Unable to Create File: {1}\n", DateTime.Now, result.ToString());
                                                 CurrentTaskErrorLevel = result;
                                                 return;
                                             }
                                         }

                                         else
                                         {
                                             StatusText += string.Format("{0}: Unable to Authenticate to App {1}; Try to Continue Anyway...\n", DateTime.Now, AppNumberCurrentAsInt);

                                             result = device.CreateMifareDesfireFile(
                                                   DesfireAppKeyCurrent,
                                                   SelectedDesfireAppKeyEncryptionTypeCurrent,
                                                   SelectedDesfireFileType,
                                                   accessRights,
                                                   SelectedDesfireFileCryptoMode,
                                                   AppNumberCurrentAsInt,
                                                   FileNumberCurrentAsInt,
                                                   FileSizeCurrentAsInt);

                                             if (result == ERROR.NoError)
                                             {
                                                 StatusText += string.Format("{0}: Successfully Created FileNo: {1} with Size: {2} in AppID: {3}\n", DateTime.Now, FileNumberCurrentAsInt, FileSizeCurrentAsInt, AppNumberNewAsInt);
                                                 CurrentTaskErrorLevel = result;
                                                 return;
                                             }
                                             else
                                             {
                                                 StatusText += string.Format("{0}: Unable to Create File: {1}\n", DateTime.Now, result.ToString());
                                                 CurrentTaskErrorLevel = result;
                                                 return;
                                             }
                                         }
                                     }
                                 }
                                 else
                                 {
                                     CurrentTaskErrorLevel = ERROR.NotReadyError;
                                     return;
                                 }
                             }
                         });

            if (CurrentTaskErrorLevel == ERROR.Empty)
            {
                desfireTask.ContinueWith((x) =>
                                         {
                                             if (CurrentTaskErrorLevel == ERROR.NoError)
                                             {
                                                 IsTaskCompletedSuccessfully = true;
                                             }
                                             else
                                             {
                                                 IsTaskCompletedSuccessfully = false;
                                             }
                                         });

                desfireTask.RunSynchronously();
            }


            return;
        }

        /// <summary>
        /// 
        /// </summary>
        public ICommand ReadDataCommand { get { return new RelayCommand(OnNewReadDataCommand); } }
        private void OnNewReadDataCommand()
        {
            CurrentTaskErrorLevel = ERROR.Empty;

            Task desfireTask =
                new Task(() =>
                         {
                             using (ReaderDevice device = ReaderDevice.Instance)
                             {
                                 if (device != null)
                                 {
                                     StatusText = string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxDllLoaded"));

                                     if (CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireAppKeyCurrent) == KEY_ERROR.NO_ERROR)
                                     {
                                         ERROR result = device.AuthToMifareDesfireApplication(
                                                 DesfireReadKeyCurrent,
                                                 SelectedDesfireReadKeyEncryptionType,
                                                 selectedDesfireReadKeyNumberAsInt, AppNumberCurrentAsInt);


                                         if (IsValidAppNumberNew != false && result == ERROR.NoError)
                                         {
                                             StatusText += string.Format("{0}: Successfully Authenticated to App {1}\n", DateTime.Now, AppNumberCurrentAsInt);

                                             result = device.ReadMiFareDESFireChipFile(DesfireAppKeyCurrent, SelectedDesfireAppKeyEncryptionTypeCurrent,
                                                                                    DesfireReadKeyCurrent, SelectedDesfireReadKeyEncryptionType, selectedDesfireReadKeyNumberAsInt,
                                                                                    DesfireWriteKeyCurrent, SelectedDesfireWriteKeyEncryptionType, selectedDesfireWriteKeyNumberAsInt,
                                                                                    EncryptionMode.CM_ENCRYPT, FileNumberCurrentAsInt, AppNumberCurrentAsInt, FileSizeCurrentAsInt);

                                             if (result == ERROR.NoError)
                                             {
                                                 FileSizeCurrent = device.MifareDESFireData.Length.ToString();

                                                 StatusText += string.Format("{0}: Successfully Read {2} Bytes Data from FileNo: {1} in AppID: {3}\n", DateTime.Now, FileNumberCurrentAsInt, FileSizeCurrentAsInt, AppNumberNewAsInt);

                                                 childNodeViewModelFromChip.Children.Single(x => x.DesfireFile != null).DesfireFile = new MifareDesfireFileModel(device.MifareDESFireData, (byte)FileNumberCurrentAsInt);

                                                 childNodeViewModelTemp.Children.Single(x => x.DesfireFile != null).DesfireFile = new MifareDesfireFileModel(device.MifareDESFireData, (byte)FileNumberCurrentAsInt);

                                                 CurrentTaskErrorLevel = result;

                                                 OnPropertyChanged(nameof(ChildNodeViewModelTemp));
                                                 OnPropertyChanged(nameof(ChildNodeViewModelFromChip));

                                                 return;
                                             }
                                             else
                                             {
                                                 StatusText += string.Format("{0}: Unable to Read File with FileID: {1}: {2}", DateTime.Now, FileNumberCurrentAsInt, result.ToString());
                                                 CurrentTaskErrorLevel = result;
                                                 return;
                                             }
                                         }
                                         else
                                         {
                                             StatusText += string.Format("{0}: Unable to Read File: {1}", DateTime.Now, result.ToString());
                                             CurrentTaskErrorLevel = result;
                                             return;
                                         }
                                     }
                                 }
                                 else
                                 {
                                     CurrentTaskErrorLevel = ERROR.NotReadyError;
                                     return;
                                 }
                             }
                         });

            if (CurrentTaskErrorLevel == ERROR.Empty)
            {
                desfireTask.ContinueWith((x) =>
                                         {
                                             if (CurrentTaskErrorLevel == ERROR.NoError)
                                             {
                                                 IsTaskCompletedSuccessfully = true;
                                             }
                                             else
                                             {
                                                 IsTaskCompletedSuccessfully = false;
                                             }
                                         });

                desfireTask.RunSynchronously();
            }


            return;
        }

        /// <summary>
        /// 
        /// </summary>
        public ICommand GetDataFromFileCommand { get { return new RelayCommand(OnNewGetDataFromFileCommand); } }
        private void OnNewGetDataFromFileCommand()
        {
            var dlg = new OpenFileDialogViewModel
            {
                Title = ResourceLoader.GetResource("windowCaptionOpenProject"),
                Multiselect = false
            };

            if (dlg.Show(this.Dialogs) && dlg.FileName != null)
            {
                try
                {

                    var data = File.ReadAllText(dlg.FileName);
                    int err;


                    childNodeViewModelTemp.Children.Single(x => x.DesfireFile != null).DesfireFile = new MifareDesfireFileModel((CustomConverter.GetBytes(data, out err)), 0);

                    OnPropertyChanged(nameof(ChildNodeViewModelTemp));
                    OnPropertyChanged(nameof(ChildNodeViewModelFromChip));
                }
                catch (Exception e)
                {
                    LogWriter.CreateLogEntry(e, FacilityName);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public ICommand WriteDataCommand { get { return new RelayCommand(OnNewWriteDataCommand); } }
        private void OnNewWriteDataCommand()
        {
            CurrentTaskErrorLevel = ERROR.Empty;

            Task desfireTask =
                new Task(() =>
                         {
                             using (ReaderDevice device = ReaderDevice.Instance)
                             {
                                 if (device != null)
                                 {
                                     StatusText = string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxDllLoaded"));

                                     if (CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireAppKeyCurrent) == KEY_ERROR.NO_ERROR)
                                     {

                                         ERROR result = device.AuthToMifareDesfireApplication(
                                                                                          DesfireWriteKeyCurrent,
                                                                                          SelectedDesfireWriteKeyEncryptionType,
                                                                                          selectedDesfireWriteKeyNumberAsInt, AppNumberCurrentAsInt);

                                         if (IsValidAppNumberNew != false && result == ERROR.NoError)
                                         {
                                             StatusText += string.Format("{0}: Successfully Authenticated to App {1}\n", DateTime.Now, AppNumberCurrentAsInt);

                                             result = device.WriteMiFareDESFireChipFile(DesfireAppKeyCurrent, SelectedDesfireAppKeyEncryptionTypeCurrent,
                                                                                    DesfireAppKeyCurrent, SelectedDesfireAppKeyEncryptionTypeCurrent,
                                                                                    DesfireReadKeyCurrent, SelectedDesfireReadKeyEncryptionType, selectedDesfireReadKeyNumberAsInt,
                                                                                    DesfireWriteKeyCurrent, SelectedDesfireWriteKeyEncryptionType, selectedDesfireWriteKeyNumberAsInt,
                                                                                    EncryptionMode.CM_ENCRYPT, FileNumberCurrentAsInt, AppNumberCurrentAsInt, childNodeViewModelTemp.Children.Single(x => x.DesfireFile != null).DesfireFile.Data);

                                             if (result == ERROR.NoError)
                                             {
                                                 StatusText += string.Format("{0}: Successfully Created FileNo: {1} with Size: {2} in AppID: {3}\n", DateTime.Now, FileNumberCurrentAsInt, FileSizeCurrentAsInt, AppNumberNewAsInt);
                                                 CurrentTaskErrorLevel = result;
                                                 return;
                                             }
                                             else
                                             {
                                                 StatusText += string.Format("{0}: Unable to Write Data: {1}\n", DateTime.Now, result.ToString());
                                                 CurrentTaskErrorLevel = result;
                                                 return;
                                             }
                                         }
                                         else
                                         {
                                             StatusText += string.Format("{0}: Unable to Write Data: {1}\n", DateTime.Now, result.ToString());
                                             CurrentTaskErrorLevel = result;
                                             return;
                                         }
                                     }
                                 }
                                 else
                                 {
                                     CurrentTaskErrorLevel = ERROR.NotReadyError;
                                     return;
                                 }
                             }
                         });

            if (CurrentTaskErrorLevel == ERROR.Empty)
            {
                desfireTask.ContinueWith((x) =>
                                         {
                                             if (CurrentTaskErrorLevel == ERROR.NoError)
                                             {
                                                 IsTaskCompletedSuccessfully = true;
                                             }
                                             else
                                             {
                                                 IsTaskCompletedSuccessfully = false;
                                             }
                                         });

                desfireTask.RunSynchronously();
            }


            return;
        }

        /// <summary>
        ///
        /// </summary>
        public ICommand ChangeAppKeyCommand { get { return new RelayCommand(OnNewChangeAppKeyCommand); } }
        private void OnNewChangeAppKeyCommand()
        {
            CurrentTaskErrorLevel = ERROR.Empty;


            Task desfireTask = new Task(() =>
                                        {
                                            using (ReaderDevice device = ReaderDevice.Instance)
                                            {
                                                if (device != null)
                                                {
                                                    StatusText = string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxDllLoaded"));

                                                    if (CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireAppKeyCurrent) == KEY_ERROR.NO_ERROR)
                                                    {
                                                        ERROR result = device.AuthToMifareDesfireApplication(
                                                                DesfireAppKeyCurrent,
                                                                SelectedDesfireAppKeyEncryptionTypeCurrent,
                                                                selectedDesfireAppKeyNumberCurrentAsInt,
                                                                AppNumberCurrentAsInt);

                                                        if (IsValidAppNumberCurrent != false &&
                                                            IsValidAppNumberTarget != false &&
                                                            IsValidDesfireAppKeyTarget != false &&
                                                            result == ERROR.NoError)
                                                        {
                                                            StatusText += string.Format("{0}: Successfully Authenticated to AppID {1}\n", DateTime.Now, AppNumberCurrentAsInt);

                                                            DESFireKeySettings keySettings = DESFireKeySettings.KS_DEFAULT;
                                                            keySettings = (DESFireKeySettings)SelectedDesfireAppKeySettingsCreateNewApp;

                                                            keySettings |= IsAllowChangeMKChecked ? (DESFireKeySettings)1 : (DESFireKeySettings)0;
                                                            keySettings |= IsAllowListingWithoutMKChecked ? (DESFireKeySettings)2 : (DESFireKeySettings)0;
                                                            keySettings |= IsAllowCreateDelWithoutMKChecked ? (DESFireKeySettings)4 : (DESFireKeySettings)0;
                                                            keySettings |= IsAllowConfigChangableChecked ? (DESFireKeySettings)8 : (DESFireKeySettings)0;

                                                            result = device.ChangeMifareDesfireApplicationKey(DesfireAppKeyCurrent,
                                                                                                         selectedDesfireAppKeyNumberCurrentAsInt,
                                                                                                         SelectedDesfireAppKeyEncryptionTypeCurrent,
                                                                                                         DesfireAppKeyTarget,
                                                                                                         selectedDesfireAppKeyNumberTargetAsInt,
                                                                                                         selectedDesfireAppKeyVersionTargetAsInt,
                                                                                                         SelectedDesfireAppKeyEncryptionTypeTarget,
                                                                                                         AppNumberCurrentAsInt, AppNumberTargetAsInt, keySettings, keyVersionCurrentAsInt);

                                                            if (result == ERROR.NoError)
                                                            {
                                                                StatusText += string.Format("{0}: Successfully Changed Key {1} of AppID {2}\n", DateTime.Now, selectedDesfireAppKeyNumberTargetAsInt, AppNumberTargetAsInt);
                                                                CurrentTaskErrorLevel = result;
                                                                return;
                                                            }
                                                            else
                                                            {
                                                                StatusText += string.Format("{0}: Unable to Change Key {1} of AppID {2}: {3}\n", DateTime.Now, selectedDesfireAppKeyNumberCurrentAsInt, AppNumberTargetAsInt, result.ToString());
                                                                CurrentTaskErrorLevel = result;
                                                                return;
                                                            }
                                                        }
                                                        else
                                                        {
                                                            StatusText += string.Format("{0}: Unable to Change Key {1} of AppID {2}: {3}\n", DateTime.Now, selectedDesfireAppKeyNumberCurrentAsInt, AppNumberTargetAsInt, result.ToString());
                                                            CurrentTaskErrorLevel = result;
                                                            return;
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    CurrentTaskErrorLevel = ERROR.NotReadyError;
                                                    return;
                                                }
                                            }
                                        });

            if (CurrentTaskErrorLevel == ERROR.Empty)
            {
                desfireTask.ContinueWith((x) =>
                                         {
                                             if (CurrentTaskErrorLevel == ERROR.NoError)
                                             {
                                                 IsTaskCompletedSuccessfully = true;
                                             }
                                             else
                                             {
                                                 IsTaskCompletedSuccessfully = false;
                                             }
                                         });

                desfireTask.RunSynchronously();
            }


            return;
        }

        /// <summary>
        ///
        /// </summary>
        public ICommand DeleteSignleCardApplicationCommand => new RelayCommand(OnNewDeleteSignleCardApplicationCommand);
        private void OnNewDeleteSignleCardApplicationCommand()
        {
            CurrentTaskErrorLevel = ERROR.Empty;

            Task desfireTask = new Task(() =>
                                        {
                                            using (ReaderDevice device = ReaderDevice.Instance)
                                            {
                                                if (device != null)
                                                {
                                                    StatusText = string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxDllLoaded"));

                                                    if (CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireAppKeyCurrent) == KEY_ERROR.NO_ERROR)
                                                    {
                                                        ERROR result = device.AuthToMifareDesfireApplication(
                                                                DesfireMasterKeyCurrent,
                                                                SelectedDesfireMasterKeyEncryptionTypeCurrent,
                                                                0);

                                                        if (IsValidAppNumberCurrent != false && result == ERROR.NoError)
                                                        {
                                                            StatusText += string.Format("{0}: Successfully Authenticated to PICC Master App 0\n", DateTime.Now);

                                                            result = device.DeleteMifareDesfireApplication(
                                                                DesfireMasterKeyCurrent,
                                                                SelectedDesfireMasterKeyEncryptionTypeCurrent,
                                                                AppNumberNewAsInt);

                                                            if (result == ERROR.NoError)
                                                            {
                                                                StatusText += string.Format("{0}: Successfully Deleted AppID {1}\n", DateTime.Now, AppNumberNewAsInt);
                                                                CurrentTaskErrorLevel = result;
                                                                return;
                                                            }
                                                            else
                                                            {
                                                                StatusText += string.Format("{0}: Unable to Remove AppID {1}: {2}\n", DateTime.Now, AppNumberNewAsInt, result.ToString());
                                                                CurrentTaskErrorLevel = result;
                                                                return;
                                                            }
                                                        }

                                                        else
                                                        {
                                                            StatusText += string.Format("{0}: Authentication to PICC failed. Try without Authentication...\n", DateTime.Now);

                                                            result = device.DeleteMifareDesfireApplication(
                                                                DesfireMasterKeyCurrent,
                                                                SelectedDesfireMasterKeyEncryptionTypeCurrent,
                                                                AppNumberNewAsInt);

                                                            if (result == ERROR.NoError)
                                                            {
                                                                StatusText += string.Format("{0}: Successfully deleted AppID {1}\n", DateTime.Now, AppNumberNewAsInt);
                                                                CurrentTaskErrorLevel = result;
                                                                return;
                                                            }
                                                            else
                                                            {
                                                                StatusText += string.Format("{0}: Unable to deleted App: {1}\n", DateTime.Now, result.ToString());
                                                                CurrentTaskErrorLevel = result;
                                                                return;
                                                            }
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    CurrentTaskErrorLevel = ERROR.NotReadyError;
                                                    return;
                                                }
                                            }
                                        });

            if (CurrentTaskErrorLevel == ERROR.Empty)
            {
                desfireTask.ContinueWith((x) =>
                                         {
                                             if (CurrentTaskErrorLevel == ERROR.NoError)
                                             {
                                                 IsTaskCompletedSuccessfully = true;
                                             }
                                             else
                                             {
                                                 IsTaskCompletedSuccessfully = false;
                                             }
                                         });

                desfireTask.RunSynchronously();
            }


            return;
        }

        /// <summary>
        /// 
        /// </summary>
        public ICommand DeleteFileCommand => new RelayCommand(OnNewDeleteFileCommand);
        private void OnNewDeleteFileCommand()
        {
            CurrentTaskErrorLevel = ERROR.Empty;

            Task desfireTask = new Task(() =>
                                        {
                                            using (ReaderDevice device = ReaderDevice.Instance)
                                            {
                                                if (device != null)
                                                {
                                                    StatusText = string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxDllLoaded"));

                                                    if (CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireAppKeyCurrent) == KEY_ERROR.NO_ERROR)
                                                    {
                                                        ERROR result = device.AuthToMifareDesfireApplication(
                                                                DesfireAppKeyCurrent,
                                                                SelectedDesfireMasterKeyEncryptionTypeCurrent,
                                                                selectedDesfireAppKeyNumberCurrentAsInt, AppNumberCurrentAsInt);

                                                        if (IsValidAppNumberCurrent != false && result == ERROR.NoError)
                                                        {

                                                            StatusText += string.Format("{0}: Successfully Authenticated to App {1}\n", DateTime.Now, AppNumberCurrentAsInt);

                                                            result = device.DeleteMifareDesfireFile(
                                                                DesfireAppKeyCurrent,
                                                                SelectedDesfireAppKeyEncryptionTypeCurrent,
                                                                AppNumberCurrentAsInt, FileNumberCurrentAsInt);

                                                            if (result == ERROR.NoError)
                                                            {
                                                                StatusText += string.Format("{0}: Successfully Deleted File {1}\n", DateTime.Now, FileNumberCurrentAsInt);
                                                                CurrentTaskErrorLevel = result;
                                                                return;
                                                            }
                                                            else
                                                            {
                                                                StatusText += string.Format("{0}: Unable to Remove FileID {1}: {2}\n", DateTime.Now, FileNumberCurrentAsInt, result.ToString());
                                                                CurrentTaskErrorLevel = result;
                                                                return;
                                                            }
                                                        }
                                                        else
                                                        {
                                                            StatusText += string.Format("{0}: Unable to Authenticate to App {1}; Try to Continue Anyway...\n", DateTime.Now, AppNumberCurrentAsInt);

                                                            result = device.DeleteMifareDesfireFile(
                                                                DesfireAppKeyCurrent,
                                                                SelectedDesfireAppKeyEncryptionTypeCurrent,
                                                                AppNumberNewAsInt, FileNumberCurrentAsInt);

                                                            if (result == ERROR.NoError)
                                                            {
                                                                StatusText += string.Format("{0}: Successfully Deleted File {1}\n", DateTime.Now, FileNumberCurrentAsInt);
                                                                CurrentTaskErrorLevel = result;
                                                                return;
                                                            }
                                                            else
                                                            {
                                                                StatusText += string.Format("{0}: Unable to Remove AppID {1}: {2}\n", DateTime.Now, AppNumberNewAsInt, result.ToString());
                                                                CurrentTaskErrorLevel = result;
                                                                return;
                                                            }
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    CurrentTaskErrorLevel = ERROR.NotReadyError;
                                                    return;
                                                }
                                            }
                                        });

            if (CurrentTaskErrorLevel == ERROR.Empty)
            {
                desfireTask.ContinueWith((x) =>
                                         {
                                             if (CurrentTaskErrorLevel == ERROR.NoError)
                                             {
                                                 IsTaskCompletedSuccessfully = true;
                                             }
                                             else
                                             {
                                                 IsTaskCompletedSuccessfully = false;
                                             }
                                         });

                desfireTask.RunSynchronously();
            }


            return;
        }

        /// <summary>
        /// public ICommand FormatDesfireCardCommand { get { return new RelayCommand<ReaderDevice>((_device) => OnNewFormatDesfireCardCommand(_device)); }}
        /// </summary>
        public ICommand FormatDesfireCardCommand => new RelayCommand(OnNewFormatDesfireCardCommand);
        private void OnNewFormatDesfireCardCommand()
        {
            CurrentTaskErrorLevel = ERROR.Empty;

            Task desfireTask = new Task(() =>
                                        {
                                            using (ReaderDevice device = ReaderDevice.Instance)
                                            {
                                                if (device != null)
                                                {
                                                    StatusText = string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxDllLoaded"));

                                                    if (CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireAppKeyCurrent) == KEY_ERROR.NO_ERROR)
                                                    {
                                                        ERROR result = device.AuthToMifareDesfireApplication(
                                                                DesfireMasterKeyCurrent,
                                                                SelectedDesfireMasterKeyEncryptionTypeCurrent,
                                                                0);

                                                        if (IsValidAppNumberCurrent != false && result == ERROR.NoError)
                                                        {
                                                            StatusText += string.Format("{0}: Successfully Authenticated to PICC Master App 0\n", DateTime.Now);

                                                            result = device.GetMiFareDESFireChipAppIDs(
                                                                DesfireMasterKeyCurrent,
                                                                SelectedDesfireMasterKeyEncryptionTypeCurrent);

                                                            if (result == ERROR.NoError)
                                                            {
                                                                if (device?.DesfireChip?.AppIDs != null)
                                                                {
                                                                    foreach (uint appID in device.DesfireChip.AppIDs)
                                                                    {
                                                                        StatusText += string.Format("{0}: FoundAppID {1}\n", DateTime.Now, appID);
                                                                    }
                                                                }

                                                                result = device.FormatDesfireCard(DesfireMasterKeyCurrent, SelectedDesfireMasterKeyEncryptionTypeCurrent);

                                                                if (result == ERROR.NoError)
                                                                {
                                                                    StatusText += string.Format("{0}: Successfully Formatted Card\n", DateTime.Now);
                                                                    CurrentTaskErrorLevel = result;
                                                                    return;
                                                                }

                                                                else
                                                                {
                                                                    StatusText += string.Format("{0}: Unable to Format Card: {1}\n", DateTime.Now, result.ToString());
                                                                    CurrentTaskErrorLevel = result;
                                                                    return;
                                                                }
                                                            }

                                                            else
                                                            {
                                                                StatusText += string.Format("{0}: Unable to get Directory Listing, Try to Continue anyway...\n", DateTime.Now);

                                                                result = device.FormatDesfireCard(DesfireMasterKeyCurrent, SelectedDesfireMasterKeyEncryptionTypeCurrent);

                                                                if (result == ERROR.NoError)
                                                                {
                                                                    StatusText += string.Format("{0}: Successfully Formatted Card\n", DateTime.Now);
                                                                    CurrentTaskErrorLevel = result;
                                                                    return;
                                                                }

                                                                else
                                                                {
                                                                    StatusText += string.Format("{0}: Unable to Format Card: {1}\n", DateTime.Now, result.ToString());
                                                                    CurrentTaskErrorLevel = result;
                                                                    return;
                                                                }
                                                            }
                                                        }
                                                        else
                                                        {
                                                            StatusText += string.Format("{0}: Unable to Format Card: {1}\n", DateTime.Now, result.ToString());
                                                            CurrentTaskErrorLevel = result;
                                                            return;
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    CurrentTaskErrorLevel = ERROR.NotReadyError;
                                                    return;
                                                }
                                            }
                                            return;
                                        });

            if (CurrentTaskErrorLevel == ERROR.Empty)
            {
                desfireTask.ContinueWith((x) =>
                                         {
                                             if (CurrentTaskErrorLevel == ERROR.NoError)
                                             {
                                                 IsTaskCompletedSuccessfully = true;
                                             }
                                             else
                                             {
                                                 IsTaskCompletedSuccessfully = false;
                                             }
                                         });

                desfireTask.RunSynchronously();
            }


            return;
        }

        /// <summary>
        ///
        /// </summary>
        public ICommand AuthenticateToCardApplicationCommand => new RelayCommand(OnNewAuthenticateToCardApplicationCommand);
        private void OnNewAuthenticateToCardApplicationCommand()
        {
            CurrentTaskErrorLevel = ERROR.Empty;

            if (SelectedTaskType == TaskType_MifareDesfireTask.ReadAppSettings)
            {
                return;
            }

            else if (SelectedTaskType == TaskType_MifareDesfireTask.AppExistCheck)
            {
                DoesAppExistCommand(new MifareDesfireChipModel());
                return;
            }

            Task desfireTask = new Task(() =>
            {
                using (ReaderDevice device = ReaderDevice.Instance)
                {
                    if (device != null)
                    {
                        StatusText = string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxDllLoaded"));

                        if (CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireAppKeyCurrent) == KEY_ERROR.NO_ERROR)
                        {
                            ERROR result = device.AuthToMifareDesfireApplication(
                                    DesfireAppKeyCurrent,
                                    SelectedDesfireAppKeyEncryptionTypeCurrent,
                                    selectedDesfireAppKeyNumberCurrentAsInt,
                                    AppNumberCurrentAsInt);

                            if (IsValidAppNumberCurrent != false && result == ERROR.NoError)
                            {
                                StatusText += string.Format("{0}: Successfully Authenticated to App {1}\n", DateTime.Now, AppNumberCurrentAsInt);
                                CurrentTaskErrorLevel = result;
                            }
                            else
                            {
                                StatusText += string.Format("{0}: Unable to Authenticate: {1}\n", DateTime.Now, result.ToString());
                                CurrentTaskErrorLevel = result;
                            }

                        }
                    }
                }
                return;
            });

            if (CurrentTaskErrorLevel == ERROR.Empty)
            {
                desfireTask.ContinueWith((x) =>
                {
                    if (CurrentTaskErrorLevel == ERROR.NoError)
                    {
                        IsTaskCompletedSuccessfully = true;
                    }
                    else
                    {
                        IsTaskCompletedSuccessfully = false;
                    }
                });

                desfireTask.RunSynchronously();
            }
            return;
        }

        /// <summary>
        ///
        /// </summary>
        public ICommand ChangeMasterCardKeyCommand => new RelayCommand(OnNewChangeMasterCardKeyCommand);
        private void OnNewChangeMasterCardKeyCommand()
        {
            CurrentTaskErrorLevel = ERROR.Empty;

            Task desfireTask = new Task(
                () =>
                {
                    using (ReaderDevice device = ReaderDevice.Instance)
                    {
                        if (device != null)
                        {
                            StatusText = string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxDllLoaded"));

                            if (CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireMasterKeyCurrent) == KEY_ERROR.NO_ERROR)
                            {
                                DESFireKeySettings keySettings;
                                keySettings = (DESFireKeySettings)SelectedDesfireAppKeySettingsCreateNewApp;

                                keySettings |= IsAllowChangeMKChecked ? (DESFireKeySettings)1 : (DESFireKeySettings)0;
                                keySettings |= IsAllowListingWithoutMKChecked ? (DESFireKeySettings)2 : (DESFireKeySettings)0;
                                keySettings |= IsAllowCreateDelWithoutMKChecked ? (DESFireKeySettings)4 : (DESFireKeySettings)0;
                                keySettings |= IsAllowConfigChangableChecked ? (DESFireKeySettings)8 : (DESFireKeySettings)0;

                                ERROR result = device.AuthToMifareDesfireApplication(
                                    CustomConverter.DesfireKeyToCheck,
                                    SelectedDesfireMasterKeyEncryptionTypeCurrent,
                                    0);

                                if (result == ERROR.NoError)
                                {
                                    StatusText += string.Format("{0}: Successfully Authenticated to App 0\n", DateTime.Now);

                                    if (IsValidDesfireMasterKeyCurrent != false &&
                                        IsValidDesfireMasterKeyTarget != false)
                                    {
                                        result = device.ChangeMifareDesfireApplicationKey(
                                            DesfireMasterKeyCurrent,
                                            0,
                                            SelectedDesfireMasterKeyEncryptionTypeCurrent,
                                            DesfireMasterKeyTarget,
                                            selectedDesfireAppKeyNumberTargetAsInt,
                                            0,
                                            SelectedDesfireMasterKeyEncryptionTypeTarget, 0, 0, keySettings, keyVersionCurrentAsInt);

                                        if (result == ERROR.NoError)
                                        {
                                            StatusText += string.Format("{0}: Keychange Successfull\n", DateTime.Now);
                                            CurrentTaskErrorLevel = result;
                                            return;
                                        }
                                        else
                                        {
                                            StatusText += string.Format("{0}: Unable to Change Key: {1}\n", DateTime.Now, result.ToString());
                                            CurrentTaskErrorLevel = result;
                                            return;
                                        }
                                    }
                                    else
                                    {
                                        StatusText += string.Format("{0}: Key Error: Wrong Format\n", DateTime.Now);
                                        CurrentTaskErrorLevel = ERROR.AuthenticationError;
                                        return;
                                    }
                                }
                                else
                                {
                                    StatusText += string.Format("{0}: {1}: {2}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxUnableToAuthenticate"), result.ToString());
                                    CurrentTaskErrorLevel = result;
                                    return;
                                }
                            }
                        }
                        else
                        {
                            CurrentTaskErrorLevel = ERROR.NotReadyError;
                            return;
                        }
                    }
                });

            if (CurrentTaskErrorLevel == ERROR.Empty)
            {
                desfireTask.ContinueWith((x) =>
                                         {
                                             if (CurrentTaskErrorLevel == ERROR.NoError)
                                             {
                                                 IsTaskCompletedSuccessfully = true;
                                             }
                                             else
                                             {
                                                 IsTaskCompletedSuccessfully = false;
                                             }
                                         });

                desfireTask.RunSynchronously();
            }


            return;
        }

        /// <summary>
        ///
        /// </summary>
        public ICommand AuthenticateToCardMasterApplicationCommand => new RelayCommand(OnNewAuthenticateToCardMasterApplicationCommand);
        private void OnNewAuthenticateToCardMasterApplicationCommand()
        {
            CurrentTaskErrorLevel = ERROR.Empty;


            Task desfireTask = new Task(
                () =>
                {
                    using (ReaderDevice device = ReaderDevice.Instance)
                    {
                        if (device != null)
                        {
                            StatusText = string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxDllLoaded"));

                            if (CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireMasterKeyCurrent) == KEY_ERROR.NO_ERROR)
                            {
                                ERROR result = device.AuthToMifareDesfireApplication(
                                    DesfireMasterKeyCurrent,
                                    SelectedDesfireMasterKeyEncryptionTypeCurrent,
                                    0);

                                if (result == ERROR.NoError)
                                {
                                    StatusText += string.Format("{0}: Successfully Authenticated to App 0\n", DateTime.Now);
                                    CurrentTaskErrorLevel = result;
                                    return;
                                }
                                else
                                {
                                    StatusText += string.Format("{0}: {1}: {2}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxUnableToAuthenticate"), result.ToString());
                                    CurrentTaskErrorLevel = result;
                                    return;
                                }

                            }
                        }
                    }
                });

            if (CurrentTaskErrorLevel == ERROR.Empty)
            {

                desfireTask.ContinueWith((x) =>
                {
                    if (CurrentTaskErrorLevel == ERROR.NoError)
                    {
                        IsTaskCompletedSuccessfully = true;
                    }
                    else
                    {
                        IsTaskCompletedSuccessfully = false;
                    }
                });

                desfireTask.RunSynchronously();
            }


            return;

        }

        #endregion Commands

        #region Methods

        /// <summary>
        ///
        /// </summary>
        public void ReadAppSettingsCommand()
        {
            CurrentTaskErrorLevel = ERROR.Empty;

            Task desfireTask = new Task(() =>
            {
                using (ReaderDevice device = ReaderDevice.Instance)
                {
                    if (device != null)
                    {
                        StatusText = string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxDllLoaded"));

                        if (CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireAppKeyCurrent) == KEY_ERROR.NO_ERROR)
                        {
                            ERROR result = device.GetMifareDesfireAppSettings(
                                    DesfireMasterKeyTarget,
                                    SelectedDesfireAppKeyEncryptionTypeCurrent,
                                    selectedDesfireAppKeyNumberCurrentAsInt,
                                    AppNumberCurrentAsInt);

                            DESFireKeySettings keySettings;
                            keySettings = (DESFireKeySettings)SelectedDesfireAppKeySettingsCreateNewApp;

                            keySettings |= IsAllowChangeMKChecked ? (DESFireKeySettings)1 : (DESFireKeySettings)0;
                            keySettings |= IsAllowListingWithoutMKChecked ? (DESFireKeySettings)2 : (DESFireKeySettings)0;
                            keySettings |= IsAllowCreateDelWithoutMKChecked ? (DESFireKeySettings)4 : (DESFireKeySettings)0;
                            keySettings |= IsAllowConfigChangableChecked ? (DESFireKeySettings)8 : (DESFireKeySettings)0;

                            //desfireChip.FreeMemory = device.GenericChip.FreeMemory;
                            //desfireChip.UID = device.GenericChip.UID;

                            if (IsValidAppNumberCurrent != false && result == ERROR.NoError)
                            {
                                StatusText += string.Format("{0}: Successfully Read App Settings of App {1}\n", DateTime.Now, AppNumberCurrentAsInt);

                                if (((byte)device.DesfireAppKeySetting & (byte)keySettings) != 0)
                                {
                                    CurrentTaskErrorLevel = ERROR.NoError;
                                    return;
                                }
                                CurrentTaskErrorLevel = ERROR.IsNotTrue;
                            }
                            else
                            {
                                StatusText += string.Format("{0}: Unable to Authenticate: {1}\n", DateTime.Now, result.ToString());
                                CurrentTaskErrorLevel = result;
                            }

                        }
                    }
                }
                return;
            });

            if (CurrentTaskErrorLevel == ERROR.Empty)
            {
                desfireTask.ContinueWith((x) =>
                {
                    if (CurrentTaskErrorLevel == ERROR.NoError)
                    {
                        IsTaskCompletedSuccessfully = true;
                    }
                    else
                    {
                        IsTaskCompletedSuccessfully = false;
                    }
                });

                desfireTask.RunSynchronously();
            }

            return;
        }

        /// <summary>
        ///
        /// </summary>
        public void DoesAppExistCommand(MifareDesfireChipModel desfireChip)
        {
            CurrentTaskErrorLevel = ERROR.Empty;

            Task desfireTask = new Task(() =>
            {
                using (ReaderDevice device = ReaderDevice.Instance)
                {
                    if (device != null)
                    {
                        StatusText = string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxDllLoaded"));

                        if (CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireAppKeyCurrent) == KEY_ERROR.NO_ERROR)
                        {
                            ERROR result = device.GetMiFareDESFireChipAppIDs(
                                    DesfireAppKeyCurrent,
                                    SelectedDesfireAppKeyEncryptionTypeCurrent);

                            desfireChip.FreeMemory = device.DesfireChip.FreeMemory;
                            desfireChip.UID = device.GenericChip.UID;

                            // Check if specified App "AppNumberCurrentAsInt" exist
                            if (IsValidAppNumberCurrent != false && AppNumberCurrentAsInt > 0 && result == ERROR.NoError && Array.Exists<uint>(device.DesfireChip.AppIDs, x => x == (uint)AppNumberNewAsInt))
                            {
                                StatusText += string.Format("{0}: Success. App with ID:{1} exists\n", DateTime.Now, AppNumberNewAsInt);

                                CurrentTaskErrorLevel = ERROR.NoError;
                            }

                            // Check if ANY App exists
                            else if (IsValidAppNumberCurrent != false && AppNumberCurrentAsInt == 0 && result == ERROR.NoError && Array.Exists<uint>(device.DesfireChip.AppIDs, x => x > 0))
                            {
                                StatusText += string.Format("{0}: Success. Existing Apps Detected\n", DateTime.Now);

                                CurrentTaskErrorLevel = ERROR.NoError;
                            }

                            // Ooops: Iam not allowed to get the info or Key "DesfireAppKeyCurrent" with "SelectedDesfireAppKeyEncryptionTypeCurrent" is incorrect
                            else if (IsValidAppNumberCurrent != false && result == ERROR.AuthenticationError)
                            {
                                StatusText += string.Format("{0}: Failed. Directory Listing is not allowed and PICC MK is Incorrect.\n", DateTime.Now);

                                CurrentTaskErrorLevel = ERROR.AuthenticationError;
                            }

                            // There are no Apps
                            else
                            {
                                StatusText += string.Format("{0}: No Apps Found: {1}\n", DateTime.Now, result.ToString());

                                CurrentTaskErrorLevel = ERROR.IsNotTrue;
                            }

                        }
                    }
                }
                return;
            });

            if (CurrentTaskErrorLevel == ERROR.Empty)
            {
                desfireTask.ContinueWith((x) =>
                {
                    if (CurrentTaskErrorLevel == ERROR.NoError)
                    {
                        IsTaskCompletedSuccessfully = true;
                    }
                    else
                    {
                        IsTaskCompletedSuccessfully = false;
                    }
                });

                desfireTask.RunSynchronously();
            }

            return;
        }

        #endregion

        #region IUserDialogViewModel Implementation

        [XmlIgnore]
        public bool IsModal { get; private set; }

        public virtual void RequestClose()
        {
            if (OnCloseRequest != null)
            {
                OnCloseRequest(this);
            }
            else
            {
                Close();
            }
        }

        public event EventHandler DialogClosing;

        public ICommand OKCommand => new RelayCommand(Ok);
        private void Ok()
        {
            if (OnOk != null)
            {
                OnOk(this);
            }
            else
            {
                Close();
            }
        }

        public ICommand CancelCommand => new RelayCommand(Cancel);
        private void Cancel()
        {
            if (OnCancel != null)
            {
                OnCancel(this);
            }
            else
            {
                Close();
            }
        }

        [XmlIgnore]
        public Action<MifareDesfireSetupViewModel> OnOk { get; set; }

        [XmlIgnore]
        public Action<MifareDesfireSetupViewModel> OnCancel { get; set; }

        [XmlIgnore]
        public Action<MifareDesfireSetupViewModel> OnCloseRequest { get; set; }

        public void Close()
        {
            DialogClosing?.Invoke(this, new EventArgs());
        }

        public void Show(IList<IDialogViewModel> collection)
        {
            collection.Add(this);
        }

        #endregion IUserDialogViewModel Implementation

        #region Localization

        public string LocalizationResourceSet { get; set; }

        [XmlIgnore]
        public string Caption
        {
            get => _Caption;
            set
            {
                _Caption = value;
                OnPropertyChanged(nameof(Caption));
            }
        }
        private string _Caption;

        #endregion Localization
    }
}