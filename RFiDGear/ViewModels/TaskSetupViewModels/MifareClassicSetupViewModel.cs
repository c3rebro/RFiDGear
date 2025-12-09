/*
 * Created by SharpDevelop.
 * Date: 10/11/2017
 * Time: 22:15
 *
 */

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using RFiDGear.Models;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Xml.Serialization;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Infrastructure.AccessControl;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.UI.UIExtensions.Interfaces;
using RFiDGear.UI.MVVMDialogs.ViewModels;
using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;

namespace RFiDGear.ViewModel.TaskSetupViewModels
{
    /// <summary>
    /// Description of MifareClassicSetupViewModel.
    /// </summary>
    public class MifareClassicSetupViewModel : ObservableObject, IUserDialogViewModel, IGenericTaskModel
    {
        #region Fields
        private readonly EventLog eventLog = new EventLog("Application", ".", Assembly.GetEntryAssembly().GetName().Name);

        private readonly ObservableCollection<MifareClassicDataBlockAccessConditionModel> dataBlock_AccessBits = new ObservableCollection<MifareClassicDataBlockAccessConditionModel>
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

        private readonly ObservableCollection<MifareClassicSectorAccessConditionModel> sectorTrailer_AccessBits = new ObservableCollection<MifareClassicSectorAccessConditionModel>
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

        private MifareClassicSectorModel sectorModel;

        private byte madGPB = 0xC1;

        #endregion fields

        #region Constructors

