using System;
using System.Collections.Generic;
using System.Windows.Input;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MVVMDialogs.ViewModels.Interfaces;

namespace MVVMDialogs.ViewModels
{
    public class CustomDialogViewModel : ObservableObject, IUserDialogViewModel
    {
        #region IUserDialogViewModel Implementation

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

        #endregion IUserDialogViewModel Implementation

        #region Commands

        public ICommand OkCommand => new RelayCommand(Ok);

        protected virtual void Ok()
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

        protected virtual void Cancel()
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

        private Window _ParentWindow = null;

        public Window ParentWindow
        {
            get => _ParentWindow;
            set => _ParentWindow = value;
        }

        private string _Message;

        public string Message
        {
            get => _Message;
            set { _Message = value; OnPropertyChanged(nameof(Message)); }
        }

        private string _Caption;

        public string Caption
        {
            get => _Caption;
            set { _Caption = value; OnPropertyChanged(nameof(Caption)); }
        }

        public Action<CustomDialogViewModel> OnOk { get; set; }
        public Action<CustomDialogViewModel> OnCancel { get; set; }
        public Action<CustomDialogViewModel> OnCloseRequest { get; set; }

        public CustomDialogViewModel(bool isModal = true)
        {
            IsModal = isModal;
        }

        public void Close()
        {
            DialogClosing?.Invoke(this, new EventArgs());
        }

        public void Show(IList<IDialogViewModel> collection)
        {
            collection.Add(this);
        }
    }
}