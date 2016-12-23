using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using MvvmDialogs.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace RFiDGear.ViewModel
{
	public class CustomDialogViewModel : ViewModelBase, IUserDialogViewModel
	{
		#region IUserDialogViewModel Implementation

		public bool IsModal { get; private set; }		
		public virtual void RequestClose()
		{
			if (this.OnCloseRequest != null)
				this.OnCloseRequest(this);
			else
				Close();
		}
		public event EventHandler DialogClosing;
		

		#endregion IUserDialogViewModel Implementation

		#region Commands

		public ICommand OkCommand { get { return new RelayCommand(Ok); } }
		protected virtual void Ok()
		{
			if (this.OnOk != null)
				this.OnOk(this);
			else
				Close();
		}

		public ICommand CancelCommand { get { return new RelayCommand(Cancel); } }
		protected virtual void Cancel()
		{
			if (this.OnCancel != null)
				this.OnCancel(this);
			else
				Close();
		}

		#endregion Commands


		private string _Message;
		public string Message
		{
			get { return _Message; }
			set { _Message = value; RaisePropertyChanged(() => this.Message); }
		}

		private string _Caption;
		public string Caption
		{
			get { return _Caption; }
			set { _Caption = value; RaisePropertyChanged(() => this.Caption); }
		}

		public Action<CustomDialogViewModel> OnOk { get; set; }
		public Action<CustomDialogViewModel> OnCancel { get; set; }
		public Action<CustomDialogViewModel> OnCloseRequest { get; set; }		

		public CustomDialogViewModel(bool isModal = true)
		{
			this.IsModal = isModal;
		}

		public void Close()
		{	
			if (this.DialogClosing != null)
				this.DialogClosing(this, new EventArgs());
		}

		public void Show(IList<IDialogViewModel> collection)
		{
			collection.Add(this);
		}

	}
}
