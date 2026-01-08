/*
 * Created by SharpDevelop.
 * User: C3rebro
 * Date: 04/18/2018
 * Time: 22:06
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using VCNEditor.Model;
using VCNEditor.View;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using RFiDGear.UI.MVVMDialogs.ViewModels;
using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Linq;
using System.Xml.Serialization;

namespace VCNEditor.ViewModel
{
    /// <summary>
    /// Description of ExpiryConfigurationDialogViewModel.
    /// </summary>
    public class ExpiryConfigurationDialogViewModel : ObservableObject, IUserDialogViewModel
    {
        private Period currentPeriod;
        private WeekSchedule schedule;

        /// <summary>
        /// 
        /// </summary>
        public ExpiryConfigurationDialogViewModel()
        {

            currentPeriod = new Period(DateTime.Now, DateTime.Now.Add(new TimeSpan(1, 0, 0)));
            ScheduleCollection = new ObservableCollection<Period>();

            StartTime = string.Format("{0:HH\\:mm\\:ss}", currentPeriod.Begin);
            EndTime = string.Format("{0:HH\\:mm\\:ss}", currentPeriod.End);

            BeginDate = new DateTime(currentPeriod.Begin.Ticks);
            EndDate = new DateTime(currentPeriod.End.Ticks);

            IsModal = true;

            schedule = new WeekSchedule();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="_accessProfile"></param>
        public ExpiryConfigurationDialogViewModel(AccessProfile _accessProfile, CultureInfo _culture)
        {
            accessProfile = _accessProfile;
            culture = _culture;

            currentPeriod = new Period(DateTime.Now, DateTime.Now.Add(new TimeSpan(1, 0, 0)));
            ScheduleCollection = new ObservableCollection<Period>();

            StartTime = string.Format("{0:HH\\:mm\\:ss}", currentPeriod.Begin);
            EndTime = string.Format("{0:HH\\:mm\\:ss}", currentPeriod.End);

            BeginDate = new DateTime(currentPeriod.Begin.Ticks);
            EndDate = new DateTime(currentPeriod.End.Ticks);

            IsModal = true;

            schedule = new WeekSchedule();
        }


        #region Dialogs

        private ObservableCollection<IDialogViewModel> dialogs = new ObservableCollection<IDialogViewModel>();
        public ObservableCollection<IDialogViewModel> Dialogs { get { return dialogs; } }

        #endregion

        #region single items

        private CultureInfo culture;
        private AccessProfile accessProfile;

        /// <summary>
        /// 
        /// </summary>
        public bool StartTimeHasWrongFormat
        {
            get => startTimeHasWrongFormat;
            set
            {
                startTimeHasWrongFormat = value;
                OnPropertyChanged(nameof(StartTimeHasWrongFormat));
            }
        }
        private bool startTimeHasWrongFormat;

        /// <summary>
        /// 
        /// </summary>
        public bool EndTimeHasWrongFormat
        {
            get => endTimeHasWrongFormat;
            set
            {
                endTimeHasWrongFormat = value;
                OnPropertyChanged(nameof(EndTimeHasWrongFormat));
            }
        }
        private bool endTimeHasWrongFormat;

        /// <summary>
        /// 
        /// </summary>
        public DateTime BeginDate
        {
            get => beginDate;
            set
            {
                beginDate = value;
                //				try{
                //					beginDate =	beginDate.Date.Add(TimeSpan.Parse(startTime));
                //				}
                //				catch(Exception e)
                //				{
                //
                //				}

                OnPropertyChanged(nameof(BeginDate));
            }
        }
        private DateTime beginDate;

        /// <summary>
        /// 
        /// </summary>
        public DateTime EndDate
        {
            get => endDate;
            set
            {
                endDate = value;
                OnPropertyChanged(nameof(EndDate));
            }
        }
        private DateTime endDate;

        /// <summary>
        /// 
        /// </summary>
        public string StartTime
        {
            get => startTime;  //TODO: Add TryCatch
            set
            {
                try
                {
                    DateTime tempDateTime;

                    if (value.Contains(":MouseWheel"))
                    {
                        tempDateTime = BeginDate.Add(new TimeSpan(0, Convert.ToInt32(value.Replace(":MouseWheel", string.Empty)) > 0 ? 15 : -15, 0));

                        BeginDate = new DateTime(tempDateTime.Ticks);
                        startTime = string.Format("{0:HH\\:mm\\:ss}", BeginDate);
                    }

                    else if (value.Length == 8 || value.Length == 5)
                    {

                        var t1 = BeginDate.ToShortDateString() + ' ' +

                            TimeSpan.ParseExact(value, "c", culture);

                        BeginDate = new DateTime(DateTime.ParseExact(t1, new string[] { "dd.MM.yyyy HH':'mm':'ss", "dd.MM.yyyy h':'mm':'ss" }, culture, DateTimeStyles.None).Ticks);

                        startTime = string.Format(culture, "{0:HH\\:mm\\:ss}", value);
                    }

                    else
                    {
                        StartTimeHasWrongFormat = true;
                        startTime = value;
                        return;
                    }


                }

                catch (Exception e)
                {
                    if (e.GetType() == typeof(FormatException))
                    {
                        StartTimeHasWrongFormat = true;
                    }

                    startTime = value;
                    OnPropertyChanged(nameof(StartTime));
                    return;
                }


                StartTimeHasWrongFormat = false;

                OnPropertyChanged(nameof(StartTime));
            }
        }
        private string startTime;

        /// <summary>
        /// 
        /// </summary>
        public string EndTime
        {
            get => endTime;  //TODO: Add TryCatch
            set
            {
                try
                {
                    DateTime tempDateTime;

                    if (value.Contains(":MouseWheel"))
                    {
                        tempDateTime = EndDate.Add(new TimeSpan(0, Convert.ToInt32(value.Replace(":MouseWheel", string.Empty)) > 0 ? 15 : -15, 0));

                        EndDate = tempDateTime;
                        endTime = string.Format("{0:HH\\:mm\\:ss}", EndDate);
                    }

                    else if (value.Length == 8 || value.Length == 5)
                    {

                        var t1 = EndDate.ToShortDateString() + ' ' +

                            TimeSpan.ParseExact(value, "c", culture);

                        EndDate = new DateTime(DateTime.ParseExact(t1, new string[] { "dd.MM.yyyy HH':'mm':'ss", "dd.MM.yyyy h':'mm':'ss" }, culture, DateTimeStyles.None).Ticks);

                        endTime = string.Format(culture, "{0:HH\\:mm\\:ss}", value);
                    }

                    else
                    {
                        EndTimeHasWrongFormat = true;
                        endTime = value;
                        return;
                    }


                }

                catch (Exception e)
                {
                    if (e.GetType() == typeof(FormatException))
                    {
                        EndTimeHasWrongFormat = true;
                    }

                    endTime = value;
                    OnPropertyChanged(nameof(EndTime));
                    return;
                }


                EndTimeHasWrongFormat = false;

                OnPropertyChanged(nameof(EndTime));
            }
        }
        private string endTime;

        #endregion

        #region collections

        /// <summary>
        /// 
        /// </summary>
        public ObservableCollection<Period> ScheduleCollection
        {
            get => scheduleCollection;
            set
            {
                scheduleCollection = value;
                OnPropertyChanged(nameof(ScheduleCollection));
            }
        }
        private ObservableCollection<Period> scheduleCollection;

        public WeekSchedule Schedule
        {
            get { return schedule; }
        }

        #endregion

        #region commands

        public ICommand AddDateTimeSpanCommand { get { return new RelayCommand(OnNewAddDateTimeSpanCommand); } }
        private void OnNewAddDateTimeSpanCommand()
        {

            currentPeriod = new Period();

            currentPeriod.Begin = BeginDate.Date.Add(TimeSpan.Parse(startTime)); //TODO: Add TryCatch and Log
            currentPeriod.End = EndDate.Date.Add(TimeSpan.Parse(endTime));

            if (currentPeriod.End > currentPeriod.Begin)
            {
                ScheduleCollection.Add(currentPeriod);
            }

        }

        #endregion

        /// <summary>
        /// 
        /// </summary>
        public Period SelectedPeriod
        {
            get => selectedPeriod;
            set
            {
                selectedPeriod = value;
                OnPropertyChanged(nameof(SelectedPeriod));
            }
        }
        private Period selectedPeriod;

        #region Localization

        /// <summary>
        /// Act as a proxy between RessourceLoader and View directly.
        /// </summary>
        public string LocalizationResourceSet { get; set; }

        public string Caption
        {
            get { return "expiry"; } //using (var resMan = new ResourceLoader()) { return resMan.getResource("windowCaptionScheduleConfigurationDialog"); }
        }

        #endregion

        #region IUserDialogViewModel Implementation

        /// <summary>
        /// gets; sets whether this dialog is modal or not
        /// </summary>
        [XmlIgnore]
        public bool IsModal { get; private set; }

        public virtual void RequestClose()
        {
            if (OnCloseRequest != null)
            {
                OnCloseRequest(this);
            }
            else
            {
                Close();
            }
        }
        public event EventHandler DialogClosing;

        public ICommand OKCommand { get { return new RelayCommand(Ok); } }
        protected virtual void Ok()
        {
            if (OnOk != null)
            {
                OnOk(this);
            }
            else
            {
                Close();
            }
        }

        public ICommand CancelCommand { get { return new RelayCommand(Cancel); } }
        protected virtual void Cancel()
        {
            if (OnCancel != null)
            {
                OnCancel(this);
            }
            else
            {
                Close();
            }
        }

        public Action<ExpiryConfigurationDialogViewModel> OnOk { get; set; }
        public Action<ExpiryConfigurationDialogViewModel> OnCancel { get; set; }
        public Action<ExpiryConfigurationDialogViewModel> OnCloseRequest { get; set; }

        public void Close()
        {
            DialogClosing?.Invoke(this, new EventArgs());
        }

        public void Show(IList<IDialogViewModel> collection)
        {
            collection.Add(this);
        }

        #endregion IUserDialogViewModel Implementation
    }
}
