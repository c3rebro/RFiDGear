/*
 * Created by SharpDevelop.
 * Date: 10/11/2017
 * Time: 22:15
 *
 */

using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

using MvvmDialogs.ViewModels;

using RFiDGear.DataAccessLayer;
using RFiDGear.Model;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
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

        private ObservableCollection<MifareClassicDataBlockModel> dataBlock_AccessBits = new ObservableCollection<MifareClassicDataBlockModel>
            (new[]
             {
                 new MifareClassicDataBlockModel(0,
                                                 SectorTrailer_DataBlock.BlockAll,
                                                 AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB,
                                                 AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB,
                                                 AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB,
                                                 AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB),

                 new MifareClassicDataBlockModel(1,
                                                 SectorTrailer_DataBlock.BlockAll,
                                                 AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB,
                                                 AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyB,
                                                 AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                 AccessCondition_MifareClassicSectorTrailer.NotAllowed),

                 new MifareClassicDataBlockModel(2,
                                                 SectorTrailer_DataBlock.BlockAll,
                                                 AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB,
                                                 AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                 AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                 AccessCondition_MifareClassicSectorTrailer.NotAllowed),

                 new MifareClassicDataBlockModel(3,
                                                 SectorTrailer_DataBlock.BlockAll,
                                                 AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB,
                                                 AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyB,
                                                 AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyB,
                                                 AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB),

                 new MifareClassicDataBlockModel(4,
                                                 SectorTrailer_DataBlock.BlockAll,
                                                 AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB,
                                                 AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                 AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                 AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB),

                 new MifareClassicDataBlockModel(5,
                                                 SectorTrailer_DataBlock.BlockAll,
                                                 AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyB,
                                                 AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                 AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                 AccessCondition_MifareClassicSectorTrailer.NotAllowed),

                 new MifareClassicDataBlockModel(6,
                                                 SectorTrailer_DataBlock.BlockAll,
                                                 AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyB,
                                                 AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyB,
                                                 AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                 AccessCondition_MifareClassicSectorTrailer.NotAllowed),

                 new MifareClassicDataBlockModel(7,
                                                 SectorTrailer_DataBlock.BlockAll,
                                                 AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                 AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                 AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                                 AccessCondition_MifareClassicSectorTrailer.NotAllowed)
             });

        private ObservableCollection<MifareClassicSectorModel> sectorTrailer_AccessBits = new ObservableCollection<MifareClassicSectorModel>
            (new[]
             {
                 new MifareClassicSectorModel(0,
                                              AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                              AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
                                              AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
                                              AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                              AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
                                              AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA),

                 new MifareClassicSectorModel(1,
                                              AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                              AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyB,
                                              AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB,
                                              AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                              AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                              AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyB),

                 new MifareClassicSectorModel(2,
                                              AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                              AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                              AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
                                              AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                              AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
                                              AccessCondition_MifareClassicSectorTrailer.NotAllowed),

                 new MifareClassicSectorModel(3,
                                              AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                              AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                              AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB,
                                              AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                              AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                              AccessCondition_MifareClassicSectorTrailer.NotAllowed),

                 new MifareClassicSectorModel(4,
                                              AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                              AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
                                              AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
                                              AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
                                              AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA,
                                              AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA),

                 new MifareClassicSectorModel(5,
                                              AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                              AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                              AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB,
                                              AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyB,
                                              AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                              AccessCondition_MifareClassicSectorTrailer.NotAllowed),

                 new MifareClassicSectorModel(6,
                                              AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                              AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyB,
                                              AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB,
                                              AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyB,
                                              AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                              AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyB),

                 new MifareClassicSectorModel(7,
                                              AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                              AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                              AccessCondition_MifareClassicSectorTrailer.Allowed_With_KeyA_Or_KeyB,
                                              AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                              AccessCondition_MifareClassicSectorTrailer.NotAllowed,
                                              AccessCondition_MifareClassicSectorTrailer.NotAllowed)
             });

        private MifareClassicChipModel chipModel;
        private MifareClassicSectorModel sectorModel;
        private MifareClassicDataBlockModel dataBlock0;
        private MifareClassicDataBlockModel dataBlock1;
        private MifareClassicDataBlockModel dataBlock2;
        private MifareClassicDataBlockModel dataBlockCombined;

        private DatabaseReaderWriter databaseReaderWriter;
        private SettingsReaderWriter settings = new SettingsReaderWriter();

        #endregion fields

        /// <summary>
        ///
        /// </summary>
        public MifareClassicSetupViewModel()
        {
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

            childNodeViewModelFromChip = new TreeViewChildNodeViewModel(sectorModel, this);
            childNodeViewModelTemp = new TreeViewChildNodeViewModel(sectorModel, this);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="_treeViewViewModel"></param>
        /// <param name="_dialogs"></param>
        public MifareClassicSetupViewModel(object _treeViewViewModel, ObservableCollection<IDialogViewModel> _dialogs)
        {
            DataBlockIsCombinedToggleButtonIsChecked = true;
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

            IsValidSectorTrailer = true;

            ClassicKeyAKeyCurrent = settings.DefaultSpecification.MifareClassicDefaultSecuritySettings[0].AccessBits.Split(new[] { ',', ';' })[0];
            ClassicKeyBKeyCurrent = settings.DefaultSpecification.MifareClassicDefaultSecuritySettings[0].AccessBits.Split(new[] { ',', ';' })[2];

            IsWriteFromMemoryToChipChecked = true;

            SelectedTaskIndex = "0";
            SelectedTaskDescription = "Enter a Description";

            sectorModel.SectorNumber = (int)selectedClassicSectorCurrent;

            childNodeViewModelFromChip = new TreeViewChildNodeViewModel(sectorModel, this);
            childNodeViewModelTemp = new TreeViewChildNodeViewModel(sectorModel, this);
        }

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
        }

        private SectorTrailer_DataBlock selected_DataBlockType;

        /// <summary>
        ///
        /// </summary>
        public bool IsValidSelectedAccessBitsTaskIndex
        {
            get
            {
                //classicKeyAKeyCurrent = SectorTrailer.Split(',')[0];
                return isValidSelectedAccessBitsTaskIndex;
            }
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
            get { return isAccessBitsEditTabEnabled; }
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
        public ObservableCollection<MifareClassicSectorModel> SectorTrailerSource
        {
            get { return sectorTrailer_AccessBits; }
        }

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public ObservableCollection<MifareClassicDataBlockModel> DataBlockSource
        {
            get
            {
                return dataBlock_AccessBits;
            }
        }

        /// <summary>
        ///
        /// </summary>
        public MifareClassicDataBlockModel Selected_DataBlock_AccessCondition
        {
            get
            {
                switch (Selected_DataBlockType)
                {
                    case SectorTrailer_DataBlock.Block0:
                        return dataBlock0;

                    case SectorTrailer_DataBlock.Block1:
                        return dataBlock1;

                    case SectorTrailer_DataBlock.Block2:
                        return dataBlock2;

                    default:
                        return dataBlockCombined;
                }
            }

            set
            {
                switch (Selected_DataBlockType)
                {
                    case SectorTrailer_DataBlock.Block0:
                        dataBlock0 = value;
                        sectorModel.DataBlock[0].Cx = value.Cx;
                        sectorModel.DataBlock[0].Read_DataBlock = value.Read_DataBlock;
                        sectorModel.DataBlock[0].Write_DataBlock = value.Write_DataBlock;
                        sectorModel.DataBlock[0].Increment_DataBlock = value.Increment_DataBlock;
                        sectorModel.DataBlock[0].Decrement_DataBlock = value.Decrement_DataBlock;
                        break;

                    case SectorTrailer_DataBlock.Block1:
                        dataBlock1 = value;
                        sectorModel.DataBlock[1].Cx = value.Cx;
                        sectorModel.DataBlock[1].Read_DataBlock = value.Read_DataBlock;
                        sectorModel.DataBlock[1].Write_DataBlock = value.Write_DataBlock;
                        sectorModel.DataBlock[1].Increment_DataBlock = value.Increment_DataBlock;
                        sectorModel.DataBlock[1].Decrement_DataBlock = value.Decrement_DataBlock;
                        break;

                    case SectorTrailer_DataBlock.Block2:
                        dataBlock2 = value;
                        sectorModel.DataBlock[2].Cx = value.Cx;
                        sectorModel.DataBlock[2].Read_DataBlock = value.Read_DataBlock;
                        sectorModel.DataBlock[2].Write_DataBlock = value.Write_DataBlock;
                        sectorModel.DataBlock[2].Increment_DataBlock = value.Increment_DataBlock;
                        sectorModel.DataBlock[2].Decrement_DataBlock = value.Decrement_DataBlock;
                        break;

                    default:
                        dataBlockCombined = value;

                        dataBlock0 = value;
                        sectorModel.DataBlock[0].Cx = value.Cx;
                        sectorModel.DataBlock[0].Read_DataBlock = value.Read_DataBlock;
                        sectorModel.DataBlock[0].Write_DataBlock = value.Write_DataBlock;
                        sectorModel.DataBlock[0].Increment_DataBlock = value.Increment_DataBlock;
                        sectorModel.DataBlock[0].Decrement_DataBlock = value.Decrement_DataBlock;

                        dataBlock1 = value;
                        sectorModel.DataBlock[1].Cx = value.Cx;
                        sectorModel.DataBlock[1].Read_DataBlock = value.Read_DataBlock;
                        sectorModel.DataBlock[1].Write_DataBlock = value.Write_DataBlock;
                        sectorModel.DataBlock[1].Increment_DataBlock = value.Increment_DataBlock;
                        sectorModel.DataBlock[1].Decrement_DataBlock = value.Decrement_DataBlock;

                        dataBlock2 = value;
                        sectorModel.DataBlock[2].Cx = value.Cx;
                        sectorModel.DataBlock[2].Read_DataBlock = value.Read_DataBlock;
                        sectorModel.DataBlock[2].Write_DataBlock = value.Write_DataBlock;
                        sectorModel.DataBlock[2].Increment_DataBlock = value.Increment_DataBlock;
                        sectorModel.DataBlock[2].Decrement_DataBlock = value.Decrement_DataBlock;
                        break;
                }

                encodeSectorTrailer(ref sectorModel);

                RaisePropertyChanged("Selected_DataBlock_AccessCondition");
                RaisePropertyChanged("SectorTrailer");
            }
        }

        /// <summary>
        ///
        /// </summary>
        public MifareClassicSectorModel Selected_Sector_AccessCondition
        {
            get
            {
                return selected_Sector_AccessCondition;
            }
            set
            {
                //TODO: create sector string
                selected_Sector_AccessCondition = value;

                sectorModel.Read_AccessCondition_MifareClassicSectorTrailer = value.Read_AccessCondition_MifareClassicSectorTrailer;
                sectorModel.Write_AccessCondition_MifareClassicSectorTrailer = value.Write_AccessCondition_MifareClassicSectorTrailer;
                sectorModel.Read_KeyA = value.Read_KeyA;
                sectorModel.Write_KeyA = value.Write_KeyA;
                sectorModel.Read_KeyB = value.Read_KeyB;
                sectorModel.Write_KeyB = value.Write_KeyB;
                sectorModel.Cx = value.Cx;

                encodeSectorTrailer(ref sectorModel);

                RaisePropertyChanged("Selected_Sector_AccessCondition");
                RaisePropertyChanged("SectorTrailer");
            }
        }

        private MifareClassicSectorModel selected_Sector_AccessCondition;

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
        }

        private bool isValidSectorTrailer;

        /// <summary>
        ///
        /// </summary>
        public string SectorTrailer
        {
            get
            {
                return sectorModel.AccessBitsAsString; //CustomConverter.encodeSectorTrailer(sector);
            }
            set
            {
                sectorModel.AccessBitsAsString = value.ToUpper();
                IsValidSectorTrailer = !decodeSectorTrailer(sectorModel.AccessBitsAsString, ref selected_Sector_AccessCondition);
                if (!IsValidSectorTrailer)
                    return;
                RaisePropertyChanged("Selected_Sector_AccessCondition");
                RaisePropertyChanged("Selected_DataBlock_AccessCondition");
            }
        }

        #endregion AccessBitsTab

        #region KeySetup

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
                classicKeyAKeyCurrent = value.ToUpper();

                IsValidClassicKeyAKeyCurrent = (CustomConverter.IsInHexFormat(value) && value.Length == 12);
                if (IsValidClassicKeyAKeyCurrent && SelectedTaskType == TaskType_MifareClassicTask.ChangeDefault)
                {
                    string currentSectorTrailer = settings.DefaultSpecification.MifareClassicDefaultSecuritySettings[(int)SelectedClassicKeyANumberCurrent].AccessBits;
                    currentSectorTrailer = string.Join(",", new[]
                                                       {
                                                           classicKeyAKeyCurrent,
                                                           currentSectorTrailer.Split(new[] {',',';'})[1],
                                                           currentSectorTrailer.Split(new[] {',',';'})[2]
                                                       });

                    settings.DefaultSpecification.MifareClassicDefaultSecuritySettings[(int)SelectedClassicKeyANumberCurrent] = new MifareClassicDefaultKeys(SelectedClassicKeyANumberCurrent, currentSectorTrailer);
                }
                else if (IsValidClassicKeyAKeyCurrent)
                    sectorModel.KeyA = classicKeyAKeyCurrent;

                RaisePropertyChanged("ClassicKeyAKeyCurrent");
            }
        }

        private string classicKeyAKeyCurrent;

        /// <summary>
        ///
        /// </summary>
        public bool IsValidClassicKeyAKeyCurrent
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

        private bool isValidClassicKeyAKeyCurrent;

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
                classicKeyBKeyCurrent = value.ToUpper();

                IsValidClassicKeyBKeyCurrent = (CustomConverter.IsInHexFormat(value) && value.Length == 12);
                if (IsValidClassicKeyBKeyCurrent && SelectedTaskType == TaskType_MifareClassicTask.ChangeDefault)
                {
                    string currentSectorTrailer = settings.DefaultSpecification.MifareClassicDefaultSecuritySettings[(int)SelectedClassicKeyBNumberCurrent].AccessBits;
                    currentSectorTrailer = string.Join(",", new[]
                                                       {
                                                           currentSectorTrailer.Split(new[] {',',';'})[0],
                                                           currentSectorTrailer.Split(new[] {',',';'})[1],
                                                           classicKeyBKeyCurrent
                                                       });

                    settings.DefaultSpecification.MifareClassicDefaultSecuritySettings[(int)SelectedClassicKeyBNumberCurrent] = new MifareClassicDefaultKeys(SelectedClassicKeyBNumberCurrent, currentSectorTrailer);
                }
                else if (IsValidClassicKeyBKeyCurrent)
                    sectorModel.KeyB = classicKeyBKeyCurrent;

                RaisePropertyChanged("ClassicKeyBKeyCurrent");
            }
        }

        private string classicKeyBKeyCurrent;

        /// <summary>
        ///
        /// </summary>
        public bool IsValidClassicKeyBKeyCurrent
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

        private bool isValidClassicKeyBKeyCurrent;

        /// <summary>
        ///
        /// </summary>
        public MifareClassicKeyNumber SelectedClassicKeyANumberCurrent
        {
            get { return selectedClassicKeyANumberCurrent; }
            set
            {
                selectedClassicKeyANumberCurrent = value;
                ClassicKeyAKeyCurrent = settings.DefaultSpecification.MifareClassicDefaultSecuritySettings.
                    First(x => x.KeyType == SelectedClassicKeyANumberCurrent).AccessBits.Split(new[] { ',', ';' })[0];

                RaisePropertyChanged("SelectedClassicKeyANumberCurrent");
            }
        }

        private MifareClassicKeyNumber selectedClassicKeyANumberCurrent;

        /// <summary>
        ///
        /// </summary>
        public MifareClassicKeyNumber SelectedClassicKeyBNumberCurrent
        {
            get { return selectedClassicKeyBNumberCurrent; }
            set
            {
                selectedClassicKeyBNumberCurrent = value;

                ClassicKeyBKeyCurrent = settings.DefaultSpecification.MifareClassicDefaultSecuritySettings.
                    First(x => x.KeyType == SelectedClassicKeyBNumberCurrent).AccessBits.Split(new[] { ',', ';' })[2];

                RaisePropertyChanged("SelectedClassicKeyBNumberCurrent");
            }
        }

        private MifareClassicKeyNumber selectedClassicKeyBNumberCurrent;

        /// <summary>
        ///
        /// </summary>
        public MifareClassicKeyNumber SelectedClassicSectorCurrent
        {
            get { return selectedClassicSectorCurrent; }
            set
            {
                //sectorModel.SectorNumber = (int)selectedClassicSectorCurrent;
                try
                {
                    //parentNodeViewModelFromChip.Children.First(x => x.SectorNumber == (int)selectedClassicSectorCurrent).SectorNumber = (int)value;
                }
                catch
                {
                }
                //(parentNodeViewModel as TreeViewParentNodeViewModel).Children.First(x => x.SectorNumber == (int)SelectedClassicSectorCurrent).IsTask = true;

                selectedClassicSectorCurrent = value;
                RaisePropertyChanged("SelectedClassicSectorCurrent");
            }
        }

        private MifareClassicKeyNumber selectedClassicSectorCurrent;

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
                foreach (TreeViewGrandChildNodeViewModel gCNVM in ChildNodeViewModelTemp.Children)
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
        public TreeViewChildNodeViewModel ChildNodeViewModelFromChip
        {
            get { return childNodeViewModelFromChip; }
            set { childNodeViewModelFromChip = value; }
        }

        private TreeViewChildNodeViewModel childNodeViewModelFromChip;

        /// <summary>
        ///
        /// </summary>
        public TreeViewChildNodeViewModel ChildNodeViewModelTemp
        {
            get { return childNodeViewModelTemp; }
            set { childNodeViewModelTemp = value; }
        }

        private TreeViewChildNodeViewModel childNodeViewModelTemp;

        #endregion DataExplorer

        #region General Properties

        public ERROR TaskErr { get; set; }

        /// <summary>
        ///
        /// </summary>
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
        public int SelectedTaskIndexAsInt
        { get { return selectedTaskIndexAsInt; } }

        private int selectedTaskIndexAsInt;

        /// <summary>
        ///
        /// </summary>
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
            }
        }

        private string selectedAccessBitsTaskDescription;

        public SettingsReaderWriter Settings
        {
            get { return settings; }
        }

        //		public int KeySelectionComboboxIndex {
        //			get {
        //				if (ViewModelContext is TreeViewChildNodeViewModel)
        //					return (ViewModelContext as TreeViewChildNodeViewModel).sectorModel.SectorNumber;
        //				else
        //					return selectedKeyNumber;
        //			}
        //			set {
        //				selectedKeyNumber = value;
        //				RaisePropertyChanged("KeySelectionComboboxIndex");
        //				RaisePropertyChanged("selectedClassicKeyAKey");
        //				RaisePropertyChanged("selectedClassicKeyBKey");
        //			}
        //		} private int selectedKeyNumber;

        public bool DataBlockSelectionComboBoxIsEnabled
        {
            get { return !dataBlockIsCombinedToggleButtonIsChecked; }
        }

        //		public bool? IsFixedKeyNumber {
        //			get {
        //				if (ViewModelContext is TreeViewChildNodeViewModel)
        //					return false;
        //				else
        //					return true;
        //			}
        //		}

        #endregion General Properties

        #region Commands

        public ICommand ReadDataCommand { get { return new RelayCommand(OnNewReadDataCommand); } }

        private void OnNewReadDataCommand()
        {
            //Mouse.OverrideCursor = Cursors.Wait;
            TaskErr = ERROR.Empty;

            Task classicTask =
                new Task(() =>
                {
                    using (RFiDDevice device = new RFiDDevice(settings.DefaultSpecification.DefaultReaderProvider))
                    {
                        StatusText = "";

                        if (device.ReadMiFareClassicSingleSector((int)SelectedClassicSectorCurrent, ClassicKeyAKeyCurrent, ClassicKeyBKeyCurrent) == ERROR.NoError)
                        {
                            StatusText = string.Format("{0}: Connection to Reader successfully established\n", DateTime.Now);

                            if (device.Sector.IsAuthenticated)
                                StatusText = StatusText + string.Format("{0}: Success for Sector: {1}\n", DateTime.Now, (int)SelectedClassicSectorCurrent);
                            else
                            {
                                StatusText = StatusText + string.Format("{0}: Unable to Authenticate to Sector: {1} using specified Keys\n", DateTime.Now, (int)SelectedClassicSectorCurrent);
                                TaskErr = ERROR.AuthenticationError;
                                return;
                            }

                            for (int i = 0; i < device.Sector.DataBlock.Count; i++) //(MifareClassicDataBlockModel b in device.Sector.DataBlock)
                            {
                                childNodeViewModelFromChip.Children[i].DataBlockNumber = i;

                                if (device.Sector.DataBlock[i].IsAuthenticated)
                                {
                                    StatusText = StatusText + string.Format("{0}: \tSuccess for Blocknumber: {1} Data: {2}\n", DateTime.Now, device.Sector.DataBlock[i].BlockNumber, CustomConverter.HexToString(device.Sector.DataBlock[i].Data));
                                    childNodeViewModelFromChip.Children.First(x => x.DataBlockNumber == i).DataBlockContent = device.Sector.DataBlock[i].Data;

                                    childNodeViewModelTemp.Children.First(x => x.DataBlockNumber == i).DataBlockContent = device.Sector.DataBlock[i].Data;
                                    childNodeViewModelFromChip.Children.First(x => x.DataBlockNumber == i).DataBlockNumber = device.Sector.DataBlock[i].BlockNumber;

                                    //parentNodeViewModelTemp.Children.First(x => x.SectorNumber == (int)SelectedClassicSectorCurrent).Children[i-1].DataBlockContent = device.Sector.DataBlock[i-1].Data;
                                    //dataAsByteArray = b.Data;
                                    TaskErr = ERROR.NoError;
                                }
                                else
                                    StatusText = StatusText + string.Format("{0}: \tBut: unable to authenticate to sector: {1}, DataBlock: {2} using specified Keys\n", DateTime.Now, (int)SelectedClassicSectorCurrent, device.Sector.DataBlock[i - 1].BlockNumber);
                            }
                            TaskErr = ERROR.NoError;
                        }
                        else
                        {
                            StatusText = "Unable to Auth";
                            TaskErr = ERROR.AuthenticationError;
                        }

                        RaisePropertyChanged("ChildNodeViewModelTemp");
                    }
                });

            classicTask.Start();

            classicTask.ContinueWith((x) =>
            {
                //Mouse.OverrideCursor = null;

                if (TaskErr == ERROR.NoError)
                {
                    IsTaskCompletedSuccessfully = true;
                }
                else
                {
                    IsTaskCompletedSuccessfully = false;
                }
            });
        }

        public ICommand WriteDataCommand { get { return new RelayCommand(OnNewWriteDataCommand); } }

        private void OnNewWriteDataCommand()
        {
            //Mouse.OverrideCursor = Cursors.Wait;

            TaskErr = ERROR.Empty;

            Task classicTask =
                new Task(() =>
                {
                    using (RFiDDevice device = RFiDDevice.Instance)
                    {
                        StatusText = "";

                        if (device != null)
                        {
                            StatusText = string.Format("{0}: Connection to Reader successfully established\n", DateTime.Now);

                            //				         			if(device.Sector.IsAuthenticated)
                            //				         				StatusText = StatusText + string.Format("{0}: Success for Sector: {1}\n", DateTime.Now, (int)SelectedClassicSectorCurrent);
                            //				         			else
                            //				         			{
                            //				         				StatusText = StatusText + string.Format("{0}: Unable to Authenticate to Sector: {1} using specified Keys\n", DateTime.Now, (int)SelectedClassicSectorCurrent);
                            //				         				Mouse.OverrideCursor = null;
                            //				         				return;
                            //				         			}

                            if (device.WriteMiFareClassicSingleBlock(childNodeViewModelFromChip.Children[(int)SelectedDataBlockToReadWrite].DataBlockNumber,
                                                                      ClassicKeyAKeyCurrent,
                                                                      ClassicKeyBKeyCurrent,
                                                                      childNodeViewModelTemp.Children[(int)SelectedDataBlockToReadWrite].DataBlockContent) == ERROR.NoError)
                            {
                                StatusText = StatusText + string.Format("{0}: \tSuccess for Blocknumber: {1} Data: {2}\n",
                                                                        DateTime.Now,
                                                                        childNodeViewModelFromChip.Children[(int)SelectedDataBlockToReadWrite].DataBlockNumber,
                                                                        CustomConverter.HexToString(childNodeViewModelTemp.Children[(int)SelectedDataBlockToReadWrite].DataBlockContent));

                                TaskErr = ERROR.NoError;
                            }
                            else
                                TaskErr = ERROR.AuthenticationError;
                        }
                        else
                        {
                            StatusText = "Unable to Auth";
                            TaskErr = ERROR.DeviceNotReadyError;
                        }
                    }
                });

            if (TaskErr == ERROR.Empty)
            {
                TaskErr = ERROR.DeviceNotReadyError;

                classicTask.ContinueWith((x) =>
                {
                    //Mouse.OverrideCursor = null;

                    if (TaskErr == ERROR.NoError)
                    {
                        IsTaskCompletedSuccessfully = true;
                    }
                    else
                    {
                        IsTaskCompletedSuccessfully = false;
                    }
                }); //TaskScheduler.FromCurrentSynchronizationContext()

                classicTask.Start();
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

        [XmlIgnore]
        public string Caption
        {
            get { return _Caption; }
            set
            {
                _Caption = value;
                RaisePropertyChanged("Caption");
            }
        }

        private string _Caption;

        #endregion Localization

        #region Extensions

        /// <summary>
        /// turns a given byte or string sector trailer to a access bits selection
        /// </summary>
        /// <param name="st"></param>
        /// <param name="_sector"></param>
        /// <param name="_keyA"></param>
        /// <param name="_keyB"></param>
        /// <returns></returns>
        private bool decodeSectorTrailer(byte[] st, ref MifareClassicSectorModel _sector)
        {
            uint C1x, C2x;

            LibLogicalAccess.SectorAccessBits sab;

            bool isTransportConfiguration;

            uint tmpAccessBitCx;

            if (CustomConverter.SectorTrailerHasWrongFormat(st))
            {
                _sector = null;
                return true;
            }

            #region getAccessBitsForSectorTrailer

            C1x = st[1];
            C2x = st[2];

            C1x &= 0xF0;
            C1x >>= 7;
            C1x &= 0x01;

            sab.d_sector_trailer_access_bits.c1 = (short)C1x;

            C2x >>= 2;

            tmpAccessBitCx = C2x;
            tmpAccessBitCx >>= 1;
            tmpAccessBitCx &= 0x01;

            sab.d_sector_trailer_access_bits.c2 = (short)tmpAccessBitCx;

            C1x |= C2x;
            C2x >>= 3;

            tmpAccessBitCx = C2x;
            tmpAccessBitCx >>= 2;
            tmpAccessBitCx &= 0x01;

            sab.d_sector_trailer_access_bits.c3 = (short)tmpAccessBitCx;

            C1x &= 0x03;
            C1x |= C2x;
            C1x &= 0x07; //now we have C1³ + C2³ + C3³ as integer in C1x see mifare manual

            if (C1x == 4)
                isTransportConfiguration = true;
            else
                isTransportConfiguration = false;

            _sector = sectorTrailer_AccessBits[(int)C1x];

            //sector_AccessCondition = new List<AccessCondition_MifareClassicSectorTrailer>(sab.d_sector_trailer_access_bits.c1,sab.d_sector_trailer_access_bits.c2, sab.d_sector_trailer_access_bits.c3);

            //sector.Sector_AccessCondition = sector_AccessCondition;
            //decodedSectorTrailerAccessBits = sectorTrailerAB[C1x];

            #endregion getAccessBitsForSectorTrailer

            #region getAccessBitsForDataBlock2

            C1x = st[1];
            C2x = st[2];

            C1x &= 0xF0;
            C1x >>= 6;
            C1x &= 0x01;

            sab.d_data_block2_access_bits.c1 = (short)C1x;

            C2x >>= 1;

            tmpAccessBitCx = C2x;
            tmpAccessBitCx >>= 1;
            tmpAccessBitCx &= 0x01;

            sab.d_data_block2_access_bits.c2 = (short)tmpAccessBitCx;

            C1x |= C2x;
            //C2 &= 0xF8;
            C2x >>= 3;

            tmpAccessBitCx = C2x;
            tmpAccessBitCx >>= 2;
            tmpAccessBitCx &= 0x01;

            sab.d_data_block2_access_bits.c3 = (short)tmpAccessBitCx;

            C1x &= 0x03;
            C1x |= C2x;
            C1x &= 0x07;

            //if(isTransportConfiguration)
            //	decodedBlock2AccessBits = dataBlockABs[C1x];
            //else
            //	decodedBlock2AccessBits = dataBlockAB[C1x];

            dataBlock2 = dataBlock_AccessBits[(int)C1x];
            //dataBlock2_AccessCondition = new DataBlock_AccessCondition(2, sab.d_data_block2_access_bits.c1, sab.d_data_block2_access_bits.c2, sab.d_data_block2_access_bits.c3);

            #endregion getAccessBitsForDataBlock2

            #region getAccessBitsForDataBlock1

            C1x = st[1];
            C2x = st[2];

            C1x &= 0xF0;
            C1x >>= 5;
            C1x &= 0x01;

            sab.d_data_block1_access_bits.c1 = (short)C1x;

            C1x |= C2x;

            tmpAccessBitCx = C2x;
            tmpAccessBitCx >>= 1;
            tmpAccessBitCx &= 0x01;

            sab.d_data_block1_access_bits.c2 = (short)tmpAccessBitCx;

            C2x >>= 3;

            tmpAccessBitCx = C2x;
            tmpAccessBitCx >>= 2;
            tmpAccessBitCx &= 0x01;

            sab.d_data_block1_access_bits.c3 = (short)tmpAccessBitCx;

            C1x &= 0x03;
            C1x |= C2x;
            C1x &= 0x07;

            //if(isTransportConfiguration)
            //	decodedBlock1AccessBits = dataBlockABs[C1x];
            //else
            //	decodedBlock1AccessBits = dataBlockAB[C1x];
            dataBlock1 = dataBlock_AccessBits[(int)C1x];

            #endregion getAccessBitsForDataBlock1

            #region getAccessBitsForDataBlock0

            C1x = st[1];
            C2x = st[2];

            C1x &= 0xF0;
            C1x >>= 4;
            C1x &= 0x01;

            tmpAccessBitCx = C1x;
            tmpAccessBitCx &= 0x01;

            sab.d_data_block0_access_bits.c1 = (short)C1x;

            tmpAccessBitCx = C2x;
            tmpAccessBitCx &= 0x01;

            sab.d_data_block0_access_bits.c2 = (short)C1x;

            C2x <<= 1;
            C1x |= C2x;
            C2x >>= 3;

            tmpAccessBitCx = C2x;
            tmpAccessBitCx >>= 2;
            tmpAccessBitCx &= 0x01;

            sab.d_data_block0_access_bits.c3 = (short)C1x;

            C2x &= 0xFC;
            C1x &= 0x03;
            C1x |= C2x;
            C1x &= 0x07;

            dataBlock0 = dataBlock_AccessBits[(int)C1x];

            if (dataBlock0 == dataBlock1 && dataBlock1 == dataBlock2)
            {
                dataBlockCombined = dataBlock0;
                Selected_DataBlockType = SectorTrailer_DataBlock.BlockAll;
                DataBlockIsCombinedToggleButtonIsChecked = true;
            }
            else
            {
                Selected_DataBlockType = SectorTrailer_DataBlock.Block0;
                DataBlockIsCombinedToggleButtonIsChecked = false;
            }

            #endregion getAccessBitsForDataBlock0

            return false;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="st"></param>
        /// <param name="_sector"></param>
        /// <returns></returns>
        private bool decodeSectorTrailer(string st, ref MifareClassicSectorModel _sector)
        {
            byte[] _bytes = new byte[255];
            int discarded = 0;

            string[] sectorTrailer = st.Split(new[] { ',', ';' });
            if (sectorTrailer.Count() != 3 ||
               !(CustomConverter.IsInHexFormat(sectorTrailer[1]) && sectorTrailer[1].Length == 8) ||
               !(CustomConverter.IsInHexFormat(sectorTrailer[0]) && sectorTrailer[0].Length == 12) ||
               !(CustomConverter.IsInHexFormat(sectorTrailer[2]) && sectorTrailer[2].Length == 12))
                return true;

            _bytes = CustomConverter.GetBytes(sectorTrailer[1], out discarded);

            if (!decodeSectorTrailer(_bytes, ref _sector))
                return false;
            else
                return true;
        }

        /// <summary>
        /// Converts a given selection for either sector
        /// access bits or datablock access bits to the equivalent 3 bytes sector trailer
        /// </summary>
        /// <param name="cond"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        private bool encodeSectorTrailer(ref MifareClassicSectorModel _sector)
        {
            byte[] st = new byte[4] { 0x00, 0x00, 0x00, 0xC3 };

            uint sectorAccessBitsIndex = (uint)_sector.Cx; //_sector.Sector_AccessCondition.Cx;
            uint dataBlock0AccessBitsIndex = (uint)_sector.DataBlock[0].Cx; //(uint)dataBlock_AccessBits.IndexOf(_sector); //_sector.DataBlock0_AccessCondition.Cx;
            uint dataBlock1AccessBitsIndex = (uint)_sector.DataBlock[1].Cx; //_sector.DataBlock1_AccessCondition.Cx;
            uint dataBlock2AccessBitsIndex = (uint)_sector.DataBlock[2].Cx; //_sector.DataBlock2_AccessCondition.Cx;

            // DataBlock 0 = C1/0; C2/0; C3/0

            st[1] |= (byte)((dataBlock0AccessBitsIndex & 0x01) << 4);   // C1/0
            st[2] |= (byte)((dataBlock0AccessBitsIndex & 0x02) >> 1);   // C2/0
            st[2] |= (byte)((dataBlock0AccessBitsIndex & 0x04) << 2);   // C3/0

            // DataBlock 1 = C1/1; C2/1; C3/1

            st[1] |= (byte)((dataBlock1AccessBitsIndex & 0x01) << 5);   // C1/1
            st[2] |= (byte)(dataBlock1AccessBitsIndex & 0x02);          // C2/1
            st[2] |= (byte)((dataBlock1AccessBitsIndex & 0x04) << 3);   // C3/1

            // DataBlock 2 = C1/2; C2/2; C3/2

            st[1] |= (byte)((dataBlock2AccessBitsIndex & 0x01) << 6);   // C1/2
            st[2] |= (byte)((dataBlock2AccessBitsIndex & 0x02) << 1);   // C2/2
            st[2] |= (byte)((dataBlock2AccessBitsIndex & 0x04) << 4);   // C3/2

            // SectorAccessBits = C1/3; C2/3; C3/3

            st[1] |= (byte)((sectorAccessBitsIndex & 0x01) << 7);   // C1/3
            st[2] |= (byte)((sectorAccessBitsIndex & 0x02) << 2);   // C2/3
            st[2] |= (byte)((sectorAccessBitsIndex & 0x04) << 5);   // C3/3

            st = CustomConverter.buildSectorTrailerInvNibble(st);
            string[] stAsString;

            if (!string.IsNullOrWhiteSpace(_sector.AccessBitsAsString))
                stAsString = _sector.AccessBitsAsString.Split(new[] { ',', ';' });
            else
                stAsString = new string[] { "FFFFFFFFFFFF", "FF0780C3", "FFFFFFFFFFFF" };

            stAsString[1] = CustomConverter.HexToString(st);
            _sector.AccessBitsAsString = string.Join(",", stAsString);
            return false;
        }

        #endregion Extensions
    }
}