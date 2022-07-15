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
    public class SplashScreenViewModel : ViewModelBase, IUserDialogViewModel
    {
        public SplashScreenViewModel()
        {
        }

        #region IUserDialogViewModel Implementation

        public Action<SplashScreenViewModel> OnOk { get; set; }
        public Action<SplashScreenViewModel> OnCancel { get; set; }
        public Action<SplashScreenViewModel> OnConnect { get; set; }
        public Action<SplashScreenViewModel> OnCloseRequest { get; set; }

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