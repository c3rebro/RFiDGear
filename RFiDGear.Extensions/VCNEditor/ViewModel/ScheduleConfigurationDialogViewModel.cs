/*
 * Created by SharpDevelop.
 * User: C3rebro
 * Date: 08/28/2017
 * Time: 23:32
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using RFiDGear.Extensions.VCNEditor.DataAccessLayer;
using RFiDGear.Extensions.VCNEditor.Model;
using RFiDGear.Extensions.VCNEditor.View;

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


namespace RFiDGear.Extensions.VCNEditor.ViewModel
{
    /// <summary>
    /// Coordinates week schedule entry creation and validation for the schedule configuration dialog.
    /// </summary>
    public class ScheduleConfigurationDialogViewModel : ObservableObject, IUserDialogViewModel
    {
        private readonly WeekSchedule schedule;
        private static readonly string[] ScheduleDateTimeFormats = new[]
        {
            "dd.MM.yyyy HH':'mm':'ss",
            "dd.MM.yyyy h':'mm':'ss",
            "dddd HH':'mm':'ss",
            "dddd h':'mm':'ss"
        };

        /// <summary>
        /// Initializes a new schedule dialog with default times and the current culture.
        /// </summary>
        public ScheduleConfigurationDialogViewModel()
        {
            accessProfile = null;
            culture = CultureInfo.CurrentCulture;

            schedule = new WeekSchedule(DateTime.MinValue.AddDays(7), DateTime.MinValue.AddDays(7).Add(new TimeSpan(1, 0, 0)));
            ScheduleCollection = new ObservableCollection<WeekSchedule>();

            StartTime = string.Format("{0:HH\\:mm\\:ss}", schedule.Begin);
            EndTime = string.Format("{0:HH\\:mm\\:ss}", schedule.End);

            IsModal = true;

            SelectedWeekStart = schedule.WeekDays[0];
            SelectedWeekEnd = schedule.WeekDays[1];
        }

        /// <summary>
        /// Initializes a new schedule dialog bound to the specified access profile and culture.
        /// </summary>
        /// <param name="_accessProfile">The access profile to update with newly added schedules.</param>
        /// <param name="_culture">The culture used to parse and format time strings.</param>
        public ScheduleConfigurationDialogViewModel(AccessProfile _accessProfile, CultureInfo _culture)
        {
            accessProfile = _accessProfile;
            culture = _culture ?? CultureInfo.CurrentCulture;

            schedule = new WeekSchedule(DateTime.MinValue.AddDays(7), DateTime.MinValue.AddDays(7).Add(new TimeSpan(1, 0, 0)));
            ScheduleCollection = new ObservableCollection<WeekSchedule>();

            StartTime = string.Format("{0:HH\\:mm\\:ss}", schedule.Begin);
            EndTime = string.Format("{0:HH\\:mm\\:ss}", schedule.End);

            IsModal = true;

            SelectedWeekStart = schedule.WeekDays[0];
            SelectedWeekEnd = schedule.WeekDays[1];
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
                        tempDateTime = schedule.Begin.Add(new TimeSpan(0, Convert.ToInt32(value.Replace(":MouseWheel", string.Empty)) > 0 ? 15 : -15, 0));

                        schedule.Begin = new DateTime(tempDateTime.Ticks);
                        startTime = string.Format("{0:HH\\:mm\\:ss}", schedule.Begin);
                    }

                    else if (value.Length == 8 || value.Length == 5)
                    {

                        schedule.Begin = new DateTime(TimeParsing.ParseDateTimeFromTimeText(schedule.Begin, value, culture, ScheduleDateTimeFormats).Ticks);

                        startTime = string.Format("{0:HH\\:mm\\:ss}", value);
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
                        tempDateTime = schedule.End.Add(new TimeSpan(0, Convert.ToInt32(value.Replace(":MouseWheel", string.Empty)) > 0 ? 15 : -15, 0));

                        schedule.End = tempDateTime;
                        endTime = string.Format("{0:HH\\:mm\\:ss}", schedule.End);
                    }

                    else if (value.Length == 8 || value.Length == 5)
                    {

                        schedule.End = new DateTime(TimeParsing.ParseDateTimeFromTimeText(schedule.End, value, culture, ScheduleDateTimeFormats).Ticks);

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
        /// Gets or sets the collection of configured week schedules.
        /// </summary>
        public ObservableCollection<WeekSchedule> ScheduleCollection
        {
            get => scheduleCollection;
            set
            {
                scheduleCollection = value;
                OnPropertyChanged(nameof(ScheduleCollection));
            }
        }
        private ObservableCollection<WeekSchedule> scheduleCollection;

        /// <summary>
        /// Gets the active week schedule being edited.
        /// </summary>
        public WeekSchedule Schedule
        {
            get { return schedule; }
        }

        /// <summary>
        /// Gets the localized day-of-week names used to populate pickers.
        /// </summary>
        public string[] DaysOfWeek
        {
            get
            {
                return schedule.WeekDays;
            }
        }

        /// <summary>
        /// Gets or sets the selected start day label for the schedule span.
        /// </summary>
        public string SelectedWeekStart
        {
            get => selectedWeekStart;
            set
            {
                if (Enum.TryParse(value, out selectedWeekStartAsEnum))
                {
                    selectedWeekStart = value;
                }

                OnPropertyChanged(nameof(SelectedWeekStart));
            }
        }
        private string selectedWeekStart;
        private DayOfWeek selectedWeekStartAsEnum;


        /// <summary>
        /// Gets or sets the selected end day label for the schedule span.
        /// </summary>
        public string SelectedWeekEnd
        {
            get => selectedWeekEnd;
            set
            {
                if (Enum.TryParse(value, out selectedWeekEndAsEnum))
                {
                    selectedWeekEnd = value;
                }

                OnPropertyChanged(nameof(SelectedWeekEnd));
            }
        }
        private string selectedWeekEnd;
        private DayOfWeek selectedWeekEndAsEnum;
        #endregion

        #region selected items

        /// <summary>
        /// Gets or sets the currently selected schedule period.
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

        #endregion

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
            get { return "scheduler"; }
        }

        #endregion

        #region commands

        /// <summary>
        /// Gets a command that adds the selected week span to the schedule collection.
        /// </summary>
        public ICommand AddDateTimeSpanCommand { get { return new RelayCommand(OnNewAddDateTimeSpanCommand); } }
        private void OnNewAddDateTimeSpanCommand()
        {
            try
            {
                var newSchedule = new WeekSchedule();

                int days = (Array.FindIndex<string>(newSchedule.WeekDays, (s) =>
                {

                    if (s == SelectedWeekEnd)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                })
                            -

                            Array.FindIndex<string>(newSchedule.WeekDays, (s) =>
                            {

                                if (s == SelectedWeekStart)
                                {
                                    return true;
                                }
                                else
                                {
                                    return false;
                                }
                            })
                           );

                newSchedule.Begin = newSchedule.Begin.AddDays(7).Date.Add(TimeSpan.Parse(startTime)); //TODO: Add TryCatch and Log
                newSchedule.End = newSchedule.End.AddDays(7).Date.Add(TimeSpan.Parse(endTime));

                newSchedule.Begin = newSchedule.Begin.AddDays((double)selectedWeekStartAsEnum - 1);
                newSchedule.End = newSchedule.End.AddDays((double)selectedWeekEndAsEnum - 1);

                newSchedule.StartMinute = (int)(newSchedule.Begin.TimeOfDay.TotalMinutes);
                newSchedule.Duration = (int)(newSchedule.End - newSchedule.Begin).TotalMinutes;

                if (newSchedule.End > newSchedule.Begin && newSchedule.Duration <= 1439)
                {
                    newSchedule.WeekScheduleAsBytes = new byte[4];
                    newSchedule.WeekScheduleAsBytes[0] = (newSchedule.Begin.DayOfWeek == DayOfWeek.Monday) ? (byte)(newSchedule.WeekScheduleAsBytes[0] | 0x01) : (byte)(newSchedule.WeekScheduleAsBytes[0] & 0xFE);
                    newSchedule.WeekScheduleAsBytes[0] = (newSchedule.Begin.DayOfWeek == DayOfWeek.Tuesday) ? (byte)(newSchedule.WeekScheduleAsBytes[0] | 0x02) : (byte)(newSchedule.WeekScheduleAsBytes[0] & 0xFD);
                    newSchedule.WeekScheduleAsBytes[0] = (newSchedule.Begin.DayOfWeek == DayOfWeek.Wednesday) ? (byte)(newSchedule.WeekScheduleAsBytes[0] | 0x04) : (byte)(newSchedule.WeekScheduleAsBytes[0] & 0xFB);
                    newSchedule.WeekScheduleAsBytes[0] = (newSchedule.Begin.DayOfWeek == DayOfWeek.Thursday) ? (byte)(newSchedule.WeekScheduleAsBytes[0] | 0x08) : (byte)(newSchedule.WeekScheduleAsBytes[0] & 0xF7);
                    newSchedule.WeekScheduleAsBytes[0] = (newSchedule.Begin.DayOfWeek == DayOfWeek.Friday) ? (byte)(newSchedule.WeekScheduleAsBytes[0] | 0x10) : (byte)(newSchedule.WeekScheduleAsBytes[0] & 0xEF);
                    newSchedule.WeekScheduleAsBytes[0] = (newSchedule.Begin.DayOfWeek == DayOfWeek.Saturday) ? (byte)(newSchedule.WeekScheduleAsBytes[0] | 0x20) : (byte)(newSchedule.WeekScheduleAsBytes[0] & 0xDF);
                    newSchedule.WeekScheduleAsBytes[0] = (newSchedule.Begin.DayOfWeek == DayOfWeek.Sunday) ? (byte)(newSchedule.WeekScheduleAsBytes[0] | 0x40) : (byte)(newSchedule.WeekScheduleAsBytes[0] & 0xBF);


                    newSchedule.WeekScheduleAsBytes[1] |= (byte)((newSchedule.StartMinute & 0x3F) << 2);
                    newSchedule.WeekScheduleAsBytes[2] |= (byte)((newSchedule.StartMinute & 0x7C0) >> 6);

                    newSchedule.WeekScheduleAsBytes[2] = (byte)(newSchedule.WeekScheduleAsBytes[2] | (byte)((newSchedule.Duration & 0x07) << 5));
                    newSchedule.WeekScheduleAsBytes[3] |= (byte)(newSchedule.Duration >> 3);

                    ScheduleCollection.Add(newSchedule);

                }

            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
            }

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
        public Action<ScheduleConfigurationDialogViewModel> OnOk { get; set; }

        /// <summary>
        /// Gets or sets the callback invoked when the dialog is canceled.
        /// </summary>
        public Action<ScheduleConfigurationDialogViewModel> OnCancel { get; set; }

        /// <summary>
        /// Gets or sets the callback invoked when the dialog requests to close.
        /// </summary>
        public Action<ScheduleConfigurationDialogViewModel> OnCloseRequest { get; set; }

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
