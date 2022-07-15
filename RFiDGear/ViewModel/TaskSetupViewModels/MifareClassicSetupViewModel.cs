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

            MifareClassicKeys = CustomConverter.GenerateStringSequence(0, 39).ToArray();

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

                MifareClassicKeys = CustomConverter.GenerateStringSequence(0, 39).ToArray();
                MADVersions = CustomConverter.GenerateStringSequence(1, 3).ToArray();
                MADSectors = CustomConverter.GenerateStringSequence(1, 39).ToArray();
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
                {
                    SelectedPlugin = Items.FirstOrDefault();
                }

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
            get => selected_DataBlockType;
            set
            {
                selected_DataBlockType = value;
                RaisePropertyChanged("Selected_DataBlock_AccessCondition");
                RaisePropertyChanged("Selected_DataBlockType");
            }
        }
        private SectorTrailer_DataBlock selected_DataBlockType;

        /// <summary>
        ///
        /// </summary>
        public bool IsValidSelectedAccessBitsTaskIndex
        {
            get => isValidSelectedAccessBitsTaskIndex;
            set
            {
                isValidSelectedAccessBitsTaskIndex = value;
                RaisePropertyChanged("IsValidSelectedAccessBitsTaskIndex");
            }
        }
        private bool isValidSelectedAccessBitsTaskIndex;

        /// <summary>
        ///
        /// </summary>
        public bool IsAccessBitsEditTabEnabled
        {
            get => isAccessBitsEditTabEnabled;
            set
            {
                isAccessBitsEditTabEnabled = value;
                RaisePropertyChanged("IsAccessBitsEditTabEnabled");
            }
        }
        private bool isAccessBitsEditTabEnabled;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public ObservableCollection<MifareClassicSectorAccessConditionModel> SectorTrailerSource => sectorTrailer_AccessBits;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public ObservableCollection<MifareClassicDataBlockAccessConditionModel> DataBlockSource => dataBlock_AccessBits;

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
            get => selected_Sector_AccessCondition;
            set
            {
                selected_Sector_AccessCondition = value;

                sectorModel.SectorAccessCondition = selected_Sector_AccessCondition;

                RaisePropertyChanged("Selected_Sector_AccessCondition");
                RaisePropertyChanged("SectorTrailer");
            }
        }
        private MifareClassicSectorAccessConditionModel selected_Sector_AccessCondition;

        /// <summary>
        ///
        /// </summary>
        public string SectorTrailer
        {
            get => sectorModel.AccessBitsAsString;
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
            get => isValidSectorTrailer;
            set
            {
                isValidSectorTrailer = value;
                RaisePropertyChanged("IsValidSectorTrailer");
            }
        }
        private bool isValidSectorTrailer;

        #endregion AccessBitsTab

        #region DataExplorer

        /// <summary>
        ///
        /// </summary>
        public DataExplorer_DataBlock SelectedDataBlockToReadWrite
        {
            get => selectedDataBlockToReadWrite;
            set
            {
                selectedDataBlockToReadWrite = value;
                foreach (RFiDChipGrandChildLayerViewModel gCNVM in ChildNodeViewModelTemp.Children)
                {
                    gCNVM.IsFocused = false;
                }

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
            get => isWriteFromMemoryToChipChecked;
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
            get => isWriteFromFileToChipChecked;
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
            get => isDataExplorerEditTabEnabled;
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
            get => childNodeViewModelFromChip;
            set => childNodeViewModelFromChip = value;
        }
        private RFiDChipChildLayerViewModel childNodeViewModelFromChip;

        /// <summary>
        ///
        /// </summary>
        public RFiDChipChildLayerViewModel ChildNodeViewModelTemp
        {
            get => childNodeViewModelTemp;
            set => childNodeViewModelTemp = value;
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
            get => hasPlugins;
            set
            {
                hasPlugins = value;
                RaisePropertyChanged("HasPlugins");
            }
        }
        private bool hasPlugins;

        /// <summary>
        /// Selected Plugin in ComboBox
        /// </summary>
        [XmlIgnore]
        public object SelectedPlugin
        {
            get => selectedPlugin;
            set
            {
                selectedPlugin = value;
                RaisePropertyChanged("SelectedPlugin");
            }
        }
        private object selectedPlugin;

        /// <summary>
        /// Imported Views by URI
        /// </summary>
        [XmlIgnore]
        [ImportMany()]
        public Lazy<IUIExtension, IUIExtensionDetails>[] Items
        {
            get => items;

            set
            {
                items = (from g in value
                         orderby g.Metadata.SortOrder, g.Metadata.Name
                         select g).ToArray();

                RaisePropertyChanged("Items");
            }
        }
        private Lazy<IUIExtension, IUIExtensionDetails>[] items;
        #endregion

        #region Visual Properties

        /// <summary>
        ///
        /// </summary>
        public bool IsFocused
        {
            get => isFocused;
            set => isFocused = value;
        }
        private bool isFocused;

        #endregion

        #region Dependency Properties

        /// <summary>
        /// The Indexnumber of the ExecuteCondition Task As String
        /// </summary>
        public string SelectedExecuteConditionTaskIndex
        {
            get => selectedExecuteConditionTaskIndex;

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
            get => isValidSelectedExecuteConditionTaskIndex;
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
        public int SelectedExecuteConditionTaskIndexAsInt => selectedExecuteConditionTaskIndexAsInt;
        private int selectedExecuteConditionTaskIndexAsInt;

        /// <summary>
        /// 
        /// </summary>
        public ERROR SelectedExecuteConditionErrorLevel
        {
            get => selectedExecuteConditionErrorLevel;

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
            get => isTaskCompletedSuccessfully;
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
            get => statusText;
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
            get =>
                //classicKeyAKeyCurrent = SectorTrailer.Split(',')[0];
                selectedAccessBitsTaskIndex;
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
        public int SelectedTaskIndexAsInt => selectedTaskIndexAsInt;
        private int selectedTaskIndexAsInt;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsValidSelectedTaskIndex
        {
            get => isValidSelectedTaskIndex;
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
            get =>
                //classicKeyAKeyCurrent = SectorTrailer.Split(',')[0];
                selectedAccessBitsTaskType;
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
            get => selectedAccessBitsTaskDescription;
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
        public SettingsReaderWriter Settings => settings;

        /// <summary>
        /// 
        /// </summary>
        public bool DataBlockSelectionComboBoxIsEnabled => !dataBlockIsCombinedToggleButtonIsChecked;

        #region KeySetup

        [XmlIgnore]
        public string[] MifareClassicKeys { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public bool UseMAD
        {
            get => useMAD;
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
        public bool UseMADInvert => !UseMAD;

        /// <summary>
        ///
        /// </summary>
        public bool IsClassicAuthInfoEnabled
        {
            get => isClassicAuthInfoEnabled;
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
            get => isClassicKeyEditingEnabled;
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
            get =>
                //classicKeyAKeyCurrent = SectorTrailer.Split(',')[0];
                isValidSelectedKeySetupTaskIndex;
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
            get => classicKeyAKeyCurrent;
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
                {
                    sectorModel.KeyA = classicKeyAKeyCurrent;
                }

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
            get => isValidClassicKeyAKeyCurrent;
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
            get => classicKeyBKeyCurrent;
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
                {
                    sectorModel.KeyB = classicKeyBKeyCurrent;
                }

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
            get => isValidClassicKeyBKeyCurrent;
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
            get => selectedClassicKeyANumberCurrent;
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
            get => selectedClassicKeyBNumberCurrent;
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
            get => selectedClassicSectorCurrent;
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
            get => dataBlockIsCombinedToggleButtonIsChecked;
            set
            {
                dataBlockIsCombinedToggleButtonIsChecked = value;

                if (value)
                {
                    Selected_DataBlockType = SectorTrailer_DataBlock.BlockAll;
                }

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
            get => selectedMADVersion;
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
            get => isMultiApplication;
            set
            {
                isMultiApplication = value;
                if (value)
                {
                    madGPB |= 0x40;
                }
                else
                {
                    madGPB &= 0xBF;
                }

                RaisePropertyChanged("IsMultiApplication");
            }
        }
        private bool isMultiApplication;

        /// <summary>
        /// Do authenticate to MAD or not before performing a write operation?
        /// </summary>
        public bool UseMadAuth
        {
            get => useMADAuth;
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
            get => fileSize;
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
            get => isValidFileSize;
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
            get => appNumber;
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
            get => isValidAppNumber;
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
            get => selectedMADSector;

            set
            {
                if (int.TryParse(value, out selectedMADSectorAsInt))
                {
                    selectedMADSector = value;
                }

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
            get => classicMADKeyAKeyCurrent;
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
            get => isValidClassicMADKeyAKeyCurrent;
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
            get => classicMADKeyBKeyCurrent;
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
            get => isValidClassicMADKeyBKeyCurrent;
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
            get => classicMADKeyAKeyTarget;
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
            get => isValidClassicMADKeyAKeyTarget;
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
            get => classicMADKeyBKeyTarget;
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
            get => isValidClassicMADKeyBKeyTarget;
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
            get => classicKeyAKeyTarget;
            set
            {
                classicKeyAKeyTarget = value != null ? (value.Length > 12 ? value.ToUpper().Remove(12, value.Length - 12) : value.ToUpper()) : null;

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
            get => isValidClassicKeyAKeyTarget;
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
            get => classicKeyBKeyTarget;
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
            get => isValidClassicKeyBKeyTarget;
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
        public ICommand ReadDataCommand => new RelayCommand(OnNewReadDataCommand);
        private protected void OnNewReadDataCommand()
        {
            //Mouse.OverrideCursor = Cursors.Wait;
            TaskErr = ERROR.Empty;

            Task classicTask =
                new Task(() =>
                         {
                             using (RFiDDevice device = RFiDDevice.Instance)
                             {
                                 if (device != null && device.ReadChipPublic() == ERROR.NoError)
                                 {
                                     StatusText += string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxDllLoaded"));

                                     if (!useMAD)
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
                                                 {
                                                     StatusText += string.Format("{0}: \tBut: unable to authenticate to sector: {1}, DataBlock: {2} using specified Keys\n", DateTime.Now, selectedClassicSectorCurrentAsInt, device.Sector.DataBlock[i - 1].DataBlockNumberChipBased);
                                                 }
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

                                         if (device.ReadMiFareClassicWithMAD(appNumberAsInt,
                                             ClassicKeyAKeyCurrent, ClassicKeyBKeyCurrent, ClassicMADKeyAKeyCurrent, ClassicMADKeyBKeyCurrent, fileSizeAsInt,
                                             UseMAD, useMADAuth) == ERROR.NoError)
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
        public ICommand WriteDataCommand => new RelayCommand(OnNewWriteDataCommand);
        private protected void OnNewWriteDataCommand()
        {
            TaskErr = ERROR.Empty;

            Task classicTask =
                new Task(() =>
                         {
                             using (RFiDDevice device = RFiDDevice.Instance)
                             {
                                 StatusText = string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxDllLoaded"));

                                 if (!UseMAD)
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
                                         {
                                             TaskErr = ERROR.AuthenticationError;
                                         }
                                     }
                                     else
                                     {
                                         StatusText = StatusText + string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxUnableToAuthenticate"));
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
                                                                          madGPB, UseMadAuth, UseMAD) == ERROR.NoError)
                                     {
                                         StatusText = StatusText + string.Format("{0}: Wrote {1} bytes to MAD ID {2}\n", DateTime.Now,
                                             ChildNodeViewModelTemp.Children.Single(x => x.MifareClassicMAD.MADApp == appNumberAsInt).MifareClassicMAD.Data.Length,
                                             ChildNodeViewModelTemp.Children.Single(x => x.MifareClassicMAD.MADApp == appNumberAsInt).MifareClassicMAD.MADApp);

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

        public ICommand OKCommand => new RelayCommand(Ok);

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

        public ICommand CancelCommand => new RelayCommand(Cancel);

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

        public ICommand AuthCommand => new RelayCommand(Auth);

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
            if (DialogClosing != null)
            {
                DialogClosing(this, new EventArgs());
            }
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
            get => _Caption;
            set
            {
                _Caption = value;
                RaisePropertyChanged("Caption");
            }
        }
        private string _Caption;

        #endregion Localization
    }
}