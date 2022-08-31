/*
 * Created by SharpDevelop.
 * Date: 10/10/2017
 * Time: 20:31
 *
 */

using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

using MvvmDialogs.ViewModels;

using RFiDGear.DataAccessLayer.Remote.FromIO;
using RFiDGear.DataAccessLayer;

using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace RFiDGear.ViewModel
{
    /// <summary>
    /// Description of SetupDialogBoxViewModel.
    /// </summary>
    public class SetupViewModel : ViewModelBase, IUserDialogViewModel
    {
        private ReaderDevice device;

        public SetupViewModel()
        {
        }

        public SetupViewModel(ReaderDevice _device)
        {
            using (SettingsReaderWriter settings = new SettingsReaderWriter())
            {
                device = _device;

                SelectedReader = settings.DefaultSpecification.DefaultReaderProvider;
                DefaultReader = Enum.GetName(typeof(ReaderTypes), SelectedReader);
                ComPort = settings.DefaultSpecification.LastUsedComPort;
                LoadOnStart = settings.DefaultSpecification.AutoLoadProjectOnStart;
                CheckOnStart = settings.DefaultSpecification.AutoCheckForUpdates;
                SelectedBaudRate = settings.DefaultSpecification.LastUsedBaudRate;

                //ConnectToReaderCommand.Execute(null);
            }
        }

        #region Commands

        public ICommand ReaderSeletedCommand => new RelayCommand(ReaderSelected);
        private void ReaderSelected()
        {
            ConnectToReaderCommand.Execute(null);
        }

        public ICommand ConnectToReaderCommand => new RelayCommand(ConnectToReader);
        private void ConnectToReader()
        {
            //OnConnect?.Invoke(this);

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
                        device.ReadChipPublic();
                    }
                    else
                    {
                        device = new LibLogicalAccessProvider(SelectedReader);
                    }
                    break;

                case ReaderTypes.Elatec:
                    if (device != null)
                    {
                        if (!(device is ElatecNetProvider))
                        {
                            device.Dispose();

                            device = new ElatecNetProvider();
                        }
                        
                        device.ReadChipPublic();
                    }
                    else
                    {
                        device = new ElatecNetProvider();
                    }
                    break;

                case ReaderTypes.None:

                    break;
            }

            if (!string.IsNullOrEmpty(device?.GenericChip?.UID))
            {
                DefaultReader = Enum.GetName(typeof(ReaderTypes), SelectedReader);

                ReaderStatus = string.Format("Connected to Card:"
                                             + '\n'
                                             + "UID: {0} "
                                             + '\n'
                                             + "Type: {1}", device.GenericChip.UID, Enum.GetName(typeof(CARD_TYPE), device.GenericChip.CardType));

            }
            else
            {
                ReaderStatus = "no Reader / Card detected";
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
            set => selectedReader = value;
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
                int.TryParse(comPort, out comPortAsInt);
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
                RaisePropertyChanged("ReaderStatus");
            }
        }
        private string readerStatus;

        public string DefaultReader
        {
            get => defaultReader;
            set
            {
                defaultReader = value;
                RaisePropertyChanged("DefaultReader");
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
            set => checkOnStart = value;
        }
        private bool checkOnStart;

        #region IUserDialogViewModel Implementation

        public Action<SetupViewModel> OnOk { get; set; }
        public Action<SetupViewModel> OnCancel { get; set; }
        public Action<SetupViewModel> OnConnect { get; set; }
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
                RaisePropertyChanged(() => Caption);
            }
        }

        #endregion Localization
    }
}