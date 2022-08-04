/*
 * Created by SharpDevelop.
 * Date: 10/11/2017
 * Time: 22:15
 *
 */

using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using MefMvvm.SharedContracts;
using MefMvvm.SharedContracts.ViewModel;
using MvvmDialogs.ViewModels;

using RFiDGear.DataAccessLayer.Remote.FromIO;
using RFiDGear.DataAccessLayer;
using RFiDGear.Model;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Xml.Serialization;

namespace RFiDGear.ViewModel
{
	/// <summary>
	/// Description of MifareClassicSetupViewModel.
	/// </summary>
	public class MifareClassicSetupViewModel : ViewModelBase, IUserDialogViewModel
	{
		#region fields

		private ObservableCollection<MifareClassicDataBlockAccessConditionModel> dataBlock_AccessBits = new ObservableCollection<MifareClassicDataBlockAccessConditionModel>
			(new[]
			 {
			 	new MifareClassicDataBlockAccessConditionModel(0,
			 	                                SectorTrailer_DataBlock.BlockAll,
			 	                                AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB,
			 	                                AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB,
			 	                                AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB,
			 	                                AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB),

			 	new MifareClassicDataBlockAccessConditionModel(1,
			 	                                SectorTrailer_DataBlock.BlockAll,
			 	                                AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB,
			 	                                AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyB,
			 	                                AccessCondition_MifareClassicSectorTrailer.NotAllowed,
			 	                                AccessCondition_MifareClassicSectorTrailer.NotAllowed),

			 	new MifareClassicDataBlockAccessConditionModel(2,
			 	                                SectorTrailer_DataBlock.BlockAll,
			 	                                AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB,
			 	                                AccessCondition_MifareClassicSectorTrailer.NotAllowed,
			 	                                AccessCondition_MifareClassicSectorTrailer.NotAllowed,
			 	                                AccessCondition_MifareClassicSectorTrailer.NotAllowed),

			 	new MifareClassicDataBlockAccessConditionModel(3,
			 	                                SectorTrailer_DataBlock.BlockAll,
			 	                                AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB,
			 	                                AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyB,
			 	                                AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyB,
			 	                                AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB),

			 	new MifareClassicDataBlockAccessConditionModel(4,
			 	                                SectorTrailer_DataBlock.BlockAll,
			 	                                AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB,
			 	                                AccessCondition_MifareClassicSectorTrailer.NotAllowed,
			 	                                AccessCondition_MifareClassicSectorTrailer.NotAllowed,
			 	                                AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB),

			 	new MifareClassicDataBlockAccessConditionModel(5,
			 	                                SectorTrailer_DataBlock.BlockAll,
			 	                                AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyB,
			 	                                AccessCondition_MifareClassicSectorTrailer.NotAllowed,
			 	                                AccessCondition_MifareClassicSectorTrailer.NotAllowed,
			 	                                AccessCondition_MifareClassicSectorTrailer.NotAllowed),

			 	new MifareClassicDataBlockAccessConditionModel(6,
			 	                                SectorTrailer_DataBlock.BlockAll,
			 	                                AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyB,
			 	                                AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyB,
			 	                                AccessCondition_MifareClassicSectorTrailer.NotAllowed,
			 	                                AccessCondition_MifareClassicSectorTrailer.NotAllowed),

			 	new MifareClassicDataBlockAccessConditionModel(7,
			 	                                SectorTrailer_DataBlock.BlockAll,
			 	                                AccessCondition_MifareClassicSectorTrailer.NotAllowed,
			 	                                AccessCondition_MifareClassicSectorTrailer.NotAllowed,
			 	                                AccessCondition_MifareClassicSectorTrailer.NotAllowed,
			 	                                AccessCondition_MifareClassicSectorTrailer.NotAllowed)
			 });

		private ObservableCollection<MifareClassicSectorAccessConditionModel> sectorTrailer_AccessBits = new ObservableCollection<MifareClassicSectorAccessConditionModel>
			(new[]
			 {
			 	new MifareClassicSectorAccessConditionModel(0,
			 	                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
			 	                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
			 	                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
			 	                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
			 	                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
			 	                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA),

			 	new MifareClassicSectorAccessConditionModel(1,
			 	                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
			 	                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyB,
			 	                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB,
			 	                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
			 	                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
			 	                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyB),

			 	new MifareClassicSectorAccessConditionModel(2,
			 	                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
			 	                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
			 	                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
			 	                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
			 	                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
			 	                             AccessCondition_MifareClassicSectorTrailer.NotAllowed),

			 	new MifareClassicSectorAccessConditionModel(3,
			 	                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
			 	                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
			 	                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB,
			 	                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
			 	                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
			 	                             AccessCondition_MifareClassicSectorTrailer.NotAllowed),

			 	new MifareClassicSectorAccessConditionModel(4,
			 	                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
			 	                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
			 	                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
			 	                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
			 	                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
			 	                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA),

			 	new MifareClassicSectorAccessConditionModel(5,
			 	                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
			 	                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
			 	                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB,
			 	                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyB,
			 	                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
			 	                             AccessCondition_MifareClassicSectorTrailer.NotAllowed),

			 	new MifareClassicSectorAccessConditionModel(6,
			 	                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
			 	                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyB,
			 	                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB,
			 	                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyB,
			 	                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
			 	                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyB),

			 	new MifareClassicSectorAccessConditionModel(7,
			 	                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
			 	                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
			 	                             AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB,
			 	                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
			 	                             AccessCondition_MifareClassicSectorTrailer.NotAllowed,
			 	                             AccessCondition_MifareClassicSectorTrailer.NotAllowed)
			 });

		private MifareClassicChipModel chipModel;
		private MifareClassicSectorModel sectorModel;
		private MifareClassicDataBlockModel dataBlock0 = new MifareClassicDataBlockModel();
		private MifareClassicDataBlockModel dataBlock1 = new MifareClassicDataBlockModel();
		private MifareClassicDataBlockModel dataBlock2 = new MifareClassicDataBlockModel();
		private MifareClassicDataBlockModel dataBlockCombined = new MifareClassicDataBlockModel();

		private DatabaseReaderWriter databaseReaderWriter;
		private SettingsReaderWriter settings = new SettingsReaderWriter();

		private byte madGPB = 0xC1;
		
		#endregion fields

		#region Constructors
		
		/// <summary>
		///
		/// </summary>
		public MifareClassicSetupViewModel()
		{
			MefHelper.Instance.Container.ComposeParts(this); //Load Plugins
			
			chipModel = new MifareClassicChipModel(string.Format("Task Description: {0}", SelectedTaskDescription), CARD_TYPE.Mifare4K);

			sectorModel = new MifareClassicSectorModel(4,
			                                           AccessCondition_MifareClassicSectorTrailer.NotAllowed,
			                                           AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
			                                           AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
			                                           AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
			                                           AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
			                                           AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA);

			sectorModel.DataBlock.Add(new MifareClassicDataBlockModel(0, SectorTrailer_DataBlock.Block0, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB));
			sectorModel.DataBlock.Add(new MifareClassicDataBlockModel(0, SectorTrailer_DataBlock.Block1, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB));
			sectorModel.DataBlock.Add(new MifareClassicDataBlockModel(0, SectorTrailer_DataBlock.Block2, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB));
			sectorModel.DataBlock.Add(new MifareClassicDataBlockModel(0, SectorTrailer_DataBlock.BlockAll, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB));

			childNodeViewModelFromChip = new RFiDChipChildLayerViewModel(sectorModel, null, CARD_TYPE.Mifare4K, null, true);
			childNodeViewModelTemp = new RFiDChipChildLayerViewModel(sectorModel, null, CARD_TYPE.Mifare4K, null, true);
			
			Selected_DataBlockType = SectorTrailer_DataBlock.BlockAll;
			Selected_Sector_AccessCondition = sectorTrailer_AccessBits[4];
			Selected_DataBlock_AccessCondition = dataBlock_AccessBits[0];
			
			MifareClassicKeys = CustomConverter.GenerateStringSequence(0,39).ToArray();
			
			SelectedClassicSectorCurrent = "0";
			
			useMAD = false;
		}