        /// <summary>
        ///
        /// </summary>
        public MifareClassicSetupViewModel()
        {
            MefHelper.Instance.Container?.ComposeParts(this); //Load Plugins

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
                SettingsReaderWriter settings = new SettingsReaderWriter();

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

                childNodeViewModelFromChip = new RFiDChipChildLayerViewModel(sectorModel, null, CARD_TYPE.Mifare4K, _dialogs, true);
                childNodeViewModelTemp = new RFiDChipChildLayerViewModel(sectorModel, null, CARD_TYPE.Mifare4K, _dialogs, true);

                Selected_DataBlockType = SectorTrailer_DataBlock.BlockAll;
                Selected_Sector_AccessCondition = sectorTrailer_AccessBits[4];
                Selected_DataBlock_AccessCondition = dataBlock_AccessBits[0];

                ClassicMADKeyAKeyCurrent = "FFFFFFFFFFFF";
                ClassicMADKeyBKeyCurrent = "FFFFFFFFFFFF";
                ClassicMADKeyAKeyTarget = "FFFFFFFFFFFF";
                ClassicMADKeyBKeyTarget = "FFFFFFFFFFFF";
                ApplicationCode = "00";
                FunctionClusterCode = "00";

                SelectedClassicSectorCurrent = "0";
                SelectedMADSector = "1";
                SelectedMADVersion = "1";

                appNumberAsInt = 0;

                ClassicKeyAKeyCurrent = settings.DefaultSpecification.MifareClassicDefaultSecuritySettings[0].AccessBits.Split(new[] { ',', ';' })[0];
                ClassicKeyBKeyCurrent = settings.DefaultSpecification.MifareClassicDefaultSecuritySettings[0].AccessBits.Split(new[] { ',', ';' })[2];

                ClassicKeyAKeyTarget = settings.DefaultSpecification.MifareClassicDefaultSecuritySettings[0].AccessBits.Split(new[] { ',', ';' })[0];
                ClassicKeyBKeyTarget = settings.DefaultSpecification.MifareClassicDefaultSecuritySettings[0].AccessBits.Split(new[] { ',', ';' })[2];

                if (_selectedSetupViewModel is MifareClassicSetupViewModel)
                {
                    var properties = typeof(MifareClassicSetupViewModel).GetProperties(BindingFlags.Public | BindingFlags.Instance);

                    foreach (var p in properties)
                    {
                        // If not writable then cannot null it; if not readable then cannot check it's value
                        if (!p.CanWrite || !p.CanRead) { continue; }

                        var mget = p.GetGetMethod(false);
                        var mset = p.GetSetMethod(false);

                        // Get and set methods have to be public
                        if (mget == null) { continue; }
                        if (mset == null) { continue; }

                        p.SetValue(this, p.GetValue(_selectedSetupViewModel));
                    }

                    MifareClassicKeys = CustomConverter.GenerateStringSequence(0, 39).ToArray();
                    MADVersions = CustomConverter.GenerateStringSequence(1, 3).ToArray();
                    MADSectors = CustomConverter.GenerateStringSequence(1, 39).ToArray();

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

                    MifareClassicKeys = CustomConverter.GenerateStringSequence(0, 39).ToArray();
                    MADVersions = CustomConverter.GenerateStringSequence(1, 3).ToArray();
                    MADSectors = CustomConverter.GenerateStringSequence(1, 39).ToArray();

                    CurrentTaskIndex = "0";
                    SelectedTaskDescription = "Enter a Description";
                }

                dialogs = _dialogs;

                MefHelper.Instance.Container?.ComposeParts(this); //Load Plugins

                HasPlugins = items != null ? items.Any() : false;

                if (HasPlugins)
                {
                    SelectedPlugin = Items.FirstOrDefault();
                }
            }
            catch (Exception e)
            {
                eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
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

                OnPropertyChanged(nameof(Selected_DataBlock_AccessCondition));
                OnPropertyChanged(nameof(Selected_DataBlockType));
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
                OnPropertyChanged(nameof(IsValidSelectedAccessBitsTaskIndex));
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
                OnPropertyChanged(nameof(IsAccessBitsEditTabEnabled));
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

                OnPropertyChanged(nameof(Selected_DataBlock_AccessCondition));
                OnPropertyChanged(nameof(SectorTrailer));
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

                OnPropertyChanged(nameof(Selected_Sector_AccessCondition));
                OnPropertyChanged(nameof(SectorTrailer));
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
                sectorModel.AccessBitsAsString = value.ToUpper(CultureInfo.CurrentCulture);
                IsValidSectorTrailer = sectorModel.IsValidSectorTrailer;
                OnPropertyChanged(nameof(Selected_Sector_AccessCondition));
                OnPropertyChanged(nameof(Selected_DataBlock_AccessCondition));
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
                OnPropertyChanged(nameof(IsValidSectorTrailer));
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
                foreach (var gCNVM in ChildNodeViewModelTemp.Children)
                {
                    gCNVM.IsFocused = false;
                }

                ChildNodeViewModelTemp.Children.First(x => x.DataBlockNumber == (int)SelectedDataBlockToReadWrite).IsFocused = true;

                OnPropertyChanged(nameof(SelectedDataBlockToReadWrite));
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
                OnPropertyChanged(nameof(IsWriteFromMemoryToChipChecked));
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
                OnPropertyChanged(nameof(IsWriteFromFileToChipChecked));
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
                OnPropertyChanged(nameof(IsDataExplorerEditTabEnabled));
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

        #region Dialogs
        [XmlIgnore]
        public ObservableCollection<IDialogViewModel> Dialogs => dialogs;
        private readonly ObservableCollection<IDialogViewModel> dialogs = new ObservableCollection<IDialogViewModel>();

        #endregion Dialogs

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
                OnPropertyChanged(nameof(HasPlugins));
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
                OnPropertyChanged(nameof(SelectedPlugin));
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

                OnPropertyChanged(nameof(Items));
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
                OnPropertyChanged(nameof(SelectedExecuteConditionTaskIndex));
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
                OnPropertyChanged(nameof(IsValidSelectedExecuteConditionTaskIndex));
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
                OnPropertyChanged(nameof(SelectedExecuteConditionErrorLevel));
            }
        }
        private ERROR selectedExecuteConditionErrorLevel;

        /// <summary>
        /// 
        /// </summary>
        [XmlIgnore]
        public ERROR CurrentTaskErrorLevel { get; set; }

