using VCNEditor.DataAccessLayer;
using VCNEditor.Model;
using VCNEditor.View;
using VCNEditor.ViewModel;

using MvvmDialogs.ViewModels;

using CRC_IT;

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using PluginSystem;

namespace VCNEditor.ViewModel
{
	public class VCNEditorViewModel : PluginViewModelBase<VCNEditor>
	{
		private byte[] idFileAsByte;
		private byte[] accessFileAsByte;
		private AccessProfile accessProfile;
		
		private byte[] sIConfAsBytes = new byte[2];
		private byte[] areaIDAsBytes = new byte[2];
		private byte sICardConfAsByte = 0x00;
		private byte fileFormatRelease = 0x00;
		
		public VCNEditorViewModel(VCNEditor plugin)
			: base(plugin)
		{
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
			
			sIConfAsBytes = new byte[2]{0,0};
			
			UpStreamFileContentAsString = "0";
			UpStreamFileTypeAsString = "0";

			accessProfile = new AccessProfile();
		}

		#region Dialogs

		/// <summary>
		/// 
		/// </summary>
		public ObservableCollection<IDialogViewModel> Dialogs { get { return dialogs; } }
		private ObservableCollection<IDialogViewModel> dialogs = new ObservableCollection<IDialogViewModel>();

		#endregion Dialogs
		
		#region Commands
		
		public ICommand AddProfileCommand { get { return new RelayCommand(OnNewAddProfileCommand); }}
		private void OnNewAddProfileCommand()
		{
			if (AccessProfiles == null)
			{
				AccessProfiles = new ObservableCollection<AccessProfile>();
			}
			
			#region accessprofile
			
			//Array.Clear(accessProfile.AccessProfileAsBytes, 0, 4);
			
			foreach(AccessProfile ap in AccessProfiles)
			{
				ap.AccessProfileAsBytes[3] &= 0xFE; // remove isLastProfile on every profile
			}
			
			accessProfile.AccessProfileAsBytes[0] |= 0x01; //set isLastProfile on current profile to 1
			
			accessProfile.AccessProfileAsBytes[3] |= (byte)((mainListWordsCount & 0x3) << 6); // set mainlistwords
			accessProfile.AccessProfileAsBytes[2] |= (byte)((mainListWordsCount & 0x3FC) >> 2); // set mainlistwords
			
			accessProfile.AccessProfileAsBytes[3] |= (byte)selectedProfileType; // set profiletype
			
			accessProfile.AccessProfileAsBytes = ConvToLittleEndian(accessProfile.AccessProfileAsBytes);
			
			AccessProfiles.Add(accessProfile);
			
			SelectedAccessProfile = accessProfile;
			
			#endregion
		}
		