		/// <summary>
		///
		/// </summary>
		/// <param name="_selectedSetupViewModel"></param>
		/// <param name="_dialogs"></param>
		public MifareClassicSetupViewModel(object _selectedSetupViewModel, ObservableCollection<IDialogViewModel> _dialogs)
		{
			try
			{
				MefHelper.Instance.Container.ComposeParts(this); //Load Plugins
				
				databaseReaderWriter = new DatabaseReaderWriter();
				
				chipModel = new MifareClassicChipModel(string.Format("Task Description: {0}", SelectedTaskDescription), CARD_TYPE.Mifare4K);

				sectorModel = new MifareClassicSectorModel(4,
				                                           AccessCondition_MifareClassicSectorTrailer.NotAllowed,
				                                           AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
				                                           AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
				                                           AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
				                                           AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
				                                           AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA);
				
				sectorModel.DataBlock.Add(new MifareClassicDataBlockModel(0, SectorTrailer_DataBlock.Block0, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB));
				sectorModel.DataBlock.Add(new MifareClassicDataBlockModel(0, SectorTrailer_DataBlock.Block1, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB));
				sectorModel.DataBlock.Add(new MifareClassicDataBlockModel(0, SectorTrailer_DataBlock.Block2, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB));
				sectorModel.DataBlock.Add(new MifareClassicDataBlockModel(0, SectorTrailer_DataBlock.BlockAll, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB));

				sectorModel.AccessBitsAsString = settings.DefaultSpecification.MifareClassicDefaultSectorTrailer;

				sectorModel.SectorNumber = selectedClassicSectorCurrentAsInt;

				childNodeViewModelFromChip = new RFiDChipChildLayerViewModel(sectorModel, null, CARD_TYPE.Mifare4K, null, true);
				childNodeViewModelTemp = new RFiDChipChildLayerViewModel(sectorModel, null, CARD_TYPE.Mifare4K, null, true);

				Selected_DataBlockType = SectorTrailer_DataBlock.BlockAll;
				Selected_Sector_AccessCondition = sectorTrailer_AccessBits[4];
				Selected_DataBlock_AccessCondition = dataBlock_AccessBits[0];

				SelectedClassicSectorCurrent = "0";
				SelectedMADSector = "1";
				SelectedMADVersion = "1";

				MifareClassicKeys = CustomConverter.GenerateStringSequence(0,39).ToArray();
				MADVersions = CustomConverter.GenerateStringSequence(1,3).ToArray();
				MADSectors = CustomConverter.GenerateStringSequence(1,39).ToArray();
				AppNumber = "1";

				ClassicMADKeyAKeyCurrent = "FFFFFFFFFFFF";
				ClassicMADKeyBKeyCurrent = "FFFFFFFFFFFF";
				ClassicMADKeyAKeyTarget = "FFFFFFFFFFFF";
				ClassicMADKeyBKeyTarget = "FFFFFFFFFFFF";

				ClassicKeyAKeyCurrent = settings.DefaultSpecification.MifareClassicDefaultSecuritySettings[0].AccessBits.Split(new[] { ',', ';' })[0];
				ClassicKeyBKeyCurrent = settings.DefaultSpecification.MifareClassicDefaultSecuritySettings[0].AccessBits.Split(new[] { ',', ';' })[2];

				ClassicKeyAKeyTarget = settings.DefaultSpecification.MifareClassicDefaultSecuritySettings[0].AccessBits.Split(new[] { ',', ';' })[0];
				ClassicKeyBKeyTarget = settings.DefaultSpecification.MifareClassicDefaultSecuritySettings[0].AccessBits.Split(new[] { ',', ';' })[2];

				if (_selectedSetupViewModel is MifareClassicSetupViewModel)
				{
					PropertyInfo[] properties = typeof(MifareClassicSetupViewModel).GetProperties(BindingFlags.Public | BindingFlags.Instance);

					foreach (PropertyInfo p in properties)
					{
						// If not writable then cannot null it; if not readable then cannot check it's value
						if (!p.CanWrite || !p.CanRead) { continue; }

						MethodInfo mget = p.GetGetMethod(false);
						MethodInfo mset = p.GetSetMethod(false);

						// Get and set methods have to be public
						if (mget == null) { continue; }
						if (mset == null) { continue; }

						p.SetValue(this, p.GetValue(_selectedSetupViewModel));
					}
				}
				
				else
				{
					DataBlockIsCombinedToggleButtonIsChecked = true;
					
					IsValidSectorTrailer = true;

					ClassicKeyAKeyCurrent = settings.DefaultSpecification.MifareClassicDefaultSecuritySettings[0].AccessBits.Split(new[] { ',', ';' })[0];
					ClassicKeyBKeyCurrent = settings.DefaultSpecification.MifareClassicDefaultSecuritySettings[0].AccessBits.Split(new[] { ',', ';' })[2];

					ClassicKeyAKeyTarget = settings.DefaultSpecification.MifareClassicDefaultSecuritySettings[0].AccessBits.Split(new[] { ',', ';' })[0];
					ClassicKeyBKeyTarget = settings.DefaultSpecification.MifareClassicDefaultSecuritySettings[0].AccessBits.Split(new[] { ',', ';' })[2];
					
					IsWriteFromMemoryToChipChecked = true;

					SelectedTaskIndex = "0";
					SelectedTaskDescription = "Enter a Description";
				}
				
				HasPlugins = items != null ? items.Any() : false;
				
				if (HasPlugins)
					SelectedPlugin = Items.FirstOrDefault();
				
				useMAD = false;
				
			}
			catch (Exception e)
			{
				LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
			}
			
		}

		#endregion
		
		#region AccessBitsTab

		/// <summary>
		///
		/// </summary>
		public SectorTrailer_DataBlock Selected_DataBlockType
		{
			get { return selected_DataBlockType; }
			set
			{
				selected_DataBlockType = value;
				RaisePropertyChanged("Selected_DataBlock_AccessCondition");
				RaisePropertyChanged("Selected_DataBlockType");
			}
		} private SectorTrailer_DataBlock selected_DataBlockType;

		/// <summary>
		///
		/// </summary>
		public bool IsValidSelectedAccessBitsTaskIndex
		{
			get
			{
				return isValidSelectedAccessBitsTaskIndex;
			}
			set
			{
				isValidSelectedAccessBitsTaskIndex = value;
				RaisePropertyChanged("IsValidSelectedAccessBitsTaskIndex");
			}
		} private bool isValidSelectedAccessBitsTaskIndex;

		/// <summary>
		///
		/// </summary>
		public bool IsAccessBitsEditTabEnabled
		{
			get { return isAccessBitsEditTabEnabled; }
			set
			{
				isAccessBitsEditTabEnabled = value;
				RaisePropertyChanged("IsAccessBitsEditTabEnabled");
			}
		} private bool isAccessBitsEditTabEnabled;

		/// <summary>
		///
		/// </summary>
		[XmlIgnore]
		public ObservableCollection<MifareClassicSectorAccessConditionModel> SectorTrailerSource
		{
			get { return sectorTrailer_AccessBits; }
		}

		/// <summary>
		///
		/// </summary>
		[XmlIgnore]
		public ObservableCollection<MifareClassicDataBlockAccessConditionModel> DataBlockSource
		{
			get
			{
				return dataBlock_AccessBits;
			}
		}

