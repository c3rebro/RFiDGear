using ByteArrayHelper;
using ByteArrayHelper.Extensions;

using VCNEditor.DataAccessLayer;
using VCNEditor.Model;


using RFiDGear.UI.MVVMDialogs.ViewModels;
using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;

using CRC_IT;
using RFiDGear.Infrastructure.DI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Input;
using System.Xml.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;


namespace VCNEditor.ViewModel
{
    [ExportViewModel("VCNEditor")]
    public class VCNEditorViewModel : ObservableObject, IUserDialogViewModel
    {
        private byte[] accessFileAsByte;

        private byte[] cardIDAsBytes = new byte[10]; // will grow in size after rnd id is generated, so keep it that size
        private byte[] idAsBytes = new byte[10];
        private byte[] sIConfAsBytes = new byte[2];
        private byte[] areaIDAsBytes = new byte[2];
        private byte sICardConfAsByte = 0x00;
        private byte fileFormatRelease = 0x00;

        private readonly byte[] expiryAsBytes = new byte[3];
        private readonly byte[] validFromAsBytes = new byte[3];

        private readonly Random rnd = new Random();

        public VCNEditorViewModel()
        {
            try
            {
                RFiDGear.UI.MVVMDialogs.Behaviors.DialogBehavior.SetResourceDictionary("/VCNEditor;component/ResourceDictionary.xaml"); // set view <-> viewmodel resources (viewmodel first pattern)

                VCNEditor.DataAccessLayer.CultureInfoProxy.Culture = culture; // set selected culture from main app via mef import

                for (int i = 0; i < cardIDAsBytes.Length; i++)
                {
                    cardIDAsBytes[i] = (byte)rnd.Next(0, 255);  // init default rnd id
                }

            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
            }

            cardType = 1;
            forHostUse = "0000000000";

            ReleaseMajor = "1";
            ReleaseMinor = "0";
            AreaID = "1";
            ContentIdentifier = "0";

            MinAccessListLogLevelAsString = "0";
            NoEntryWhenALFull = false;
            SuppressBeeping = false;
            LongCoupling = false;
            SuppressCoupling = false;
            ToggleDoorState = false;

            sIConfAsBytes = new byte[2] { 0, 0 };

            UpStreamFileContentAsString = "0";
            UpStreamFileTypeAsString = "0";
        }

        #region Dialogs

        public string WindowName { get; } = "VCNEditor";

        /// <summary>
        /// 
        /// </summary>
        private readonly ObservableCollection<IDialogViewModel> dialogs = new ObservableCollection<IDialogViewModel>();
        public ObservableCollection<IDialogViewModel> Dialogs { get { return dialogs; } }


        #endregion Dialogs

        #region Localization

        [ImportProperty("Culture")]
        private CultureInfo culture;

        /// <summary>
        /// Expose translated strings from ResourceLoader
        /// </summary>
        public string LocalizationResourceSet { get; set; }

        #endregion Localization

        #region Commands

        public ICommand EditMainListCommand { get { return new RelayCommand(OnNewEditMainListCommand); } }
        private void OnNewEditMainListCommand()
        {
            try
            {

                Dialogs.Add(new ProfileEditorViewModel()
                {

                    AccessProfiles = AccessProfiles,

                    OnOk = (sender) =>
                    {

                        AccessProfiles = sender.AccessProfiles;

                        sender.Close();
                    },

                    OnCancel = (sender) =>
                    {
                        sender.Close();
                    },

                    OnAuth = (sender) =>
                    {
                    },

                    OnCloseRequest = (sender) =>
                    {
                        sender.Close();
                    }
                });

            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
                Dialogs.Clear();
            }
        }

