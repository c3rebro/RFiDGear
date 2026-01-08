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
        private AccessProfile accessProfile;

        private byte[] cardIDAsBytes = new byte[10]; // will grow in size after rnd id is generated, so keep it that size
        private byte[] idAsBytes = new byte[10];
        private byte[] sIConfAsBytes = new byte[2];
        private byte[] areaIDAsBytes = new byte[2];
        private byte sICardConfAsByte = 0x00;
        private byte fileFormatRelease = 0x00;

        private byte[] expiryAsBytes = new byte[3];
        private byte[] validFromAsBytes = new byte[3];

        private Random rnd = new Random();

        public VCNEditorViewModel()
        {
            try
            {
                //MefHelper.Instance.Container.ComposeParts(this); //load mef imports if any

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

            accessProfile = new AccessProfile();
        }

        #region Dialogs

        public string WindowName = "VCNEditor";

        /// <summary>
        /// 
        /// </summary>
        private ObservableCollection<IDialogViewModel> dialogs = new ObservableCollection<IDialogViewModel>();
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
            int err = 0;
            string combinedWeekSchedule = "";

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
                                combinedWeekSchedule +=
                                    (ByteConverter.GetStringFrom(ws.WeekScheduleAsBytes));
                            }

                            SelectedAccessProfile.WeekSchedules = ByteConverter.GetBytesFrom(combinedWeekSchedule);

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
            string combinedAccessProfile = "";
            _ = new ObservableCollection<ByteArray>();

            if (withID)
            {
                cardIDAsBytes = new byte[10];

                for (int i = 0; i < cardIDAsBytes.Length; i++)
                {
                    cardIDAsBytes[i] = (byte)rnd.Next(0, 255);
                }
            }

            CardID = ByteConverter.GetStringFrom(cardIDAsBytes);

            idAsBytes = new byte[
                cardIDAsBytes.Count()
                + ByteConverter.GetByteCount(ForHostUse)
                + 1];
            idAsBytes = ByteConverter.GetBytesFrom(ByteConverter.GetStringFrom(cardType) + ByteConverter.GetStringFrom(cardIDAsBytes) + ForHostUse);

            #region siconf

            Array.Clear(sIConfAsBytes, 0, 2);

            if (minAccessListLogLevelAsInt >= 0 && minAccessListLogLevelAsInt <= 3)
            {
                sIConfAsBytes[1] = (byte)((sIConfAsBytes[1] & 0xFC) | (byte)minAccessListLogLevelAsInt);
            }

            // toggl si config bits
            sIConfAsBytes[1] = toggleDoorState ? (sIConfAsBytes[1] |= 0x40) : (sIConfAsBytes[1] &= 0xBF);
            sIConfAsBytes[1] = suppressCoupling ? (sIConfAsBytes[1] |= 0x20) : (sIConfAsBytes[1] &= 0xDF);
            sIConfAsBytes[1] = longCoupling ? (sIConfAsBytes[1] |= 0x10) : (sIConfAsBytes[1] &= 0xEF);
            sIConfAsBytes[1] = suppressBeeping ? (sIConfAsBytes[1] |= 0x08) : (sIConfAsBytes[1] &= 0xF7);
            sIConfAsBytes[1] = noEntryWhenALFull ? (sIConfAsBytes[1] |= 0x04) : (sIConfAsBytes[1] &= 0xFB);

            sIConfAsBytes = ByteConverter.Reverse(sIConfAsBytes);

            #endregion

            // create access profiles
            foreach (AccessProfile ap in AccessProfiles ?? new ObservableCollection<AccessProfile>())
            {
                combinedAccessProfile +=
                    ((ByteConverter.GetStringFrom(ap.AccessProfileAsBytes)
                      + ByteConverter.GetStringFrom(ap.MainListWords)
                      + (ap.WeekSchedules != null ? ByteConverter.GetStringFrom(ap.WeekSchedules) : string.Empty) // week schedules
                                                                                                                //					  + "0000" // extra door list
                                                                                                                //					  + "0000" // neg excpt. list
                     ));
            }

            //resize access file
            accessFileAsByte = new byte[
                ByteConverter.GetByteCount(
                    ByteConverter.GetStringFrom(fileFormatRelease)// CRC32_Begin; fileFormat major plus minor
                    + "00"//content identifier
                    + ByteConverter.GetStringFrom(areaIDAsBytes)
                    + ByteConverter.GetStringFrom(sIConfAsBytes)
                    + ByteConverter.GetStringFrom(validFromAsBytes)// valid from
                    + ByteConverter.GetStringFrom(expiryAsBytes)// expiry
                    + ByteConverter.GetStringFrom(sICardConfAsByte)
                    + "0000"//blacklist addr
                    + "000000000000"// reserved
                    + "00"
                    + combinedAccessProfile
                )];

            //fill access file
            accessFileAsByte = ByteConverter.GetBytesFrom(
                ByteConverter.GetStringFrom(fileFormatRelease)// fileFormat major plus minor
                + "02"//content identifier
                + ByteConverter.GetStringFrom(areaIDAsBytes)
                + ByteConverter.GetStringFrom(sIConfAsBytes)
                + ByteConverter.GetStringFrom(validFromAsBytes)// valid from
                + ByteConverter.GetStringFrom(expiryAsBytes)// expiry
                + ByteConverter.GetStringFrom(sICardConfAsByte)
                + "0000"//blacklist addr
                + "000000000000"// reserved
                + ByteConverter.GetStringFrom((byte)(Math.Ceiling((double)(accessFileAsByte.Length / 16) + 4)))// size of accessFile + 4bytes CRC32 which is added afterwards
                + combinedAccessProfile);

            try
            {
                byte[] arrToUse = new byte[28];

                Array.Copy(accessFileAsByte, arrToUse, 28);

                Crc32 crc = new Crc32();
                byte[] crc32AsByte = crc.ComputeHash(arrToUse);

                CRC32 = ByteConverter.GetStringFrom(ByteConverter.Reverse(crc32AsByte));

                accessFileAsByte = ByteConverter.GetBytesFrom(ByteConverter.GetStringFrom(crc32AsByte) + ByteConverter.GetStringFrom(accessFileAsByte));
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
            }

            VCNDataTextBox = string.Format("IDFile: {0}\nAccessFile: {1}",
                                           ByteConverter.GetStringFrom(idAsBytes),
                                           ByteConverter.GetStringFrom(accessFileAsByte));
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

                sICardConfAsByte = value ? (sICardConfAsByte |= 0x10) : (sICardConfAsByte &= 0xEF);

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
    }
}
