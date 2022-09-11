/*
 * Created by SharpDevelop.
 * Date: 10/10/2017
 * Time: 20:31
 *
 */

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MvvmDialogs.ViewModels;
using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace RFiDGear.ViewModel
{
    /// <summary>
    /// Description of SetupDialogBoxViewModel.
    /// </summary>
    public class UpdateNotifierViewModel : ObservableObject, IUserDialogViewModel
    {
        public UpdateNotifierViewModel()
        {
        }

        public UpdateNotifierViewModel(string _text)
        {
            UpdateHistoryText = _text;
        }

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

        /// <summary>
        /// 
        /// </summary>
        public string UpdateHistoryText
        {
            get => updateHistoryText;
            set
            {
                updateHistoryText = value;
                OnPropertyChanged(nameof(UpdateHistoryText));
            }
        }
        private string updateHistoryText;

        #region IUserDialogViewModel Implementation

        public Action<UpdateNotifierViewModel> OnOk { get; set; }
        public Action<UpdateNotifierViewModel> OnCancel { get; set; }
        public Action<UpdateNotifierViewModel> OnConnect { get; set; }
        public Action<UpdateNotifierViewModel> OnCloseRequest { get; set; }

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