        public ICommand EditWeekSchedulesCommand { get { return new RelayCommand(OnNewEditWeekSchedulesCommand); } }
        private void OnNewEditWeekSchedulesCommand()
        {
            StringBuilder combinedWeekScheduleBuilder = new StringBuilder();

            try
            {
                if (SelectedAccessProfile != null)
                {
                    Dialogs.Add(new ScheduleConfigurationDialogViewModel(SelectedAccessProfile, CultureInfoProxy.Culture)
                    {

                        OnOk = (sender) =>
                        {

                            SelectedAccessProfile.WeekSchedules = new byte[sender.ScheduleCollection.Count() * 4];

                            foreach (WeekSchedule ws in sender.ScheduleCollection ?? new ObservableCollection<WeekSchedule>())
                            {
                                combinedWeekScheduleBuilder.Append(ByteArrayConverter.GetStringFrom(ws.WeekScheduleAsBytes));
                            }

                            SelectedAccessProfile.WeekSchedules = ByteArrayConverter.GetBytesFrom(combinedWeekScheduleBuilder.ToString());

                            SelectedAccessProfile.AccessProfileAsBytes[0] |= (byte)(((sender.ScheduleCollection.Count) << 2) & 0x3C); // set week schedules count / size

                            sender.Close();
                        },

                        OnCancel = (sender) =>
                        {
                            sender.Close();
                        },

                        OnCloseRequest = (sender) =>
                        {
                            sender.Close();
                        }
                    });
                }

                OnPropertyChanged(nameof(AccessProfiles));

            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
                Dialogs.Clear();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public ICommand GenerateVCNDataCommand { get { return new RelayCommand<bool>(OnNewGenerateVCNDataCommand); } }
        private void OnNewGenerateVCNDataCommand(bool withID)
        {
            StringBuilder combinedAccessProfileBuilder = new StringBuilder();

            if (withID)
            {
                cardIDAsBytes = new byte[10];

                for (int i = 0; i < cardIDAsBytes.Length; i++)
                {
                    cardIDAsBytes[i] = (byte)rnd.Next(0, 255);
                }
            }

            CardID = ByteArrayConverter.GetStringFrom(cardIDAsBytes);

            idAsBytes = new byte[
                cardIDAsBytes.Count()
                + ByteArrayConverter.GetByteCount(ForHostUse)
                + 1];
            idAsBytes = ByteArrayConverter.GetBytesFrom(ByteArrayConverter.GetStringFrom(cardType) + ByteArrayConverter.GetStringFrom(cardIDAsBytes) + ForHostUse);

            #region siconf

            Array.Clear(sIConfAsBytes, 0, 2);

            if (minAccessListLogLevelAsInt >= 0 && minAccessListLogLevelAsInt <= 3)
            {
                sIConfAsBytes[1] = (byte)((sIConfAsBytes[1] & 0xFC) | (byte)minAccessListLogLevelAsInt);
            }

            SetFlag(ref sIConfAsBytes[1], 0x40, toggleDoorState);
            SetFlag(ref sIConfAsBytes[1], 0x20, suppressCoupling);
            SetFlag(ref sIConfAsBytes[1], 0x10, longCoupling);
            SetFlag(ref sIConfAsBytes[1], 0x08, suppressBeeping);
            SetFlag(ref sIConfAsBytes[1], 0x04, noEntryWhenALFull);

            sIConfAsBytes = ByteArrayConverter.Reverse(sIConfAsBytes);

            #endregion

            foreach (AccessProfile ap in AccessProfiles ?? new ObservableCollection<AccessProfile>())
            {
                combinedAccessProfileBuilder.Append(ByteArrayConverter.GetStringFrom(ap.AccessProfileAsBytes)
                                                    + ByteArrayConverter.GetStringFrom(ap.MainListWords)
                                                    + (ap.WeekSchedules != null ? ByteArrayConverter.GetStringFrom(ap.WeekSchedules) : string.Empty)); // week schedules
            }

            string combinedAccessProfile = combinedAccessProfileBuilder.ToString();

            accessFileAsByte = new byte[
                ByteArrayConverter.GetByteCount(
                    ByteArrayConverter.GetStringFrom(fileFormatRelease)// CRC32_Begin; fileFormat major plus minor
                    + "00"//content identifier
                    + ByteArrayConverter.GetStringFrom(areaIDAsBytes)
                    + ByteArrayConverter.GetStringFrom(sIConfAsBytes)
                    + ByteArrayConverter.GetStringFrom(validFromAsBytes)// valid from
                    + ByteArrayConverter.GetStringFrom(expiryAsBytes)// expiry
                    + ByteArrayConverter.GetStringFrom(sICardConfAsByte)
                    + "0000"//blacklist addr
                    + "000000000000"// reserved
                    + "00"
                    + combinedAccessProfile
                )];

            accessFileAsByte = ByteArrayConverter.GetBytesFrom(
                ByteArrayConverter.GetStringFrom(fileFormatRelease)// fileFormat major plus minor
                + "02"//content identifier
                + ByteArrayConverter.GetStringFrom(areaIDAsBytes)
                + ByteArrayConverter.GetStringFrom(sIConfAsBytes)
                + ByteArrayConverter.GetStringFrom(validFromAsBytes)// valid from
                + ByteArrayConverter.GetStringFrom(expiryAsBytes)// expiry
                + ByteArrayConverter.GetStringFrom(sICardConfAsByte)
                + "0000"//blacklist addr
                + "000000000000"// reserved
                + ByteArrayConverter.GetStringFrom((byte)(Math.Ceiling((double)(accessFileAsByte.Length / 16) + 4)))// size of accessFile + 4bytes CRC32 which is added afterwards
                + combinedAccessProfile);

            try
            {
                byte[] arrToUse = new byte[28];

                Array.Copy(accessFileAsByte, arrToUse, 28);

                Crc32 crc = new Crc32();
                byte[] crc32AsByte = crc.ComputeHash(arrToUse);

                CRC32 = ByteArrayConverter.GetStringFrom(ByteArrayConverter.Reverse(crc32AsByte));

                accessFileAsByte = ByteArrayConverter.GetBytesFrom(ByteArrayConverter.GetStringFrom(crc32AsByte) + ByteArrayConverter.GetStringFrom(accessFileAsByte));
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
            }

            VCNDataTextBox = string.Format("IDFile: {0}\nAccessFile: {1}",
                                           ByteArrayConverter.GetStringFrom(idAsBytes),
                                           ByteArrayConverter.GetStringFrom(accessFileAsByte));
        }

        public ICommand SetExpiryCommand { get { return new RelayCommand(OnNewSetExpiryCommand); } }
        private void OnNewSetExpiryCommand()
        {
            try
            {
                Dialogs.Add(new ExpiryConfigurationDialogViewModel(SelectedAccessProfile, CultureInfoProxy.Culture)
                {

                    OnOk = (sender) =>
                    {
                        Expires = string.Format("{0} ({1} quarter hours since 01.01.2015)", sender.EndDate.ToString(), (Math.Round((sender.EndDate - new DateTime(2015, 01, 01)).TotalHours * 4)));
                        ValidFrom = string.Format("{0} ({1} quarter hours since 01.01.2015)", sender.BeginDate.ToString(), (Math.Round((sender.BeginDate - new DateTime(2015, 01, 01)).TotalHours * 4)));

                        Array.Copy(BitConverter.GetBytes((Int32)(sender.EndDate - new DateTime(2015, 01, 01)).TotalHours * 4), expiryAsBytes, 3);
                        Array.Copy(BitConverter.GetBytes((Int32)(sender.BeginDate - new DateTime(2015, 01, 01)).TotalHours * 4), validFromAsBytes, 3);

                        sender.Close();
                    },

                    OnCancel = (sender) =>
                    {
                        sender.Close();
                    },

                    OnCloseRequest = (sender) =>
                    {
                        sender.Close();
                    }
                });




            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
                Dialogs.Clear();
            }
        }

        #endregion

        #region IDFile

        /// <summary>
        /// 
        /// </summary>
        public byte CardType
        {
            get => cardType;
            set
            {
                cardType = value;
                OnPropertyChanged(nameof(CardType));
            }
        }
        private byte cardType;

        /// <summary>
        /// 
        /// </summary>
        public string ForHostUse
        {
            get => forHostUse;
            set
            {
                forHostUse = value;
                OnPropertyChanged(nameof(ForHostUse));
            }
        }
        private string forHostUse;

        /// <summary>
        /// 
        /// </summary>
        public string CardID
        {
            get => cardID;
            set
            {
                cardID = value;
                OnPropertyChanged(nameof(CardID));
            }
        }
        private string cardID;

        #endregion

        #region accessFileHeader

        /// <summary>
        /// 
        /// </summary>
        public string ReleaseMajor
        {
            get => releaseMajor;
            set
            {
                releaseMajor = value;
                int.TryParse(value, out int i);

                ReleaseMajorAsInt = i;

                OnPropertyChanged(nameof(ReleaseMajor));
            }
        }
        private string releaseMajor;

        /// <summary>
        /// 
        /// </summary>
        public int ReleaseMajorAsInt
        {
            get => releaseMajorAsInt;
            set
            {
                releaseMajorAsInt = value;

                if (value >= 0 && value <= 7)
                {
                    fileFormatRelease = (byte)((fileFormatRelease & 0xF8) | (byte)releaseMajorAsInt);
                }

                OnPropertyChanged(nameof(ReleaseMajorAsInt));
            }
        }
        private int releaseMajorAsInt;

        /// <summary>
        /// 
        /// </summary>
        public string ReleaseMinor
        {
            get => releaseMinor;
            set
            {
                releaseMinor = value;
                if (int.TryParse(value, out int i))
                {
                    ReleaseMinorAsInt = i;
                }

                OnPropertyChanged(nameof(ReleaseMinor));
            }
        }
        private string releaseMinor;

        /// <summary>
        /// 
        /// </summary>
        public int ReleaseMinorAsInt
        {
            get => releaseMinorAsInt;
            set
            {
                releaseMinorAsInt = value;

                if (value >= 0 && value <= 31)
                {
                    fileFormatRelease = (byte)((fileFormatRelease & 0x03) | (byte)(releaseMinorAsInt << 3));
                }

                OnPropertyChanged(nameof(ReleaseMinorAsInt));
            }
        }
        private int releaseMinorAsInt;

        /// <summary>
        /// 
        /// </summary>
        public string ContentIdentifier
        {
            get => contentIdentifier;
            set
            {
                contentIdentifier = value;
                OnPropertyChanged(nameof(ContentIdentifier));
            }
        }
        private string contentIdentifier;

        #region si conf
        /// <summary>
        /// 
        /// </summary>
        public string MinAccessListLogLevelAsString
        {
            get => minAccessListLogLevelAsString;
            set
            {
                minAccessListLogLevelAsString = value;

                int.TryParse(value, out int i);

                MinAccessListLogLevelAsInt = i;

                OnPropertyChanged(nameof(MinAccessListLogLevelAsString));
            }
        }
        private string minAccessListLogLevelAsString;

        /// <summary>
        /// 
        /// </summary>
        public int MinAccessListLogLevelAsInt
        {
            get => minAccessListLogLevelAsInt;
            set
            {
                minAccessListLogLevelAsInt = value;

                OnPropertyChanged(nameof(MinAccessListLogLevelAsInt));
            }
        }
        private int minAccessListLogLevelAsInt;

        /// <summary>
        /// 
        /// </summary>
        public bool NoEntryWhenALFull
        {
            get => noEntryWhenALFull;
            set
            {
                noEntryWhenALFull = value;

                OnPropertyChanged(nameof(NoEntryWhenALFull));
            }
        }
        private bool noEntryWhenALFull;

        /// <summary>
        /// 
        /// </summary>
        public bool SuppressBeeping
        {
            get => suppressBeeping;
            set
            {
                suppressBeeping = value;

                OnPropertyChanged(nameof(SuppressBeeping));
            }
        }
        private bool suppressBeeping;

        /// <summary>
        /// 
        /// </summary>
        public bool LongCoupling
        {
            get => longCoupling;
            set
            {
                longCoupling = value;

                OnPropertyChanged(nameof(LongCoupling));
            }
        }
        private bool longCoupling;

        /// <summary>
        /// 
        /// </summary>
        public bool SuppressCoupling
        {
            get => suppressCoupling;
            set
            {
                suppressCoupling = value;

                OnPropertyChanged(nameof(SuppressCoupling));
            }
        }
        private bool suppressCoupling;

        /// <summary>
        /// 
        /// </summary>
        public bool ToggleDoorState
        {
            get => toggleDoorState;
            set
            {
                toggleDoorState = value;

                OnPropertyChanged(nameof(ToggleDoorState));
            }
        }
        private bool toggleDoorState;

        #endregion

        #region si card conf

        /// <summary>
        /// 
        /// </summary>
        public string UpStreamFileContentAsString
        {
            get => upStreamFileContentAsString;
            set
            {
                upStreamFileContentAsString = value;

                int.TryParse(value, out int i);

                UpStreamFileContentAsInt = i;

                OnPropertyChanged(nameof(UpStreamFileContentAsString));
            }
        }
        private string upStreamFileContentAsString;

        /// <summary>
        /// 
        /// </summary>
        public int UpStreamFileContentAsInt
        {
            get => upStreamFileContentAsInt;
            set
            {
                upStreamFileContentAsInt = value;

                if (value >= 0 && value <= 3)
                {
                    sICardConfAsByte = (byte)((sICardConfAsByte & 0xFC) | (byte)upStreamFileContentAsInt);
                }

                OnPropertyChanged(nameof(UpStreamFileContentAsInt));
            }
        }
        private int upStreamFileContentAsInt;

        /// <summary>
        /// 
        /// </summary>
        public string UpStreamFileTypeAsString
        {
            get => upStreamFileTypeAsString;
            set
            {
                upStreamFileTypeAsString = value;

                int.TryParse(value, out int i);

                UpStreamFileTypeAsInt = i;

                OnPropertyChanged(nameof(UpStreamFileTypeAsString));
            }
        }
        private string upStreamFileTypeAsString;

        /// <summary>
        /// 
        /// </summary>
        public int UpStreamFileTypeAsInt
        {
            get => upStreamFileTypeAsInt;
            set
            {
                upStreamFileTypeAsInt = value;

                if (value >= 0 && value <= 3)
                {
                    sICardConfAsByte = (byte)((sICardConfAsByte & 0xF3) | (byte)(upStreamFileTypeAsInt << 2));
                }

                OnPropertyChanged(nameof(UpStreamFileTypeAsInt));
            }
        }
        private int upStreamFileTypeAsInt;

        /// <summary>
        /// 
        /// </summary>
        public bool IsAccessListLongDate
        {
            get => isAccessListLongDate;
            set
            {
                isAccessListLongDate = value;

                SetFlag(ref sICardConfAsByte, 0x10, value);

                OnPropertyChanged(nameof(IsAccessListLongDate));
            }
        }
        private bool isAccessListLongDate;

        #endregion

        /// <summary>
        /// 
        /// </summary>
        public string AreaID
        {
            get => areaID;
            set
            {

                areaID = value;

                if (int.TryParse(value, out int areaIDAsInt))
                {
                    areaIDAsBytes[1] = (byte)((areaIDAsInt & 0xFF00) >> 8);
                    areaIDAsBytes[0] = (byte)(areaIDAsInt & 0x00FF);
                }

                OnPropertyChanged(nameof(AreaID));
            }
        }
        private string areaID;

        /// <summary>
        /// 
        /// </summary>
        public byte[] AreaIDAsBytes
        {
            get => areaIDAsBytes;
            set
            {
                areaIDAsBytes = value;
                OnPropertyChanged(nameof(AreaIDAsBytes));
            }
        }

        #endregion

        #region accessProfiles

        /// <summary>
        /// 
        /// </summary>
        public ObservableCollection<AccessProfile> AccessProfiles
        {
            get => accessProfiles;
            set
            {
                accessProfiles = value;
                OnPropertyChanged(nameof(AccessProfiles));
            }
        }
        private ObservableCollection<AccessProfile> accessProfiles;

        /// <summary>
        /// 
        /// </summary>
        public byte[] CombinedAccessProfileAsBytes
        {
            get => combinedAccessProfileAsBytes;
            set
            {
                combinedAccessProfileAsBytes = value;
                OnPropertyChanged(nameof(CombinedAccessProfileAsBytes));
            }
        }
        private byte[] combinedAccessProfileAsBytes;

        /// <summary>
        /// 
        /// </summary>
        public AccessProfile SelectedAccessProfile
        {
            get => selectedAccessProfile;
            set
            {
                selectedAccessProfile = value;
                OnPropertyChanged(nameof(SelectedAccessProfile));
            }
        }
        private AccessProfile selectedAccessProfile;

        /// <summary>
        /// 
        /// </summary>
        public int MainListWordsCount
        {
            get => mainListWordsCount;

            set
            {
                mainListWordsCount = value;
                OnPropertyChanged(nameof(MainListWordsCount));
            }
        }
        private int mainListWordsCount;

        /// <summary>
        /// 
        /// </summary>
        public string Expires
        {
            get => expires;
            set
            {
                expires = value;
                OnPropertyChanged(nameof(Expires));
            }
        }
        private string expires;

        /// <summary>
        /// 
        /// </summary>
        public string ValidFrom
        {
            get => validFrom;
            set
            {
                validFrom = value;
                OnPropertyChanged(nameof(ValidFrom));
            }
        }
        private string validFrom;

        #endregion

        #region PluginValue

        /// <summary>
        /// 
        /// </summary>
        public string CRC32
        {
            get => crc32;
            set
            {
                crc32 = value;
                OnPropertyChanged(nameof(CRC32));
            }
        }
        private string crc32;

        /// <summary>
        /// Gets the VCNDataTextBox property.
        /// </summary>
        public string VCNDataTextBox
        {
            get => _VCNDataTextBox;

            set
            {
                if (_VCNDataTextBox == value)
                {
                    return;
                }

                _VCNDataTextBox = value;
                OnPropertyChanged(nameof(VCNDataTextBox));
            }
        }
        private string _VCNDataTextBox;
        #endregion

        #region IUserDialogViewModel Implementation
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

        public Action<VCNEditorViewModel> OnOk { get; set; }

        public Action<VCNEditorViewModel> OnCancel { get; set; }

        public Action<VCNEditorViewModel> OnAuth { get; set; }

        public Action<VCNEditorViewModel> OnCloseRequest { get; set; }

        public void Close()
        {
            DialogClosing?.Invoke(this, new EventArgs());
        }

        public void Show(IList<IDialogViewModel> collection)
        {
            collection.Add(this);
        }

        #endregion IUserDialogViewModel Implementation

        /// <summary>
        /// Sets or clears a bit flag on the provided byte.
        /// </summary>
        /// <param name="value">The byte value to update.</param>
        /// <param name="mask">The bit mask to apply.</param>
        /// <param name="enabled">True to set the flag; false to clear it.</param>
        private static void SetFlag(ref byte value, byte mask, bool enabled)
        {
            value = enabled ? (byte)(value | mask) : (byte)(value & (byte)~mask);
        }
    }
}
