using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

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
