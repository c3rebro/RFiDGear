/*
 * Created by SharpDevelop.
 * Date: 10/10/2017
 * Time: 20:31
 *
 */

using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using MvvmDialogs.ViewModels;
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
        private readonly RFiDDevice device;

        public SetupViewModel()
        {
        }

        public SetupViewModel(RFiDDevice _device)
        {
            using (SettingsReaderWriter settings = new SettingsReaderWriter())
            {
                device = _device;

                SelectedReader = settings.DefaultSpecification.DefaultReaderProvider;
                ComPort = settings.DefaultSpecification.LastUsedComPort;
                LoadOnStart = settings.DefaultSpecification.AutoLoadProjectOnStart;
                CheckOnStart = settings.DefaultSpecification.AutoCheckForUpdates;
                SelectedBaudRate = settings.DefaultSpecification.LastUsedBaudRate;

                ConnectToReaderCommand.Execute(null);
            }
        }

        #region Commands

        public ICommand ReaderSeletedCommand => new RelayCommand(ReaderSelected);
        private void ReaderSelected()
        {
        }

        public ICommand ConnectToReaderCommand => new RelayCommand(ConnectToReader);
        private void ConnectToReader()
        {
            if (OnConnect != null)
            {
                OnConnect(this);
            }

            device.ChangeProvider(SelectedReader);

            if (device != null && device.ReadChipPublic() == ERROR.NoError)
            {
                ReaderStatus = string.Format("Connected to Card:"
                                             + '\n'
                                             + "UID: {0} "
                                             + '\n'
                                             + "Type: {1}", device.GenericChip.UID, Enum.GetName(typeof(CARD_TYPE), device.GenericChip.CardType));
            }
            else
                ReaderStatus = "no Reader detected";
        }

        public ICommand ApplyAndExitCommand => new RelayCommand(Ok);
        private void Ok()
        {
            if (OnOk != null)
                OnOk(this);
            else
                Close();
        }

        public ICommand CancelCommand => new RelayCommand(Cancel);
        private void Cancel()
        {
            if (OnCancel != null)
                OnCancel(this);
            else
                Close();
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
                uint.TryParse(comPort, out _);
            }
        }
        private string comPort;
        private readonly uint comPortAsUInt;

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
            get
            {
                using (SettingsReaderWriter settings = new SettingsReaderWriter())
                { return Enum.GetName(typeof(ReaderTypes), settings.DefaultSpecification.DefaultReaderProvider); }
            }
        }

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
                OnCloseRequest(this);
            else
                Close();
        }

        public event EventHandler DialogClosing;

        public void Close()
        {
            if (DialogClosing != null)
                DialogClosing(this, new EventArgs());
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