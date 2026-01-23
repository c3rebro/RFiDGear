/*
 * Created by SharpDevelop.
 * Date: 10/11/2017
 * Time: 22:15
 *
 */

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using RFiDGear.Models;

using RFiDGear.UI.MVVMDialogs.ViewModels;

using System;
using System.ComponentModel.Composition;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Xml.Serialization;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Infrastructure.AccessControl;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.UI.UIExtensions.Interfaces;
using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;
using RFiDGear.Infrastructure.Tasks.Interfaces;
namespace RFiDGear.ViewModel.TaskSetupViewModels
{
    /// <summary>
    /// Description of MifareDesfireSetupViewModel.
    /// </summary>
    public class MifareDesfireSetupViewModel : ObservableObject, IUserDialogViewModel, IGenericTask
    {
        #region Fields
        private readonly EventLog eventLog = new EventLog("Application", ".", Assembly.GetEntryAssembly().GetName().Name);
        private readonly object editedTaskReference; // Tracks the original task instance during edit mode.
        private bool hasFinalizedTask;
        private string desfireDataFilePath;
        private bool refreshDesfireDataFromFileBeforeWrite;
        private string desfireReadDataFilePath;
        private bool overwriteReadDataFileOnRead;

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
            chip = new MifareDesfireChipModel(string.Format("Task Description: {0}", SelectedTaskDescription), CARD_TYPE.DESFireEV1);

            app = new MifareDesfireAppModel(0);

            childNodeViewModelFromChip = new RFiDChipChildLayerViewModel(app, null, chip.CardType, new ObservableCollection<IDialogViewModel>(), true);
            childNodeViewModelTemp = new RFiDChipChildLayerViewModel(app, null, chip.CardType, new ObservableCollection<IDialogViewModel>(), true);

            MifareDesfireKeys = CustomConverter.GenerateStringSequence(0, 16).ToArray();
            MifareDesfireKeyCount = CustomConverter.GenerateStringSequence(1, 16).ToArray();
            KeyVersionCurrent = "00";
            KeyVersionTarget = "00";
            SelectedDesfireAppKeyVersionTarget = "00";

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
                MefHelper.Instance.EnsureCompose();
                MefHelper.Instance.Container?.ComposeParts(this); //Load Plugins

                chip = new MifareDesfireChipModel(string.Format("Task Description: {0}", ""), CARD_TYPE.DESFireEV1);
                app = new MifareDesfireAppModel(0);

                childNodeViewModelFromChip = new RFiDChipChildLayerViewModel(app, null, chip.CardType, _dialogs, true);
                childNodeViewModelTemp = new RFiDChipChildLayerViewModel(app, null, chip.CardType, _dialogs, true);

                MifareDesfireKeys = CustomConverter.GenerateStringSequence(0, 16).ToArray();
                MifareDesfireKeyCount = CustomConverter.GenerateStringSequence(1, 16).ToArray();
                KeyVersionCurrent = "00";
                KeyVersionTarget = "00";
                SelectedDesfireAppKeyVersionTarget = "00";
                isAllowChangeMKChecked = true;
                isAllowConfigChangableChecked = true;
                isAllowListingWithoutMKChecked = true;

                if (_selectedSetupViewModel is MifareDesfireSetupViewModel)
                {
                    editedTaskReference = _selectedSetupViewModel;
                    var properties = typeof(MifareDesfireSetupViewModel).GetProperties(BindingFlags.Public | BindingFlags.Instance);

                    foreach (var p in properties.Where(x => x.PropertyType != items.GetType()))
                    {
                        // If not writable then cannot null it; if not readable then cannot check it's value
                        if (!p.CanWrite || !p.CanRead) { continue; }

                        var mget = p.GetGetMethod(false);
                        var mset = p.GetSetMethod(false);

                        // Get and set methods have to be public
                        if (mget == null) { continue; }
                        if (mset == null) { continue; }

                        p.SetValue(this, p.GetValue(_selectedSetupViewModel));
                    }
                }

                else
                {
                    editedTaskReference = null;
                    DesfireMasterKeyCurrent = settings.DefaultSpecification.MifareDesfireDefaultSecuritySettings.First(x => x.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardCardMasterKey).Key;
                    SelectedDesfireMasterKeyEncryptionTypeCurrent = settings.DefaultSpecification.MifareDesfireDefaultSecuritySettings.First(x => x.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardCardMasterKey).EncryptionType;

                    DesfireMasterKeyTarget = settings.DefaultSpecification.MifareDesfireDefaultSecuritySettings.First(x => x.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardApplicationMasterKey).Key;
                    SelectedDesfireMasterKeyEncryptionTypeTarget = settings.DefaultSpecification.MifareDesfireDefaultSecuritySettings.First(x => x.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardCardMasterKey).EncryptionType;

                    DesfireAppKeyCurrent = settings.DefaultSpecification.MifareDesfireDefaultSecuritySettings.First(x => x.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardApplicationMasterKey).Key;
                    SelectedDesfireAppKeyEncryptionTypeCurrent = settings.DefaultSpecification.MifareDesfireDefaultSecuritySettings.First(x => x.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardApplicationMasterKey).EncryptionType;
                    SelectedDesfireAppKeyNumberCurrent = "0";

                    DesfireAppKeyCurrentOld = DesfireAppKeyCurrent;

                    DesfireAppKeyTarget = settings.DefaultSpecification.MifareDesfireDefaultSecuritySettings.First(x => x.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardApplicationMasterKey).Key;
                    SelectedDesfireAppKeyEncryptionTypeTarget = settings.DefaultSpecification.MifareDesfireDefaultSecuritySettings.First(x => x.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardApplicationMasterKey).EncryptionType;


                    DesfireReadKeyCurrent = settings.DefaultSpecification.MifareDesfireDefaultSecuritySettings.First(x => x.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardReadKey).Key;
                    SelectedDesfireReadKeyNumber = "1";

                    DesfireWriteKeyCurrent = settings.DefaultSpecification.MifareDesfireDefaultSecuritySettings.First(x => x.KeyType == KeyType_MifareDesFireKeyType.DefaultDesfireCardWriteKey).Key;
                    SelectedDesfireWriteKeyNumber = "1";

                    KeyVersionCurrent = "00";
                    KeyVersionTarget = "00";
                    SelectedDesfireAppKeyVersionTarget = "00";

                    accessRights = new DESFireAccessRights();

                    AppNumberNew = "1";
                    AppNumberCurrent = "0";
                    AppNumberTarget = "0";

                    SelectedDesfireAppMaxNumberOfKeys = "1";

                    IsValidDesfireMasterKeyCurrent = null;
                    IsValidDesfireMasterKeyTarget = null;

                    IsValidDesfireAppKeyCurrent = null;
                    IsValidDesfireAppKeyCurrentOld = null;
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

                    SetTabAvailability(false, false, false, false, false, false, false);
                }

                HasPlugins = items != null && items.Any();

                if (HasPlugins)
                {
                    SelectedPlugin = Items.FirstOrDefault();
                }
            }
            catch (Exception e)
            {
                eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
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
                IsValidSelectedExecuteConditionTaskIndex = TaskIndexValidation.TryValidateExecuteConditionIndex(value, SelectedExecuteConditionErrorLevel, AvailableTasks, out _);
                int.TryParse(value, out selectedExecuteConditionTaskIndexAsInt);
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
                RevalidateSelectedExecuteConditionTaskIndex();
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
            set => SetProperty(ref isDesfireAppAuthoringTabEnabled, value);
        }
        private bool isDesfireAppAuthoringTabEnabled;

        /// <summary>
        ///
        /// </summary>
        public bool IsDesfireAppCreationTabEnabled
        {
            get => isDesfireAppCreationTabEnabled;
            set => SetProperty(ref isDesfireAppCreationTabEnabled, value);
        }
        private bool isDesfireAppCreationTabEnabled;

        /// <summary>
        /// Updates the enabled state of the DESFire authoring and authentication tabs in one place to reduce duplication.
        /// </summary>
        /// <param name="fileAuthoring">Flag indicating whether file authoring UI is enabled.</param>
        /// <param name="dataExplorerEdit">Flag indicating whether the data explorer editing UI is enabled.</param>
        /// <param name="desfirePiccAuthoring">Flag indicating whether PICC authoring UI is enabled.</param>
        /// <param name="desfireAuthentication">Flag indicating whether DESFire authentication UI is enabled.</param>
        /// <param name="desfireAppAuthentication">Flag indicating whether DESFire application authentication UI is enabled.</param>
        /// <param name="desfireAppAuthoring">Flag indicating whether DESFire application authoring UI is enabled.</param>
        /// <param name="desfireAppCreation">Flag indicating whether DESFire application creation UI is enabled.</param>
        private void SetTabAvailability(
            bool fileAuthoring,
            bool dataExplorerEdit,
            bool desfirePiccAuthoring,
            bool desfireAuthentication,
            bool desfireAppAuthentication,
            bool desfireAppAuthoring,
            bool desfireAppCreation)
        {
            IsDesfireFileAuthoringTabEnabled = fileAuthoring;
            IsDataExplorerEditTabEnabled = dataExplorerEdit;
            IsDesfirePICCAuthoringTabEnabled = desfirePiccAuthoring;
            IsDesfireAuthenticationTabEnabled = desfireAuthentication;
            IsDesfireAppAuthenticationTabEnabled = desfireAppAuthentication;
            IsDesfireAppAuthoringTabEnabled = desfireAppAuthoring;
            IsDesfireAppCreationTabEnabled = desfireAppCreation;
        }

        /// <summary>
        /// Updates the status text and error level for a failed operation and refreshes the reader status.
        /// </summary>
        /// <param name="errorResult">The error result to assign to <see cref="CurrentTaskErrorLevel"/>.</param>
        /// <param name="messageFormat">The format string appended to <see cref="StatusText"/>.</param>
        /// <param name="args">Arguments used to format the status message.</param>
        private async Task SetErrorStatusAsync(ERROR errorResult, string messageFormat, params object[] args)
        {
            StatusText += string.Format(messageFormat, args);
            CurrentTaskErrorLevel = errorResult;
            await UpdateReaderStatusCommand.ExecuteAsync(false);
        }

        /// <summary>
        /// Updates the status text and error level for an operation and refreshes the reader status.
        /// </summary>
        /// <param name="operationResult">Result of the operation.</param>
        /// <param name="successFormat">Status message to append when the operation succeeds.</param>
        /// <param name="successArgs">Arguments for the success status message.</param>
        /// <param name="errorFormat">Status message to append when the operation fails.</param>
        /// <param name="errorArgs">Arguments for the error status message.</param>
        private async Task<bool> SetOperationResultAsync(
            ERROR operationResult,
            string successFormat,
            object[] successArgs,
            string errorFormat,
            object[] errorArgs)
        {
            if (operationResult == ERROR.NoError)
            {
                StatusText += string.Format(successFormat, successArgs);
                CurrentTaskErrorLevel = operationResult;
                await UpdateReaderStatusCommand.ExecuteAsync(false);
                return true;
            }

            await SetErrorStatusAsync(operationResult, errorFormat, errorArgs);
            return false;
        }

        /// <summary>
        /// Finalizes a task by updating success state and reader status.
        /// </summary>
        private async Task FinalizeTaskAsync()
        {
            if (hasFinalizedTask)
            {
                return;
            }

            hasFinalizedTask = true;
            IsTaskCompletedSuccessfully = CurrentTaskErrorLevel == ERROR.NoError;
            await UpdateReaderStatusCommand.ExecuteAsync(false);
        }

