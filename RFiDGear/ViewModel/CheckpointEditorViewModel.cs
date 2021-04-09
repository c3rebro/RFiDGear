/*
 * Created by SharpDevelop.
 * Date: 21.02.2018
 * Time: 09:00
 * 
 */
//using ByteArrayHelper.Extensions;

using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.Xml.Serialization;

using RFiDGear.DataAccessLayer;
using RFiDGear.View;
using RFiDGear.Model;

using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

using MvvmDialogs.ViewModels;

namespace RFiDGear.ViewModel
{
	/// <summary>
	/// Description of CheckpointEditorViewModel.
	/// </summary>
	public class CheckpointEditorViewModel : ViewModelBase, IUserDialogViewModel
	{
		
		private protected Checkpoint checkpoint;
		private protected readonly ObservableCollection<object> _availableTasks;

		public CheckpointEditorViewModel()
		{
			checkpoint = new Checkpoint();
            if (Checkpoints == null)
            {
                Checkpoints = new ObservableCollection<Checkpoint>();
            }
        }

		public CheckpointEditorViewModel(ObservableCollection<object> _tasks, Checkpoint cp = null)
		{
			_availableTasks = _tasks;

			if(cp != null)
				checkpoint = cp;
			else
				checkpoint = new Checkpoint();

            if (Checkpoints == null)
            {
                Checkpoints = new ObservableCollection<Checkpoint>();
            }
        }
		
		#region Commands
		
		public ICommand AddCheckpointCommand { get { return new RelayCommand(OnNewAddCheckpointCommand); } }
		private void OnNewAddCheckpointCommand()
		{
			try
			{

				checkpoint.ErrorLevel = ERROR.Empty;

				#region checkpoint

				checkpoint.TaskIndex = SelectedTaskIndex;
				checkpoint.ErrorLevel = SelectedErrorLevel;
				checkpoint.TemplateField = SelectedTemplateField;
				checkpoint.Content = Content;
				//Array.Clear(checkpoint.checkpointAsBytes, 0, 4);
				
				Checkpoints.Add(checkpoint);
				RaisePropertyChanged("Checkpoints");

				//SelectedCheckpoint = checkpoint;
				
				#endregion
			}
			
			catch(Exception e)
			{
				LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
			}

		}

		#endregion

		#region DependencyProps


		/// <summary>
		///
		/// </summary>
		[XmlIgnore]
		public ObservableCollection<string> AvailableTaskIndices
		{
			get
			{
				return
					new ObservableCollection<string>(_availableTasks.Where(x => x is MifareDesfireSetupViewModel).Select(x => (x as MifareDesfireSetupViewModel).SelectedTaskIndex));
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public ObservableCollection<string> AvailableTemplateFields
		{
			get { return availableTemplateFields; }
			set
            {
				availableTemplateFields = value;
				RaisePropertyChanged("AvailableTemplateFields");
            }
		}
		private ObservableCollection<string> availableTemplateFields;

		/// <summary>
		/// 
		/// </summary>
		public ERROR SelectedErrorLevel
        {
			get { return selectedErrorLevel;  }
			set
            {
				selectedErrorLevel = value;
				RaisePropertyChanged("SelectedErrorLevel");
            }
        }
		private ERROR selectedErrorLevel;

		/// <summary>
		/// 
		/// </summary>
		public int SelectedTaskIndex {
			get {
				return selectedTaskIndex;
			}
			
			set {
				selectedTaskIndex = value;
				RaisePropertyChanged("SelectedTaskIndex");
			}
		}
		private int selectedTaskIndex;

		/// <summary>
		/// 
		/// </summary>
		public string SelectedTemplateField
		{
			get
			{
				return selectedTemplateField;
			}

			set
			{
				selectedTemplateField = value;
				RaisePropertyChanged("SelectedTemplateField");
			}
		}
		private string selectedTemplateField;

		/// <summary>
		/// 
		/// </summary>
		public string Content {
			get {
				return content;
			}
			
			set {
				content = value;
				RaisePropertyChanged("Content");
			}
		}
		private string content;
		
		/// <summary>
		/// 
		/// </summary>
		public ObservableCollection<Checkpoint> Checkpoints {
			get {
				return checkpoints;
			}
			set {
				checkpoints = value;
				RaisePropertyChanged("Checkpoints");
			}
		}
		private ObservableCollection<Checkpoint> checkpoints;
		
		/// <summary>
		/// 
		/// </summary>
		public Checkpoint SelectedCheckpoint {
			get {
				return selectedCheckpoint;
			}
			set {
				selectedCheckpoint = value;
				checkpoint.Content = selectedCheckpoint.Content;
				checkpoint.ErrorLevel = selectedCheckpoint.ErrorLevel;
				checkpoint.TaskIndex = selectedCheckpoint.TaskIndex;
				checkpoint.TemplateField = selectedCheckpoint.TemplateField;

				RaisePropertyChanged("SelectedCheckpoint");
			}
		}
		private Checkpoint selectedCheckpoint;
		
		#endregion
		
		#region IUserDialogViewModel Implementation

		[XmlIgnore]
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

		public Action<CheckpointEditorViewModel> OnOk { get; set; }

		public Action<CheckpointEditorViewModel> OnCancel { get; set; }

		public Action<CheckpointEditorViewModel> OnAuth { get; set; }

		public Action<CheckpointEditorViewModel> OnCloseRequest { get; set; }

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
		/// localization strings
		/// </summary>
		
		public string Ccption
		{
			get { return _Ccption; }
			set
			{
				_Ccption = value;
				RaisePropertyChanged("Ccption");
			}
		} private string _Ccption;

		#endregion Localization
	}
}
