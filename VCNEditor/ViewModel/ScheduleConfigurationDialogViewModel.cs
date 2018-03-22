/*
 * Created by SharpDevelop.
 * User: C3rebro
 * Date: 08/28/2017
 * Time: 23:32
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using VCNEditor.Model;
using VCNEditor.View;

using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;

using MvvmDialogs.ViewModels;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Linq;


namespace VCNEditor.ViewModel
{
	/// <summary>
	/// Description of ScheduleConfigurationDialogViewModel.
	/// </summary>
	public class ScheduleConfigurationDialogViewModel : ViewModelBase, IUserDialogViewModel
	{
		private ScheduleConfigurationDialog scheduleConfigView;
		
		private Period currentPeriod;
		private WeekSchedule schedule;
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="_responseCollection"></param>
		/// <param name="_responseObject"></param>
		public ScheduleConfigurationDialogViewModel()
		{
			
			currentPeriod = new Period(DateTime.Now, DateTime.Now.Add(new TimeSpan(1,0,0)));
			ScheduleCollection = new ObservableCollection<Period>();
			
			StartTime = string.Format("{0:HH\\:mm\\:ss}",currentPeriod.Begin);
			EndTime = string.Format("{0:HH\\:mm\\:ss}",currentPeriod.End);
			
			BeginDate = new DateTime(currentPeriod.Begin.Ticks);
			EndDate = new DateTime(currentPeriod.End.Ticks);
			
			IsModal = true;
			
			schedule = new WeekSchedule();
			
			scheduleConfigView = new ScheduleConfigurationDialog();
			scheduleConfigView.DataContext = this;
			
			scheduleConfigView.Show();
		}
		
		#region Dialogs
		
		private ObservableCollection<IDialogViewModel> dialogs = new ObservableCollection<IDialogViewModel>();
		public ObservableCollection<IDialogViewModel> Dialogs { get { return dialogs; } }
		
		#endregion
		
		#region single items
		
		/// <summary>
		/// 
		/// </summary>
		public bool StartTimeHasWrongFormat
		{
			get { return startTimeHasWrongFormat; }
			set {
				startTimeHasWrongFormat = value;
				RaisePropertyChanged("StartTimeHasWrongFormat");
			}
		} private bool startTimeHasWrongFormat;

		/// <summary>
		/// 
		/// </summary>
		public bool EndTimeHasWrongFormat
		{
			get { return endTimeHasWrongFormat; }
			set {
				endTimeHasWrongFormat = value;
				RaisePropertyChanged("EndTimeHasWrongFormat");
			}
		} private bool endTimeHasWrongFormat;
		
		/// <summary>
		/// 
		/// </summary>
		public DateTime BeginDate
		{
			get { return beginDate; }
			set {
				beginDate = value;
				try{
					beginDate =	beginDate.Date.Add(TimeSpan.Parse(startTime));
				}
				catch(Exception e)
				{
					
				}

				RaisePropertyChanged("BeginDate");
			}
		} private DateTime beginDate;
		
		/// <summary>
		/// 
		/// </summary>
		public DateTime EndDate
		{
			get { return endDate; }
			set {
				endDate = value;
				try{
					endDate = endDate.Date.Add(TimeSpan.Parse(endTime));
				}
				catch(Exception e)
				{
					
				}
				RaisePropertyChanged("EndDate");
			}
		} private DateTime endDate;
		
		/// <summary>
		/// 
		/// </summary>
		public string StartTime
		{
			get { return startTime; } //TODO: Add TryCatch
			set {
				try{
					DateTime tempDateTime;
					
					if(value.Contains(":MouseWheel"))
					{
						tempDateTime = BeginDate.Add(new TimeSpan(0,Convert.ToInt32(value.Replace(":MouseWheel",string.Empty)) > 0 ? 15 : -15 ,0));
						
						BeginDate = new DateTime(tempDateTime.Ticks);
						startTime = string.Format("{0:HH\\:mm\\:ss}",BeginDate);
					}
					
					else if(value.Length == 8 || value.Length == 5)
					{

						var t1 = 	BeginDate.ToShortDateString() + ' ' +

							TimeSpan.ParseExact(value, "c", CultureInfo.InvariantCulture);
						
						BeginDate = DateTime.Parse(t1);
						
						startTime = string.Format("{0:HH\\:mm\\:ss}",value);
					}
					
					else
					{
						StartTimeHasWrongFormat = true;
						startTime = value;
						return;
					}


				}
				
				catch(Exception e)
				{
					if(e.GetType() == typeof(FormatException))
						StartTimeHasWrongFormat = true;
					
					startTime = value;
					RaisePropertyChanged("StartTime");
					return;
				}
				
				
				StartTimeHasWrongFormat = false;
				
				RaisePropertyChanged("StartTime");
			}
		} private string startTime;
		
		/// <summary>
		/// 
		/// </summary>
		public string EndTime
		{
			get { return endTime; } //TODO: Add TryCatch
			set {
				try{
					DateTime tempDateTime;
					
					if(value.Contains(":MouseWheel"))
					{
						tempDateTime = EndDate.Add(new TimeSpan(0,Convert.ToInt32(value.Replace(":MouseWheel",string.Empty)) > 0 ? 15 : -15 ,0));
						
						EndDate = tempDateTime;
						endTime = string.Format("{0:HH\\:mm\\:ss}",EndDate);
					}
					
					else if(value.Length == 8 || value.Length == 5)
					{

						var t1 = 	EndDate.ToShortDateString() + ' ' +

							TimeSpan.ParseExact(value, "c", CultureInfo.InvariantCulture);
						
						EndDate = DateTime.Parse(t1);
						
						endTime = string.Format("{0:HH\\:mm\\:ss}",value);
					}
					
					else
					{
						EndTimeHasWrongFormat = true;
						endTime = value;
						return;
					}
					

				}
				
				catch(Exception e)
				{
					if(e.GetType() == typeof(FormatException))
						EndTimeHasWrongFormat = true;
					
					endTime = value;
					RaisePropertyChanged("EndTime");
					return;
				}
				
				
				EndTimeHasWrongFormat = false;
				
				RaisePropertyChanged("EndTime");
			}
		} private string endTime;
		
		#endregion
		
		#region collections
		
		/// <summary>
		/// 
		/// </summary>
		public ObservableCollection<Period> ScheduleCollection
		{
			get { return scheduleCollection; }
			set {
				scheduleCollection = value;
				RaisePropertyChanged("ScheduleCollection");
			}
		} private ObservableCollection<Period> scheduleCollection;
		
		public WeekSchedule Schedule
		{
			get { return schedule; }
		}
		public string[] DaysOfWeek
		{
			get
			{
				return schedule.WeekDays;
			}
		}
		
		#endregion
		
		#region selected items
		
		/// <summary>
		/// 
		/// </summary>
		public Period SelectedPeriod
		{
			get { return selectedPeriod; }
			set
			{
				selectedPeriod = value;
				RaisePropertyChanged("SelectedPeriod");
			}
		} private Period selectedPeriod;
		
		#endregion

		#region Localization

		/// <summary>
		/// Act as a proxy between RessourceLoader and View directly.
		/// </summary>
		public string LocalizationResourceSet { get; set; }
		
		public string Caption
		{
			get { return "scheduler"; } //using (var resMan = new ResourceLoader()) { return resMan.getResource("windowCaptionScheduleConfigurationDialog"); }
		}
		
		#endregion
		
		#region commands
		
		public ICommand AddDateTimeSpanCommand { get { return new RelayCommand(OnNewAddDateTimeSpanCommand); } }
		private void OnNewAddDateTimeSpanCommand()
		{
			currentPeriod = new Period();
			currentPeriod.Begin = BeginDate.Date.Add(TimeSpan.Parse(startTime)); //TODO: Add TryCatch and Log
			currentPeriod.End = EndDate.Date.Add(TimeSpan.Parse(endTime));
			
			//currentPeriod.RepeatType = SelectedRepeatType;
			
			if(schedule == null)
				schedule = new WeekSchedule();
			
			//schedule = ScheduleCollection;
			
			if(currentPeriod.End > currentPeriod.Begin)
			{
//				if(ScheduleCollection != null && ScheduleCollection.Count > 0)
//				{
//					for(int i = ScheduleCollection.Count; i > 0; i--)
//					{
//						if(
//							scheduler.Exist(currentPeriod)
//						)
//						{
//							if(new MessageBoxViewModel {
//							   	Caption = "Already in list",
//							   	Message = "This period overlaps",
//							   	Buttons = MessageBoxButton.YesNo,
//							   	Image = MessageBoxImage.Information
//							   }
//							   .Show(this.Dialogs) == MessageBoxResult.Yes)
//							{
//								ScheduleCollection.Add(currentPeriod);
//								return;
//							}
//							else
//								return;
//						}
//					}
//					ScheduleCollection.Add(currentPeriod);
//					return;
//				}
//				else if(ScheduleCollection != null)
				ScheduleCollection.Add(currentPeriod);
			}
		}
		
		#endregion
		
		#region IUserDialogViewModel Implementation

		/// <summary>
		/// gets; sets whether this dialog is modal or not
		/// </summary>
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
		
		public Action<ScheduleConfigurationDialogViewModel> OnOk { get; set; }
		public Action<ScheduleConfigurationDialogViewModel> OnCancel { get; set; }
		public Action<ScheduleConfigurationDialogViewModel> OnCloseRequest { get; set; }

		public void Close()
		{
			scheduleConfigView.Close();
//			if (this.DialogClosing != null)
//				this.DialogClosing(this, new EventArgs());
		}

		public void Show(IList<IDialogViewModel> collection)
		{
			collection.Add(this);
		}
		
		#endregion IUserDialogViewModel Implementation
	}
}
