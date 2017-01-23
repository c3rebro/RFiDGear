using GalaSoft.MvvmLight;
using MvvmDialogs.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

using LibLogicalAccess;

namespace RFiDGear.ViewModel
{
	/// <summary>
	/// Description of ReaderSetupDialogViewModel.
	/// </summary>
	public class ReaderSetupDialogViewModel : ViewModelBase, IUserDialogViewModel
	{
		
		public ReaderSetupDialogViewModel(bool isModal = true)
		{
			this.IsModal = isModal;
		}
		
		#region Commands
		
		public ICommand ReaderSeletedCommand { get { return new RelayCommand(ReaderSelected); } }
		protected virtual void ReaderSelected()
		{
			
		}
		
		public ICommand ConnectToReaderCommand { get { return new RelayCommand(ConnectToReader); } }
		protected virtual void ConnectToReader()
		{
			RaisePropertyChanged("DefaultReader");
			RaisePropertyChanged("ReaderStatus");
		}
		
		public ICommand ApplyAndExitCommand { get { return new RelayCommand(Ok); } }
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
		
		public string[] ReaderProviderList {
			get {
				return new ReaderSetupModel(null).ReaderList;
			}
		}

		public string SelectedReader {
			get { return new ReaderSetupModel(null).SelectedReader; }
			set { new ReaderSetupModel(null).SelectedReader = value; }
		}
		
		public string ReaderStatus {
			get { return !String.IsNullOrWhiteSpace(new ReaderSetupModel(null).GetChipUID)
					? String.Format("Connected to Card:"
					                + '\n'
					                +"UID: {0} "
					                + '\n'
					                +"Type: {1}",new ReaderSetupModel(null).GetChipUID, new ReaderSetupModel(null).GetChipType)
					: "not Connected";}
		}
		
		public string DefaultReader {
			get { return new ReaderSetupModel(null).GetReaderName;}
		}
		
		public string ConnectButtonText {
			get { return ResourceLoader.getResource("buttonConnectToReaderText"); }
		}

		#region IUserDialogViewModel Implementation
		
		public Action<ReaderSetupDialogViewModel> OnOk { get; set; }
		public Action<ReaderSetupDialogViewModel> OnCancel { get; set; }
		public Action<ReaderSetupDialogViewModel> OnCloseRequest { get; set; }

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
		
		#region ResourceLoader
		public string ButtonSaveAndExitReaderSetupText {
			
			get { return ResourceLoader.getResource("buttonSaveAndExitReaderSetupText");}
		}
		
		public string ButtonConnectToReaderText {
			get { return ResourceLoader.getResource("buttonConnectToReaderText");}
		}
		
		public string ButtonCancelReaderSetupText {
			get { return ResourceLoader.getResource("buttonCancelReaderSetupText");}
		}
		
		private string _Caption;
		public string Caption {
			get { return _Caption; }
			set {
				_Caption = value;
				RaisePropertyChanged(() => this.Caption);
			}
		}
		
		#endregion
	}
}
