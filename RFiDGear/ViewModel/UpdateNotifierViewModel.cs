/*
 * Created by SharpDevelop.
 * Date: 10/10/2017
 * Time: 20:31
 *
 */

using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using MvvmDialogs.ViewModels;
using System;
using System.Collections.Generic;

namespace RFiDGear.ViewModel
{
    /// <summary>
    /// Description of SetupDialogBoxViewModel.
    /// </summary>
    public class UpdateNotifierViewModel : ViewModelBase, IUserDialogViewModel
    {
        public UpdateNotifierViewModel()
        {
        }

        public UpdateNotifierViewModel(string _text)
        {
            UpdateHistoryText = _text;
        }

        /// <summary>
        /// 
        /// </summary>
        public string UpdateHistoryText
        {
            get
            {
                return updateHistoryText;
            }
            set
            {
                updateHistoryText = value;
                RaisePropertyChanged("UpdateHistoryText");
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