		public ICommand EditMainListCommand { get { return new RelayCommand(OnNewEditMainListCommand); }}
		private void OnNewEditMainListCommand()
		{
			try
			{
				switch (SelectedProfileType)
				{
					case 2:
						Dialogs.Add(new ProfileEditorViewModel()
						            {
						            	
						            	OnOk = (sender) =>
						            	{
						            		accessProfile = new AccessProfile();
						            		
						            		int lidCount = sender.ProfileText.Replace(" ",string.Empty).Replace(";",string.Empty).Split(',').Count();
						            		int[] lids = Array.ConvertAll<string, int>(sender.ProfileText.Replace(" ",string.Empty).Replace(";",string.Empty).Split(','), new Converter<string, int>((x) => {return int.Parse(x);}));
						            		double bytesCount = (double)lids.Max() / 8;

						            		accessProfile.MainListWords = new byte[Convert.ToInt32((Convert.ToInt32(Math.Ceiling(bytesCount)) % 2 == 0)
						            		                                                       ? (Math.Ceiling(bytesCount))
						            		                                                       : (Math.Ceiling(bytesCount) +1))];
						            		
						            		lids = lids.OrderBy((x) => { return x;}).ToArray();
						            		
						            		for (int i = 0; i < lids.Count(); i++)
						            		{
						            			//int ceilTest = (Convert.ToInt32(Math.Ceiling((double)lids[i] / 8)));
						            			//int bitMaskTest = (8 * ceilTest) - lids[i]; //(((byte)lids[i] - 1) / (Convert.ToInt32(Math.Ceiling((double)lids[i] / 8))));
						            			//int bytePosTest = (Convert.ToInt32(Math.Ceiling((double)lids[i] / 8)) - 1);
						            			
						            			accessProfile.MainListWords[(Convert.ToInt32(Math.Ceiling((double)lids[i] / 8)) - 1)]
						            				|= Reverse((byte)(1 << (byte)((8 * (Convert.ToInt32(Math.Ceiling((double)lids[i] / 8)))) - lids[i])));
						            		}
						            		
						            		MainListWordsCount = accessProfile.MainListWords.Length / 2;
						            		
						            		//mainListWords = ConvToLittleEndian(mainListWords);
						            		
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
						break;
						
					case 1:
						break;
				}
			}
			
			catch (Exception e)
			{
				//LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
				//dialogs.Clear();
			}
		}

		public ICommand GenerateRandomIDCommand { get { return new RelayCommand(OnNewGenerateRandomIDCommand); }}
		private void OnNewGenerateRandomIDCommand()
		{
			var err = 0;
			var rnd = new Random();
			var idAsBytes = new byte[10];
			
			for (int i = 0; i < idAsBytes.Length; i++)
			{
				idAsBytes[i] = (byte)rnd.Next(0,255);
			}
			
			CardID = CustomConverter.HexToString(idAsBytes);
			
			idAsBytes = new byte[
				CustomConverter.GetByteCount(CardID)
				+ CustomConverter.GetByteCount(ForHostUse)
				+ 1];

			idAsBytes = CustomConverter.GetBytes(CustomConverter.HexToString(cardType) + CardID + ForHostUse, out err);
			
			#region siconf
			
			Array.Clear(sIConfAsBytes, 0, 2);
			
			if(minAccessListLogLevelAsInt >= 0 && minAccessListLogLevelAsInt <= 3)
			{
				sIConfAsBytes[1] = (byte)((sIConfAsBytes[1] & 0xFC) | (byte)minAccessListLogLevelAsInt);
			}
			
			sIConfAsBytes[1] = toggleDoorState ? (sIConfAsBytes[1] |= 0x40) : (sIConfAsBytes[1] &= 0xBF);
			sIConfAsBytes[1] = suppressCoupling ? (sIConfAsBytes[1] |= 0x20) : (sIConfAsBytes[1] &= 0xDF);
			sIConfAsBytes[1] = longCoupling ? (sIConfAsBytes[1] |= 0x10) : (sIConfAsBytes[1] &= 0xEF);
			sIConfAsBytes[1] = suppressBeeping ? (sIConfAsBytes[1] |= 0x08) : (sIConfAsBytes[1] &= 0xF7);
			sIConfAsBytes[1] = noEntryWhenALFull ? (sIConfAsBytes[1] |= 0x04) : (sIConfAsBytes[1] &= 0xFB);
			
			sIConfAsBytes = ConvToLittleEndian(sIConfAsBytes);
			
			#endregion
			
			accessFileAsByte = new byte[
				CustomConverter.GetByteCount(
					CustomConverter.HexToString(fileFormatRelease) // CRC32_Begin; fileFormat major plus minor
					+ "00" //content identifier
					+ CustomConverter.HexToString(areaIDAsBytes)
					+ CustomConverter.HexToString(sIConfAsBytes)
					+ "000000" // valid from
					+ "000000" // expiry
					+ CustomConverter.HexToString(sICardConfAsByte)
					+ "0000" //blacklist addr
					+ "000000000000" // reserved
					+ "00"
					+ CustomConverter.HexToString(SelectedAccessProfile.AccessProfileAsBytes) //CustomConverter.HexToString(SelectedAccessProfile.AccessProfileAsBytes)
					+ CustomConverter.HexToString(SelectedAccessProfile.MainListWords)
					+ "00000000" // week schedules
					+ "0000" // extra door list
					+ "0000" // neg excpt. list
				)];
			
			accessFileAsByte = CustomConverter.GetBytes(
				CustomConverter.HexToString(fileFormatRelease) // fileFormat major plus minor
				+ "02" //content identifier
				+ CustomConverter.HexToString(areaIDAsBytes)
				+ CustomConverter.HexToString(sIConfAsBytes)
				+ "000000" // valid from
				+ "000000" // expiry
				+ CustomConverter.HexToString(sICardConfAsByte)
				+ "0000" //blacklist addr
				+ "000000000000" // reserved
				+ CustomConverter.HexToString((byte)(Math.Ceiling((double)accessFileAsByte.Length / 16))) // size
				+ CustomConverter.HexToString(SelectedAccessProfile.AccessProfileAsBytes)
				+ CustomConverter.HexToString(SelectedAccessProfile.MainListWords)
				+ "00000000" // week schedules
				+ "0000" // extra door list
				+ "0000", // neg excpt. list
				
				out err);

			if(err == 0)
			{
				byte[] arrToUse = new byte[28];
				
				Array.Copy(accessFileAsByte, arrToUse, 28);
				
				Crc32 crc = new Crc32();
				byte[] crc32AsByte = crc.ComputeHash(arrToUse);
				
				CRC32 = CustomConverter.HexToString(ConvToLittleEndian(crc32AsByte));
				
				accessFileAsByte = CustomConverter.GetBytes(CustomConverter.HexToString(crc32AsByte) + CustomConverter.HexToString(accessFileAsByte), out err);
			}
			
			Plugin1Value = string.Format("IDFile: {0}\nAccessFile: {1}",
			                             CustomConverter.HexToString(idAsBytes),
			                             CustomConverter.HexToString(accessFileAsByte));
		}
		
		
		#endregion

		#region IDFile
		
		/// <summary>
		/// 
		/// </summary>
		public byte CardType
		{
			get
			{
				return cardType;
			}
			set
			{
				cardType = value;
				RaisePropertyChanged("CardType");
			}
		} private byte cardType;

		/// <summary>
		/// 
		/// </summary>
		public string ForHostUse
		{
			get
			{
				return forHostUse;
			}
			set
			{
				forHostUse = value;
				RaisePropertyChanged("ForHostUse");
			}
		} private string forHostUse;

		/// <summary>
		/// 
		/// </summary>
		public string CardID
		{
			get
			{
				return cardID;
			}
			set
			{
				cardID = value;
				RaisePropertyChanged("CardID");
			}
		} private string cardID;

		#endregion

		#region accessFileHeader

		/// <summary>
		/// 
		/// </summary>
		public string ReleaseMajor
		{
			get
			{
				return releaseMajor;
			}
			set
			{
				releaseMajor = value;
				int i;
				int.TryParse(value, out i);
				
				ReleaseMajorAsInt = i;
				
				RaisePropertyChanged("ReleaseMajor");
			}
		} private string releaseMajor;

		/// <summary>
		/// 
		/// </summary>
		public int ReleaseMajorAsInt
		{
			get
			{
				return releaseMajorAsInt;
			}
			set
			{
				releaseMajorAsInt = value;
				
				if(value >= 0 && value <= 7)
				{
					fileFormatRelease = (byte)((fileFormatRelease & 0xF8) | (byte)releaseMajorAsInt);
				}
				
				RaisePropertyChanged("ReleaseMajorAsInt");
			}
		} private int releaseMajorAsInt;

		/// <summary>
		/// 
		/// </summary>
		public string ReleaseMinor
		{
			get
			{
				return releaseMinor;
			}
			set
			{
				releaseMinor = value;
				int i;
				int.TryParse(value, out i);
				
				ReleaseMinorAsInt = i;
				RaisePropertyChanged("ReleaseMinor");
			}
		} private string releaseMinor;

		/// <summary>
		/// 
		/// </summary>
		public int ReleaseMinorAsInt
		{
			get
			{
				return releaseMinorAsInt;
			}
			set
			{
				releaseMinorAsInt = value;
				
				if(value >= 0 && value <= 31)
				{
					fileFormatRelease = (byte)((fileFormatRelease & 0x03) | (byte)(releaseMinorAsInt << 3));
				}
				
				RaisePropertyChanged("ReleaseMinorAsInt");
			}
		} private int releaseMinorAsInt;

		/// <summary>
		/// 
		/// </summary>
		public string ContentIdentifier
		{
			get
			{
				return contentIdentifier;
			}
			set
			{
				contentIdentifier = value;
				RaisePropertyChanged("ContentIdentifier");
			}
		} private string contentIdentifier;

		#region si conf
		/// <summary>
		/// 
		/// </summary>
		public string MinAccessListLogLevelAsString
		{
			get
			{
				return minAccessListLogLevelAsString;
			}
			set
			{
				minAccessListLogLevelAsString = value;
				
				int i;
				int.TryParse(value, out i);
				
				MinAccessListLogLevelAsInt = i;
				
				RaisePropertyChanged("MinAccessListLogLevelAsString");
			}
		} private string minAccessListLogLevelAsString;

		/// <summary>
		/// 
		/// </summary>
		public int MinAccessListLogLevelAsInt
		{
			get
			{
				return minAccessListLogLevelAsInt;
			}
			set
			{
				minAccessListLogLevelAsInt = value;
				
				RaisePropertyChanged("MinAccessListLogLevelAsInt");
			}
		} private int minAccessListLogLevelAsInt;

		/// <summary>
		/// 
		/// </summary>
		public bool NoEntryWhenALFull
		{
			get
			{
				return noEntryWhenALFull;
			}
			set
			{
				noEntryWhenALFull = value;
				
				RaisePropertyChanged("NoEntryWhenALFull");
			}
		} private bool noEntryWhenALFull;

		/// <summary>
		/// 
		/// </summary>
		public bool SuppressBeeping
		{
			get
			{
				return suppressBeeping;
			}
			set
			{
				suppressBeeping = value;
				
				RaisePropertyChanged("SuppressBeeping");
			}
		} private bool suppressBeeping;

		/// <summary>
		/// 
		/// </summary>
		public bool LongCoupling
		{
			get
			{
				return longCoupling;
			}
			set
			{
				longCoupling = value;
				
				RaisePropertyChanged("LongCoupling");
			}
		} private bool longCoupling;

		/// <summary>
		/// 
		/// </summary>
		public bool SuppressCoupling
		{
			get
			{
				return suppressCoupling;
			}
			set
			{
				suppressCoupling = value;
				
				RaisePropertyChanged("SuppressCoupling");
			}
		} private bool suppressCoupling;

		/// <summary>
		/// 
		/// </summary>
		public bool ToggleDoorState
		{
			get
			{
				return toggleDoorState;
			}
			set
			{
				toggleDoorState = value;
				
				RaisePropertyChanged("ToggleDoorState");
			}
		} private bool toggleDoorState;

		#endregion

		#region si card conf

		/// <summary>
		/// 
		/// </summary>
		public string UpStreamFileContentAsString
		{
			get
			{
				return upStreamFileContentAsString;
			}
			set
			{
				upStreamFileContentAsString = value;
				
				int i;
				int.TryParse(value, out i);
				
				UpStreamFileContentAsInt = i;
				
				RaisePropertyChanged("UpStreamFileContentAsString");
			}
		} private string upStreamFileContentAsString;

		/// <summary>
		/// 
		/// </summary>
		public int UpStreamFileContentAsInt
		{
			get
			{
				return upStreamFileContentAsInt;
			}
			set
			{
				upStreamFileContentAsInt = value;
				
				if(value >= 0 && value <= 3)
				{
					sICardConfAsByte = (byte)((sICardConfAsByte & 0xFC) | (byte)upStreamFileContentAsInt);
				}
				
				RaisePropertyChanged("UpStreamFileContentAsInt");
			}
		} private int upStreamFileContentAsInt;

		/// <summary>
		/// 
		/// </summary>
		public string UpStreamFileTypeAsString
		{
			get
			{
				return upStreamFileTypeAsString;
			}
			set
			{
				upStreamFileTypeAsString = value;
				
				int i;
				int.TryParse(value, out i);
				
				UpStreamFileTypeAsInt = i;
				
				RaisePropertyChanged("UpStreamFileTypeAsString");
			}
		} private string upStreamFileTypeAsString;

		/// <summary>
		/// 
		/// </summary>
		public int UpStreamFileTypeAsInt
		{
			get
			{
				return upStreamFileTypeAsInt;
			}
			set
			{
				upStreamFileTypeAsInt = value;
				
				if(value >= 0 && value <= 3)
				{
					sICardConfAsByte = (byte)((sICardConfAsByte & 0xF3) | (byte)(upStreamFileTypeAsInt << 2));
				}
				
				RaisePropertyChanged("UpStreamFileTypeAsInt");
			}
		} private int upStreamFileTypeAsInt;

		/// <summary>
		/// 
		/// </summary>
		public bool IsAccessListLongDate
		{
			get
			{
				return isAccessListLongDate;
			}
			set
			{
				isAccessListLongDate = value;

				sICardConfAsByte = value ? (sICardConfAsByte |= 0x10) : (sICardConfAsByte &= 0xEF);
				
				RaisePropertyChanged("IsAccessListLongDate");
			}
		} private bool isAccessListLongDate;

		#endregion
		
		/// <summary>
		/// 
		/// </summary>
		public string AreaID
		{
			get
			{
				return areaID;
			}
			set
			{
				int areaIDAsInt;
				
				areaID = value;
				
				if(int.TryParse(value, out areaIDAsInt))
				{
					areaIDAsBytes[1] = (byte)((areaIDAsInt & 0xFF00) >> 8);
					areaIDAsBytes[0] = (byte)(areaIDAsInt & 0x00FF);
				}
				
				RaisePropertyChanged("AreaID");
			}
		} private string areaID;

		/// <summary>
		/// 
		/// </summary>
		public byte[] AreaIDAsBytes
		{
			get
			{
				return areaIDAsBytes;
			}
			set
			{
				areaIDAsBytes = value;
				RaisePropertyChanged("AreaIDAsInt");
			}
		}
		
		#endregion

		#region accessProfiles

		/// <summary>
		/// 
		/// </summary>
		public ObservableCollection<AccessProfile> AccessProfiles
		{
			get
			{
				return accessProfiles;
			}
			set
			{
				accessProfiles = value;
				RaisePropertyChanged("AccessProfiles");
			}
		} private ObservableCollection<AccessProfile> accessProfiles;
		
		/// <summary>
		/// 
		/// </summary>
		public byte[] CombinedAccessProfileAsBytes
		{
			get
			{
				return combinedAccessProfileAsBytes;
			}
			set
			{
				combinedAccessProfileAsBytes = value;
				RaisePropertyChanged("CombinedAccessProfileAsBytes");
			}
		} private byte[] combinedAccessProfileAsBytes;
		
		/// <summary>
		/// 
		/// </summary>
		public AccessProfile SelectedAccessProfile
		{
			get
			{
				return selectedAccessProfile;
			}
			set
			{
				selectedAccessProfile = value;
				RaisePropertyChanged("SelectedAccessProfile");
			}
		}
		private AccessProfile selectedAccessProfile;
		
		/// <summary>
		/// 
		/// </summary>
		public int MainListWordsCount
		{
			get
			{
				return mainListWordsCount;
			}
			
			set
			{
				mainListWordsCount = value;
				RaisePropertyChanged("MainListWordsCount");
			}
		} private int mainListWordsCount;
		
		/// <summary>
		/// 
		/// </summary>
		public int SelectedProfileType
		{
			get
			{
				return selectedProfileType;
			}
			
			set
			{
				selectedProfileType = value;
				RaisePropertyChanged("SelectedProfileType");
			}
		} private int selectedProfileType;

		/// <summary>
		/// 
		/// </summary>
		public int[] ProfileType
		{
			get { return new int[3]{0,1,2}; }
		}
		
		
		#endregion
		
		#region Plugin1Value
		
		/// <summary>
		/// 
		/// </summary>
		public string CRC32
		{
			get
			{
				return crc32;
			}
			set
			{
				crc32 = value;
				RaisePropertyChanged("CRC32");
			}
		}	private string crc32;

		/// <summary>
		/// Gets the Plugin1Value property.
		/// Changes to that property's value raise the PropertyChanged event.
		/// This property's value is broadcasted by the Messenger's default instance when it changes.
		/// </summary>
		public string Plugin1Value
		{
			get
			{
				return _Plugin1Value;
			}

			set
			{
				if (_Plugin1Value == value)
				{
					return;
				}

				_Plugin1Value = value;

				// Update bindings, no broadcast
				RaisePropertyChanged("Plugin1Value");
			}
		} private string _Plugin1Value;
		#endregion
		
		#region extensions
		
		private byte[] ConvToLittleEndian(byte[] bigEndian)
		{
			if (BitConverter.IsLittleEndian)
				Array.Reverse(bigEndian);
			
			return bigEndian;
		}
		
		private byte Reverse(byte x)
		{
			byte y = 0;
			for (byte i = 0; i < 8; ++i)
			{
				y <<= 1;
				y |= (byte)(x & 1);
				x >>= 1;
			}
			return y;
		}
		
		#endregion
	}
}