		/// <summary>
		///
		/// </summary>
		public MifareClassicDataBlockAccessConditionModel Selected_DataBlock_AccessCondition
		{
			get
			{
				switch (Selected_DataBlockType)
				{
					case SectorTrailer_DataBlock.Block0:
						return sectorModel.DataBlock0.DataBlockAccessCondition;

					case SectorTrailer_DataBlock.Block1:
						return sectorModel.DataBlock1.DataBlockAccessCondition;

					case SectorTrailer_DataBlock.Block2:
						return sectorModel.DataBlock2.DataBlockAccessCondition;

					default:
						return sectorModel.DataBlockCombined.DataBlockAccessCondition;
				}
			}

			set
			{
				switch (Selected_DataBlockType)
				{
					case SectorTrailer_DataBlock.Block0:
						sectorModel.DataBlock0.DataBlockAccessCondition = value;
						break;

					case SectorTrailer_DataBlock.Block1:
						sectorModel.DataBlock1.DataBlockAccessCondition = value;
						break;

					case SectorTrailer_DataBlock.Block2:
						sectorModel.DataBlock2.DataBlockAccessCondition = value;
						break;

					default:
						sectorModel.DataBlockCombined.DataBlockAccessCondition = value;
						sectorModel.DataBlock0.DataBlockAccessCondition = value;
						sectorModel.DataBlock1.DataBlockAccessCondition = value;
						sectorModel.DataBlock2.DataBlockAccessCondition = value;
						break;
				}

				RaisePropertyChanged("Selected_DataBlock_AccessCondition");
				RaisePropertyChanged("SectorTrailer");
			}
		}

		/// <summary>
		///
		/// </summary>
		public MifareClassicSectorAccessConditionModel Selected_Sector_AccessCondition
		{
			get
			{
				return selected_Sector_AccessCondition;
			}
			set
			{
				selected_Sector_AccessCondition = value;

				sectorModel.SectorAccessCondition = selected_Sector_AccessCondition;

				RaisePropertyChanged("Selected_Sector_AccessCondition");
				RaisePropertyChanged("SectorTrailer");
			}
		} private MifareClassicSectorAccessConditionModel selected_Sector_AccessCondition;

		/// <summary>
		///
		/// </summary>
		public string SectorTrailer
		{
			get
			{
				return sectorModel.AccessBitsAsString;
			}
			set
			{
				sectorModel.AccessBitsAsString = value.ToUpper();
				//IsValidSectorTrailer = !decodeSectorTrailer(sectorModel.AccessBitsAsString, ref sectorModel);
				//if (!IsValidSectorTrailer)
				//	return;
				
				IsValidSectorTrailer = sectorModel.IsValidSectorTrailer;
				RaisePropertyChanged("Selected_Sector_AccessCondition");
				RaisePropertyChanged("Selected_DataBlock_AccessCondition");
			}
		}
		
		/// <summary>
		///
		/// </summary>
		public bool IsValidSectorTrailer
		{
			get { return isValidSectorTrailer; }
			set
			{
				isValidSectorTrailer = value;
				RaisePropertyChanged("IsValidSectorTrailer");
			}
		} private bool isValidSectorTrailer;

		#endregion AccessBitsTab

		#region DataExplorer

		/// <summary>
		///
		/// </summary>
		public DataExplorer_DataBlock SelectedDataBlockToReadWrite
		{
			get { return selectedDataBlockToReadWrite; }
			set
			{
				selectedDataBlockToReadWrite = value;
				foreach (RFiDChipGrandChildLayerViewModel gCNVM in ChildNodeViewModelTemp.Children)
					gCNVM.IsFocused = false;

				ChildNodeViewModelTemp.Children.First(x => x.DataBlockNumber == (int)SelectedDataBlockToReadWrite).IsFocused = true;

				RaisePropertyChanged("SelectedDataBlockToReadWrite");
			}
		}
		private DataExplorer_DataBlock selectedDataBlockToReadWrite;

		/// <summary>
		///
		/// </summary>
		public bool IsWriteFromMemoryToChipChecked
		{
			get { return isWriteFromMemoryToChipChecked; }
			set
			{
				isWriteFromMemoryToChipChecked = value;
				RaisePropertyChanged("IsWriteFromMemoryToChipChecked");
			}
		}
		private bool isWriteFromMemoryToChipChecked;

		/// <summary>
		///
		/// </summary>
		public bool IsWriteFromFileToChipChecked
		{
			get { return isWriteFromFileToChipChecked; }
			set
			{
				isWriteFromFileToChipChecked = value;
				RaisePropertyChanged("IsWriteFromFileToChipChecked");
			}
		}
		private bool isWriteFromFileToChipChecked;

		/// <summary>
		///
		/// </summary>
		public bool IsDataExplorerEditTabEnabled
		{
			get { return isDataExplorerEditTabEnabled; }
			set
			{
				isDataExplorerEditTabEnabled = value;
				RaisePropertyChanged("IsDataExplorerEditTabEnabled");
			}
		}
		private bool isDataExplorerEditTabEnabled;

		/// <summary>
		///
		/// </summary>
		public RFiDChipChildLayerViewModel ChildNodeViewModelFromChip
		{
			get { return childNodeViewModelFromChip; }
			set { childNodeViewModelFromChip = value; }
		}
		private RFiDChipChildLayerViewModel childNodeViewModelFromChip;

		/// <summary>
		///
		/// </summary>
		public RFiDChipChildLayerViewModel ChildNodeViewModelTemp
		{
			get { return childNodeViewModelTemp; }
			set { childNodeViewModelTemp = value; }
		}
		private RFiDChipChildLayerViewModel childNodeViewModelTemp;

		#endregion DataExplorer

		#region Plugins

		/// <summary>
		/// True if Count of Imported Views is > 0; See constructor
		/// </summary>
		[XmlIgnore]
		public bool HasPlugins
		{
			get {
				return hasPlugins;
			}
			set
			{
				hasPlugins = value;
				RaisePropertyChanged("HasPlugins");
			}
		} private bool hasPlugins;
		
		/// <summary>
		/// Selected Plugin in ComboBox
		/// </summary>
		[XmlIgnore]
		public object SelectedPlugin
		{
			get {return selectedPlugin;}
			set {
				selectedPlugin = value;
				RaisePropertyChanged("SelectedPlugin");
			}
		} private object selectedPlugin;
		
		/// <summary>
		/// Imported Views by URI
		/// </summary>
		[XmlIgnore]
		[ImportMany()]
		public Lazy<IUIExtension, IUIExtensionDetails>[] Items
		{
			get
			{
				return items;
			}
			
			set
			{
				items = (from g in value
				         orderby g.Metadata.SortOrder, g.Metadata.Name
				         select g).ToArray();
				
				RaisePropertyChanged("Items");
			}
		} private Lazy<IUIExtension, IUIExtensionDetails>[] items;
		#endregion

		#region Visual Properties

		/// <summary>
		///
		/// </summary>
		public bool IsFocused
		{
			get
			{
				return isFocused;
			}
			set
			{
				isFocused = value;
			}
		}
		private bool isFocused;

        #endregion

        #region Dependency Properties

