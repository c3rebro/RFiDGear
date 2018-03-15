/*
 * Created by SharpDevelop.
 * Date: 21.02.2018
 * Time: 09:00
 * 
 */
using System;
using System.Collections.Generic;
using System.Windows.Input;

using VCNEditor.View;

using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

using MvvmDialogs.ViewModels;

namespace VCNEditor.ViewModel
{
	/// <summary>
	/// Description of ProfileEditorViewModel.
	/// </summary>
	public class ProfileEditorViewModel : ViewModelBase, IUserDialogViewModel
	{
		ProfileEditor profileEditorView;
		
		public ProfileEditorViewModel()
		{
			profileEditorView = new ProfileEditor();
			profileEditorView.DataContext = this;
			
			profileEditorView.Show();
		}
		
		public string ProfileText
		{
			get
			{
				return profileText;
			}
			set
			{
				profileText = value;
				RaisePropertyChanged("ProfileText");
			}
		} private string profileText;
			
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

		public ICommand OKCommand { get { return new RelayCommand(Ok); } }

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

		public ICommand AuthCommand { get { return new RelayCommand(Auth); } }

		protected virtual void Auth()
		{
			if (this.OnAuth != null)
				this.OnAuth(this);
			else
				Close();
		}

		public Action<ProfileEditorViewModel> OnOk { get; set; }

		public Action<ProfileEditorViewModel> OnCancel { get; set; }

		public Action<ProfileEditorViewModel> OnAuth { get; set; }

		public Action<ProfileEditorViewModel> OnCloseRequest { get; set; }

		public void Close()
		{
			profileEditorView.Close();
			//if (this.DialogClosing != null)
			//	this.DialogClosing(this, new EventArgs());
		}

		public void Show(IList<IDialogViewModel> collection)
		{
			collection.Add(this);
		}

		#endregion IUserDialogViewModel Implementation

		#region Localization

		/// <summary>
		/// localization strings
		/// </summary>
		
		public string Caption
		{
			get { return _Caption; }
			set
			{
				_Caption = value;
				RaisePropertyChanged("Caption");
			}
		} private string _Caption;

		#endregion Localization
	}
}
