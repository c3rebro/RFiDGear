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
    /// Coordinates validity period entry and validation for the expiry configuration dialog.
    /// </summary>
    public class ExpiryConfigurationDialogViewModel : ObservableObject, IUserDialogViewModel
    {
        private Period currentPeriod;
        private readonly WeekSchedule schedule;
        private static readonly string[] ExpiryDateTimeFormats = new[]
        {
            "dd.MM.yyyy HH':'mm':'ss",
            "dd.MM.yyyy h':'mm':'ss"
        };

        /// <summary>
        /// Initializes a new expiry dialog with the current culture and default values.
        /// </summary>
        public ExpiryConfigurationDialogViewModel()
        {
            accessProfile = null;
            culture = CultureInfo.CurrentCulture;

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
        /// Initializes a new expiry dialog for the specified access profile and culture.
        /// </summary>
        /// <param name="_accessProfile">The access profile to update with selected periods.</param>
        /// <param name="_culture">The culture used to parse and format time strings.</param>
        public ExpiryConfigurationDialogViewModel(AccessProfile _accessProfile, CultureInfo _culture)
        {
            accessProfile = _accessProfile;
            culture = _culture ?? CultureInfo.CurrentCulture;

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

        private readonly ObservableCollection<IDialogViewModel> dialogs = new ObservableCollection<IDialogViewModel>();
        /// <summary>
        /// Gets the active dialog view models owned by this view model.
        /// </summary>
        public ObservableCollection<IDialogViewModel> Dialogs { get { return dialogs; } }

        #endregion

        #region single items

        private readonly CultureInfo culture;
        private readonly AccessProfile accessProfile;

        /// <summary>
        /// Gets or sets whether the start time input failed validation.
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
        /// Gets or sets whether the end time input failed validation.
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
        /// Gets or sets the selected start date.
        /// </summary>
        public DateTime BeginDate
        {
            get => beginDate;
            set
            {
                beginDate = value;

                OnPropertyChanged(nameof(BeginDate));
            }
        }
        private DateTime beginDate;

        /// <summary>
        /// Gets or sets the selected end date.
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
        /// Gets or sets the start time text in <c>HH:mm:ss</c> or <c>HH:mm</c> format, with optional
        /// <c>:MouseWheel</c> suffix indicating 15-minute increments.
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

                        BeginDate = new DateTime(TimeParsing.ParseDateTimeFromTimeText(BeginDate, value, culture, ExpiryDateTimeFormats).Ticks);

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
        /// Gets or sets the end time text in <c>HH:mm:ss</c> or <c>HH:mm</c> format, with optional
        /// <c>:MouseWheel</c> suffix indicating 15-minute increments.
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

                        EndDate = new DateTime(TimeParsing.ParseDateTimeFromTimeText(EndDate, value, culture, ExpiryDateTimeFormats).Ticks);

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
        /// Gets or sets the collection of configured periods.
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

        /// <summary>
        /// Gets the backing schedule used to store period values.
        /// </summary>
        public WeekSchedule Schedule
        {
            get { return schedule; }
        }

        #endregion

        #region commands

        /// <summary>
        /// Gets a command that adds the selected period to the collection.
        /// </summary>
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
        /// Gets or sets the currently selected period.
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
        /// Acts as a proxy between <see cref="ResourceLoader"/> and the view for localization.
        /// </summary>
        public string LocalizationResourceSet { get; set; }

        /// <summary>
        /// Gets the localization caption key for the dialog.
        /// </summary>
        public string Caption
        {
            get { return "expiry"; }
        }

        #endregion

        #region IUserDialogViewModel Implementation

        /// <summary>
        /// Gets whether this dialog should behave as a modal dialog.
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

        /// <summary>
        /// Gets a command that confirms the dialog selection.
        /// </summary>
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

        /// <summary>
        /// Gets a command that cancels the dialog selection.
        /// </summary>
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

        /// <summary>
        /// Gets or sets the callback invoked when the dialog is confirmed.
        /// </summary>
        public Action<ExpiryConfigurationDialogViewModel> OnOk { get; set; }

        /// <summary>
        /// Gets or sets the callback invoked when the dialog is canceled.
        /// </summary>
        public Action<ExpiryConfigurationDialogViewModel> OnCancel { get; set; }

        /// <summary>
        /// Gets or sets the callback invoked when the dialog requests to close.
        /// </summary>
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
