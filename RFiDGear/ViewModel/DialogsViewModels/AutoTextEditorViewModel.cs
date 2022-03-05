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
    public class AutoTextEditorViewModel : ViewModelBase, IUserDialogViewModel
    {
        public AutoTextEditorViewModel()
        {
        }

        public AutoTextEditorViewModel(RFiDDevice _device)
        {
            using (SettingsReaderWriter settings = new SettingsReaderWriter())
            {
                
            }
        }

        #region Commands

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



        #region IUserDialogViewModel Implementation

        public Action<AutoTextEditorViewModel> OnOk { get; set; }
        public Action<AutoTextEditorViewModel> OnCancel { get; set; }
        public Action<AutoTextEditorViewModel> OnConnect { get; set; }
        public Action<AutoTextEditorViewModel> OnCloseRequest { get; set; }

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