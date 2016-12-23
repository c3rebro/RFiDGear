/*
 * Created by SharpDevelop.
 * User: C3rebro
 * Date: 12/03/2016
 * Time: 23:36
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using MvvmDialogs.ViewModels;
using System;
using System.Globalization;
using System.Resources;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

using LibLogicalAccess;

namespace RFiDGear.ViewModel
{
	/// <summary>
	/// Description of ReaderSetupDialogViewModel.
	/// </summary>
	public class ReaderSetupDialogViewModel : ViewModelBase, IUserDialogViewModel
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
		
		public ReaderSetupDialogViewModel()  {
			//currentReader = new RFiDReaderSetup(null);
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
				return new RFiDReaderSetup(null).ReaderList;
			}
		}

		public string SelectedReader {
			get { return new RFiDReaderSetup(null).SelectedReader; }
			set { new RFiDReaderSetup(null).SelectedReader = value; }
		}
		
		public string ReaderStatus {
			get { return !String.IsNullOrWhiteSpace(new RFiDReaderSetup(null).GetChipUID)
					? String.Format("Connected to Card:"
					                + '\n'
					                +"UID: {0} "
					                + '\n'
					                +"Type: {1}",new RFiDReaderSetup(null).GetChipUID, new RFiDReaderSetup(null).GetChipType)
					: "not Connected";}
		}
		
		public string DefaultReader {
			get { return new RFiDReaderSetup(null).GetReaderName;}
		}
		
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
		#endregion
		private string _Caption;
		public string Caption {
			get { return _Caption; }
			set {
				_Caption = value;
				RaisePropertyChanged(() => this.Caption);
			}
		}
		
		public Action<ReaderSetupDialogViewModel> OnOk { get; set; }
		public Action<ReaderSetupDialogViewModel> OnCancel { get; set; }
		public Action<ReaderSetupDialogViewModel> OnCloseRequest { get; set; }

		public ReaderSetupDialogViewModel(bool isModal = true)
		{
			this.IsModal = isModal;
		}
		
		public string ConnectButtonText {
			get { return ResourceLoader.getResource("buttonConnectToReaderText"); }
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
