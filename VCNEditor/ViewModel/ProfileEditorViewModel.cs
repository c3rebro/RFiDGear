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

using RFiDGear.UI.MVVMDialogs.ViewModels;
using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;

namespace VCNEditor.ViewModel
{
    /// <summary>
    /// Builds and edits access profiles based on user-entered main list values.
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

        /// <summary>
        /// Gets or sets the raw profile text input used to build main list words.
        /// </summary>
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

        /// <summary>
        /// Gets a command that parses <see cref="ProfileText"/> into a new access profile.
        /// </summary>
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
                                |= ByteArrayConverter.Reverse((byte)(1 << (byte)((8 * (Convert.ToInt32(Math.Ceiling((double)mainListRecords[i] / 8)))) - mainListRecords[i])));
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
                                |= ByteArrayConverter.Reverse((byte)(1 << (byte)((8 * (Convert.ToInt32(Math.Ceiling((double)mainListRecords[i] / 8)))) - mainListRecords[i])));
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
                        }

                        MainListWordsCount = accessProfile.MainListWords.Length / 2;

                        break;
                }

                #region accessprofile

                foreach (AccessProfile ap in AccessProfiles)
                {
                    ap.AccessProfileAsBytes[3] &= 0xFE; // remove isLastProfile on every profile
                }

                accessProfile.AccessProfileAsBytes[0] |= 0x01; //set isLastProfile on current profile to 1

                accessProfile.AccessProfileAsBytes[3] |= (byte)((mainListWordsCount & 0x3) << 6); // set mainlistwords
                accessProfile.AccessProfileAsBytes[2] |= (byte)((mainListWordsCount & 0x3FC) >> 2); // set mainlistwords

                accessProfile.AccessProfileAsBytes[3] |= (byte)selectedProfileType; // set profiletype

                accessProfile.AccessProfileAsBytes = ByteArrayConverter.Reverse(accessProfile.AccessProfileAsBytes);

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
        /// Gets the available profile type identifiers for selection.
        /// </summary>
        public int[] ProfileType
        {
            get { return new int[3] { 0, 1, 2 }; }
        }

        /// <summary>
        /// Gets or sets the selected profile type, which controls how profile bytes are serialized.
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
        /// Gets or sets the number of 16-bit words in the main list.
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
        /// Gets or sets the collection of available access profiles.
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
        /// Gets or sets the currently selected access profile.
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
        /// Gets a command that triggers authentication handling for the dialog.
        /// </summary>
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

        /// <summary>
        /// Gets or sets the callback invoked when the dialog is confirmed.
        /// </summary>
        public Action<ProfileEditorViewModel> OnOk { get; set; }

        /// <summary>
        /// Gets or sets the callback invoked when the dialog is canceled.
        /// </summary>
        public Action<ProfileEditorViewModel> OnCancel { get; set; }

        /// <summary>
        /// Gets or sets the callback invoked when authentication is requested.
        /// </summary>
        public Action<ProfileEditorViewModel> OnAuth { get; set; }

        /// <summary>
        /// Gets or sets the callback invoked when the dialog requests to close.
        /// </summary>
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
        /// Gets or sets the localization caption key for the dialog.
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
