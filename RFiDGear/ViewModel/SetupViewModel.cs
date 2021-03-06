﻿/*
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
        private RFiDDevice device;

        public SetupViewModel()
        {
        }

        public SetupViewModel(RFiDDevice _device)
        {
            using (SettingsReaderWriter settings = new SettingsReaderWriter())
            {
                device = _device;

                selectedReader = settings.DefaultSpecification.DefaultReaderProvider;
                comPort = "10";
                ConnectToReaderCommand.Execute(null);
            }
        }

        #region Commands

        public ICommand ReaderSeletedCommand { get { return new RelayCommand(ReaderSelected); } }
        private void ReaderSelected()
        {
        }

        public ICommand ConnectToReaderCommand { get { return new RelayCommand(ConnectToReader); } }
        private void ConnectToReader()
        {
            if (this.OnConnect != null)
            {
                this.OnConnect(this);
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

        public ICommand ApplyAndExitCommand { get { return new RelayCommand(Ok); } }
        private void Ok()
        {
            if (this.OnOk != null)
                this.OnOk(this);
            else
                Close();
        }

        public ICommand CancelCommand { get { return new RelayCommand(Cancel); } }
        private void Cancel()
        {
            if (this.OnCancel != null)
                this.OnCancel(this);
            else
                Close();
        }

        #endregion Commands

        public ReaderTypes SelectedReader
        {
            get { return selectedReader; }
            set { selectedReader = value; }
        }
        private ReaderTypes selectedReader;

        /// <summary>
        ///
        /// </summary>
        public string SelectedBaudRate
        {
            get
            {
                return selectedBaudRate;
            }
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
        public int SelectedBaudRateAsInt
        { get { return selectedBaudRateAsInt; } }
        private int selectedBaudRateAsInt;

        /// <summary>
        /// ComPort for Readers that use VCP or Serial
        /// </summary>
        public string ComPort
        {
            get { return comPort; }
            set { comPort = value; }
        }
        private string comPort;

        /// <summary>
        /// BaudRate for Readers that use VCP or Serial
        /// </summary>
        public string[] BaudRates
        {
            get { return new string[] {"1200", "2400", "4800", "9600", "115000"}; }
        }

        public string ReaderStatus
        {
            get
            {
                return readerStatus;
            }
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

        #region IUserDialogViewModel Implementation

        public Action<SetupViewModel> OnOk { get; set; }
        public Action<SetupViewModel> OnCancel { get; set; }
        public Action<SetupViewModel> OnConnect { get; set; }
        public Action<SetupViewModel> OnCloseRequest { get; set; }

        public bool IsModal { get; private set; }

        public virtual void RequestClose()
        {
            if (this.OnCloseRequest != null)
                this.OnCloseRequest(this);
            else
                Close();
        }

        public event EventHandler DialogClosing;

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

        /// <summary>
        /// Act as a proxy between RessourceLoader and View directly.
        /// </summary>
        public string LocalizationResourceSet { get; set; }

        private string _Caption;

        public string Caption
        {
            get { return _Caption; }
            set
            {
                _Caption = value;
                RaisePropertyChanged(() => this.Caption);
            }
        }

        #endregion Localization
    }
}