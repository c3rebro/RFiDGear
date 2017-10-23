/*
 * Created by SharpDevelop.
 * Date: 10/11/2017
 * Time: 22:15
 * 
 */
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

using MvvmDialogs.ViewModels;

using System;
using System.Security;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace RFiDGear.ViewModel
{
	/// <summary>
	/// Description of MifareDesfireSetupViewModel.
	/// </summary>
	public class MifareDesfireSetupViewModel : ViewModelBase, IUserDialogViewModel
	{
		public MifareDesfireSetupViewModel(object treeViewViewModel)
		{
			
			
		}
		
		
		
		#region IUserDialogViewModel Implementation

		public bool IsModal { get; private set; }
		public virtual void RequestClose()
		{
			if (this.OnCloseRequest != null)
				OnCloseRequest(this);
			else
				Close();
		}
		public event EventHandler DialogClosing;

		public ICommand ApplyCommand { get { return new RelayCommand(Ok); } }
		protected virtual void Ok()
		{
			if (this.OnOk != null)
				this.OnOk(this);
			else
				Close();
		}
		
		public ICommand ExitCommand { get { return new RelayCommand(Cancel); } }
		protected virtual void Cancel()
		{
			if (this.OnCancel != null)
				this.OnCancel(this);
			else
				Close();
		}
		
		public ICommand AuthCommand { get { return new RelayCommand(Auth); } }
		protected virtual void Auth()
		{
			if (this.OnAuth != null)
				this.OnAuth(this);
			else
				Close();
		}
		
		public Action<MifareDesfireSetupViewModel> OnOk { get; set; }
		public Action<MifareDesfireSetupViewModel> OnCancel { get; set; }
		public Action<MifareDesfireSetupViewModel> OnAuth { get; set; }
		public Action<MifareDesfireSetupViewModel> OnCloseRequest { get; set; }

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
		
		#region INotifyPropertyChanged Members

		public event PropertyChangedEventHandler KeySettingsPropertyChanged;

		protected virtual void OnPropertyChanged(string propertyName)
		{
			if (this.KeySettingsPropertyChanged != null)
				this.KeySettingsPropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}

		#endregion
		
		#region Localization

		private string _Caption;
		public string Caption {
			get { return _Caption; }
			set {
				_Caption = value;
				RaisePropertyChanged("Caption");
			}
		}
		
		#endregion
	}
}