        /// <summary>
        /// Encapsulates the resolved inputs for an application key change call.
        /// </summary>
        /// <param name="AppId">Target application identifier.</param>
        /// <param name="TargetKeyNo">Target key slot number.</param>
        /// <param name="TargetKeyType">Cryptographic type of the new key.</param>
        /// <param name="CurrentTargetKeyHex">Current key value for the target slot.</param>
        /// <param name="NewTargetKeyHex">New key value for the target slot.</param>
        /// <param name="NewTargetKeyVersion">Version byte for the new key.</param>
        /// <param name="MasterKeyHex">Authentication key value (key 0 when policy requires it).</param>
        /// <param name="MasterKeyType">Cryptographic type of the authentication key.</param>
        /// <param name="KeySettings">Key settings bits used to drive authentication policy.</param>
        internal sealed record AppKeyChangePayload(
            uint AppId,
            byte TargetKeyNo,
            DESFireKeyType TargetKeyType,
            string CurrentTargetKeyHex,
            string NewTargetKeyHex,
            byte NewTargetKeyVersion,
            string MasterKeyHex,
            DESFireKeyType MasterKeyType,
            DESFireKeySettings KeySettings);

        /// <summary>
        /// Builds the input payload for changing an application key.
        /// </summary>
        /// <param name="appId">Target application identifier.</param>
        /// <param name="keyNumberForChange">Target key slot number.</param>
        /// <param name="authKeyHex">Authentication key value (master key or targeted key).</param>
        /// <param name="oldKeyForTargetSlot">Current key value for the target slot.</param>
        /// <param name="keySettings">Key settings bits used to drive authentication policy.</param>
        /// <returns>Resolved change-key payload for provider calls.</returns>
        internal AppKeyChangePayload BuildAppKeyChangePayload(
            int appId,
            int keyNumberForChange,
            string authKeyHex,
            string oldKeyForTargetSlot,
            DESFireKeySettings keySettings)
        {
            return new AppKeyChangePayload(
                (uint)appId,
                (byte)keyNumberForChange,
                SelectedDesfireAppKeyEncryptionTypeTarget,
                oldKeyForTargetSlot,
                DesfireAppKeyTarget,
                (byte)selectedDesfireAppKeyVersionTargetAsInt,
                authKeyHex,
                SelectedDesfireAppKeyEncryptionTypeCurrent,
                keySettings);
        }

        private DESFireKeySettings GetGeneralDesfireKeyFlags()
        {
            var keySettings = DESFireKeySettings.ChangeKeyWithMasterKey;

            keySettings |= IsAllowChangeMKChecked ? DESFireKeySettings.AllowChangeMasterKey : 0;
            keySettings |= IsAllowListingWithoutMKChecked ? DESFireKeySettings.AllowFreeListingWithoutMasterKey : 0;
            keySettings |= IsAllowCreateDelWithoutMKChecked ? DESFireKeySettings.AllowFreeCreateDeleteWithoutMasterKey : 0;
            keySettings |= IsAllowConfigChangableChecked ? DESFireKeySettings.ConfigurationChangeable : 0;

            return keySettings;
        }

        private DESFireKeySettings GetChangeKeyModeForApplication(int appId)
        {
            var changeKeyMode = appId == 0
                ? DESFireKeySettings.ChangeKeyWithMasterKey
                : (DESFireKeySettings)((DESFireKeySettings)SelectedDesfireAppKeySettingsCreateNewApp & DESFireKeySettings.ChangeKeyMask);

            AccessConditionValidation.EnsureValid(changeKeyMode);

            return changeKeyMode;
        }

        /// <summary>
        /// Builds the authentication status line for app-key changes.
        /// </summary>
        /// <param name="timestamp">Timestamp used for status output.</param>
        /// <param name="appId">Current application identifier.</param>
        /// <param name="appKeyNumber">Selected application key number.</param>
        /// <param name="selectedSettings">Selected key settings.</param>
        /// <param name="authKeyNo">Computed authentication key number.</param>
        internal static string BuildChangeAppKeyAuthStatusLine(
            DateTime timestamp,
            int appId,
            int appKeyNumber,
            DESFireKeySettings selectedSettings,
            int authKeyNo)
        {
            return string.Format(
                "{0}: AppID {1}, KeyNo {2}, Settings {3}, AuthKeyNo {4}\n",
                timestamp,
                appId,
                appKeyNumber,
                DescribeKeySettings(selectedSettings),
                authKeyNo);
        }

        /// <summary>
        /// Builds a readable string for DESFire key settings, ensuring change-key policies are visible.
        /// </summary>
        /// <param name="settings">Selected key settings.</param>
        /// <returns>Readable key settings string.</returns>
        internal static string DescribeKeySettings(DESFireKeySettings settings)
        {
            var parts = new List<string>();
            var changeKeyMode = settings & DESFireKeySettings.ChangeKeyMask;

            if (changeKeyMode == DESFireKeySettings.ChangeKeyWithMasterKey)
            {
                parts.Add(DESFireKeySettings.ChangeKeyWithMasterKey.ToString());
            }
            else if (changeKeyMode == DESFireKeySettings.ChangeKeyWithTargetedKeyNumber
                     || changeKeyMode == DESFireKeySettings.ChangeKeyFrozen)
            {
                parts.Add(changeKeyMode.ToString());
            }

            if (settings.HasFlag(DESFireKeySettings.AllowChangeMasterKey))
            {
                parts.Add(DESFireKeySettings.AllowChangeMasterKey.ToString());
            }

            if (settings.HasFlag(DESFireKeySettings.AllowFreeListingWithoutMasterKey))
            {
                parts.Add(DESFireKeySettings.AllowFreeListingWithoutMasterKey.ToString());
            }

            if (settings.HasFlag(DESFireKeySettings.AllowFreeCreateDeleteWithoutMasterKey))
            {
                parts.Add(DESFireKeySettings.AllowFreeCreateDeleteWithoutMasterKey.ToString());
            }

            if (settings.HasFlag(DESFireKeySettings.ConfigurationChangeable))
            {
                parts.Add(DESFireKeySettings.ConfigurationChangeable.ToString());
            }

            return parts.Count == 0 ? settings.ToString() : string.Join(", ", parts);
        }

        /// <summary>
        /// Determines the authentication key number for change-app-key operations.
        /// </summary>
        /// <param name="appId">Current application identifier.</param>
        /// <param name="changeKeyMode">Selected change-key policy for the app.</param>
        /// <param name="appKeyNumber">Selected application key number.</param>
        internal static int GetAuthKeyNumberForChangeAppKey(int appId, DESFireKeySettings changeKeyMode, int appKeyNumber)
        {
            if (appId == 0)
            {
                return 0;
            }

            return changeKeyMode == DESFireKeySettings.ChangeKeyWithMasterKey ? 0 : appKeyNumber;
        }

        /// <summary>
        /// Builds a warning line for frozen change-key policies.
        /// </summary>
        /// <param name="timestamp">Timestamp used for status output.</param>
        internal static string BuildChangeKeyFrozenWarningLine(DateTime timestamp)
        {
            return string.Format(
                "{0}: Warning: Change key policy is frozen (ChangeKeyFrozen).\n",
                timestamp);
        }

        /// <summary>
        /// Appends authentication diagnostic status lines for change-app-key operations.
        /// </summary>
        /// <param name="authKeyNo">Authentication key number used for the app.</param>
        /// <param name="changeKeyMode">Selected change-key policy for the app.</param>
        private void AppendChangeAppKeyAuthStatusLines(int authKeyNo, DESFireKeySettings changeKeyMode)
        {
            StatusText += BuildChangeAppKeyAuthStatusLine(
                DateTime.Now,
                AppNumberCurrentAsInt,
                selectedDesfireAppKeyNumberCurrentAsInt,
                (DESFireKeySettings)SelectedDesfireAppKeySettingsCreateNewApp,
                authKeyNo);

            if (changeKeyMode == DESFireKeySettings.ChangeKeyFrozen)
            {
                StatusText += BuildChangeKeyFrozenWarningLine(DateTime.Now);
            }
        }

        private DESFireKeySettings BuildSelectedKeySettings(int appId)
        {
            var keySettings = GetGeneralDesfireKeyFlags();
            keySettings &= ~DESFireKeySettings.ChangeKeyMask;
            keySettings |= GetChangeKeyModeForApplication(appId);

            AccessConditionValidation.EnsureValid(keySettings);

            return keySettings;
        }

        private void UpdateOldAppKeyDefaults()
        {
            if (ShowAppKeyOldInputs)
            {
                return;
            }

            DesfireAppKeyCurrentOld = DesfireAppKeyCurrent ?? string.Empty;
            IsValidDesfireAppKeyCurrentOld = IsValidDesfireAppKeyCurrent;
        }

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
                        SetTabAvailability(false, false, false, false, false, false, false);
                        break;

                    case TaskType_MifareDesfireTask.ReadAppSettings:
                        SetTabAvailability(false, false, true, false, false, false, false);
                        break;

                    case TaskType_MifareDesfireTask.AppExistCheck:
                        SetTabAvailability(false, false, false, false, false, true, true);
                        break;

                    case TaskType_MifareDesfireTask.ApplicationKeyChangeover:
                        SetTabAvailability(false, false, false, false, true, true, false);
                        break;

                    case TaskType_MifareDesfireTask.ApplicationKeySettingsChangeover:
                        SetTabAvailability(false, false, false, false, true, true, false);
                        break;

                    case TaskType_MifareDesfireTask.ChangeDefault:
                        SetTabAvailability(true, true, true, true, true, true, true);
                        break;

                    case TaskType_MifareDesfireTask.CreateApplication:
                        SetTabAvailability(false, false, true, true, false, false, true);
                        break;

                    case TaskType_MifareDesfireTask.AuthenticateApplication:
                        SetTabAvailability(false, false, false, true, true, true, false);
                        break;

                    case TaskType_MifareDesfireTask.CreateFile:
                        SetTabAvailability(true, false, false, false, true, true, false);
                        break;

                    case TaskType_MifareDesfireTask.DeleteApplication:
                        SetTabAvailability(false, false, true, true, false, false, true);
                        break;

                    case TaskType_MifareDesfireTask.DeleteFile:
                        SetTabAvailability(true, false, false, false, true, true, false);
                        break;

                    case TaskType_MifareDesfireTask.FormatDesfireCard:
                        SetTabAvailability(false, false, true, true, false, false, false);
                        break;

                    case TaskType_MifareDesfireTask.PICCMasterKeyChangeover:
                        SetTabAvailability(false, false, true, true, false, false, false);
                        break;

                    case TaskType_MifareDesfireTask.PICCMasterKeySettingsChangeover:
                        SetTabAvailability(false, false, true, true, false, false, false);
                        break;

                    case TaskType_MifareDesfireTask.ReadData:
                        SetTabAvailability(true, true, false, false, true, true, false);
                        break;

