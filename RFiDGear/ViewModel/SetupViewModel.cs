/*
 * Created by SharpDevelop.
 * Date: 10/10/2017
 * Time: 20:31
 *
 */

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MVVMDialogs.ViewModels;

using RFiDGear.DataAccessLayer.Remote.FromIO;
using RFiDGear.DataAccessLayer;

using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows.Input;
using System.Data;
using System.Xml.Serialization;

namespace RFiDGear.ViewModel
{
    /// <summary>
    /// Description of SetupDialogBoxViewModel.
    /// </summary>
    public class SetupViewModel : ObservableObject, IUserDialogViewModel
    {
        private ReaderDevice device;

        public SetupViewModel()
        {
        }

        public SetupViewModel(ReaderDevice _device)
        {
            using (var settings = new SettingsReaderWriter())
            {
                device = _device;

                SelectedReader = settings.DefaultSpecification.DefaultReaderProvider;
                DefaultReader = Enum.GetName(typeof(ReaderTypes), SelectedReader);
                ComPort = settings.DefaultSpecification.LastUsedComPort;
                LoadOnStart = settings.DefaultSpecification.AutoLoadProjectOnStart;
                CheckOnStart = settings.DefaultSpecification.AutoCheckForUpdates;
                SelectedBaudRate = settings.DefaultSpecification.LastUsedBaudRate;
            }
        }

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

        public ICommand ReaderSeletedCommand => new RelayCommand(ReaderSelected);
        private void ReaderSelected()
        {
            ConnectToReaderCommand.Execute(null);
        }

        public IAsyncRelayCommand ConnectToReaderCommand => new AsyncRelayCommand(ConnectToReader);
        private async Task ConnectToReader()
        {
            await UpdateReaderStatusCommand.ExecuteAsync(true);

            switch (SelectedReader)
            {
                case ReaderTypes.PCSC:
                    if (device != null)
                    {
                        if (!(device is LibLogicalAccessProvider))
                        {
                            device.Dispose();
                            
                            device = new LibLogicalAccessProvider(SelectedReader);
                        }
                    }
                    else
                    {
                        device = new LibLogicalAccessProvider(SelectedReader);
                    }

                    await device.ReadChipPublic();
                    break;

                case ReaderTypes.Elatec:
                    if (device != null)
                    {
                        if (!(device is ElatecNetProvider))
                        {
                            device.Dispose();

                            device = new ElatecNetProvider();
                        }

                        if (device != null && !device.IsConnected)
                        {
                            await device.ConnectAsync();
                        }
                    }
                    else
                    {
                        device = new ElatecNetProvider();
                    }

                    await device.ReadChipPublic();
                    break;

                case ReaderTypes.None:

                    break;
            }

            if (device?.GenericChip?.UID != null)
            {
                DefaultReader = Enum.GetName(typeof(ReaderTypes), SelectedReader);

                ReaderStatus = string.Format(ResourceLoader.GetResource("labelReaderSetupReaderConnectStatus")
                                             + '\n'
                                             + "UID: {0} "
                                             + '\n'
                                             + "Type: {1}",
                                             device.GenericChip.UID, 
                                             ResourceLoader.GetResource(string.Format("ENUM.CARD_TYPE.{0}", Enum.GetName(typeof(CARD_TYPE), device.GenericChip.CardType))));

            }
            else
            {
                ReaderStatus = "no Reader / Card detected";
            }

            await UpdateReaderStatusCommand.ExecuteAsync(false);
        }

        /// <summary>
        /// 
        /// </summary>
        public IRelayCommand BeginUpdateCheckCommand => new RelayCommand<bool>(BeginUpdateCheck);
        private void BeginUpdateCheck(bool isBusy)
        {
            if (OnBeginUpdateCheck != null)
            {
                OnBeginUpdateCheck(isBusy);
            }
            else
            {
                return;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public IAsyncRelayCommand UpdateReaderStatusCommand => new AsyncRelayCommand<bool>(UpdateReaderStatus);
        private async Task UpdateReaderStatus(bool isBusy)
        {
            if (OnUpdateReaderStatus != null)
            {
                OnUpdateReaderStatus(isBusy);
            }
            else
            {
                return;
            }
        }

        public ICommand ApplyAndExitCommand => new RelayCommand(Ok);
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

        #endregion Commands

        public ReaderTypes SelectedReader
        {
            get => selectedReader;
            set
            {
                selectedReader = value;

                if (device == null)
                {
                    switch (selectedReader)
                    {
                        case ReaderTypes.Elatec:
                            device = new ElatecNetProvider();
                            break;
                        case ReaderTypes.PCSC:
                            device = new LibLogicalAccessProvider();
                            break;

                        default:
                            device = null;
                            break;
                    }
                }
            }
        }
        private ReaderTypes selectedReader;

        /// <summary>
        ///
        /// </summary>
        public string SelectedBaudRate
        {
            get => selectedBaudRate;
            set
            {
                selectedBaudRate = value;
                int.TryParse(value, out selectedBaudRateAsInt);
            }
        }
        private string selectedBaudRate;

        /// <summary>
        /// Selected Baud Rate as Integer Value
        /// </summary>
        public int SelectedBaudRateAsInt => selectedBaudRateAsInt;
        private int selectedBaudRateAsInt;

        /// <summary>
        /// ComPort for Readers that use VCP or Serial
        /// </summary>
        public string ComPort
        {
            get => comPort;
            set
            {
                comPort = value;
                if(!int.TryParse(comPort, out comPortAsInt))
                {
                    if(comPort == "USB")
                    {
                        comPortAsInt = 0;
                    }
                }
                
            }
        }
        private string comPort;
        private int comPortAsInt;

        /// <summary>
        /// BaudRate for Readers that use VCP or Serial
        /// </summary>
        public string[] BaudRates => new string[] { "1200", "2400", "4800", "9600", "115000" };

        public string ReaderStatus
        {
            get => readerStatus;
            set
            {
                readerStatus = value;
                OnPropertyChanged(nameof(ReaderStatus));
            }
        }
        private string readerStatus;

        public string DefaultReader
        {
            get => defaultReader;
            set
            {
                defaultReader = value;
                OnPropertyChanged(nameof(DefaultReader));
            }
        }
        private string defaultReader;

        /// <summary>
        ///
        /// </summary>
        public bool LoadOnStart
        {
            get => loadOnStart;
            set => loadOnStart = value;
        }
        private bool loadOnStart;

        /// <summary>
        ///
        /// </summary>
        public bool CheckOnStart
        {
            get => checkOnStart;
            set
            {
                checkOnStart = value;
                BeginUpdateCheckCommand.Execute(value);
            }
        }
        private bool checkOnStart;

        #region IUserDialogViewModel Implementation

        [XmlIgnore]
        public Action<bool> OnBeginUpdateCheck { get; set; }

        [XmlIgnore]
        public Action<bool> OnUpdateReaderStatus { get; set; }

        [XmlIgnore]
        public Action<SetupViewModel> OnOk { get; set; }

        [XmlIgnore]
        public Action<SetupViewModel> OnCancel { get; set; }

        [XmlIgnore]
        public Action<SetupViewModel> OnConnect { get; set; }

        [XmlIgnore]
        public Action<SetupViewModel> OnCloseRequest { get; set; }

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

        /// <summary>
        /// Act as a proxy between RessourceLoader and View directly.
        /// </summary>
        public string LocalizationResourceSet { get; set; }

        private string _Caption;

        public string Caption
        {
            get => _Caption;
            set
            {
                _Caption = value;
                OnPropertyChanged(nameof(Caption));
            }
        }

#endregion Localization
    }
}