        /// <summary>
        /// The Indexnumber of the ExecuteCondition Task As String
        /// </summary>
        public string SelectedExecuteConditionTaskIndex
        {
            get
            {
                return selectedExecuteConditionTaskIndex;
            }

            set
            {
                selectedExecuteConditionTaskIndex = value;
                IsValidSelectedExecuteConditionTaskIndex = int.TryParse(value, out selectedExecuteConditionTaskIndexAsInt);
                RaisePropertyChanged("SelectedExecuteConditionTaskIndex");
            }
        }
        private string selectedExecuteConditionTaskIndex;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsValidSelectedExecuteConditionTaskIndex
        {
            get
            {
                return isValidSelectedExecuteConditionTaskIndex;
            }
            set
            {
                isValidSelectedExecuteConditionTaskIndex = value;
                RaisePropertyChanged("IsValidSelectedExecuteConditionTaskIndex");
            }
        }
        private bool? isValidSelectedExecuteConditionTaskIndex;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public int SelectedExecuteConditionTaskIndexAsInt
        { get { return selectedExecuteConditionTaskIndexAsInt; } }
        private int selectedExecuteConditionTaskIndexAsInt;

        /// <summary>
        /// 
        /// </summary>
        public ERROR SelectedExecuteConditionErrorLevel
        {
            get
            {
                return selectedExecuteConditionErrorLevel;
            }

            set
            {
                selectedExecuteConditionErrorLevel = value;
                RaisePropertyChanged("SelectedExecuteConditionErrorLevel");
            }
        }
        private ERROR selectedExecuteConditionErrorLevel;

        /// <summary>
        /// 
        /// </summary>
        [XmlIgnore]
		public ERROR TaskErr { get; set; }

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsTaskCompletedSuccessfully
		{
			get { return isTaskCompletedSuccessfully; }
			set
			{
				isTaskCompletedSuccessfully = value;
				RaisePropertyChanged("IsTaskCompletedSuccessfully");
			}
		}
		private bool? isTaskCompletedSuccessfully;

		/// <summary>
		///
		/// </summary>
		[XmlIgnore]
		public string StatusText
		{
			get { return statusText; }
			set
			{
				statusText = value;
				RaisePropertyChanged("StatusText");
			}
		}
		private string statusText;

		/// <summary>
		///
		/// </summary>
		public string SelectedTaskIndex
		{
			get
			{
				//classicKeyAKeyCurrent = SectorTrailer.Split(',')[0];
				return selectedAccessBitsTaskIndex;
			}
			set
			{
				selectedAccessBitsTaskIndex = value;
				IsValidSelectedTaskIndex = int.TryParse(value, out selectedTaskIndexAsInt);
			}
		}
		private string selectedAccessBitsTaskIndex;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public int SelectedTaskIndexAsInt
		{ get { return selectedTaskIndexAsInt; } }
		private int selectedTaskIndexAsInt;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsValidSelectedTaskIndex
		{
			get
			{
				return isValidSelectedTaskIndex;
			}
			set
			{
				isValidSelectedTaskIndex = value;
				RaisePropertyChanged("IsValidSelectedTaskIndex");
			}
		}
		private bool? isValidSelectedTaskIndex;

		/// <summary>
		///
		/// </summary>
		public TaskType_MifareClassicTask SelectedTaskType
		{
			get
			{
				//classicKeyAKeyCurrent = SectorTrailer.Split(',')[0];
				return selectedAccessBitsTaskType;
			}
			set
			{
				selectedAccessBitsTaskType = value;
				switch (value)
				{
					case TaskType_MifareClassicTask.None:
						IsClassicKeyEditingEnabled = false;
						IsClassicAuthInfoEnabled = true;
						IsAccessBitsEditTabEnabled = true;
						break;

					case TaskType_MifareClassicTask.ReadData:
						IsClassicKeyEditingEnabled = false;
						IsClassicAuthInfoEnabled = true;
						IsAccessBitsEditTabEnabled = true;
						IsDataExplorerEditTabEnabled = true;
						break;

					case TaskType_MifareClassicTask.WriteData:
						IsClassicKeyEditingEnabled = false;
						IsClassicAuthInfoEnabled = true;
						IsAccessBitsEditTabEnabled = true;
						IsDataExplorerEditTabEnabled = true;
						break;

					case TaskType_MifareClassicTask.ChangeDefault:
						IsClassicKeyEditingEnabled = false;
						IsClassicAuthInfoEnabled = true;
						IsAccessBitsEditTabEnabled = false;
						break;
				}
				RaisePropertyChanged("SelectedTaskType");
			}
		}
		private TaskType_MifareClassicTask selectedAccessBitsTaskType;

		/// <summary>
		///
		/// </summary>
		public string SelectedTaskDescription
		{
			get
			{
				return selectedAccessBitsTaskDescription;
			}
			set
			{
				selectedAccessBitsTaskDescription = value;
				RaisePropertyChanged("SelectedTaskDescription");
			}
		}
		private string selectedAccessBitsTaskDescription;

		/// <summary>
		/// 
		/// </summary>
		[XmlIgnore]
		public SettingsReaderWriter Settings
		{
			get { return settings; }
		}

		/// <summary>
		/// 
		/// </summary>
		public bool DataBlockSelectionComboBoxIsEnabled
		{
			get { return !dataBlockIsCombinedToggleButtonIsChecked; }
		}

        #region KeySetup

