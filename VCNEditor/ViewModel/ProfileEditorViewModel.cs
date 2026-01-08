/*
 * Created by SharpDevelop.
 * Date: 21.02.2018
 * Time: 09:00
 * 
 */
using ByteArrayHelper.Extensions;

using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.Xml.Serialization;

using VCNEditor.DataAccessLayer;
using VCNEditor.View;
using VCNEditor.Model;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MvvmDialogs.ViewModels;

namespace VCNEditor.ViewModel
{
    /// <summary>
    /// Description of ProfileEditorViewModel.
    /// </summary>
    public class ProfileEditorViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject, IUserDialogViewModel
    {

        private AccessProfile accessProfile;

        public ProfileEditorViewModel()
        {
            accessProfile = new AccessProfile();
        }

        public ProfileEditorViewModel(AccessProfile ap)
        {
            if (ap != null)
            {
                accessProfile = ap;
            }
            else
            {
                accessProfile = new AccessProfile();
            }
        }

        public string ProfileText
        {
            get => profileText;
            set
            {
                profileText = value;
                OnPropertyChanged(nameof(ProfileText));
            }
        }
        private string profileText;

        #region Commands

        public ICommand AddProfileCommand { get { return new RelayCommand(OnNewAddProfileCommand); } }
        private void OnNewAddProfileCommand()
        {
            try
            {
                int mainListRecordCount;
                int[] mainListRecords;
                double bytesCount;

                if (string.IsNullOrWhiteSpace(ProfileText))
                {
                    return;
                }

                if (AccessProfiles == null)
                {
                    AccessProfiles = new ObservableCollection<AccessProfile>();
                }

                switch (SelectedProfileType)
                {
                    case 2:

                        accessProfile = new AccessProfile();

                        mainListRecordCount = ProfileText.Replace(" ", string.Empty).Replace(";", string.Empty).Split(',').Count();
                        mainListRecords = Array.ConvertAll<string, int>(ProfileText.Replace(" ", string.Empty).Replace(";", string.Empty).Split(','), new Converter<string, int>((x) =>
                        {
                            return int.Parse(x);
                        }));

                        if (mainListRecords.Length < 1)
                        {
                            return;
                        }

                        bytesCount = (double)mainListRecords.Max() / 8;

                        accessProfile.MainListWords = new byte[Convert.ToInt32((Convert.ToInt32(Math.Ceiling(bytesCount)) % 2 == 0)
                                                                               ? (Math.Ceiling(bytesCount))
                                                                               : (Math.Ceiling(bytesCount) + 1))];

                        mainListRecords = mainListRecords.OrderBy((x) =>
                        {
                            return x;
                        }).ToArray();

                        for (int i = 0; i < mainListRecords.Count(); i++)
                        {
                            accessProfile.MainListWords[(Convert.ToInt32(Math.Ceiling((double)mainListRecords[i] / 8)) - 1)]
                                |= ByteConverter.Reverse((byte)(1 << (byte)((8 * (Convert.ToInt32(Math.Ceiling((double)mainListRecords[i] / 8)))) - mainListRecords[i])));
                        }

                        MainListWordsCount = accessProfile.MainListWords.Length / 2;

                        break;

                    case 1:

                        accessProfile = new AccessProfile();

                        if (string.IsNullOrWhiteSpace(ProfileText))
                        {
                            break;
                        }

                        mainListRecordCount = ProfileText.Replace(" ", string.Empty).Replace(";", string.Empty).Split(',').Count();
                        mainListRecords = Array.ConvertAll<string, int>(ProfileText.Replace(" ", string.Empty).Replace(";", string.Empty).Split(','), new Converter<string, int>((x) =>
                        {
                            return int.Parse(x);
                        }));
                        if (mainListRecords.Length < 1)
                        {
                            return;
                        }

                        bytesCount = (double)mainListRecords.Max() / 8;

                        accessProfile.MainListWords = new byte[Convert.ToInt32((Convert.ToInt32(Math.Ceiling(bytesCount)) % 2 == 0)
                                                                               ? (Math.Ceiling(bytesCount))
                                                                               : (Math.Ceiling(bytesCount) + 1))];

                        mainListRecords = mainListRecords.OrderBy((x) =>
                        {
                            return x;
                        }).ToArray();

                        for (int i = 0; i < mainListRecords.Count(); i++)
                        {
                            accessProfile.MainListWords[(Convert.ToInt32(Math.Ceiling((double)mainListRecords[i] / 8)) - 1)]
                                |= ByteConverter.Reverse((byte)(1 << (byte)((8 * (Convert.ToInt32(Math.Ceiling((double)mainListRecords[i] / 8)))) - mainListRecords[i])));
                        }

                        MainListWordsCount = accessProfile.MainListWords.Length / 2;


                        break;

                    case 0:

                        accessProfile = new AccessProfile();

                        if (string.IsNullOrWhiteSpace(ProfileText))
                        {
                            break;
                        }

                        mainListRecordCount = ProfileText
                            .Replace(" ", string.Empty)
                            .Replace(";", string.Empty)
                            .Split(',').Count();

                        mainListRecords = Array
                            .ConvertAll<string, int>(ProfileText
                                                     .Replace(" ", string.Empty)
                                                     .Replace(";", string.Empty)
                                                     .Split(','), new Converter<string, int>((x) =>
                                                                                             {
                                                                                                 return int.Parse(x);
                                                                                             }));

                        if (mainListRecords.Any(x => x > 0xffff))
                        {
                            break;
                        }

                        accessProfile.MainListWords = new byte[mainListRecords.Count() * 2];

                        mainListRecords = mainListRecords.OrderBy((x) =>
                        {
                            return x;
                        }).ToArray();

                        for (int i = 0; i < mainListRecords.Count(); i++)
                        {
                            accessProfile.MainListWords[i * 2] |= (byte)(mainListRecords[i] & 0xFF);
                            accessProfile.MainListWords[(i * 2) + 1] |= (byte)((mainListRecords[i] & 0xFF00) >> 8);

                            //						Array.ConvertAll<int, byte[]>((int[])mainListRecords,
                            //						                                                            new Converter<int, byte[]>((x) =>
                            //						                                                                                           {
                            //						                                                                                           	return int.Parse(x);
                            //						                                                                                           }));
                        }

                        MainListWordsCount = accessProfile.MainListWords.Length / 2;

                        break;
                }

                #region accessprofile

                //Array.Clear(accessProfile.AccessProfileAsBytes, 0, 4);

                foreach (AccessProfile ap in AccessProfiles)
                {
                    ap.AccessProfileAsBytes[3] &= 0xFE; // remove isLastProfile on every profile
                }

                accessProfile.AccessProfileAsBytes[0] |= 0x01; //set isLastProfile on current profile to 1

                accessProfile.AccessProfileAsBytes[3] |= (byte)((mainListWordsCount & 0x3) << 6); // set mainlistwords
                accessProfile.AccessProfileAsBytes[2] |= (byte)((mainListWordsCount & 0x3FC) >> 2); // set mainlistwords

                accessProfile.AccessProfileAsBytes[3] |= (byte)selectedProfileType; // set profiletype

                accessProfile.AccessProfileAsBytes = ByteConverter.Reverse(accessProfile.AccessProfileAsBytes);

                AccessProfiles.Add(accessProfile);

                AccessProfiles = new ObservableCollection<AccessProfile>(AccessProfiles);

                SelectedAccessProfile = accessProfile;

                #endregion
            }

            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
            }

        }

        #endregion

        #region DependencyProps


        /// <summary>
        /// 
        /// </summary>
        public int[] ProfileType
        {
            get { return new int[3] { 0, 1, 2 }; }
        }

        /// <summary>
        /// 
        /// </summary>
        public int SelectedProfileType
        {
            get => selectedProfileType;

            set
            {
                selectedProfileType = value;
                OnPropertyChanged(nameof(SelectedProfileType));
            }
        }
        private int selectedProfileType;

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

        public ICommand AuthCommand { get { return new RelayCommand(Auth); } }

        protected virtual void Auth()
        {
            if (OnAuth != null)
            {
                OnAuth(this);
            }
            else
            {
                Close();
            }
        }

        public Action<ProfileEditorViewModel> OnOk { get; set; }

        public Action<ProfileEditorViewModel> OnCancel { get; set; }

        public Action<ProfileEditorViewModel> OnAuth { get; set; }

        public Action<ProfileEditorViewModel> OnCloseRequest { get; set; }

        public void Close()
        {
            DialogClosing?.Invoke(this, new EventArgs());
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
            get => _Caption;
            set
            {
                _Caption = value;
                OnPropertyChanged(nameof(Caption));
            }
        }
        private string _Caption;

        #endregion Localization
    }
}