        [XmlIgnore]
        public ObservableCollection<TaskAttemptResult> AttemptResults { get; } = new ObservableCollection<TaskAttemptResult>();

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
                OnPropertyChanged(nameof(IsTaskCompletedSuccessfully));
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
                OnPropertyChanged(nameof(StatusText));
            }
        }
        private string statusText;

        /// <summary>
        ///
        /// </summary>
        public string CurrentTaskIndex
        {
            get =>
                currentTaskIndex;
            set
            {
                currentTaskIndex = value;
                IsValidSelectedTaskIndex = int.TryParse(value, out selectedTaskIndexAsInt);
            }
        }
        private string currentTaskIndex;

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
                OnPropertyChanged(nameof(IsValidSelectedTaskIndex));
            }
        }
        private bool? isValidSelectedTaskIndex;

        /// <summary>
        ///
        /// </summary>
        public TaskType_MifareClassicTask SelectedTaskType
        {
            get =>
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

                    default:
                        break;
                }
                OnPropertyChanged(nameof(SelectedTaskType));
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
                OnPropertyChanged(nameof(SelectedTaskDescription));
            }
        }
        private string selectedAccessBitsTaskDescription;

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

                    FileSize = "48";
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

                    OnPropertyChanged(nameof(ChildNodeViewModelFromChip));
                    OnPropertyChanged(nameof(ChildNodeViewModelTemp));
                }

                OnPropertyChanged(nameof(UseMAD));
                OnPropertyChanged(nameof(UseMADInvert));
            }
        }
        private bool useMAD;

        /// <summary>
        /// 
        /// </summary>
        [XmlIgnore]
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
                OnPropertyChanged(nameof(IsClassicAuthInfoEnabled));
            }
        }
        private bool isClassicAuthInfoEnabled;

        /// <summary>
        ///
        /// </summary>
        public bool IsClassicKeyEditingEnabled
        {
            get => isClassicKeyEditingEnabled;
            set
            {
                isClassicKeyEditingEnabled = value;
                OnPropertyChanged(nameof(IsClassicKeyEditingEnabled));
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
                isValidSelectedKeySetupTaskIndex;
            set
            {
                isValidSelectedKeySetupTaskIndex = value;
                OnPropertyChanged(nameof(IsValidSelectedKeySetupTaskIndex));
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
                SettingsReaderWriter settings = new SettingsReaderWriter();

                classicKeyAKeyCurrent = value.Length > 12 ? value.ToUpper(CultureInfo.CurrentCulture).Remove(12, value.Length - 12) : value.ToUpper(CultureInfo.CurrentCulture);

                IsValidClassicKeyAKeyCurrent = (CustomConverter.IsInHexFormat(classicKeyAKeyCurrent) && classicKeyAKeyCurrent.Length == 12);

                if (IsValidClassicKeyAKeyCurrent != false && SelectedTaskType == TaskType_MifareClassicTask.ChangeDefault)
                {
                    var currentSectorTrailer = settings.DefaultSpecification.MifareClassicDefaultSecuritySettings[selectedClassicKeyANumberCurrentAsInt].AccessBits;
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

                OnPropertyChanged(nameof(ClassicKeyAKeyCurrent));
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
                OnPropertyChanged(nameof(IsValidClassicKeyAKeyCurrent));
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
                SettingsReaderWriter settings = new SettingsReaderWriter();

                classicKeyBKeyCurrent = value.Length > 12 ? value.ToUpper(CultureInfo.CurrentCulture).Remove(12, value.Length - 12) : value.ToUpper(CultureInfo.CurrentCulture);

                IsValidClassicKeyBKeyCurrent = (CustomConverter.IsInHexFormat(classicKeyBKeyCurrent) && classicKeyBKeyCurrent.Length == 12);
                if (IsValidClassicKeyBKeyCurrent != false && SelectedTaskType == TaskType_MifareClassicTask.ChangeDefault)
                {
                    var currentSectorTrailer = settings.DefaultSpecification.MifareClassicDefaultSecuritySettings[selectedClassicKeyBNumberCurrentAsInt].AccessBits;
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

                OnPropertyChanged(nameof(ClassicKeyBKeyCurrent));
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
                OnPropertyChanged(nameof(IsValidClassicKeyBKeyCurrent));
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
                    OnPropertyChanged(nameof(SelectedClassicKeyANumberCurrent));
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
                    OnPropertyChanged(nameof(SelectedClassicKeyBNumberCurrent));
                }
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
                    SettingsReaderWriter settings = new SettingsReaderWriter();

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
                    else
                    {
                        sectorModel.SectorNumber = selectedClassicSectorCurrentAsInt;
                    }
                    OnPropertyChanged(nameof(SelectedClassicSectorCurrent));
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

                OnPropertyChanged(nameof(DataBlockIsCombinedToggleButtonIsChecked));
                OnPropertyChanged(nameof(DataBlockSelectionComboBoxIsEnabled));
            }
        }
        private bool dataBlockIsCombinedToggleButtonIsChecked;

        #endregion KeySetup

        #region MADEditor

        [XmlIgnore]
        public string[] MADVersions
        {
            get;
            set;
        }

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

                OnPropertyChanged(nameof(SelectedMADVersion));
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

                OnPropertyChanged(nameof(IsMultiApplication));
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
                OnPropertyChanged(nameof(UseMadAuth));
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

                OnPropertyChanged(nameof(FileSize));
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
                OnPropertyChanged(nameof(IsValidFileSize));
            }
        }
        private bool? isValidFileSize;

        /// <summary>
        ///
        /// </summary>
        public string ApplicationCode
        {
            get => applicationCode;
            set
            {
                applicationCode = value.Length > 2 ? value.ToUpper(CultureInfo.CurrentCulture).Remove(2, value.Length - 2) : value.ToUpper(CultureInfo.CurrentCulture);
                IsValidApplicationCode = (CustomConverter.IsInHexFormat(applicationCode) && applicationCode.Length <= 2);
                OnPropertyChanged(nameof(ApplicationCode));
            }
        }
        private string applicationCode;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsValidApplicationCode
        {
            get => isValidApplicationCode;
            set
            {
                isValidApplicationCode = value;

                if (isValidApplicationCode == true)
                {
                    appNumberAsInt &= 0xff00;
                    appNumberAsInt |= CustomConverter.GetBytes(applicationCode, out var _)[0];
                }
                OnPropertyChanged(nameof(IsValidApplicationCode));
            }
        }
        private bool? isValidApplicationCode;

        /// <summary>
        ///
        /// </summary>
        public string FunctionClusterCode
        {
            get => functionClusterCode;
            set
            {
                functionClusterCode = value.Length > 2 ? value.ToUpper(CultureInfo.CurrentCulture).Remove(2, value.Length - 2) : value.ToUpper(CultureInfo.CurrentCulture);
                IsValidFunctionClusterCode = (CustomConverter.IsInHexFormat(functionClusterCode) && functionClusterCode.Length <= 2);
                OnPropertyChanged(nameof(FunctionClusterCode));
            }
        }
        private string functionClusterCode;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsValidFunctionClusterCode
        {
            get => isValidFunctionClusterCode;
            set
            {
                isValidFunctionClusterCode = value;

                if (isValidFunctionClusterCode == true)
                {
                    appNumberAsInt &= 0x00ff;
                    appNumberAsInt |= (CustomConverter.GetBytes(functionClusterCode, out var _)[0] << 8);
                }
                OnPropertyChanged(nameof(IsValidFunctionClusterCode));
            }
        }
        private bool? isValidFunctionClusterCode;

        /*
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
                OnPropertyChanged(nameof(AppNumberNew));
            }
        }
        private string appNumber;
        */
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
                OnPropertyChanged(nameof(IsValidAppNumber));
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

                OnPropertyChanged(nameof(SelectedMADSector));
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
                classicMADKeyAKeyCurrent = value.Length > 12 ? value.ToUpper(CultureInfo.CurrentCulture).Remove(12, value.Length - 12) : value.ToUpper(CultureInfo.CurrentCulture);

                IsValidClassicMADKeyAKeyCurrent = (CustomConverter.IsInHexFormat(classicMADKeyAKeyCurrent) && classicMADKeyAKeyCurrent.Length == 12);

                OnPropertyChanged(nameof(ClassicMADKeyAKeyCurrent));
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
                OnPropertyChanged(nameof(IsValidClassicMADKeyAKeyCurrent));
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
                classicMADKeyBKeyCurrent = value.Length > 12 ? value.ToUpper(CultureInfo.CurrentCulture).Remove(12, value.Length - 12) : value.ToUpper(CultureInfo.CurrentCulture);

                IsValidClassicMADKeyBKeyCurrent = (CustomConverter.IsInHexFormat(classicMADKeyBKeyCurrent) && classicMADKeyBKeyCurrent.Length == 12);

                OnPropertyChanged(nameof(ClassicMADKeyBKeyCurrent));
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
                OnPropertyChanged(nameof(IsValidClassicMADKeyBKeyCurrent));
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
                classicMADKeyAKeyTarget = value.Length > 12 ? value.ToUpper(CultureInfo.CurrentCulture).Remove(12, value.Length - 12) : value.ToUpper(CultureInfo.CurrentCulture);

                IsValidClassicMADKeyAKeyTarget = (CustomConverter.IsInHexFormat(classicMADKeyAKeyTarget) && classicMADKeyAKeyTarget.Length == 12);

                OnPropertyChanged(nameof(ClassicMADKeyAKeyTarget));
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
                OnPropertyChanged(nameof(IsValidClassicMADKeyAKeyTarget));
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
                classicMADKeyBKeyTarget = value.Length > 12 ? value.ToUpper(CultureInfo.CurrentCulture).Remove(12, value.Length - 12) : value.ToUpper(CultureInfo.CurrentCulture);

                IsValidClassicMADKeyBKeyTarget = (CustomConverter.IsInHexFormat(classicMADKeyBKeyTarget) && classicMADKeyBKeyTarget.Length == 12);

                OnPropertyChanged(nameof(ClassicMADKeyBKeyTarget));
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
                OnPropertyChanged(nameof(IsValidClassicMADKeyBKeyTarget));
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
                classicKeyAKeyTarget = value != null ? (value.Length > 12 ? value.ToUpper(CultureInfo.CurrentCulture).Remove(12, value.Length - 12) : value.ToUpper(CultureInfo.CurrentCulture)) : null;

                IsValidClassicKeyAKeyTarget = (CustomConverter.IsInHexFormat(classicKeyAKeyTarget) && classicKeyAKeyTarget.Length == 12);

                OnPropertyChanged(nameof(ClassicKeyAKeyTarget));
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
                OnPropertyChanged(nameof(IsValidClassicKeyAKeyTarget));
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
                classicKeyBKeyTarget = value.Length > 12 ? value.ToUpper(CultureInfo.CurrentCulture).Remove(12, value.Length - 12) : value.ToUpper(CultureInfo.CurrentCulture);

                IsValidClassicKeyBKeyTarget = (CustomConverter.IsInHexFormat(classicKeyBKeyTarget) && classicKeyBKeyTarget.Length == 12);

                OnPropertyChanged(nameof(ClassicKeyBKeyTarget));
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
                OnPropertyChanged(nameof(IsValidClassicKeyBKeyTarget));
            }
        }
        private bool? isValidClassicKeyBKeyTarget;

        #endregion

        #endregion General Properties

        #region Commands

        /// <summary>
        /// 
        /// </summary>
        public IAsyncRelayCommand SaveSettings => new AsyncRelayCommand(OnNewSaveSettingsCommand);
        private async Task OnNewSaveSettingsCommand()
        {
            SettingsReaderWriter settings = new SettingsReaderWriter();
            await settings.SaveSettings();
        }

        public IAsyncRelayCommand CommandDelegator => new AsyncRelayCommand<TaskType_MifareClassicTask>((x) => OnNewCommandDelegatorCall(x));
        private async Task OnNewCommandDelegatorCall(TaskType_MifareClassicTask classicTaskType)
        {
            switch(classicTaskType)
            {
                case TaskType_MifareClassicTask.ReadData:
                    await OnNewReadDataCommand();
                    break;
                case TaskType_MifareClassicTask.WriteData:
                    await OnNewWriteDataCommand();
                    break;
                case TaskType_MifareClassicTask.EmptyCheck:
                    await OnNewCheckEmptyCommand();
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Check if DataBlock 0, 1 and 2 contain all 0 and if Authentication with KeyA = 6xFF works
        /// </summary>
        public ICommand CheckEmptyCommand => new AsyncRelayCommand(OnNewCheckEmptyCommand);
        private protected async Task OnNewCheckEmptyCommand()
        {
            CurrentTaskErrorLevel = ERROR.Empty;

            using (var device = ReaderDevice.Instance)
            {
                if (device != null)
                {
                    StatusText += string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxDllLoaded"));

                    if (await device.ReadMifareClassicSingleSector(
                        selectedClassicSectorCurrentAsInt,
                        ClassicKeyAKeyCurrent,
                        ClassicKeyBKeyCurrent) == ERROR.NoError)
                    {
                        StatusText += string.Format("{0}: Success for Sector: {1}\n", DateTime.Now, selectedClassicSectorCurrentAsInt);

                        for (var i = 0; i < device.Sector.DataBlock.Count - 1; i++)
                        {
                            for (var j = 0; j < device.Sector.DataBlock[i].Data.Length; j++)
                            {
                                if (device.Sector.DataBlock[i].Data[j] == 0 && device.Sector.IsAuthenticated == true)
                                {
                                    continue;
                                }
                                else
                                {
                                    CurrentTaskErrorLevel = ERROR.IsNotTrue;
                                    break;
                                }
                            }
                        }

                        CurrentTaskErrorLevel = ERROR.NoError;

                    }

                    else
                    {
                        StatusText += string.Format("{0}: Unable to Authenticate to Sector: {1} using specified Keys\n", DateTime.Now, selectedClassicSectorCurrentAsInt);
                        CurrentTaskErrorLevel = ERROR.AuthFailure;
                    }
                }

                else
                {
                    CurrentTaskErrorLevel = ERROR.TransportError;
                    return;
                }

                OnPropertyChanged(nameof(ChildNodeViewModelTemp));

                if (CurrentTaskErrorLevel == ERROR.NoError)
                {
                    IsTaskCompletedSuccessfully = true;
                }
                else
                {
                    IsTaskCompletedSuccessfully = false;
                }

                return;
            }
        }

        /// <summary>
        /// Read Data to Memory
        /// </summary>
        public ICommand ReadDataCommand => new AsyncRelayCommand(OnNewReadDataCommand);
        private protected async Task OnNewReadDataCommand()
        {
            CurrentTaskErrorLevel = ERROR.Empty;

            using (var device = ReaderDevice.Instance)
            {
                if (device != null)
                {
                    await UpdateReaderStatusCommand.ExecuteAsync(true);

                    StatusText += string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxDllLoaded"));

                    if (!useMAD)
                    {
                        if (await device.ReadMifareClassicSingleSector(
                            selectedClassicSectorCurrentAsInt,
                            ClassicKeyAKeyCurrent,
                            ClassicKeyBKeyCurrent) == ERROR.NoError)
                        {

                            childNodeViewModelFromChip.SectorNumber = selectedClassicSectorCurrentAsInt;
                            childNodeViewModelTemp.SectorNumber = selectedClassicSectorCurrentAsInt;

                            StatusText += string.Format("{0}: Success for Sector: {1}\n", DateTime.Now, selectedClassicSectorCurrentAsInt);

                            for (var i = 0; i < device.Sector.DataBlock.Count; i++)
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

                                    CurrentTaskErrorLevel = ERROR.NoError;
                                }
                                else
                                {
                                    StatusText += string.Format("{0}: \tBut: unable to authenticate to sector: {1}, DataBlock: {2} using specified Keys\n", DateTime.Now, selectedClassicSectorCurrentAsInt, device.Sector.DataBlock[i - 1].DataBlockNumberChipBased);
                                }
                            }

                            CurrentTaskErrorLevel = ERROR.NoError;

                        }

                        else
                        {
                            StatusText += string.Format("{0}: Unable to Authenticate to Sector: {1} using specified Keys\n", DateTime.Now, selectedClassicSectorCurrentAsInt);
                            CurrentTaskErrorLevel = ERROR.AuthFailure;
                            await UpdateReaderStatusCommand.ExecuteAsync(false);
                            return;
                        }
                    }

                    else
                    {
                        ChildNodeViewModelFromChip.Children.FirstOrDefault().MifareClassicMAD.MADApp = appNumberAsInt;
                        ChildNodeViewModelTemp.Children.FirstOrDefault().MifareClassicMAD.MADApp = appNumberAsInt;

                        if (await device.ReadMifareClassicWithMAD(appNumberAsInt,
                            ClassicKeyAKeyCurrent, ClassicKeyBKeyCurrent, ClassicMADKeyAKeyCurrent, ClassicMADKeyBKeyCurrent, fileSizeAsInt,
                            madGPB, UseMAD, useMADAuth) == ERROR.NoError)
                        {
                            ChildNodeViewModelFromChip.Children.FirstOrDefault().MifareClassicMAD.Data = device.MifareClassicData;
                            ChildNodeViewModelTemp.Children.FirstOrDefault().MifareClassicMAD.Data = device.MifareClassicData;

                            ChildNodeViewModelTemp.Children.Single().RequestRefresh();
                            ChildNodeViewModelFromChip.Children.Single().RequestRefresh();

                            StatusText = StatusText + string.Format("{0}: Successfully Read Data from MAD\n", DateTime.Now);

                            CurrentTaskErrorLevel = ERROR.NoError;
                        }

                        else
                        {
                            StatusText = StatusText + string.Format("{0}: Unable to Authenticate to MAD Sector using specified MAD Key(s)\n", DateTime.Now);

                            CurrentTaskErrorLevel = ERROR.AuthFailure;
                            await UpdateReaderStatusCommand.ExecuteAsync(false);
                            return;
                        }
                    }


                }

                else
                {
                    CurrentTaskErrorLevel = ERROR.TransportError;
                    await UpdateReaderStatusCommand.ExecuteAsync(false);
                    return;
                }

                OnPropertyChanged(nameof(ChildNodeViewModelTemp));
            }

            if (CurrentTaskErrorLevel == ERROR.NoError)
            {
                IsTaskCompletedSuccessfully = true;
            }
            else
            {
                IsTaskCompletedSuccessfully = false;
            }

            await UpdateReaderStatusCommand.ExecuteAsync(false);
        }


        /// <summary>
        /// 
        /// </summary>
        public IAsyncRelayCommand WriteDataCommand => new AsyncRelayCommand(OnNewWriteDataCommand);
        private protected async Task OnNewWriteDataCommand()
        {
            CurrentTaskErrorLevel = ERROR.Empty;

            using (var device = ReaderDevice.Instance)
            {
                StatusText = string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxDllLoaded"));

                if (!UseMAD)
                {
                    if (device != null)
                    {
                        await UpdateReaderStatusCommand.ExecuteAsync(true);

                        childNodeViewModelFromChip.SectorNumber = selectedClassicSectorCurrentAsInt;
                        childNodeViewModelTemp.SectorNumber = selectedClassicSectorCurrentAsInt;

                        await device.ReadChipPublic();

                        if (await device.WriteMifareClassicSingleBlock(CustomConverter.GetChipBasedDataBlockNumber(selectedClassicSectorCurrentAsInt, (byte)SelectedDataBlockToReadWrite),
                                                                ClassicKeyAKeyCurrent,
                                                                ClassicKeyBKeyCurrent,
                                                                childNodeViewModelTemp.Children[(int)SelectedDataBlockToReadWrite].MifareClassicDataBlock.Data) == ERROR.NoError)
                        {
                            StatusText = StatusText + string.Format("{0}: \tSuccess for Blocknumber: {1} Data: {2}\n",
                                                                    DateTime.Now,
                                                                    childNodeViewModelFromChip.Children[(int)SelectedDataBlockToReadWrite].DataBlockNumber,
                                                                    CustomConverter.HexToString(childNodeViewModelTemp.Children[(int)SelectedDataBlockToReadWrite].MifareClassicDataBlock.Data));
                            CurrentTaskErrorLevel = ERROR.NoError;
                        }
                        else
                        {
                            StatusText = StatusText + string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxUnableToAuthenticate"));
                            CurrentTaskErrorLevel = ERROR.AuthFailure;
                        }
                    }
                    else
                    {
                        StatusText = StatusText + string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxUnableToAuthenticate"));
                        CurrentTaskErrorLevel = ERROR.TransportError;
                    }
                }

                else
                {
                    ChildNodeViewModelFromChip.Children.FirstOrDefault().MifareClassicMAD.MADApp = appNumberAsInt;
                    ChildNodeViewModelTemp.Children.FirstOrDefault().MifareClassicMAD.MADApp = appNumberAsInt;

                    if (await device.WriteMifareClassicWithMAD(appNumberAsInt, selectedMADSectorAsInt,
                                                        ClassicKeyAKeyCurrent, ClassicKeyBKeyCurrent,
                                                        ClassicKeyAKeyTarget, ClassicKeyBKeyTarget,
                                                        ClassicMADKeyAKeyCurrent, ClassicMADKeyBKeyCurrent,
                                                        ClassicMADKeyAKeyTarget, ClassicMADKeyBKeyTarget,
                                                        ChildNodeViewModelTemp.Children.Single(x => x.MifareClassicMAD.MADApp == appNumberAsInt).MifareClassicMAD.Data,
                                                        madGPB, ChildNodeViewModelTemp.SectorModel.SAB, UseMadAuth, UseMAD) == ERROR.NoError)
                    {
                        StatusText = StatusText + string.Format("{0}: Wrote {1} bytes to MAD ID {2}\n", DateTime.Now,
                            ChildNodeViewModelTemp.Children.Single(x => x.MifareClassicMAD.MADApp == appNumberAsInt).MifareClassicMAD.Data.Length,
                            ChildNodeViewModelTemp.Children.Single(x => x.MifareClassicMAD.MADApp == appNumberAsInt).MifareClassicMAD.MADApp);

                        CurrentTaskErrorLevel = ERROR.NoError;
                    }

                    else
                    {
                        StatusText = StatusText + string.Format("{0}: Unable to Authenticate to MAD Sector using specified MAD Key(s)\n", DateTime.Now);
                        CurrentTaskErrorLevel = ERROR.AuthFailure;
                        await UpdateReaderStatusCommand.ExecuteAsync(false);
                        return;
                    }
                }
            }

            if (CurrentTaskErrorLevel == ERROR.NoError)
            {
                IsTaskCompletedSuccessfully = true;
            }
            else
            {
                IsTaskCompletedSuccessfully = false;
            }
            await UpdateReaderStatusCommand.ExecuteAsync(false);
        }

        /// <summary>
        /// 
        /// </summary>
        public IAsyncRelayCommand GetDataFromFileCommand => new AsyncRelayCommand(OnNewGetDataFromFileCommand);
        private Task OnNewGetDataFromFileCommand()
        {
            var dlg = new OpenFileDialogViewModel
            {
                Title = ResourceLoader.GetResource("windowCaptionOpenProject"),
                //Filter = ResourceLoader.getResource("filterStringSaveTasks"),
                Multiselect = false
            };

            if (dlg.Show(Dialogs) && dlg.FileName != null)
            {
                try
                {
                    var data = File.ReadAllText(dlg.FileName).Replace(" ", "").Replace("\n", "").Replace("-", "").Replace("\r", "");

                    childNodeViewModelTemp.Children.Single().DataAsHexString = data;

                    OnPropertyChanged(nameof(ChildNodeViewModelTemp));
                    OnPropertyChanged(nameof(ChildNodeViewModelFromChip));
                }
                catch (Exception e)
                {
                    eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
                }
            }
            return Task.CompletedTask;
        }

        #endregion Commands

        #region IUserDialogViewModel Implementation

        [XmlIgnore]
        public bool IsModal { get; private set; }

        public IAsyncRelayCommand UpdateReaderStatusCommand => new AsyncRelayCommand<bool>(UpdateStatus);
        private Task UpdateStatus(bool isBusy)
        {
            if (OnUpdateStatus != null)
            {
                OnUpdateStatus(isBusy);
            }

            return Task.CompletedTask;
        }

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
        public Action<bool> OnUpdateStatus { get; set; }

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
        public string LocalizationResourceSet { get; set; }

        [XmlIgnore]
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
