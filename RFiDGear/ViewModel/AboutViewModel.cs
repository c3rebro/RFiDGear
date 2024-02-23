﻿/*
 * Created by SharpDevelop.
 * Date: 10/10/2017
 * Time: 20:31
 *
 */

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MVVMDialogs.ViewModels;
using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace RFiDGear.ViewModel
{
    /// <summary>
    /// Description of SetupDialogBoxViewModel.
    /// </summary>
    public class AboutViewModel : ObservableObject, IUserDialogViewModel
    {
        public AboutViewModel()
        {
        }

        public AboutViewModel(string _text)
        {
            AboutText = _text;
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

        #endregion Commands

        /// <summary>
        /// 
        /// </summary>
        public string AboutText
        {
            get => aboutText;
            set
            {
                aboutText = value;
                OnPropertyChanged(nameof(AboutText));
            }
        }
        private string aboutText;

        #region IUserDialogViewModel Implementation

        public Action<AboutViewModel> OnOk { get; set; }
        public Action<AboutViewModel> OnCancel { get; set; }
        public Action<AboutViewModel> OnConnect { get; set; }
        public Action<AboutViewModel> OnCloseRequest { get; set; }

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