        [XmlIgnore]
        public string[] MifareClassicKeys { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public bool UseMAD
        {
            get { return useMAD; }
            set
            {
                useMAD = value;
                if (UseMAD)
                {
                    ChildNodeViewModelTemp.Children.Clear();
                    ChildNodeViewModelFromChip.Children.Clear();

                    ChildNodeViewModelFromChip.Children.Add(new RFiDChipGrandChildLayerViewModel(new MifareClassicMADModel(0, 1), this));

                    ChildNodeViewModelTemp.Children.Add(new RFiDChipGrandChildLayerViewModel(new MifareClassicMADModel(0, 1), this));

                    FileSize = "100";
                }
                else
                {
                    ChildNodeViewModelTemp.Children.Clear();
                    ChildNodeViewModelFromChip.Children.Clear();

                    sectorModel = new MifareClassicSectorModel(4,
                                                               AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                               AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
                                                               AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
                                                               AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
                                                               AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
                                                               AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA);

                    sectorModel.DataBlock.Add(new MifareClassicDataBlockModel(0, SectorTrailer_DataBlock.Block0, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB));
                    sectorModel.DataBlock.Add(new MifareClassicDataBlockModel(0, SectorTrailer_DataBlock.Block1, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB));
                    sectorModel.DataBlock.Add(new MifareClassicDataBlockModel(0, SectorTrailer_DataBlock.Block2, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB));
                    sectorModel.DataBlock.Add(new MifareClassicDataBlockModel(0, SectorTrailer_DataBlock.BlockAll, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB));

                    childNodeViewModelFromChip = new RFiDChipChildLayerViewModel(sectorModel, null, CARD_TYPE.Mifare4K, null, true);
                    childNodeViewModelTemp = new RFiDChipChildLayerViewModel(sectorModel, null, CARD_TYPE.Mifare4K, null, true);

                    RaisePropertyChanged("ChildNodeViewModelFromChip");
                    RaisePropertyChanged("ChildNodeViewModelTemp");
                }

                RaisePropertyChanged("UseMAD");
                RaisePropertyChanged("UseMADInvert");
            }
        }
        private bool useMAD;

        /// <summary>
        /// 
        /// </summary>
        public bool UseMADInvert
        {
            get { return !UseMAD; }
        }

        /// <summary>
        ///
        /// </summary>
        public bool IsClassicAuthInfoEnabled
        {
            get { return isClassicAuthInfoEnabled; }
            set
            {
                isClassicAuthInfoEnabled = value;
                RaisePropertyChanged("IsClassicAuthInfoEnabled");
            }
        }
        private bool isClassicAuthInfoEnabled = false;

        /// <summary>
        ///
        /// </summary>
        public bool IsClassicKeyEditingEnabled
        {
            get { return isClassicKeyEditingEnabled; }
            set
            {
                isClassicKeyEditingEnabled = value;
                RaisePropertyChanged("IsClassicKeyEditingEnabled");
            }
        }
        private bool isClassicKeyEditingEnabled;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool IsValidSelectedKeySetupTaskIndex
        {
            get
            {
                //classicKeyAKeyCurrent = SectorTrailer.Split(',')[0];
                return isValidSelectedKeySetupTaskIndex;
            }
            set
            {
                isValidSelectedKeySetupTaskIndex = value;
                RaisePropertyChanged("IsValidSelectedKeySetupTaskIndex");
            }
        }
        private bool isValidSelectedKeySetupTaskIndex;

        /// <summary>
        ///
        /// </summary>
        public string ClassicKeyAKeyCurrent
        {
            get
            {
                return classicKeyAKeyCurrent;
            }
            set
            {
                classicKeyAKeyCurrent = value.Length > 12 ? value.ToUpper().Remove(12, value.Length - 12) : value.ToUpper();

                IsValidClassicKeyAKeyCurrent = (CustomConverter.IsInHexFormat(classicKeyAKeyCurrent) && classicKeyAKeyCurrent.Length == 12);

                if (IsValidClassicKeyAKeyCurrent != false && SelectedTaskType == TaskType_MifareClassicTask.ChangeDefault)
                {
                    string currentSectorTrailer = settings.DefaultSpecification.MifareClassicDefaultSecuritySettings[selectedClassicKeyANumberCurrentAsInt].AccessBits;
                    currentSectorTrailer = string.Join(",", new[]
                                                       {
                                                           classicKeyAKeyCurrent,
                                                           currentSectorTrailer.Split(new[] {',',';'})[1],
                                                           currentSectorTrailer.Split(new[] {',',';'})[2]
                                                       });

                    settings.DefaultSpecification.MifareClassicDefaultSecuritySettings[selectedClassicKeyANumberCurrentAsInt] = new MifareClassicDefaultKeys(selectedClassicKeyANumberCurrentAsInt, currentSectorTrailer);
                }
                else if (IsValidClassicKeyAKeyCurrent != false)
                    sectorModel.KeyA = classicKeyAKeyCurrent;

                RaisePropertyChanged("ClassicKeyAKeyCurrent");
            }
        }
        private string classicKeyAKeyCurrent;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsValidClassicKeyAKeyCurrent
        {
            get
            {
                return isValidClassicKeyAKeyCurrent;
            }
            set
            {
                isValidClassicKeyAKeyCurrent = value;
                RaisePropertyChanged("IsValidClassicKeyAKeyCurrent");
            }
        }
        private bool? isValidClassicKeyAKeyCurrent;

        /// <summary>
        ///
        /// </summary>
        public string ClassicKeyBKeyCurrent
        {
            get
            {
                return classicKeyBKeyCurrent;
            }
            set
            {
                classicKeyBKeyCurrent = value.Length > 12 ? value.ToUpper().Remove(12, value.Length - 12) : value.ToUpper();

                IsValidClassicKeyBKeyCurrent = (CustomConverter.IsInHexFormat(classicKeyBKeyCurrent) && classicKeyBKeyCurrent.Length == 12);
                if (IsValidClassicKeyBKeyCurrent != false && SelectedTaskType == TaskType_MifareClassicTask.ChangeDefault)
                {
                    string currentSectorTrailer = settings.DefaultSpecification.MifareClassicDefaultSecuritySettings[selectedClassicKeyBNumberCurrentAsInt].AccessBits;
                    currentSectorTrailer = string.Join(",", new[]
                                                       {
                                                           currentSectorTrailer.Split(new[] {',',';'})[0],
                                                           currentSectorTrailer.Split(new[] {',',';'})[1],
                                                           classicKeyBKeyCurrent
                                                       });

                    settings.DefaultSpecification.MifareClassicDefaultSecuritySettings[selectedClassicKeyBNumberCurrentAsInt] = new MifareClassicDefaultKeys(selectedClassicKeyBNumberCurrentAsInt, currentSectorTrailer);
                }
                else if (IsValidClassicKeyBKeyCurrent != false)
                    sectorModel.KeyB = classicKeyBKeyCurrent;

                RaisePropertyChanged("ClassicKeyBKeyCurrent");
            }
        }
        private string classicKeyBKeyCurrent;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsValidClassicKeyBKeyCurrent
        {
            get
            {
                return isValidClassicKeyBKeyCurrent;
            }
            set
            {
                isValidClassicKeyBKeyCurrent = value;
                RaisePropertyChanged("IsValidClassicKeyBKeyCurrent");
            }
        }
        private bool? isValidClassicKeyBKeyCurrent;

        /// <summary>
        ///
        /// </summary>
        public string SelectedClassicKeyANumberCurrent
        {
            get
            {
                return selectedClassicKeyANumberCurrent;
            }
            set
            {
                if (int.TryParse(value, out selectedClassicKeyANumberCurrentAsInt))
                {
                    selectedClassicKeyANumberCurrent = value;
                    RaisePropertyChanged("SelectedClassicKeyANumberCurrent");
                }
            }
        }
        private string selectedClassicKeyANumberCurrent;
        private int selectedClassicKeyANumberCurrentAsInt;

        /// <summary>
        ///
        /// </summary>
        public string SelectedClassicKeyBNumberCurrent
        {
            get
            {
                return selectedClassicKeyBNumberCurrent;
            }
            set
            {
                if (int.TryParse(value, out selectedClassicKeyBNumberCurrentAsInt))
                {
                    selectedClassicKeyBNumberCurrent = value;
                    RaisePropertyChanged("SelectedClassicKeyBNumberCurrent");
                }

                //				ClassicKeyBKeyCurrent = settings.DefaultSpecification.MifareClassicDefaultSecuritySettings.
                //					First(x => x.KeyType == SelectedClassicKeyBNumberCurrent).AccessBits.Split(new[] { ',', ';' })[2];

            }
        }
        private string selectedClassicKeyBNumberCurrent;
        private int selectedClassicKeyBNumberCurrentAsInt;

        /// <summary>
        ///
        /// </summary>
        public string SelectedClassicSectorCurrent
        {
            get
            {
                return selectedClassicSectorCurrent;
            }
            set
            {
                if (int.TryParse(value, out selectedClassicSectorCurrentAsInt))
                {
                    selectedClassicSectorCurrent = value;

					if (selectedClassicSectorCurrentAsInt > 31)
					{
						sectorModel = new MifareClassicSectorModel(4,
							AccessCondition_MifareClassicSectorTrailer.NotAllowed,
							AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
							AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
							AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
							AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
							AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA);

						sectorModel.DataBlock.Add(new MifareClassicDataBlockModel(0, SectorTrailer_DataBlock.Block0, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB));
						sectorModel.DataBlock.Add(new MifareClassicDataBlockModel(0, SectorTrailer_DataBlock.Block1, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB));
						sectorModel.DataBlock.Add(new MifareClassicDataBlockModel(0, SectorTrailer_DataBlock.Block2, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB));

						sectorModel.DataBlock.Add(new MifareClassicDataBlockModel(0, SectorTrailer_DataBlock.Block0, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB));
						sectorModel.DataBlock.Add(new MifareClassicDataBlockModel(0, SectorTrailer_DataBlock.Block1, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB));
						sectorModel.DataBlock.Add(new MifareClassicDataBlockModel(0, SectorTrailer_DataBlock.Block2, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB));

						sectorModel.DataBlock.Add(new MifareClassicDataBlockModel(0, SectorTrailer_DataBlock.Block0, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB));
						sectorModel.DataBlock.Add(new MifareClassicDataBlockModel(0, SectorTrailer_DataBlock.Block1, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB));
						sectorModel.DataBlock.Add(new MifareClassicDataBlockModel(0, SectorTrailer_DataBlock.Block2, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB));

						sectorModel.DataBlock.Add(new MifareClassicDataBlockModel(0, SectorTrailer_DataBlock.Block0, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB));
						sectorModel.DataBlock.Add(new MifareClassicDataBlockModel(0, SectorTrailer_DataBlock.Block1, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB));
						sectorModel.DataBlock.Add(new MifareClassicDataBlockModel(0, SectorTrailer_DataBlock.Block2, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB));

						sectorModel.DataBlock.Add(new MifareClassicDataBlockModel(0, SectorTrailer_DataBlock.Block0, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB));
						sectorModel.DataBlock.Add(new MifareClassicDataBlockModel(0, SectorTrailer_DataBlock.Block1, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB));
						sectorModel.DataBlock.Add(new MifareClassicDataBlockModel(0, SectorTrailer_DataBlock.Block2, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB));

						sectorModel.DataBlock.Add(new MifareClassicDataBlockModel(0, SectorTrailer_DataBlock.BlockAll, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB, AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB));

						sectorModel.AccessBitsAsString = settings.DefaultSpecification.MifareClassicDefaultSectorTrailer;

						sectorModel.SectorNumber = selectedClassicSectorCurrentAsInt;

						childNodeViewModelFromChip = new RFiDChipChildLayerViewModel(sectorModel, null, CARD_TYPE.Mifare4K, null, true);
						childNodeViewModelTemp = new RFiDChipChildLayerViewModel(sectorModel, null, CARD_TYPE.Mifare4K, null, true);

					}

					RaisePropertyChanged("SelectedClassicSectorCurrent");
                }
            }
        }
        private string selectedClassicSectorCurrent;
        private int selectedClassicSectorCurrentAsInt;

        public bool DataBlockIsCombinedToggleButtonIsChecked
        {
            get { return dataBlockIsCombinedToggleButtonIsChecked; }
            set
            {
                dataBlockIsCombinedToggleButtonIsChecked = value;

                if (value)
                    Selected_DataBlockType = SectorTrailer_DataBlock.BlockAll;

                RaisePropertyChanged("SelectedDataBlockItem");
                RaisePropertyChanged("DataBlockIsCombinedToggleButtonIsChecked");
                RaisePropertyChanged("DataBlockSelectionComboBoxIsEnabled");
                RaisePropertyChanged("DataBlockSelection");
            }
        }
        private bool dataBlockIsCombinedToggleButtonIsChecked;

        #endregion KeySetup

        #region MADEditor

        [XmlIgnore]
        public string[] MADVersions { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string SelectedMADVersion
        {
            get
            {
                return selectedMADVersion;
            }
            set
            {
                if (byte.TryParse(value, out selectedMADVersionAsByte))
                {
                    selectedMADVersion = value;

                    madGPB = (byte)((madGPB &= 0xFC) | selectedMADVersionAsByte);
                }

                RaisePropertyChanged("SelectedMADVersion");
            }

        }
        private string selectedMADVersion;
        private byte selectedMADVersionAsByte;

        [XmlIgnore]
        public string[] MADSectors { get; set; }

        /// <summary>
        /// Change MAD GPB
        /// </summary>
        public bool IsMultiApplication
        {
            get { return isMultiApplication; }
            set
            {
                isMultiApplication = value;
                if (value)
                    madGPB |= 0x40;
                else
                    madGPB &= 0xBF;
                RaisePropertyChanged("IsMultiApplication");
            }
        }
        private bool isMultiApplication;

        /// <summary>
        /// Do authenticate to MAD or not before performing a write operation?
        /// </summary>
        public bool UseMadAuth
        {
            get
            {
                return useMADAuth;
            }
            set
            {
                useMADAuth = value;
                RaisePropertyChanged("UseMadAuth");
            }
        }
        private bool useMADAuth;

        /// <summary>
        ///
        /// </summary>
        public string FileSize
        {
            get { return fileSize; }
            set
            {
                fileSize = value;
                IsValidFileSize = (int.TryParse(value, out fileSizeAsInt) && fileSizeAsInt <= 4200);

                if (IsValidFileSize != false)
                {
                    if (childNodeViewModelFromChip.Children.Any(x => x.MifareClassicMAD != null))
                    {
                        try
                        {
                            childNodeViewModelFromChip.Children.Single().MifareClassicMAD = new MifareClassicMADModel(new byte[fileSizeAsInt], appNumberAsInt);
                            childNodeViewModelTemp.Children.Single().MifareClassicMAD = new MifareClassicMADModel(new byte[fileSizeAsInt], appNumberAsInt);

                            childNodeViewModelTemp.Children.Single().RequestRefresh();
                            childNodeViewModelFromChip.Children.Single().RequestRefresh();
                        }
                        catch
                        {

                        }

                    }
                    else
                    {
                        childNodeViewModelFromChip.Children.Add(new RFiDChipGrandChildLayerViewModel(new MifareClassicMADModel(new byte[fileSizeAsInt], appNumberAsInt), null));
                        childNodeViewModelTemp.Children.Add(new RFiDChipGrandChildLayerViewModel(new MifareClassicMADModel(new byte[fileSizeAsInt], appNumberAsInt), null));
                    }
                }

                RaisePropertyChanged("FileSize");
            }
        }
        private string fileSize;
        private int fileSizeAsInt;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsValidFileSize
        {
            get
            {
                return isValidFileSize;
            }
            set
            {
                isValidFileSize = value;
                RaisePropertyChanged("IsValidFileSize");
            }
        }
        private bool? isValidFileSize;

        /// <summary>
        ///
        /// </summary>
        public string AppNumber
        {
            get { return appNumber; }
            set
            {
                appNumber = value;
                IsValidAppNumber = (int.TryParse(value, out appNumberAsInt) && appNumberAsInt <= 0xFFFF);
                RaisePropertyChanged("AppNumberNew");
            }
        }
        private string appNumber;
        private int appNumberAsInt;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsValidAppNumber
        {
            get
            {
                return isValidAppNumber;
            }
            set
            {
                isValidAppNumber = value;
                RaisePropertyChanged("IsValidAppNumber");
            }
        }
        private bool? isValidAppNumber;

        /// <summary>
        /// 
        /// </summary>
        public string SelectedMADSector
        {
            get { return selectedMADSector; }

            set
            {
                if (int.TryParse(value, out selectedMADSectorAsInt))
                    selectedMADSector = value;
                RaisePropertyChanged("SelectedMADSector");
            }
        }
        private string selectedMADSector;
        private int selectedMADSectorAsInt;

        /// <summary>
        ///
        /// </summary>
        public string ClassicMADKeyAKeyCurrent
        {
            get
            {
                return classicMADKeyAKeyCurrent;
            }
            set
            {
                classicMADKeyAKeyCurrent = value.Length > 12 ? value.ToUpper().Remove(12, value.Length - 12) : value.ToUpper();

                IsValidClassicMADKeyAKeyCurrent = (CustomConverter.IsInHexFormat(classicMADKeyAKeyCurrent) && classicMADKeyAKeyCurrent.Length == 12);

                RaisePropertyChanged("ClassicMADKeyAKeyCurrent");
            }
        }
        private string classicMADKeyAKeyCurrent;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsValidClassicMADKeyAKeyCurrent
        {
            get
            {
                return isValidClassicMADKeyAKeyCurrent;
            }
            set
            {
                isValidClassicMADKeyAKeyCurrent = value;
                RaisePropertyChanged("IsValidClassicMADKeyAKeyCurrent");
            }
        }
        private bool? isValidClassicMADKeyAKeyCurrent;

        /// <summary>
        ///
        /// </summary>
        public string ClassicMADKeyBKeyCurrent
        {
            get
            {
                return classicMADKeyBKeyCurrent;
            }
            set
            {
                classicMADKeyBKeyCurrent = value.Length > 12 ? value.ToUpper().Remove(12, value.Length - 12) : value.ToUpper();

                IsValidClassicMADKeyBKeyCurrent = (CustomConverter.IsInHexFormat(classicMADKeyBKeyCurrent) && classicMADKeyBKeyCurrent.Length == 12);

                RaisePropertyChanged("ClassicMADKeyBKeyCurrent");
            }
        }
        private string classicMADKeyBKeyCurrent;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsValidClassicMADKeyBKeyCurrent
        {
            get
            {
                return isValidClassicMADKeyBKeyCurrent;
            }
            set
            {
                isValidClassicMADKeyBKeyCurrent = value;
                RaisePropertyChanged("IsValidClassicMADKeyBKeyCurrent");
            }
        }
        private bool? isValidClassicMADKeyBKeyCurrent;

        /// <summary>
        ///
        /// </summary>
        public string ClassicMADKeyAKeyTarget
        {
            get
            {
                return classicMADKeyAKeyTarget;
            }
            set
            {
                classicMADKeyAKeyTarget = value.Length > 12 ? value.ToUpper().Remove(12, value.Length - 12) : value.ToUpper();

                IsValidClassicMADKeyAKeyTarget = (CustomConverter.IsInHexFormat(classicMADKeyAKeyTarget) && classicMADKeyAKeyTarget.Length == 12);

                RaisePropertyChanged("ClassicMADKeyAKeyTarget");
            }
        }
        private string classicMADKeyAKeyTarget;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsValidClassicMADKeyAKeyTarget
        {
            get
            {
                return isValidClassicMADKeyAKeyTarget;
            }
            set
            {
                isValidClassicMADKeyAKeyTarget = value;
                RaisePropertyChanged("IsValidClassicMADKeyAKeyTarget");
            }
        }
        private bool? isValidClassicMADKeyAKeyTarget;

        /// <summary>
        ///
        /// </summary>
        public string ClassicMADKeyBKeyTarget
        {
            get
            {
                return classicMADKeyBKeyTarget;
            }
            set
            {
                classicMADKeyBKeyTarget = value.Length > 12 ? value.ToUpper().Remove(12, value.Length - 12) : value.ToUpper();

                IsValidClassicMADKeyBKeyTarget = (CustomConverter.IsInHexFormat(classicMADKeyBKeyTarget) && classicMADKeyBKeyTarget.Length == 12);

                RaisePropertyChanged("ClassicMADKeyBKeyTarget");
            }
        }
        private string classicMADKeyBKeyTarget;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsValidClassicMADKeyBKeyTarget
        {
            get
            {
                return isValidClassicMADKeyBKeyTarget;
            }
            set
            {
                isValidClassicMADKeyBKeyTarget = value;
                RaisePropertyChanged("IsValidClassicMADKeyBKeyTarget");
            }
        }
        private bool? isValidClassicMADKeyBKeyTarget;

        /// <summary>
        ///
        /// </summary>
        public string ClassicKeyAKeyTarget
        {
            get
            {
                return classicKeyAKeyTarget;
            }
            set
            {
                classicKeyAKeyTarget = value != null ? (value.Length > 12 ? value.ToUpper().Remove(12, value.Length - 12) : value.ToUpper() ): null;

                IsValidClassicKeyAKeyTarget = (CustomConverter.IsInHexFormat(classicKeyAKeyTarget) && classicKeyAKeyTarget.Length == 12);

                RaisePropertyChanged("ClassicKeyAKeyTarget");
            }
        }
        private string classicKeyAKeyTarget;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsValidClassicKeyAKeyTarget
        {
            get
            {
                return isValidClassicKeyAKeyTarget;
            }
            set
            {
                isValidClassicKeyAKeyTarget = value;
                RaisePropertyChanged("IsValidClassicKeyAKeyTarget");
            }
        }
        private bool? isValidClassicKeyAKeyTarget;

        /// <summary>
        ///
        /// </summary>
        public string ClassicKeyBKeyTarget
        {
            get
            {
                return classicKeyBKeyTarget;
            }
            set
            {
                classicKeyBKeyTarget = value.Length > 12 ? value.ToUpper().Remove(12, value.Length - 12) : value.ToUpper();

                IsValidClassicKeyBKeyTarget = (CustomConverter.IsInHexFormat(classicKeyBKeyTarget) && classicKeyBKeyTarget.Length == 12);

                RaisePropertyChanged("ClassicKeyBKeyTarget");
            }
        }
        private string classicKeyBKeyTarget;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsValidClassicKeyBKeyTarget
        {
            get
            {
                return isValidClassicKeyBKeyTarget;
            }
            set
            {
                isValidClassicKeyBKeyTarget = value;
                RaisePropertyChanged("IsValidClassicKeyBKeyTarget");
            }
        }
        private bool? isValidClassicKeyBKeyTarget;

        #endregion

        #endregion General Properties

        #region Commands

        /// <summary>
        /// 
        /// </summary>
        public ICommand ReadDataCommand { get { return new RelayCommand(OnNewReadDataCommand); } }
		private protected void OnNewReadDataCommand()
		{
			//Mouse.OverrideCursor = Cursors.Wait;
			TaskErr = ERROR.Empty;
			
			Task classicTask =
				new Task(() =>
				         {
				         	using (ReaderDevice device = ReaderDevice.Instance)
				         	{
				         		if(device != null && device.ReadChipPublic() == ERROR.NoError)
				         		{
				         			StatusText += string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.getResource("textBoxStatusTextBoxDllLoaded"));
				         			
				         			if(!useMAD)
				         			{
				         				if (device.ReadMiFareClassicSingleSector(
				         					selectedClassicSectorCurrentAsInt,
				         					ClassicKeyAKeyCurrent,
				         					ClassicKeyBKeyCurrent) == ERROR.NoError)
				         				{
				         					
				         					childNodeViewModelFromChip.SectorNumber = selectedClassicSectorCurrentAsInt;
				         					childNodeViewModelTemp.SectorNumber = selectedClassicSectorCurrentAsInt;
				         						 
				         					StatusText += string.Format("{0}: Success for Sector: {1}\n", DateTime.Now, selectedClassicSectorCurrentAsInt);
				         					
				         					for (int i = 0; i < device.Sector.DataBlock.Count; i++)
				         					{
				         						childNodeViewModelFromChip.Children[i].DataBlockNumber = i;
				         						childNodeViewModelTemp.Children[i].DataBlockNumber = i;
				         						
				         						childNodeViewModelFromChip.Children[i].MifareClassicDataBlock.DataBlockNumberChipBased = device.Sector.DataBlock.First(x => x.DataBlockNumberSectorBased == i).DataBlockNumberChipBased;
				         						
				         						if (device.Sector.DataBlock[i].IsAuthenticated)
				         						{
				         							StatusText += string.Format("{0}: \tSuccess for Blocknumber: {1} Data: {2}\n", DateTime.Now, device.Sector.DataBlock[i].DataBlockNumberChipBased, CustomConverter.HexToString(device.Sector.DataBlock[i].Data));
				         							childNodeViewModelFromChip.Children.First(x => x.DataBlockNumber == i).MifareClassicDataBlock.Data = device.Sector.DataBlock[i].Data;
				         							childNodeViewModelFromChip.Children.First(x => x.DataBlockNumber == i).RequestRefresh();
				         							
				         							childNodeViewModelTemp.Children.First(x => x.DataBlockNumber == i).MifareClassicDataBlock.Data = device.Sector.DataBlock[i].Data;
				         							childNodeViewModelTemp.Children.First(x => x.DataBlockNumber == i).RequestRefresh();
				         							
				         							TaskErr = ERROR.NoError;
				         						}
				         						else
				         							StatusText += string.Format("{0}: \tBut: unable to authenticate to sector: {1}, DataBlock: {2} using specified Keys\n", DateTime.Now, selectedClassicSectorCurrentAsInt, device.Sector.DataBlock[i - 1].DataBlockNumberChipBased);
				         					}
				         					
				         					TaskErr = ERROR.NoError;


				         				}
				         				
				         				else
				         				{
				         					StatusText += string.Format("{0}: Unable to Authenticate to Sector: {1} using specified Keys\n", DateTime.Now, selectedClassicSectorCurrentAsInt);
				         					TaskErr = ERROR.AuthenticationError;
				         					return;
				         				}
				         			}
				         			
				         			else
				         			{
										 ChildNodeViewModelFromChip.Children.FirstOrDefault().MifareClassicMAD.MADApp = appNumberAsInt;
										 ChildNodeViewModelTemp.Children.FirstOrDefault().MifareClassicMAD.MADApp = appNumberAsInt;

										if (device.ReadMiFareClassicWithMAD(appNumberAsInt, ClassicKeyAKeyCurrent, ClassicKeyBKeyCurrent, ClassicMADKeyAKeyCurrent, ClassicMADKeyBKeyCurrent, fileSizeAsInt) == ERROR.NoError)
										{
											 ChildNodeViewModelFromChip.Children.FirstOrDefault().MifareClassicMAD.Data = device.MifareClassicData;
											 ChildNodeViewModelTemp.Children.FirstOrDefault().MifareClassicMAD.Data = device.MifareClassicData;

											 ChildNodeViewModelTemp.Children.Single().RequestRefresh();
											 ChildNodeViewModelFromChip.Children.Single().RequestRefresh();

											 StatusText = StatusText + string.Format("{0}: Successfully Read Data from MAD\n", DateTime.Now);

											 TaskErr = ERROR.NoError;
										 }

										else
				         				{
				         					StatusText = StatusText + string.Format("{0}: Unable to Authenticate to MAD Sector using specified MAD Key(s)\n", DateTime.Now);

				         					TaskErr = ERROR.AuthenticationError;
				         					return;
				         				}
				         			}


				         		}
				         		
				         		else
				         		{
				         			TaskErr = ERROR.DeviceNotReadyError;
				         			return;
				         		}

				         		RaisePropertyChanged("ChildNodeViewModelTemp");
				         		
				         		return;
				         	}
				         });

			if (TaskErr == ERROR.Empty)
			{
				classicTask.ContinueWith((x) =>
				{
					if (TaskErr == ERROR.NoError)
					{
						IsTaskCompletedSuccessfully = true;
					}
					else
					{
						IsTaskCompletedSuccessfully = false;
					}
				});

				classicTask.RunSynchronously();
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public ICommand WriteDataCommand { get { return new RelayCommand(OnNewWriteDataCommand); } }
		private protected void OnNewWriteDataCommand()
		{
			TaskErr = ERROR.Empty;

			Task classicTask =
				new Task(() =>
				         {
				         	using (ReaderDevice device = ReaderDevice.Instance)
				         	{
				         		StatusText = string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.getResource("textBoxStatusTextBoxDllLoaded"));
				         		
				         		if(!UseMAD)
				         		{
				         			if (device != null)
				         			{
				         				childNodeViewModelFromChip.SectorNumber = selectedClassicSectorCurrentAsInt;
				         				childNodeViewModelTemp.SectorNumber = selectedClassicSectorCurrentAsInt;
				         				
				         				if (device.WriteMiFareClassicSingleBlock(childNodeViewModelFromChip.Children[(int)SelectedDataBlockToReadWrite].MifareClassicDataBlock.DataBlockNumberChipBased,
				         				                                         ClassicKeyAKeyCurrent,
				         				                                         ClassicKeyBKeyCurrent,
				         				                                         childNodeViewModelTemp.Children[(int)SelectedDataBlockToReadWrite].MifareClassicDataBlock.Data) == ERROR.NoError)
				         				{
				         					StatusText = StatusText + string.Format("{0}: \tSuccess for Blocknumber: {1} Data: {2}\n",
				         					                                        DateTime.Now,
				         					                                        childNodeViewModelFromChip.Children[(int)SelectedDataBlockToReadWrite].DataBlockNumber,
				         					                                        CustomConverter.HexToString(childNodeViewModelTemp.Children[(int)SelectedDataBlockToReadWrite].MifareClassicDataBlock.Data));
				         					TaskErr = ERROR.NoError;
				         				}
				         				else
				         					TaskErr = ERROR.AuthenticationError;

				         			}
				         			else
				         			{
				         				StatusText = StatusText + string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.getResource("textBoxStatusTextBoxUnableToAuthenticate"));
				         				TaskErr = ERROR.DeviceNotReadyError;
				         			}
				         		}

				         		else
				         		{
									 ChildNodeViewModelFromChip.Children.FirstOrDefault().MifareClassicMAD.MADApp = appNumberAsInt;
									 ChildNodeViewModelTemp.Children.FirstOrDefault().MifareClassicMAD.MADApp = appNumberAsInt;

									if (device.WriteMiFareClassicWithMAD(appNumberAsInt, selectedMADSectorAsInt,
																		 ClassicMADKeyAKeyCurrent, ClassicMADKeyBKeyCurrent,
																		 ClassicMADKeyAKeyTarget, ClassicMADKeyBKeyTarget,
																		 ClassicKeyAKeyCurrent, ClassicKeyBKeyCurrent,
																		 ClassicKeyAKeyTarget, ClassicKeyBKeyTarget,
																		 ChildNodeViewModelTemp.Children.Single(x => x.MifareClassicMAD.MADApp == appNumberAsInt).MifareClassicMAD.Data,
																		 madGPB, UseMadAuth) == ERROR.NoError)
									{
										 StatusText = StatusText + string.Format("{0}: Wrote n bytes to MAD ID x\n", DateTime.Now);

										 TaskErr = ERROR.NoError;
									}

									else
				         			{
				         				StatusText = StatusText + string.Format("{0}: Unable to Authenticate to MAD Sector using specified MAD Key(s)\n", DateTime.Now);
				         				TaskErr = ERROR.AuthenticationError;
				         				return;
				         			}
				         		}
				         	}
				         });

			if (TaskErr == ERROR.Empty)
			{
				classicTask.ContinueWith((x) =>
				{
					if (TaskErr == ERROR.NoError)
					{
						IsTaskCompletedSuccessfully = true;
					}
					else
					{
						IsTaskCompletedSuccessfully = false;
					}
				});

				classicTask.RunSynchronously();
			}

			return;
		}

		#endregion Commands

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

		[XmlIgnore]
		public Action<MifareClassicSetupViewModel> OnOk { get; set; }

		[XmlIgnore]
		public Action<MifareClassicSetupViewModel> OnCancel { get; set; }

		[XmlIgnore]
		public Action<MifareClassicSetupViewModel> OnAuth { get; set; }

		[XmlIgnore]
		public Action<MifareClassicSetupViewModel> OnCloseRequest { get; set; }

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
		public string LocalizationResourceSet { get; set; }
		
		[XmlIgnore]
		public string Caption
		{
			get { return _Caption; }
			set
			{
				_Caption = value;
				RaisePropertyChanged("Caption");
			}
		} private string _Caption;

		#endregion Localization
	}
}