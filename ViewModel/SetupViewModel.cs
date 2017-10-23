/*
 * Created by SharpDevelop.
 * Date: 10/10/2017
 * Time: 20:31
 * 
 */
using RFiDGear;
using RFiDGear.DataAccessLayer;
using RFiDGear.Model;

using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

using MvvmDialogs.ViewModels;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace RFiDGear.ViewModel
{
	/// <summary>
	/// Description of SetupDialogBoxViewModel.
	/// </summary>
	public class SetupViewModel : ViewModelBase, IUserDialogViewModel
	{
		private RFiDDevice device;
		private SettingsReaderWriter settings;
		
		public SetupViewModel()
		{
			
		}
		
		public SetupViewModel(SettingsReaderWriter _settings, RFiDDevice _device)
		{
			device = _device;
			settings = _settings;
			
			if(device != null)
				device.ReadChipPublic();
			
			selectedReader = settings.DefaultSpecification.DefaultReaderProvider;
		}
		
		#region Commands
		
		public ICommand ReaderSeletedCommand { get { return new RelayCommand(ReaderSelected); } }
		protected virtual void ReaderSelected()
		{
			
		}
		
		public ICommand ConnectToReaderCommand { get { return new RelayCommand(ConnectToReader); } }
		protected virtual void ConnectToReader()
		{
			device.ReaderProvider = SelectedReader;
			device.ReadChipPublic();
			
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
		
		public ReaderTypes SelectedReader {
			get { return selectedReader; }
			set { selectedReader = value; }
		} ReaderTypes selectedReader;
		
		public string ReaderStatus {
			get {
				if(device != null)
					device.ReadChipPublic();
				
				return device != null ? (!String.IsNullOrWhiteSpace(device.CardInfo.uid)
				                         ? String.Format("Connected to Card:"
				                                         + '\n'
				                                         +"UID: {0} "
				                                         + '\n'
				                                         +"Type: {1}",device.CardInfo.uid, Enum.GetName(typeof(CARD_TYPE), device.CardInfo.cardType))
				                         : "not Connected") : "no Reader detected" ;}
		}
		
		public string DefaultReader {
			get { return Enum.GetName(typeof(ReaderTypes), settings.DefaultSpecification.DefaultReaderProvider);}
		}
		
		public string ConnectButtonText {
			get { return "buttonConnectToReaderText"; }
		}
		
		#region IUserDialogViewModel Implementation
		
		public Action<SetupViewModel> OnOk { get; set; }
		public Action<SetupViewModel> OnCancel { get; set; }
		public Action<SetupViewModel> OnCloseRequest { get; set; }

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
		
		#region resLoader
		public string ButtonSaveAndExitReaderSetupText {
			
			get { return "buttonSaveAndExitReaderSetupText";}
		}
		
		public string ButtonConnectToReaderText {
			get { return "buttonConnectToReaderText";}
		}
		
		public string ButtonCancelReaderSetupText {
			get { return "buttonCancelReaderSetupText";}
		}
		

		
		#endregion
		
		#region Localization

		/// <summary>
		/// Act as a proxy between RessourceLoader and View directly.
		/// </summary>
		public string LocalizationResourceSet { get; set; }
		
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