                    case TaskType_MifareDesfireTask.WriteData:
                        SetTabAvailability(true, true, false, false, true, true, false);
                        break;
                }
                OnPropertyChanged(nameof(SelectedTaskType));
                OnPropertyChanged(nameof(IsFormatTaskSelected));
                OnPropertyChanged(nameof(ShowAppKeyTargetInputs));
                OnPropertyChanged(nameof(ShowAppKeySettingsInputs));
                OnPropertyChanged(nameof(ShowAppKeyTargetSection));
                OnPropertyChanged(nameof(ShowPiccMasterKeyTargetInputs));
                OnPropertyChanged(nameof(ShowPiccMasterKeySettingsInputs));
                OnPropertyChanged(nameof(ShowPiccMasterKeyAuthoringSection));
                OnPropertyChanged(nameof(ShowCreateApplicationInputs));
                OnPropertyChanged(nameof(ShowDeleteApplicationInputs));
                OnPropertyChanged(nameof(ShowAppKeyCurrentInputs));
                OnPropertyChanged(nameof(ShowAppKeyOldInputs));
                OnPropertyChanged(nameof(ShowFileAccessRights));
                OnPropertyChanged(nameof(ShowFileAuthoringCommands));

                UpdateOldAppKeyDefaults();
            }
        }
        private TaskType_MifareDesfireTask selectedAccessBitsTaskType;

        [XmlIgnore]
        public bool IsFormatTaskSelected => SelectedTaskType == TaskType_MifareDesfireTask.FormatDesfireCard;

        /// <summary>
        /// Gets a value indicating whether UI elements for providing a target application key should be shown.
        /// The target key is unnecessary when changing only the application key settings.
        /// </summary>
        [XmlIgnore]
        public bool ShowAppKeyTargetInputs => SelectedTaskType == TaskType_MifareDesfireTask.ApplicationKeyChangeover;

        /// <summary>
        /// Gets a value indicating whether UI elements for configuring application key settings should be shown.
        /// The settings check boxes are unnecessary when only changing the application key material.
        /// </summary>
        [XmlIgnore]
        public bool ShowAppKeySettingsInputs => SelectedTaskType == TaskType_MifareDesfireTask.ApplicationKeySettingsChangeover;

        /// <summary>
        /// Gets a value indicating whether the target application key settings section should be shown.
        /// The target key section is unnecessary for file and data operations.
        /// </summary>
        [XmlIgnore]
        public bool ShowAppKeyTargetSection => SelectedTaskType != TaskType_MifareDesfireTask.CreateFile
                                               && SelectedTaskType != TaskType_MifareDesfireTask.DeleteFile
                                               && SelectedTaskType != TaskType_MifareDesfireTask.ReadData
                                               && SelectedTaskType != TaskType_MifareDesfireTask.WriteData;

        /// <summary>
        /// Gets a value indicating whether UI elements for supplying the current application key should be shown.
        /// The current key inputs are unnecessary when reading or writing data.
        /// </summary>
        [XmlIgnore]
        public bool ShowAppKeyCurrentInputs => SelectedTaskType != TaskType_MifareDesfireTask.ReadData
                                               && SelectedTaskType != TaskType_MifareDesfireTask.WriteData;

        /// <summary>
        /// Gets a value indicating whether UI elements for providing a target PICC master key should be shown.
        /// The target key is unnecessary when changing only the PICC master key settings.
        /// </summary>
        [XmlIgnore]
        public bool ShowPiccMasterKeyTargetInputs => SelectedTaskType == TaskType_MifareDesfireTask.PICCMasterKeyChangeover;

        /// <summary>
        /// Gets a value indicating whether UI elements for configuring PICC master key settings should be shown.
        /// Settings controls are unnecessary when only changing the PICC master key material.
        /// </summary>
        [XmlIgnore]
        public bool ShowPiccMasterKeySettingsInputs => SelectedTaskType == TaskType_MifareDesfireTask.PICCMasterKeySettingsChangeover;

        /// <summary>
        /// Gets a value indicating whether PICC master key authoring inputs should be shown.
        /// The PICC controls are unnecessary when creating or deleting applications.
        /// </summary>
        [XmlIgnore]
        public bool ShowPiccMasterKeyAuthoringSection => SelectedTaskType != TaskType_MifareDesfireTask.FormatDesfireCard
                                                         && SelectedTaskType != TaskType_MifareDesfireTask.CreateApplication
                                                         && SelectedTaskType != TaskType_MifareDesfireTask.DeleteApplication;

        /// <summary>
        /// Gets a value indicating whether file access rights controls should be shown.
        /// The access rights are unnecessary when reading or writing file data.
        /// </summary>
        [XmlIgnore]
        public bool ShowFileAccessRights => SelectedTaskType != TaskType_MifareDesfireTask.ReadData
                                            && SelectedTaskType != TaskType_MifareDesfireTask.WriteData;

        /// <summary>
        /// Gets a value indicating whether file authoring commands should be shown.
        /// The create/delete actions are unnecessary when reading or writing file data.
        /// </summary>
        [XmlIgnore]
        public bool ShowFileAuthoringCommands => SelectedTaskType != TaskType_MifareDesfireTask.ReadData
                                                 && SelectedTaskType != TaskType_MifareDesfireTask.WriteData;

        /// <summary>
        /// Gets a value indicating whether application creation inputs should be shown.
        /// </summary>
        [XmlIgnore]
        public bool ShowCreateApplicationInputs => SelectedTaskType == TaskType_MifareDesfireTask.CreateApplication;

        /// <summary>
        /// Gets a value indicating whether application deletion inputs should be shown.
        /// </summary>
        [XmlIgnore]
        public bool ShowDeleteApplicationInputs => SelectedTaskType == TaskType_MifareDesfireTask.DeleteApplication;

        /// <summary>
        /// Gets a value indicating whether UI elements for supplying the previous application key should be shown.
        /// These inputs are only needed when changing a non-PICC key using master-key mode (MK) for authentication.
        /// </summary>
        [XmlIgnore]
        public bool ShowAppKeyOldInputs
        {
            get
            {
                var changeKeyMode = GetChangeKeyModeForApplication(AppNumberCurrentAsInt);

                return changeKeyMode == DESFireKeySettings.ChangeKeyWithMasterKey
                       && SelectedTaskType == TaskType_MifareDesfireTask.ApplicationKeyChangeover
                       && AppNumberCurrentAsInt != 0
                       && selectedDesfireAppKeyNumberCurrentAsInt != 0;
            }
        }

        /// <summary>
        ///
        /// </summary>
        public string CurrentTaskIndex
        {
            get => currentTaskIndex;
            set
            {
                currentTaskIndex = value;
                IsValidSelectedTaskIndex = TaskIndexValidation.TryValidateTaskIndex(value, AvailableTasks, editedTaskReference ?? this, out _);
                int.TryParse(value, out selectedTaskIndexAsInt);
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
        /// The collection of available tasks for validation.
        /// </summary>
        [XmlIgnore]
        public ObservableCollection<object> AvailableTasks
        {
            get => availableTasks;
            set
            {
                availableTasks = value;
                RevalidateSelectedTaskIndex();
                RevalidateSelectedExecuteConditionTaskIndex();
            }
        }
        private ObservableCollection<object> availableTasks;

        private void RevalidateSelectedTaskIndex()
        {
            IsValidSelectedTaskIndex = TaskIndexValidation.TryValidateTaskIndex(CurrentTaskIndex, AvailableTasks, editedTaskReference ?? this, out _);
        }

        private void RevalidateSelectedExecuteConditionTaskIndex()
        {
            IsValidSelectedExecuteConditionTaskIndex = TaskIndexValidation.TryValidateExecuteConditionIndex(SelectedExecuteConditionTaskIndex, SelectedExecuteConditionErrorLevel, AvailableTasks, out _);
        }

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
                if (isTaskCompletedSuccessfully == value)
                {
                    return;
                }

                isTaskCompletedSuccessfully = value;
                UiDispatcher.InvokeIfRequired(() => OnPropertyChanged(nameof(IsTaskCompletedSuccessfully)));
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

        [XmlIgnore]
        public ObservableCollection<TaskAttemptResult> AttemptResults { get; } = new ObservableCollection<TaskAttemptResult>();

        #region Key Properties Card Master

        [XmlIgnore]
        public string[] MifareDesfireKeys { get; set; }

        [XmlIgnore]
        public string[] MifareDesfireKeyCount { get; set; }

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsValidKeyVersionCurrent
        {
            get => isValidKeyVersionCurrent;
            private set
            {
                isValidKeyVersionCurrent = value;
                OnPropertyChanged(nameof(IsValidKeyVersionCurrent));
            }
        }
        private bool? isValidKeyVersionCurrent;

        /// <summary>
        ///
        /// </summary>
        public string KeyVersionCurrent
        {
            get => keyVersionCurrent;
            set
            {
                keyVersionCurrent = value?.ToUpperInvariant();

                if (TryParseDesfireByteValue(keyVersionCurrent, out var parsedVersion))
                {
                    keyVersionCurrentAsInt = parsedVersion;
                    keyVersionCurrent = parsedVersion.ToString("X2", CultureInfo.InvariantCulture);
                    IsValidKeyVersionCurrent = true;
                }
                else
                {
                    keyVersionCurrentAsInt = 0;
                    IsValidKeyVersionCurrent = false;
                }

                OnPropertyChanged(nameof(KeyVersionCurrent));
            }
        }
        private string keyVersionCurrent;
        private int keyVersionCurrentAsInt;

        /// <summary>
        ///
        /// </summary>
        public string KeyVersionTarget
        {
            get => keyVersionTarget;
            set
            {
                keyVersionTarget = value?.ToUpperInvariant();

                if (byte.TryParse(keyVersionTarget, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsedVersion))
                {
                    keyVersionTargetAsInt = parsedVersion;
                    keyVersionTarget = parsedVersion.ToString("X2", CultureInfo.InvariantCulture);
                    IsValidKeyVersionTarget = true;
                }
                else
                {
                    keyVersionTargetAsInt = 0;
                    IsValidKeyVersionTarget = false;
                }

                OnPropertyChanged(nameof(KeyVersionTarget));
            }
        }
        private string keyVersionTarget;
        private int keyVersionTargetAsInt;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsValidKeyVersionTarget
        {
            get => isValidKeyVersionTarget;
            set
            {
                isValidKeyVersionTarget = value;
                OnPropertyChanged(nameof(IsValidKeyVersionTarget));
            }
        }
        private bool? isValidKeyVersionTarget;

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
                desfireMasterKeyCurrent = NormalizeDesfireKeyInput(value);
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
                desfireMasterKeyTarget = NormalizeDesfireKeyInput(value);

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
                OnPropertyChanged(nameof(ShowAppKeyOldInputs));

                UpdateOldAppKeyDefaults();
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
                    appNumberNew = value.Length > 8 ? value.ToUpper().Remove(8) : value;
                }
                catch
                {
                    appNumberNew = value.ToUpper();
                }
                IsValidAppNumberNew = TryParseDesfireAppId(value, out appNumberNewAsInt);
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
        /// Parses a DESFire application identifier from decimal or hexadecimal input.
        /// </summary>
        /// <param name="value">The user input to parse.</param>
        /// <param name="appId">The parsed application identifier.</param>
        /// <returns><c>true</c> when the input yields a valid app ID.</returns>
        private static bool TryParseDesfireAppId(string value, out int appId)
        {
            appId = 0;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var trimmed = value.Trim();
            var isHex = trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
            var candidate = isHex ? trimmed.Substring(2) : trimmed;

            if (isHex && int.TryParse(candidate, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out appId))
            {
                return appId <= (int)0xFFFFFF;
            }

            return int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out appId)
                   && appId <= (int)0xFFFFFF;
        }

        /// <summary>
        /// Parses a byte value from decimal or hexadecimal input.
        /// </summary>
        /// <param name="value">The user input to parse.</param>
        /// <param name="parsed">The parsed byte value.</param>
        /// <returns><c>true</c> when the input yields a valid byte.</returns>
        private static bool TryParseDesfireByteValue(string value, out byte parsed)
        {
            parsed = 0;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var trimmed = value.Trim();
            var isHex = trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
            var candidate = isHex ? trimmed.Substring(2) : trimmed;

            if (isHex && byte.TryParse(candidate, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsed))
            {
                return true;
            }

            return byte.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);
        }

        /// <summary>
        /// Normalizes DESFire key input by stripping non-hex characters, uppercasing, and trimming to the expected length.
        /// </summary>
        /// <param name="value">The user-provided key input.</param>
        /// <param name="maxLength">The maximum number of hex characters to retain.</param>
        /// <returns>The normalized key string.</returns>
        private static string NormalizeDesfireKeyInput(string value, int maxLength = 32)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);
            foreach (var character in value)
            {
                if (Uri.IsHexDigit(character))
                {
                    builder.Append(char.ToUpperInvariant(character));
                }
            }

            var normalized = builder.ToString();
            return normalized.Length > maxLength ? normalized.Substring(0, maxLength) : normalized;
        }

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
                OnPropertyChanged(nameof(ShowAppKeyOldInputs));

                UpdateOldAppKeyDefaults();
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
                desfireAppKeyCurrent = NormalizeDesfireKeyInput(value);
                IsValidDesfireAppKeyCurrent = (CustomConverter.IsInHexFormat(desfireAppKeyCurrent) && desfireAppKeyCurrent.Length == 32);
                OnPropertyChanged(nameof(DesfireAppKeyCurrent));

                UpdateOldAppKeyDefaults();
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
        /// Provides the previous value of the application key when using master-key (MK) change mode for non-PICC keys.
        /// </summary>
        public string DesfireAppKeyCurrentOld
        {
            get => desfireAppKeyCurrentOld;
            set
            {
                desfireAppKeyCurrentOld = NormalizeDesfireKeyInput(value);
                IsValidDesfireAppKeyCurrentOld = (CustomConverter.IsInHexFormat(desfireAppKeyCurrentOld) && desfireAppKeyCurrentOld.Length == 32);
                OnPropertyChanged(nameof(DesfireAppKeyCurrentOld));
            }
        }
        private string desfireAppKeyCurrentOld;

        /// <summary>
        /// Indicates whether the previously configured application key adheres to the expected hex format and length.
        /// </summary>
        [XmlIgnore]
        public bool? IsValidDesfireAppKeyCurrentOld
        {
            get => isValidDesfireAppKeyCurrentOld;
            set
            {
                isValidDesfireAppKeyCurrentOld = value;
                OnPropertyChanged(nameof(IsValidDesfireAppKeyCurrentOld));
            }
        }
        private bool? isValidDesfireAppKeyCurrentOld;

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
                    appNumberCurrent = value.Length > 8 ? value.ToUpper().Remove(8) : value;
                }
                catch
                {
                    appNumberCurrent = value.ToUpper();
                }
                IsValidAppNumberCurrent = TryParseDesfireAppId(value, out appNumberCurrentAsInt);
                OnPropertyChanged(nameof(AppNumberCurrent));
                OnPropertyChanged(nameof(IsAppKeyChangeEnabled));
                OnPropertyChanged(nameof(ShowAppKeyOldInputs));

                UpdateOldAppKeyDefaults();
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

        /// <summary>
        /// Gets a value indicating whether changing an application key is allowed for the current application ID.
        /// </summary>
        [XmlIgnore]
        public bool IsAppKeyChangeEnabled => AppNumberCurrentAsInt > 0;

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
        public string SelectedDesfireAppKeyVersionTarget
        {
            get => selectedDesfireAppKeyVersionTarget;
            set
            {
                selectedDesfireAppKeyVersionTarget = value?.ToUpperInvariant();

                if (TryParseDesfireByteValue(selectedDesfireAppKeyVersionTarget, out var parsedVersion))
                {
                    selectedDesfireAppKeyVersionTargetAsInt = parsedVersion;
                    selectedDesfireAppKeyVersionTarget = parsedVersion.ToString("X2", CultureInfo.InvariantCulture);
                    IsValidDesfireAppKeyVersionTarget = true;
                }
                else
                {
                    selectedDesfireAppKeyVersionTargetAsInt = 0;
                    IsValidDesfireAppKeyVersionTarget = false;
                }
                OnPropertyChanged(nameof(SelectedDesfireAppKeyVersionTarget));
            }
        }
        private string selectedDesfireAppKeyVersionTarget;
        private int selectedDesfireAppKeyVersionTargetAsInt;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsValidDesfireAppKeyVersionTarget
        {
            get => isValidDesfireAppKeyVersionTarget;
            set
            {
                isValidDesfireAppKeyVersionTarget = value;
                OnPropertyChanged(nameof(IsValidDesfireAppKeyVersionTarget));
            }
        }
        private bool? isValidDesfireAppKeyVersionTarget;

        /// <summary>
        ///
        /// </summary>
        public string DesfireAppKeyTarget
        {
            get => desfireAppKeyTarget;
            set
            {
                desfireAppKeyTarget = NormalizeDesfireKeyInput(value);

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
                    appNumberTarget = value.Length > 8 ? value.ToUpper().Remove(8) : value;
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
                IsValidFileNumberCurrent = TryParseDesfireByteValue(value, out var parsedFileNumber);
                fileNumberCurrentAsInt = parsedFileNumber;
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
                            return;
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
        /// Stores the most recently loaded DESFire data file path for optional refresh operations.
        /// </summary>
        public string DesfireDataFilePath
        {
            get => desfireDataFilePath;
            set
            {
                desfireDataFilePath = value;
                OnPropertyChanged(nameof(DesfireDataFilePath));
            }
        }

        /// <summary>
        /// Determines whether the DESFire data file should be reloaded before executing a write.
        /// </summary>
        public bool RefreshDesfireDataFromFileBeforeWrite
        {
            get => refreshDesfireDataFromFileBeforeWrite;
            set
            {
                refreshDesfireDataFromFileBeforeWrite = value;
                OnPropertyChanged(nameof(RefreshDesfireDataFromFileBeforeWrite));
            }
        }

        /// <summary>
        /// Stores the target file path used to save read DESFire data.
        /// </summary>
        public string DesfireReadDataFilePath
        {
            get => desfireReadDataFilePath;
            set
            {
                desfireReadDataFilePath = value;
                OnPropertyChanged(nameof(DesfireReadDataFilePath));
            }
        }

        /// <summary>
        /// Determines whether read data should overwrite the selected file on each read.
        /// </summary>
        public bool OverwriteReadDataFileOnRead
        {
            get => overwriteReadDataFileOnRead;
            set
            {
                overwriteReadDataFileOnRead = value;
                OnPropertyChanged(nameof(OverwriteReadDataFileOnRead));
            }
        }

        /// <summary>
        ///
        /// </summary>
        public string DesfireReadKeyCurrent
        {
            get => desfireReadKeyCurrent;
            set
            {
                desfireReadKeyCurrent = NormalizeDesfireKeyInput(value);

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
                desfireWriteKeyCurrent = NormalizeDesfireKeyInput(value);

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

        /// <summary>
        /// 
        /// </summary>
        public IAsyncRelayCommand SaveSettings => new AsyncRelayCommand(OnNewSaveSettingsCommand);
        private async Task OnNewSaveSettingsCommand()
        {
            SettingsReaderWriter settings = new SettingsReaderWriter();
            await settings.SaveSettings();
        }

        public IAsyncRelayCommand CommandDelegator => new AsyncRelayCommand<TaskType_MifareDesfireTask>((x) => OnNewCommandDelegatorCall(x));
        private async Task OnNewCommandDelegatorCall(TaskType_MifareDesfireTask desfireTaskType)
        {
            hasFinalizedTask = false;

            try
            {
                switch (desfireTaskType)
                {
                    case TaskType_MifareDesfireTask.AppExistCheck:
                        await DoesAppExistCommand(null);
                        break;

                    case TaskType_MifareDesfireTask.ApplicationKeyChangeover:
                        await OnNewChangeAppKeyCommand();
                        break;

                    case TaskType_MifareDesfireTask.ApplicationKeySettingsChangeover:
                        await OnNewChangeAppKeySettingsCommand();
                        break;

                    case TaskType_MifareDesfireTask.AuthenticateApplication:
                        await OnNewAuthenticateToCardApplicationCommand();
                        break;

                    case TaskType_MifareDesfireTask.CreateApplication:
                        await OnNewCreateAppCommand();
                        break;

                    case TaskType_MifareDesfireTask.CreateFile:
                        await OnNewCreateFileCommand();
                        break;

                    case TaskType_MifareDesfireTask.DeleteApplication:
                        await OnNewDeleteSignleCardApplicationCommand();
                        break;

                    case TaskType_MifareDesfireTask.DeleteFile:
                        await OnNewDeleteFileCommand();
                        break;

                    case TaskType_MifareDesfireTask.FormatDesfireCard:
                        await OnNewFormatDesfireCardCommand();
                        break;

                    case TaskType_MifareDesfireTask.PICCMasterKeyChangeover:
                        await OnNewChangeMasterCardKeyCommand();
                        break;

                    case TaskType_MifareDesfireTask.PICCMasterKeySettingsChangeover:
                        await OnNewChangeAppKeySettingsCommand();
                        break;

                    case TaskType_MifareDesfireTask.ReadAppSettings:
                        await ReadAppSettingsCommand();
                        break;

                    case TaskType_MifareDesfireTask.ReadData:
                        await OnNewReadDataCommand();
                        break;

                    case TaskType_MifareDesfireTask.WriteData:
                        await OnNewWriteDataCommand();
                        break;

                    default:
                        break;
                }
            }
            finally
            {
                await FinalizeTaskAsync();
            }
        }
        /// <summary>
        /// return new RelayCommand<LibLogicalAccessProvider>((_device) => OnNewCreateAppCommand(_device));
        /// </summary>
        public IAsyncRelayCommand CreateAppCommand => new AsyncRelayCommand(OnNewCreateAppCommand);
        private async Task OnNewCreateAppCommand()
        {
            CurrentTaskErrorLevel = ERROR.Empty;

            using (var device = ReaderDevice.Instance)
            {
                if (device != null)
                {
                    await UpdateReaderStatusCommand.ExecuteAsync(true);

                    StatusText = string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxDllLoaded"));

                    if (CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireReadKeyCurrent) == KEY_ERROR.NO_ERROR)
                    {
                        var authResult = await device.AuthToMifareDesfireApplication(
                                  DesfireMasterKeyCurrent,
                                  SelectedDesfireMasterKeyEncryptionTypeCurrent,
                                  0);

                        if (IsValidAppNumberNew != false && authResult == ERROR.NoError)
                        {
                            StatusText += string.Format("{0}: Successfully Authenticated to App 0\n", DateTime.Now);

                            DESFireKeySettings keySettings;
                            keySettings = (DESFireKeySettings)SelectedDesfireAppKeySettingsCreateNewApp;

                            keySettings |= IsAllowChangeMKChecked ? (DESFireKeySettings)1 : (DESFireKeySettings)0;
                            keySettings |= IsAllowListingWithoutMKChecked ? (DESFireKeySettings)2 : (DESFireKeySettings)0;
                            keySettings |= IsAllowCreateDelWithoutMKChecked ? (DESFireKeySettings)4 : (DESFireKeySettings)0;
                            keySettings |= IsAllowConfigChangableChecked ? (DESFireKeySettings)8 : (DESFireKeySettings)0;

                            var createResult = await device.CreateMifareDesfireApplication(
                                DesfireMasterKeyCurrent,
                                keySettings,
                                SelectedDesfireMasterKeyEncryptionTypeCurrent,
                                SelectedDesfireAppKeyEncryptionTypeCreateNewApp,
                                selectedDesfireAppMaxNumberOfKeysAsInt,
                                AppNumberNewAsInt);

                            if (createResult.Code == ERROR.NoError)
                            {
                                StatusText += string.Format("{0}: Successfully Created AppID {1}\n", DateTime.Now, AppNumberNewAsInt);
                                CurrentTaskErrorLevel = createResult.Code;
                                await UpdateReaderStatusCommand.ExecuteAsync(false);
                                return;
                            }
                            else
                            {
                                StatusText += string.Format("{0}: Unable to Create App: {1}\n", DateTime.Now, createResult.Message ?? createResult.Code.ToString());
                                CurrentTaskErrorLevel = createResult.Code;
                                await UpdateReaderStatusCommand.ExecuteAsync(false);
                                return;
                            }
                        }
                        else
                        {
                            StatusText += string.Format("{0}: Authentication to PICC failed.\n", DateTime.Now);
                            CurrentTaskErrorLevel = authResult;
                            await UpdateReaderStatusCommand.ExecuteAsync(false);
                            return;
                        }
                    }
                }
                else
                {
                    CurrentTaskErrorLevel = ERROR.TransportError;
                    await UpdateReaderStatusCommand.ExecuteAsync(false);
                    return;
                }
            }

            await FinalizeTaskAsync();
            return;
        }

        /// <summary>
        /// return new RelayCommand<LibLogicalAccessProvider>((_device) => OnNewCreateAppCommand(_device));
        /// </summary>
        public IAsyncRelayCommand CreateFileCommand => new AsyncRelayCommand(OnNewCreateFileCommand);
        private async Task OnNewCreateFileCommand()
        {
            CurrentTaskErrorLevel = ERROR.Empty;

            accessRights.changeAccess = SelectedDesfireFileAccessRightChange;
            accessRights.readAccess = SelectedDesfireFileAccessRightRead;
            accessRights.writeAccess = SelectedDesfireFileAccessRightWrite;
            accessRights.readAndWriteAccess = SelectedDesfireFileAccessRightReadWrite;

            using (var device = ReaderDevice.Instance)
            {
                if (device != null)
                {
                    await UpdateReaderStatusCommand.ExecuteAsync(true);

                    StatusText = string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxDllLoaded"));

                    if (CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireAppKeyCurrent) == KEY_ERROR.NO_ERROR && IsValidAppNumberNew != false)
                    {
                        var result = await device.AuthToMifareDesfireApplication(
                                DesfireAppKeyCurrent,
                                SelectedDesfireAppKeyEncryptionTypeCurrent,
                                selectedDesfireAppKeyNumberCurrentAsInt, AppNumberCurrentAsInt);

                        if (result == ERROR.NoError)
                        {
                            StatusText += string.Format("{0}: Successfully Authenticated to App {1}\n", DateTime.Now, AppNumberCurrentAsInt);

                            result = await device.CreateMifareDesfireFile(
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
                                await UpdateReaderStatusCommand.ExecuteAsync(false);
                                return;
                            }
                            else
                            {
                                StatusText += string.Format("{0}: Unable to Create File: {1}\n", DateTime.Now, result.ToString());
                                CurrentTaskErrorLevel = result;
                                await UpdateReaderStatusCommand.ExecuteAsync(false);
                                return;
                            }
                        }

                        else
                        {
                            StatusText += string.Format("{0}: Unable to Authenticate to App {1}; Try to Continue Anyway...\n", DateTime.Now, AppNumberCurrentAsInt);

                            result = await device.CreateMifareDesfireFile(
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
                                await UpdateReaderStatusCommand.ExecuteAsync(false);
                                return;
                            }
                            else
                            {
                                StatusText += string.Format("{0}: Unable to Create File: {1}\n", DateTime.Now, result.ToString());
                                CurrentTaskErrorLevel = result;
                                await UpdateReaderStatusCommand.ExecuteAsync(false);
                                return;
                            }
                        }
                    }
                }
                else
                {
                    CurrentTaskErrorLevel = ERROR.TransportError;
                    await UpdateReaderStatusCommand.ExecuteAsync(false);
                    return;
                }
            }

            await FinalizeTaskAsync();
            return;
        }

        /// <summary>
        /// 
        /// </summary>
        public IAsyncRelayCommand ReadDataCommand => new AsyncRelayCommand(OnNewReadDataCommand);
        private async Task OnNewReadDataCommand()
        {
            CurrentTaskErrorLevel = ERROR.Empty;

            using (var device = ReaderDevice.Instance)
            {
                if (device != null)
                {
                    await UpdateReaderStatusCommand.ExecuteAsync(true);

                    StatusText = string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxDllLoaded"));

                    if (CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireReadKeyCurrent) == KEY_ERROR.NO_ERROR)
                    {
                        var result = await device.AuthToMifareDesfireApplication(
                                DesfireReadKeyCurrent,
                                SelectedDesfireReadKeyEncryptionType,
                                selectedDesfireReadKeyNumberAsInt, AppNumberCurrentAsInt);


                        if (IsValidAppNumberNew != false && result == ERROR.NoError)
                        {
                            StatusText += string.Format("{0}: Successfully Authenticated to App {1}\n", DateTime.Now, AppNumberCurrentAsInt);

                            result = await device.ReadMiFareDESFireChipFile(DesfireReadKeyCurrent, SelectedDesfireReadKeyEncryptionType, selectedDesfireReadKeyNumberAsInt,
                                                                   EncryptionMode.CM_ENCRYPT, FileNumberCurrentAsInt, AppNumberCurrentAsInt, FileSizeCurrentAsInt);

                            if (result == ERROR.NoError)
                            {
                                FileSizeCurrent = device.MifareDESFireData.Length.ToString();

                                StatusText += string.Format("{0}: Successfully Read {2} Bytes Data from FileNo: {1} in AppID: {3}\n", DateTime.Now, FileNumberCurrentAsInt, FileSizeCurrentAsInt, AppNumberNewAsInt);

                                var fromChipNode = EnsureDesfireDataNode(childNodeViewModelFromChip, FileNumberCurrentAsInt);
                                var tempNode = EnsureDesfireDataNode(childNodeViewModelTemp, FileNumberCurrentAsInt);

                                fromChipNode.DesfireFile = new MifareDesfireFileModel(device.MifareDESFireData, (byte)FileNumberCurrentAsInt);
                                tempNode.DesfireFile = new MifareDesfireFileModel(device.MifareDESFireData, (byte)FileNumberCurrentAsInt);

                                CurrentTaskErrorLevel = result;

                                OnPropertyChanged(nameof(ChildNodeViewModelTemp));
                                OnPropertyChanged(nameof(ChildNodeViewModelFromChip));
                                SaveReadDataToFile(device.MifareDESFireData);
                                await UpdateReaderStatusCommand.ExecuteAsync(false);
                                return;
                            }
                            else
                            {
                                await SetErrorStatusAsync(result, "{0}: Unable to Read File with FileID: {1}: {2}", DateTime.Now, FileNumberCurrentAsInt, result.ToString());
                                return;
                            }
                        }
                        else
                        {
                            await SetErrorStatusAsync(result, "{0}: Unable to Read File: {1}", DateTime.Now, result.ToString());
                            return;
                        }
                    }
                }
                else
                {
                    CurrentTaskErrorLevel = ERROR.TransportError;
                    await UpdateReaderStatusCommand.ExecuteAsync(false);
                    return;
                }
            }

            await FinalizeTaskAsync();
            return;
        }

        /// <summary>
        /// Opens a data file and loads its hex payload into the in-memory DESFire buffer.
        /// </summary>
        public ICommand GetDataFromFileCommand => new RelayCommand(OnNewGetDataFromFileCommand);
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
                    if (TryLoadDesfireDataFromFile(dlg.FileName, out var bytes, out var errorMessage))
                    {
                        DesfireDataFilePath = dlg.FileName;
                        ApplyDesfireFileData(bytes);
                        StatusText += string.Format("{0}: Loaded {1} bytes from {2}\n", DateTime.Now, bytes.Length, Path.GetFileName(dlg.FileName));
                    }
                    else
                    {
                        StatusText += string.Format("{0}: Unable to load data file: {1}\n", DateTime.Now, errorMessage);
                    }
                }
                catch (Exception e)
                {
                    eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
                }
            }
        }

        /// <summary>
        /// Selects the target file to write DESFire read data to.
        /// </summary>
        public ICommand SelectReadDataFileCommand => new RelayCommand(OnNewSelectReadDataFileCommand);
        private void OnNewSelectReadDataFileCommand()
        {
            var dlg = new SaveFileDialogViewModel
            {
                Title = ResourceLoader.GetResource("windowCaptionSaveDesfireReadData"),
                Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                RestoreDirectory = true
            };

            if (dlg.Show(this.Dialogs) && !string.IsNullOrWhiteSpace(dlg.FileName))
            {
                DesfireReadDataFilePath = dlg.FileName;
            }
        }

        /// <summary>
        /// Writes the current DESFire buffer contents to the configured file, optionally reloading from disk.
        /// </summary>
        public IAsyncRelayCommand WriteDataCommand => new AsyncRelayCommand(OnNewWriteDataCommand);
        private async Task OnNewWriteDataCommand()
        {
            CurrentTaskErrorLevel = ERROR.Empty;

            if (RefreshDesfireDataFromFileBeforeWrite)
            {
                if (string.IsNullOrWhiteSpace(DesfireDataFilePath))
                {
                    StatusText += string.Format("{0}: Data file refresh requested but no file has been selected.\n", DateTime.Now);
                    CurrentTaskErrorLevel = ERROR.Unknown;
                    return;
                }

                if (!TryLoadDesfireDataFromFile(DesfireDataFilePath, out var bytes, out var errorMessage))
                {
                    StatusText += string.Format("{0}: Unable to reload data file: {1}\n", DateTime.Now, errorMessage);
                    CurrentTaskErrorLevel = ERROR.Unknown;
                    return;
                }

                ApplyDesfireFileData(bytes);
            }

            using (var device = ReaderDevice.Instance)
            {
                if (device != null)
                {
                    await UpdateReaderStatusCommand.ExecuteAsync(true);

                    StatusText = string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxDllLoaded"));

                    if (CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireWriteKeyCurrent) == KEY_ERROR.NO_ERROR)
                    {

                        var result = await device.AuthToMifareDesfireApplication(
                                                                         DesfireWriteKeyCurrent,
                                                                         SelectedDesfireWriteKeyEncryptionType,
                                                                         selectedDesfireWriteKeyNumberAsInt, AppNumberCurrentAsInt);

                        if (IsValidAppNumberNew != false && result == ERROR.NoError)
                        {
                            StatusText += string.Format("{0}: Successfully Authenticated to App {1}\n", DateTime.Now, AppNumberCurrentAsInt);

                            if (!TryGetDesfireWritePayload(out var payload, out var errorMessage))
                            {
                                await SetErrorStatusAsync(ERROR.Unknown, "{0}: Unable to prepare DESFire data payload: {1}\n", DateTime.Now, errorMessage);
                                return;
                            }

                            result = await device.WriteMiFareDESFireChipFile(DesfireWriteKeyCurrent, SelectedDesfireWriteKeyEncryptionType, selectedDesfireWriteKeyNumberAsInt,
                                                                   EncryptionMode.CM_ENCRYPT, FileNumberCurrentAsInt, AppNumberCurrentAsInt, payload);

                            if (result == ERROR.NoError)
                            {
                                StatusText += string.Format("{0}: Successfully Created FileNo: {1} with Size: {2} in AppID: {3}\n", DateTime.Now, FileNumberCurrentAsInt, FileSizeCurrentAsInt, AppNumberNewAsInt);
                                CurrentTaskErrorLevel = result;
                                await UpdateReaderStatusCommand.ExecuteAsync(false);
                                return;
                            }
                            else
                            {
                                await SetErrorStatusAsync(result, "{0}: Unable to Write Data: {1}\n", DateTime.Now, result.ToString());
                                return;
                            }
                        }
                        else
                        {
                            await SetErrorStatusAsync(result, "{0}: Unable to Write Data: {1}\n", DateTime.Now, result.ToString());
                            return;
                        }
                    }
                }
                else
                {
                    CurrentTaskErrorLevel = ERROR.TransportError;
                    await UpdateReaderStatusCommand.ExecuteAsync(false);
                    return;
                }
            }

            await FinalizeTaskAsync();
            return;
        }

        private bool TryLoadDesfireDataFromFile(string filePath, out byte[] data, out string errorMessage)
        {
            data = null;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(filePath))
            {
                errorMessage = "File path is empty.";
                return false;
            }

            if (!File.Exists(filePath))
            {
                errorMessage = "File does not exist.";
                return false;
            }

            var fileContents = File.ReadAllText(filePath);
            var discarded = 0;
            var parsedBytes = CustomConverter.GetBytes(fileContents, out discarded);

            if (parsedBytes.Length == 0)
            {
                errorMessage = "File does not contain any valid hex data.";
                return false;
            }

            data = parsedBytes;
            return true;
        }

        private void ApplyDesfireFileData(byte[] data)
        {
            var targetNode = EnsureDesfireDataNode(childNodeViewModelTemp, FileNumberCurrentAsInt);

            targetNode.DesfireFile = new MifareDesfireFileModel(data, (byte)FileNumberCurrentAsInt);
            targetNode.SelectedDataIndexStartInBytes = 0;
            targetNode.SelectedDataLengthInBytes = data.Length;

            OnPropertyChanged(nameof(ChildNodeViewModelTemp));
            OnPropertyChanged(nameof(ChildNodeViewModelFromChip));
        }

        /// <summary>
        /// Attempts to resolve the selected DESFire data slice for writing based on offset and length.
        /// </summary>
        /// <param name="payload">The resolved payload to write.</param>
        /// <param name="errorMessage">Error details when resolution fails.</param>
        /// <returns>True when a valid payload is produced; otherwise false.</returns>
        internal bool TryGetDesfireWritePayload(out byte[] payload, out string errorMessage)
        {
            payload = null;
            errorMessage = null;

            var targetNode = FindDesfireDataNode(childNodeViewModelTemp, FileNumberCurrentAsInt);
            if (targetNode?.DesfireFile?.Data == null)
            {
                errorMessage = "No DESFire data has been loaded.";
                return false;
            }

            var data = targetNode.DesfireFile.Data;
            var startIndex = targetNode.SelectedDataIndexStartInBytes;
            var length = targetNode.SelectedDataLengthInBytes;

            if (startIndex < 0)
            {
                errorMessage = "Start index must be zero or positive.";
                return false;
            }

            if (length <= 0)
            {
                errorMessage = "Length must be greater than zero.";
                return false;
            }

            if (startIndex + length > data.Length)
            {
                errorMessage = "Selected range exceeds available data length.";
                return false;
            }

            payload = new byte[length];
            Buffer.BlockCopy(data, startIndex, payload, 0, length);
            return true;
        }

        /// <summary>
        /// Finds the best matching DESFire data node for the current file number.
        /// </summary>
        /// <param name="parentNode">Parent node that holds DESFire file children.</param>
        /// <param name="fileId">File ID to match when available.</param>
        /// <returns>The matching node or null if none exist.</returns>
        private static RFiDChipGrandChildLayerViewModel FindDesfireDataNode(
            RFiDChipChildLayerViewModel parentNode,
            int fileId)
        {
            if (parentNode?.Children == null)
            {
                return null;
            }

            var dataNodes = parentNode.Children.Where(node => node?.DesfireFile != null).ToList();
            if (dataNodes.Count == 0)
            {
                return null;
            }

            var matchingNode = dataNodes.FirstOrDefault(node => node.DesfireFile.FileID == fileId);
            return matchingNode ?? dataNodes.FirstOrDefault();
        }

        /// <summary>
        /// Ensures a DESFire data node exists for the given file identifier.
        /// </summary>
        /// <param name="parentNode">Parent node that holds DESFire file children.</param>
        /// <param name="fileId">File ID to match.</param>
        /// <returns>A valid DESFire data node.</returns>
        private static RFiDChipGrandChildLayerViewModel EnsureDesfireDataNode(
            RFiDChipChildLayerViewModel parentNode,
            int fileId)
        {
            if (parentNode == null)
            {
                return null;
            }

            var node = FindDesfireDataNode(parentNode, fileId);
            if (node != null)
            {
                return node;
            }

            node = new RFiDChipGrandChildLayerViewModel(new MifareDesfireFileModel(new byte[0], (byte)fileId), parentNode);
            parentNode.Children.Add(node);
            return node;
        }

        /// <summary>
        /// Builds the output path for read data based on overwrite settings.
        /// </summary>
        /// <param name="timestamp">Timestamp used to suffix the file name when not overwriting.</param>
        /// <returns>The output path, or null when no target path is configured.</returns>
        internal string BuildReadDataOutputPath(DateTime timestamp)
        {
            if (string.IsNullOrWhiteSpace(DesfireReadDataFilePath))
            {
                return null;
            }

            if (OverwriteReadDataFileOnRead)
            {
                return DesfireReadDataFilePath;
            }

            var directory = Path.GetDirectoryName(DesfireReadDataFilePath);
            var baseName = Path.GetFileNameWithoutExtension(DesfireReadDataFilePath);
            var extension = Path.GetExtension(DesfireReadDataFilePath);
            var stamp = timestamp.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            var fileName = string.Format("{0}_{1}{2}", baseName, stamp, extension);

            return string.IsNullOrWhiteSpace(directory)
                ? fileName
                : Path.Combine(directory, fileName);
        }

        private void SaveReadDataToFile(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                StatusText += string.Format("{0}: No data available to save.\n", DateTime.Now);
                return;
            }

            var outputPath = BuildReadDataOutputPath(DateTime.Now);
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                StatusText += string.Format("{0}: Read data file path is not configured.\n", DateTime.Now);
                return;
            }

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                StatusText += string.Format("{0}: Target directory does not exist: {1}\n", DateTime.Now, directory);
                return;
            }

            try
            {
                var hexPayload = CustomConverter.HexToString(data);
                File.WriteAllText(outputPath, hexPayload);
                StatusText += string.Format("{0}: Saved read data to {1}\n", DateTime.Now, outputPath);
            }
            catch (Exception e)
            {
                eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
                StatusText += string.Format("{0}: Unable to save read data: {1}\n", DateTime.Now, e.Message);
            }
        }

        /// <summary>
        /// Changes the selected DESFire application key with the provided new key value and version. Only when the APP ID is > 0.
        /// </summary>
        public IAsyncRelayCommand ChangeAppKeyCommand => new AsyncRelayCommand(OnNewChangeAppKeyCommand);
        private async Task OnNewChangeAppKeyCommand()
        {
            CurrentTaskErrorLevel = ERROR.Empty;

            if (ShouldBlockAppKeyChange())
            {
                return;
            }

            IsDesfireAppCreationTabEnabled = false;

            using (var device = ReaderDevice.Instance)
            {
                if (device != null)
                {
                    await UpdateReaderStatusCommand.ExecuteAsync(true);

                    StatusText = string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxDllLoaded"));

                    var changeKeyMode = GetChangeKeyModeForApplication(AppNumberCurrentAsInt);
                    var authKeyNo = GetAuthKeyNumberForChangeAppKey(
                        AppNumberCurrentAsInt,
                        changeKeyMode,
                        selectedDesfireAppKeyNumberCurrentAsInt);

                    var authKeyValue = DesfireAppKeyCurrent;
                    var oldKeyForTargetSlot = ShowAppKeyOldInputs ? DesfireAppKeyCurrentOld : DesfireAppKeyCurrent;
                    var keyNumberForChange = AppNumberCurrentAsInt == 0 ? 0 : selectedDesfireAppKeyNumberCurrentAsInt;
                    var numberOfKeys = AppNumberCurrentAsInt == 0 ? 1 : 15;

                    var isAuthKeyValid = CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(authKeyValue) == KEY_ERROR.NO_ERROR;
                    var isOldKeyValid = !ShowAppKeyOldInputs || CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(oldKeyForTargetSlot) == KEY_ERROR.NO_ERROR;

                    if (isAuthKeyValid && isOldKeyValid)
                    {
                        AppendChangeAppKeyAuthStatusLines(authKeyNo, changeKeyMode);

                        var result = await device.AuthToMifareDesfireApplication(
                                authKeyValue,
                                SelectedDesfireAppKeyEncryptionTypeCurrent,
                                authKeyNo,
                                AppNumberCurrentAsInt);

                        if (IsValidAppNumberCurrent != false &&
                            IsValidAppNumberTarget != false &&
                            IsValidDesfireAppKeyTarget != false &&
                            IsValidDesfireAppKeyVersionTarget != false &&
                            result == ERROR.NoError)
                        {
                            StatusText += string.Format("{0}: Successfully Authenticated to AppID {1}\n", DateTime.Now, AppNumberCurrentAsInt);
                            await TryUpdateKeyVersionAsync(device, keyNumberForChange);

                            var keySettings = GetChangeKeyModeForApplication(AppNumberCurrentAsInt);
                            var payload = BuildAppKeyChangePayload(
                                AppNumberCurrentAsInt,
                                keyNumberForChange,
                                authKeyValue,
                                oldKeyForTargetSlot,
                                keySettings);

                            result = await device.ChangeMifareDesfireKeyAsync(
                                payload.AppId,
                                payload.TargetKeyNo,
                                payload.TargetKeyType,
                                payload.CurrentTargetKeyHex,
                                payload.NewTargetKeyHex,
                                payload.NewTargetKeyVersion,
                                payload.MasterKeyHex,
                                payload.MasterKeyType,
                                payload.KeySettings);

                            if (await SetOperationResultAsync(
                                    result,
                                    "{0}: Successfully Changed Key {1} of AppID {2}\n",
                                    new object[] { DateTime.Now, selectedDesfireAppKeyNumberCurrentAsInt, AppNumberCurrentAsInt },
                                    "{0}: Unable to Change Key {1} of AppID {2}: {3}\n",
                                    new object[] { DateTime.Now, selectedDesfireAppKeyNumberCurrentAsInt, AppNumberCurrentAsInt, result.ToString() }))
                            {
                                return;
                            }
                            return;
                        }
                        else
                        {
                            await SetOperationResultAsync(
                                result,
                                "{0}: Successfully Changed Key {1} of AppID {2}\n",
                                new object[] { DateTime.Now, selectedDesfireAppKeyNumberCurrentAsInt, AppNumberTargetAsInt },
                                "{0}: Unable to Change Key {1} of AppID {2}: {3}\n",
                                new object[] { DateTime.Now, selectedDesfireAppKeyNumberCurrentAsInt, AppNumberTargetAsInt, result.ToString() });
                            return;
                        }
                    }
                }
                else
                {
                    CurrentTaskErrorLevel = ERROR.TransportError;
                    await UpdateReaderStatusCommand.ExecuteAsync(false);
                    return;
                }
            }

            await FinalizeTaskAsync();
            return;
        }

        /// <summary>
        /// Sets a status reminder if the PICC master key should be used instead of an application key.
        /// </summary>
        /// <returns>True when the change-app-key command should be blocked.</returns>
        private bool ShouldBlockAppKeyChange()
        {
            if (AppNumberCurrentAsInt > 0)
            {
                return false;
            }

            StatusText = string.Format(
                "{0}: Select the PICC master key tab to change the PICC master key (App ID 0).\n",
                DateTime.Now);
            return true;
        }

        /// <summary>
        /// Authenticates with the selected PICC or application master key and updates the DESFire key settings.
        /// For PICC (AID 0) the change-key bits are forced to 0x00; applications honour the UI-selected
        /// change-key mode (0x00/0xE0/0xF0) while general flags are derived from the existing checkboxes.
        /// </summary>
        public IAsyncRelayCommand ChangeAppKeySettingsCommand => new AsyncRelayCommand(OnNewChangeAppKeySettingsCommand);
        private async Task OnNewChangeAppKeySettingsCommand()
        {
            CurrentTaskErrorLevel = ERROR.Empty;

            using (var device = ReaderDevice.Instance)
            {
                if (device != null)
                {
                    await UpdateReaderStatusCommand.ExecuteAsync(true);

                    StatusText = string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxDllLoaded"));

                    var appId = AppNumberCurrentAsInt;
                    var authKey = appId == 0 ? DesfireMasterKeyCurrent : DesfireAppKeyCurrent;
                    var authKeyType = appId == 0 ? SelectedDesfireMasterKeyEncryptionTypeCurrent : SelectedDesfireAppKeyEncryptionTypeCurrent;
                    var authKeyNumber = 0;

                    try
                    {
                        var keySettings = BuildSelectedKeySettings(appId);

                        if (CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(authKey) == KEY_ERROR.NO_ERROR)
                        {
                            var result = await device.AuthToMifareDesfireApplication(
                                authKey,
                                authKeyType,
                                authKeyNumber,
                                appId);

                            if (IsValidAppNumberCurrent != false && IsValidDesfireAppKeyVersionTarget != false && result == ERROR.NoError)
                            {
                                StatusText += string.Format("{0}: Successfully Authenticated to AppID {1}\n", DateTime.Now, appId);
                                await TryUpdateKeyVersionAsync(device, authKeyNumber);

                                var updateResult = await device.ChangeMifareDesfireApplicationKeySettings(
                                    authKey,
                                    authKeyNumber,
                                    authKeyType,
                                    appId,
                                    keySettings);

                                if (await SetOperationResultAsync(
                                        updateResult,
                                        "{0}: Successfully Updated Key Settings of AppID {1}\n",
                                        new object[] { DateTime.Now, appId },
                                        "{0}: Unable to Update Key Settings of AppID {1}: {2}\n",
                                        new object[] { DateTime.Now, appId, updateResult.ToString() }))
                                {
                                    return;
                                }

                                return;
                            }
                            else
                            {
                                await SetOperationResultAsync(
                                    result,
                                    "{0}: Successfully Updated Key Settings of AppID {1}\n",
                                    new object[] { DateTime.Now, appId },
                                    "{0}: Unable to Update Key Settings of AppID {1}: {2}\n",
                                    new object[] { DateTime.Now, appId, result.ToString() });
                                return;
                            }
                        }
                    }
                    catch (ArgumentException ex)
                    {
                        StatusText += string.Format("{0}: {1}\n", DateTime.Now, ex.Message);
                        CurrentTaskErrorLevel = ERROR.ProtocolConstraint;
                        await UpdateReaderStatusCommand.ExecuteAsync(false);
                        return;
                    }
                }
                else
                {
                    CurrentTaskErrorLevel = ERROR.TransportError;
                    await UpdateReaderStatusCommand.ExecuteAsync(false);
                    return;
                }
            }

            await FinalizeTaskAsync();
            return;
        }

        /// <summary>
        ///
        /// </summary>
        public IAsyncRelayCommand DeleteSignleCardApplicationCommand => new AsyncRelayCommand(OnNewDeleteSignleCardApplicationCommand);
        private async Task OnNewDeleteSignleCardApplicationCommand()
        {
            CurrentTaskErrorLevel = ERROR.Empty;

            using (var device = ReaderDevice.Instance)
            {
                if (device != null)
                {
                    await UpdateReaderStatusCommand.ExecuteAsync(true);

                    StatusText = string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxDllLoaded"));

                    if (CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireAppKeyCurrent) == KEY_ERROR.NO_ERROR)
                    {
                        var result = await device.AuthToMifareDesfireApplication(
                                DesfireMasterKeyCurrent,
                                SelectedDesfireMasterKeyEncryptionTypeCurrent,
                                0);

                        if (IsValidAppNumberCurrent != false && result == ERROR.NoError)
                        {
                            StatusText += string.Format("{0}: Successfully Authenticated to PICC Master App 0\n", DateTime.Now);

                            result = await device.DeleteMifareDesfireApplication(
                                DesfireMasterKeyCurrent,
                                SelectedDesfireMasterKeyEncryptionTypeCurrent,
                                (uint)AppNumberNewAsInt);

                            if (await SetOperationResultAsync(
                                    result,
                                    "{0}: Successfully Deleted AppID {1}\n",
                                    new object[] { DateTime.Now, AppNumberNewAsInt },
                                    "{0}: Unable to Remove AppID {1}: {2}\n",
                                    new object[] { DateTime.Now, AppNumberNewAsInt, result.ToString() }))
                            {
                                return;
                            }
                            return;
                        }

                        else
                        {
                            StatusText += string.Format("{0}: Authentication to PICC failed. Try without Authentication...\n", DateTime.Now);

                            result = await device.DeleteMifareDesfireApplication(
                                DesfireMasterKeyCurrent,
                                SelectedDesfireMasterKeyEncryptionTypeCurrent,
                                (uint)AppNumberNewAsInt);

                            if (await SetOperationResultAsync(
                                    result,
                                    "{0}: Successfully deleted AppID {1}\n",
                                    new object[] { DateTime.Now, AppNumberNewAsInt },
                                    "{0}: Unable to deleted App: {1}\n",
                                    new object[] { DateTime.Now, result.ToString() }))
                            {
                                return;
                            }
                            return;
                        }
                    }
                }
                else
                {
                    CurrentTaskErrorLevel = ERROR.TransportError;
                    await UpdateReaderStatusCommand.ExecuteAsync(false);
                    return;
                }
            }

            await FinalizeTaskAsync();
            return;
        }

        /// <summary>
        /// 
        /// </summary>
        public IAsyncRelayCommand DeleteFileCommand => new AsyncRelayCommand(OnNewDeleteFileCommand);
        private async Task OnNewDeleteFileCommand()
        {
            CurrentTaskErrorLevel = ERROR.Empty;

            using (var device = ReaderDevice.Instance)
            {
                if (device != null)
                {
                    await UpdateReaderStatusCommand.ExecuteAsync(true);

                    StatusText = string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxDllLoaded"));

                    if (CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireAppKeyCurrent) == KEY_ERROR.NO_ERROR)
                    {
                        var result = await device.AuthToMifareDesfireApplication(
                                DesfireAppKeyCurrent,
                                SelectedDesfireAppKeyEncryptionTypeCurrent,
                                selectedDesfireAppKeyNumberCurrentAsInt, AppNumberCurrentAsInt);

                        if (IsValidAppNumberCurrent != false && result == ERROR.NoError)
                        {

                            StatusText += string.Format("{0}: Successfully Authenticated to App {1}\n", DateTime.Now, AppNumberCurrentAsInt);

                            result = await device.DeleteMifareDesfireFile(
                                DesfireAppKeyCurrent,
                                SelectedDesfireAppKeyEncryptionTypeCurrent,
                                AppNumberCurrentAsInt, FileNumberCurrentAsInt);

                            if (await SetOperationResultAsync(
                                    result,
                                    "{0}: Successfully Deleted File {1}\n",
                                    new object[] { DateTime.Now, FileNumberCurrentAsInt },
                                    "{0}: Unable to Remove FileID {1}: {2}\n",
                                    new object[] { DateTime.Now, FileNumberCurrentAsInt, result.ToString() }))
                            {
                                return;
                            }
                            return;
                        }
                        else
                        {
                            StatusText += string.Format("{0}: Unable to Authenticate to App {1}; Try to Continue Anyway...\n", DateTime.Now, AppNumberCurrentAsInt);

                            result = await device.DeleteMifareDesfireFile(
                                DesfireAppKeyCurrent,
                                SelectedDesfireAppKeyEncryptionTypeCurrent,
                                AppNumberNewAsInt, FileNumberCurrentAsInt);

                            if (await SetOperationResultAsync(
                                    result,
                                    "{0}: Successfully Deleted File {1}\n",
                                    new object[] { DateTime.Now, FileNumberCurrentAsInt },
                                    "{0}: Unable to Remove AppID {1}: {2}\n",
                                    new object[] { DateTime.Now, AppNumberNewAsInt, result.ToString() }))
                            {
                                return;
                            }
                            return;
                        }
                    }
                }
                else
                {
                    CurrentTaskErrorLevel = ERROR.TransportError;
                    await UpdateReaderStatusCommand.ExecuteAsync(false);
                    return;
                }
            }

            await FinalizeTaskAsync();
            return;
        }

        /// <summary>
        /// public ICommand FormatDesfireCardCommand { get { return new RelayCommand<ReaderDevice>((_device) => OnNewFormatDesfireCardCommand(_device)); }}
        /// </summary>
        public IAsyncRelayCommand FormatDesfireCardCommand => new AsyncRelayCommand(OnNewFormatDesfireCardCommand);
        private async Task OnNewFormatDesfireCardCommand()
        {
            CurrentTaskErrorLevel = ERROR.Empty;

            using (var device = ReaderDevice.Instance)
            {
                if (device != null)
                {
                    await UpdateReaderStatusCommand.ExecuteAsync(true);

                    StatusText = string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxDllLoaded"));

                    if (CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireAppKeyCurrent) == KEY_ERROR.NO_ERROR)
                    {
                        var result = await device.AuthToMifareDesfireApplication(
                                DesfireMasterKeyCurrent,
                                SelectedDesfireMasterKeyEncryptionTypeCurrent,
                                0);

                        if (IsValidAppNumberCurrent != false && result == ERROR.NoError)
                        {
                            StatusText += string.Format("{0}: Successfully Authenticated to PICC Master App 0\n", DateTime.Now);

                            result = await device.GetMiFareDESFireChipAppIDs(
                                DesfireMasterKeyCurrent,
                                SelectedDesfireMasterKeyEncryptionTypeCurrent);

                            if (result == ERROR.NoError)
                            {
                                if (device?.DesfireChip?.AppIDs != null)
                                {
                                    foreach (var appID in device.DesfireChip.AppIDs)
                                    {
                                        StatusText += string.Format("{0}: FoundAppID {1}\n", DateTime.Now, appID);
                                    }
                                }

                                result = await device.FormatDesfireCard(DesfireMasterKeyCurrent, SelectedDesfireMasterKeyEncryptionTypeCurrent);

                                if (await SetOperationResultAsync(
                                        result,
                                        "{0}: Successfully Formatted Card\n",
                                        new object[] { DateTime.Now },
                                        "{0}: Unable to Format Card: {1}\n",
                                        new object[] { DateTime.Now, result.ToString() }))
                                {
                                    return;
                                }
                                return;
                            }

                            else
                            {
                                StatusText += string.Format("{0}: Unable to get Directory Listing, Try to Continue anyway...\n", DateTime.Now);

                                result = await device.FormatDesfireCard(DesfireMasterKeyCurrent, SelectedDesfireMasterKeyEncryptionTypeCurrent);

                                if (await SetOperationResultAsync(
                                        result,
                                        "{0}: Successfully Formatted Card\n",
                                        new object[] { DateTime.Now },
                                        "{0}: Unable to Format Card: {1}\n",
                                        new object[] { DateTime.Now, result.ToString() }))
                                {
                                    return;
                                }
                                return;
                            }
                        }
                        else
                        {
                            StatusText += string.Format("{0}: Unable to Format Card: {1}\n", DateTime.Now, result.ToString());
                            CurrentTaskErrorLevel = result;
                            await UpdateReaderStatusCommand.ExecuteAsync(false);
                            return;
                        }
                    }
                }
                else
                {
                    CurrentTaskErrorLevel = ERROR.TransportError;
                    await UpdateReaderStatusCommand.ExecuteAsync(false);
                    return;
                }
            }

            await FinalizeTaskAsync();
            return;
        }

        /// <summary>
        ///
        /// </summary>
        public IAsyncRelayCommand AuthenticateToCardApplicationCommand => new AsyncRelayCommand(OnNewAuthenticateToCardApplicationCommand);
        private async Task OnNewAuthenticateToCardApplicationCommand()
        {
            CurrentTaskErrorLevel = ERROR.Empty;

            if (SelectedTaskType == TaskType_MifareDesfireTask.ReadAppSettings)
            {
                return;
            }

            else if (SelectedTaskType == TaskType_MifareDesfireTask.AppExistCheck)
            {
                await UpdateReaderStatusCommand.ExecuteAsync(true);
                await DoesAppExistCommand(new MifareDesfireChipModel());
                await UpdateReaderStatusCommand.ExecuteAsync(false);
                return;
            }

            using (var device = ReaderDevice.Instance)
            {
                if (device != null)
                {
                    await UpdateReaderStatusCommand.ExecuteAsync(true);

                    StatusText = string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxDllLoaded"));

                    if (CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireAppKeyCurrent) == KEY_ERROR.NO_ERROR)
                    {
                        var result = await device.AuthToMifareDesfireApplication(
                                DesfireAppKeyCurrent,
                                SelectedDesfireAppKeyEncryptionTypeCurrent,
                                selectedDesfireAppKeyNumberCurrentAsInt,
                                AppNumberCurrentAsInt);

                        if (IsValidAppNumberCurrent != false && result == ERROR.NoError)
                        {
                            await TryUpdateKeyVersionAsync(device, selectedDesfireAppKeyNumberCurrentAsInt);
                            StatusText += string.Format("{0}: Successfully Authenticated to App {1}\n", DateTime.Now, AppNumberCurrentAsInt);
                            await UpdateReaderStatusCommand.ExecuteAsync(false);
                            CurrentTaskErrorLevel = result;
                        }
                        else
                        {
                            StatusText += string.Format("{0}: Unable to Authenticate: {1}\n", DateTime.Now, result.ToString());
                            await UpdateReaderStatusCommand.ExecuteAsync(false);
                            CurrentTaskErrorLevel = result;
                        }

                    }
                }
            }

            await FinalizeTaskAsync();
            return;
        }

        /// <summary>
        /// Changes the PICC master key using the provided target key and version.
        /// </summary>
        public IAsyncRelayCommand ChangeMasterCardKeyCommand => new AsyncRelayCommand(OnNewChangeMasterCardKeyCommand);
        private async Task OnNewChangeMasterCardKeyCommand()
        {
            CurrentTaskErrorLevel = ERROR.Empty;

            using (var device = ReaderDevice.Instance)
            {
                if (device != null)
                {
                    await UpdateReaderStatusCommand.ExecuteAsync(true);

                    StatusText = string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxDllLoaded"));

                    if (CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireMasterKeyCurrent) == KEY_ERROR.NO_ERROR)
                    {
                        var keySettings = GetPiccMasterKeyChangeSettings();

                        var result = await device.AuthToMifareDesfireApplication(
                            CustomConverter.DesfireKeyToCheck,
                            SelectedDesfireMasterKeyEncryptionTypeCurrent,
                            0);

                        if (result == ERROR.NoError)
                        {
                            await TryUpdateKeyVersionAsync(device, 0);
                            StatusText += string.Format("{0}: Successfully Authenticated to App 0\n", DateTime.Now);

                            if (IsValidDesfireMasterKeyCurrent != false &&
                                IsValidDesfireMasterKeyTarget != false &&
                                IsValidKeyVersionTarget != false)
                            {
                                result = await device.ChangeMifareDesfireKeyAsync(
                                    0,
                                    0,
                                    SelectedDesfireMasterKeyEncryptionTypeTarget,
                                    DesfireMasterKeyCurrent,
                                    DesfireMasterKeyTarget,
                                    (byte)keyVersionTargetAsInt,
                                    DesfireMasterKeyCurrent,
                                    SelectedDesfireMasterKeyEncryptionTypeCurrent,
                                    keySettings);

                                if (result == ERROR.NoError)
                                {
                                    StatusText += string.Format("{0}: Keychange Successfull\n", DateTime.Now);
                                    CurrentTaskErrorLevel = result;
                                    await UpdateReaderStatusCommand.ExecuteAsync(false);
                                    return;
                                }
                                else
                                {
                                    StatusText += string.Format("{0}: Unable to Change Key: {1}\n", DateTime.Now, result.ToString());
                                    CurrentTaskErrorLevel = result;
                                    await UpdateReaderStatusCommand.ExecuteAsync(false);
                                    return;
                                }
                            }
                            else
                            {
                                StatusText += string.Format("{0}: Key Error: Wrong Format\n", DateTime.Now);
                                CurrentTaskErrorLevel = ERROR.AuthFailure;
                                await UpdateReaderStatusCommand.ExecuteAsync(false);
                                return;
                            }
                        }
                        else
                        {
                            StatusText += string.Format("{0}: {1}: {2}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxUnableToAuthenticate"), result.ToString());
                            CurrentTaskErrorLevel = result;
                            await UpdateReaderStatusCommand.ExecuteAsync(false);
                            return;
                        }
                    }
                }
                else
                {
                    CurrentTaskErrorLevel = ERROR.TransportError;
                    await UpdateReaderStatusCommand.ExecuteAsync(false);
                    return;
                }
            }

            await FinalizeTaskAsync();
            return;
        }

        /// <summary>
        /// Returns the minimal key settings used when changing the PICC master key.
        /// </summary>
        internal static DESFireKeySettings GetPiccMasterKeyChangeSettings() => DESFireKeySettings.ChangeKeyWithMasterKey;

        /// <summary>
        ///
        /// </summary>
        public IAsyncRelayCommand AuthenticateToCardMasterApplicationCommand => new AsyncRelayCommand(OnNewAuthenticateToCardMasterApplicationCommand);
        private async Task OnNewAuthenticateToCardMasterApplicationCommand()
        {
            CurrentTaskErrorLevel = ERROR.Empty;

            using (var device = ReaderDevice.Instance)
            {
                if (device != null)
                {
                    await UpdateReaderStatusCommand.ExecuteAsync(true);

                    StatusText = string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxDllLoaded"));

                    if (CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireMasterKeyCurrent) == KEY_ERROR.NO_ERROR)
                    {
                        var result = await device.AuthToMifareDesfireApplication(
                            DesfireMasterKeyCurrent,
                            SelectedDesfireMasterKeyEncryptionTypeCurrent,
                            0);

                        if (result == ERROR.NoError)
                        {
                            await TryUpdateKeyVersionAsync(device, 0);
                            StatusText += string.Format("{0}: Successfully Authenticated to App 0\n", DateTime.Now);
                            CurrentTaskErrorLevel = result;
                            await UpdateReaderStatusCommand.ExecuteAsync(false);
                            return;
                        }
                        else
                        {
                            StatusText += string.Format("{0}: {1}: {2}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxUnableToAuthenticate"), result.ToString());
                            CurrentTaskErrorLevel = result;
                            await UpdateReaderStatusCommand.ExecuteAsync(false);
                            return;
                        }

                    }
                }
            }

            await FinalizeTaskAsync();
            return;

        }

        #endregion Commands

        #region Methods

        /// <summary>
        /// Query the active reader for the current key version and update the bound property.
        /// </summary>
        /// <param name="device">The reader device to query.</param>
        /// <param name="keyNumber">The key slot number whose version should be read.</param>
        private async Task TryUpdateKeyVersionAsync(ReaderDevice device, int keyNumber)
        {
            try
            {
                var keyVersion = await device.MifareDesfire_GetKeyVersionAsync((byte)keyNumber);
                KeyVersionCurrent = keyVersion.ToString("X2", CultureInfo.InvariantCulture);
            }
            catch (Exception e)
            {
                StatusText += string.Format("{0}: Unable to read key version: {1}\n", DateTime.Now, e.Message);
            }
        }

        /// <summary>
        ///
        /// </summary>
        public async Task ReadAppSettingsCommand()
        {
            CurrentTaskErrorLevel = ERROR.Empty;

            using (var device = ReaderDevice.Instance)
            {
                if (device != null)
                {
                    StatusText = string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxDllLoaded"));

                    if (CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireAppKeyCurrent) == KEY_ERROR.NO_ERROR)
                    {
                        var result = await device.GetMifareDesfireAppSettings(
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

                        if (IsValidAppNumberCurrent != false && result.Code == ERROR.NoError)
                        {
                            StatusText += string.Format("{0}: Successfully Read App Settings of App {1}\n", DateTime.Now, AppNumberCurrentAsInt);

                            if (((byte)device.DesfireAppKeySetting & (byte)keySettings) != 0)
                            {
                                CurrentTaskErrorLevel = ERROR.NoError;
                            }
                            else
                            {
                                CurrentTaskErrorLevel = ERROR.IsNotTrue;
                            }
                        }
                        else
                        {
                            StatusText += string.Format("{0}: Unable to Authenticate: {1}\n", DateTime.Now, result.Message ?? result.Code.ToString());
                            CurrentTaskErrorLevel = result.Code;
                        }

                    }
                }
            }

            await FinalizeTaskAsync();
            return;
        }

        /// <summary>
        ///
        /// </summary>
        public async Task<ERROR> DoesAppExistCommand(MifareDesfireChipModel desfireChip)
        {
            CurrentTaskErrorLevel = ERROR.Empty;

            using (var device = ReaderDevice.Instance)
            {
                if (device != null)
                {
                    StatusText = string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxDllLoaded"));

                    if (CustomConverter.FormatMifareDesfireKeyStringWithSpacesEachByte(DesfireAppKeyCurrent) == KEY_ERROR.NO_ERROR)
                    {
                        var result = await device.GetMiFareDESFireChipAppIDs(
                                DesfireAppKeyCurrent,
                                SelectedDesfireAppKeyEncryptionTypeCurrent);

                        desfireChip.FreeMemory = device.DesfireChip.FreeMemory;
                        //desfireChip.UID = device.GenericChip.Select(x => x.UID).UID;

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
                        else if (IsValidAppNumberCurrent != false && result == ERROR.AuthFailure)
                        {
                            StatusText += string.Format("{0}: Failed. Directory Listing is not allowed and PICC MK is Incorrect.\n", DateTime.Now);

                            CurrentTaskErrorLevel = ERROR.AuthFailure;
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

            await FinalizeTaskAsync();
            return CurrentTaskErrorLevel;
        }

        #endregion

        #region IUserDialogViewModel Implementation

        /// <summary>
        /// 
        /// </summary>
        public IAsyncRelayCommand UpdateReaderStatusCommand => new AsyncRelayCommand<bool>(UpdateStatus);
        private Task UpdateStatus(bool isBusy)
        {
            if (OnUpdateStatus != null)
            {
                OnUpdateStatus(isBusy);
            }

            return Task.CompletedTask;
        }

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
        public Action<bool> OnUpdateStatus { get; set; }

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
