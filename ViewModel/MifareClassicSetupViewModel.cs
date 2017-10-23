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
using System.Security;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows.Documents;

namespace RFiDGear.ViewModel
{
	/// <summary>
	/// Description of MifareClassicSetupViewModel.
	/// </summary>
	public class MifareClassicSetupViewModel : ViewModelBase, IUserDialogViewModel
	{
		
		#region fields
		private ObservableCollection<MifareClassicDataBlockTreeViewModel> dataBlock_AccessBits = new ObservableCollection<MifareClassicDataBlockTreeViewModel>
			(new[]
			 {
			 	new MifareClassicDataBlockTreeViewModel(0,
			 	                                        Data_Block.BlockAll,
			 	                                        Access_Condition.Allowed_With_KeyA_Or_KeyB,
			 	                                        Access_Condition.Allowed_With_KeyA_Or_KeyB,
			 	                                        Access_Condition.Allowed_With_KeyA_Or_KeyB,
			 	                                        Access_Condition.Allowed_With_KeyA_Or_KeyB),
			 	
			 	new MifareClassicDataBlockTreeViewModel(1,
			 	                                        Data_Block.BlockAll,
			 	                                        Access_Condition.Allowed_With_KeyA_Or_KeyB,
			 	                                        Access_Condition.Allowed_With_KeyB,
			 	                                        Access_Condition.NotAllowed,
			 	                                        Access_Condition.NotAllowed),
			 	
			 	new MifareClassicDataBlockTreeViewModel(2,
			 	                                        Data_Block.BlockAll,
			 	                                        Access_Condition.Allowed_With_KeyA_Or_KeyB,
			 	                                        Access_Condition.NotAllowed,
			 	                                        Access_Condition.NotAllowed,
			 	                                        Access_Condition.NotAllowed),
			 	
			 	new MifareClassicDataBlockTreeViewModel(3,
			 	                                        Data_Block.BlockAll,
			 	                                        Access_Condition.Allowed_With_KeyA_Or_KeyB,
			 	                                        Access_Condition.Allowed_With_KeyB,
			 	                                        Access_Condition.Allowed_With_KeyB,
			 	                                        Access_Condition.Allowed_With_KeyA_Or_KeyB),
			 	
			 	new MifareClassicDataBlockTreeViewModel(4,
			 	                                        Data_Block.BlockAll,
			 	                                        Access_Condition.Allowed_With_KeyA_Or_KeyB,
			 	                                        Access_Condition.NotAllowed,
			 	                                        Access_Condition.NotAllowed,
			 	                                        Access_Condition.Allowed_With_KeyA_Or_KeyB),
			 	
			 	new MifareClassicDataBlockTreeViewModel(5,
			 	                                        Data_Block.BlockAll,
			 	                                        Access_Condition.Allowed_With_KeyB,
			 	                                        Access_Condition.NotAllowed,
			 	                                        Access_Condition.NotAllowed,
			 	                                        Access_Condition.NotAllowed),
			 	
			 	new MifareClassicDataBlockTreeViewModel(6,
			 	                                        Data_Block.BlockAll,
			 	                                        Access_Condition.Allowed_With_KeyB,
			 	                                        Access_Condition.Allowed_With_KeyB,
			 	                                        Access_Condition.NotAllowed,
			 	                                        Access_Condition.NotAllowed),
			 	
			 	new MifareClassicDataBlockTreeViewModel(7,
			 	                                        Data_Block.BlockAll,
			 	                                        Access_Condition.NotAllowed,
			 	                                        Access_Condition.NotAllowed,
			 	                                        Access_Condition.NotAllowed,
			 	                                        Access_Condition.NotAllowed)
			 });
		
		private ObservableCollection<MifareClassicSectorTreeViewModel> sectorTrailer_AccessBits = new ObservableCollection<MifareClassicSectorTreeViewModel>
			(new[]
			 {
			 	new MifareClassicSectorTreeViewModel(0,
			 	                                     Access_Condition.NotAllowed,
			 	                                     Access_Condition.Allowed_With_KeyA,
			 	                                     Access_Condition.Allowed_With_KeyA,
			 	                                     Access_Condition.NotAllowed,
			 	                                     Access_Condition.Allowed_With_KeyA,
			 	                                     Access_Condition.Allowed_With_KeyA),
			 	
			 	new MifareClassicSectorTreeViewModel(1,
			 	                                     Access_Condition.NotAllowed,
			 	                                     Access_Condition.Allowed_With_KeyB,
			 	                                     Access_Condition.Allowed_With_KeyA_Or_KeyB,
			 	                                     Access_Condition.NotAllowed,
			 	                                     Access_Condition.NotAllowed,
			 	                                     Access_Condition.Allowed_With_KeyB),
			 	
			 	new MifareClassicSectorTreeViewModel(2,
			 	                                     Access_Condition.NotAllowed,
			 	                                     Access_Condition.NotAllowed,
			 	                                     Access_Condition.Allowed_With_KeyA,
			 	                                     Access_Condition.NotAllowed,
			 	                                     Access_Condition.Allowed_With_KeyA,
			 	                                     Access_Condition.NotAllowed),
			 	
			 	new MifareClassicSectorTreeViewModel(3,
			 	                                     Access_Condition.NotAllowed,
			 	                                     Access_Condition.NotAllowed,
			 	                                     Access_Condition.Allowed_With_KeyA_Or_KeyB,
			 	                                     Access_Condition.NotAllowed,
			 	                                     Access_Condition.NotAllowed,
			 	                                     Access_Condition.NotAllowed),
			 	
			 	new MifareClassicSectorTreeViewModel(4,
			 	                                     Access_Condition.NotAllowed,
			 	                                     Access_Condition.Allowed_With_KeyA,
			 	                                     Access_Condition.Allowed_With_KeyA,
			 	                                     Access_Condition.Allowed_With_KeyA,
			 	                                     Access_Condition.Allowed_With_KeyA,
			 	                                     Access_Condition.Allowed_With_KeyA),
			 	
			 	new MifareClassicSectorTreeViewModel(5,
			 	                                     Access_Condition.NotAllowed,
			 	                                     Access_Condition.NotAllowed,
			 	                                     Access_Condition.Allowed_With_KeyA_Or_KeyB,
			 	                                     Access_Condition.Allowed_With_KeyB,
			 	                                     Access_Condition.NotAllowed,
			 	                                     Access_Condition.NotAllowed),
			 	
			 	new MifareClassicSectorTreeViewModel(6,
			 	                                     Access_Condition.NotAllowed,
			 	                                     Access_Condition.Allowed_With_KeyB,
			 	                                     Access_Condition.Allowed_With_KeyA_Or_KeyB,
			 	                                     Access_Condition.Allowed_With_KeyB,
			 	                                     Access_Condition.NotAllowed,
			 	                                     Access_Condition.Allowed_With_KeyB),
			 	
			 	new MifareClassicSectorTreeViewModel(7,
			 	                                     Access_Condition.NotAllowed,
			 	                                     Access_Condition.NotAllowed,
			 	                                     Access_Condition.Allowed_With_KeyA_Or_KeyB,
			 	                                     Access_Condition.NotAllowed,
			 	                                     Access_Condition.NotAllowed,
			 	                                     Access_Condition.NotAllowed)
			 });
		//private MifareClassicAccessBitsBaseModel baseModel = new MifareClassicAccessBitsBaseModel();
		
		private MifareClassicSectorTreeViewModel sector;
		private MifareClassicDataBlockTreeViewModel dataBlock0;
		private MifareClassicDataBlockTreeViewModel dataBlock1;
		private MifareClassicDataBlockTreeViewModel dataBlock2;
		private MifareClassicDataBlockTreeViewModel dataBlockCombined;
		
		private DatabaseReaderWriter databaseReaderWriter;
		private SettingsReaderWriter settings = new SettingsReaderWriter();
		
		private int keySetupTaskIndex;
		
		public object ViewModelContext { get; set; }
		
		#endregion
		
		public MifareClassicSetupViewModel(object treeViewViewModel)
		{
			DataBlockIsCombinedToggleButtonIsChecked = true;
			databaseReaderWriter = new DatabaseReaderWriter();
			ViewModelContext = treeViewViewModel;
			
			sector = new MifareClassicSectorTreeViewModel();
			
			sector.DataBlock.Add(new MifareClassicDataBlockTreeViewModel(0, Data_Block.Block0,Access_Condition.Allowed_With_KeyA_Or_KeyB, Access_Condition.Allowed_With_KeyA_Or_KeyB, Access_Condition.Allowed_With_KeyA_Or_KeyB,Access_Condition.Allowed_With_KeyA_Or_KeyB));
			sector.DataBlock.Add(new MifareClassicDataBlockTreeViewModel(0, Data_Block.Block1,Access_Condition.Allowed_With_KeyA_Or_KeyB, Access_Condition.Allowed_With_KeyA_Or_KeyB, Access_Condition.Allowed_With_KeyA_Or_KeyB,Access_Condition.Allowed_With_KeyA_Or_KeyB));
			sector.DataBlock.Add(new MifareClassicDataBlockTreeViewModel(0, Data_Block.Block2,Access_Condition.Allowed_With_KeyA_Or_KeyB, Access_Condition.Allowed_With_KeyA_Or_KeyB, Access_Condition.Allowed_With_KeyA_Or_KeyB,Access_Condition.Allowed_With_KeyA_Or_KeyB));
			sector.DataBlock.Add(new MifareClassicDataBlockTreeViewModel(0, Data_Block.BlockAll,Access_Condition.Allowed_With_KeyA_Or_KeyB, Access_Condition.Allowed_With_KeyA_Or_KeyB, Access_Condition.Allowed_With_KeyA_Or_KeyB,Access_Condition.Allowed_With_KeyA_Or_KeyB));
			
			sector.AccessBitsAsString = settings.DefaultSpecification.MifareClassicDefaultSectorTrailer;
			
			//Selected_DataBlock_AccessCondition = dataBlock_AccessCondition;
			//Selected_Sector_AccessCondition = sector_AccessCondition;
			
			//SectorTrailer = "FFFFFFFFFFFF,FF0780C3,FFFFFFFFFFFF";
			IsValidSectorTrailer = true;
			
			ClassicKeyAKeyCurrent = settings.DefaultSpecification.MifareClassicDefaultSecuritySettings[0].AccessBits.Split(new[] {',',';'})[0];
			ClassicKeyBKeyCurrent = settings.DefaultSpecification.MifareClassicDefaultSecuritySettings[0].AccessBits.Split(new[] {',',';'})[2];
			
			IsValidClassicKeyAKeyTarget = true;
			IsValidClassicKeyBKeyTarget = true;
			
			IsValidClassicKeyAKeyTargetRetyped = true;
			IsValidClassicKeyBKeyTargetRetyped = true;
			
			SelectedKeySetupTaskIndex = "0";
			
			this.IsModal = true;
		}
		
		#region AccessBitsTab
		
		/// <summary>
		/// 
		/// </summary>
		public Data_Block Selected_DataBlockType
		{
			get { return selected_DataBlockType; }
			set {
				
				selected_DataBlockType = value;
				RaisePropertyChanged("Selected_DataBlock_AccessCondition");
				RaisePropertyChanged("Selected_DataBlockType");
			}
		} private Data_Block selected_DataBlockType;
		
		/// <summary>
		/// 
		/// </summary>
		public Task_Type SelectedAccessBitsTaskType {
			get
			{
				//classicKeyAKeyCurrent = SectorTrailer.Split(',')[0];
				return selectedAccessBitsTaskType;
			}
			set
			{
				selectedAccessBitsTaskType = value;
				switch(value)
				{
					case Task_Type.None:
						IsClassicKeyEditingEnabled = false;
						IsClassicAuthInfoEnabled = true;
						break;
					case Task_Type.Add:
						IsClassicKeyEditingEnabled = false;
						IsClassicAuthInfoEnabled = false;
						break;
					case Task_Type.Edit:
						IsClassicKeyEditingEnabled = true;
						IsClassicAuthInfoEnabled = true;
						break;
					case Task_Type.Authenticate:
						IsClassicAuthInfoEnabled = true;
						IsClassicKeyEditingEnabled = false;
						break;
					case Task_Type.ChangeDefault:
						IsClassicKeyEditingEnabled = true;
						IsClassicAuthInfoEnabled = true;
						break;
				}
				RaisePropertyChanged("SelectedAccessBitsTaskType");
			}
		} private Task_Type selectedAccessBitsTaskType;
		
		/// <summary>
		/// 
		/// </summary>
		public string SelectedAccessBitsTaskIndex {
			get
			{
				//classicKeyAKeyCurrent = SectorTrailer.Split(',')[0];
				return selectedAccessBitsTaskIndex;
			}
			set
			{
				selectedAccessBitsTaskIndex = value;
			}
		} private string selectedAccessBitsTaskIndex;
		
		/// <summary>
		/// 
		/// </summary>
		public string SelectedAccessBitsTaskDescription {
			get
			{
				//classicKeyAKeyCurrent = SectorTrailer.Split(',')[0];
				return selectedAccessBitsTaskDescription;
			}
			set
			{
				selectedAccessBitsTaskDescription = value;
			}
		} private string selectedAccessBitsTaskDescription;
		
		/// <summary>
		/// 
		/// </summary>
		public bool IsValidSelectedAccessBitsTaskIndex {
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
		} private bool isValidSelectedAccessBitsTaskIndex;
		
		/// <summary>
		/// 
		/// </summary>
		public ObservableCollection<MifareClassicSectorTreeViewModel> SectorTrailerSource {
			get { return sectorTrailer_AccessBits; }
		}
		
		/// <summary>
		/// 
		/// </summary>
		public ObservableCollection<MifareClassicDataBlockTreeViewModel> DataBlockSource {
			get {
				
				return dataBlock_AccessBits;
			}
		}
		
		/// <summary>
		/// 
		/// </summary>
		public MifareClassicDataBlockTreeViewModel Selected_DataBlock_AccessCondition
		{
			get {
				switch(Selected_DataBlockType)
				{
					case Data_Block.Block0:
						return dataBlock0;
						break;
					case Data_Block.Block1:
						return dataBlock1;
						break;
					case Data_Block.Block2:
						return dataBlock2;
						break;
					default:
						return dataBlockCombined;
						break;
				}
			}
			
			set {
				switch(Selected_DataBlockType)
				{
					case Data_Block.Block0:
						dataBlock0 = value;
						sector.DataBlock[0].Cx = value.Cx;
						sector.DataBlock[0].Read_DataBlock = value.Read_DataBlock;
						sector.DataBlock[0].Write_DataBlock = value.Write_DataBlock;
						sector.DataBlock[0].Increment_DataBlock = value.Increment_DataBlock;
						sector.DataBlock[0].Decrement_DataBlock = value.Decrement_DataBlock;
						break;
					case Data_Block.Block1:
						dataBlock1 = value;
						sector.DataBlock[1].Cx = value.Cx;
						sector.DataBlock[1].Read_DataBlock = value.Read_DataBlock;
						sector.DataBlock[1].Write_DataBlock = value.Write_DataBlock;
						sector.DataBlock[1].Increment_DataBlock = value.Increment_DataBlock;
						sector.DataBlock[1].Decrement_DataBlock = value.Decrement_DataBlock;
						break;
					case Data_Block.Block2:
						dataBlock2 = value;
						sector.DataBlock[2].Cx = value.Cx;
						sector.DataBlock[2].Read_DataBlock = value.Read_DataBlock;
						sector.DataBlock[2].Write_DataBlock = value.Write_DataBlock;
						sector.DataBlock[2].Increment_DataBlock = value.Increment_DataBlock;
						sector.DataBlock[2].Decrement_DataBlock = value.Decrement_DataBlock;
						break;
					default:
						dataBlockCombined = value;
						
						dataBlock0 = value;
						sector.DataBlock[0].Cx = value.Cx;
						sector.DataBlock[0].Read_DataBlock = value.Read_DataBlock;
						sector.DataBlock[0].Write_DataBlock = value.Write_DataBlock;
						sector.DataBlock[0].Increment_DataBlock = value.Increment_DataBlock;
						sector.DataBlock[0].Decrement_DataBlock = value.Decrement_DataBlock;

						dataBlock1 = value;
						sector.DataBlock[1].Cx = value.Cx;
						sector.DataBlock[1].Read_DataBlock = value.Read_DataBlock;
						sector.DataBlock[1].Write_DataBlock = value.Write_DataBlock;
						sector.DataBlock[1].Increment_DataBlock = value.Increment_DataBlock;
						sector.DataBlock[1].Decrement_DataBlock = value.Decrement_DataBlock;

						dataBlock2 = value;
						sector.DataBlock[2].Cx = value.Cx;
						sector.DataBlock[2].Read_DataBlock = value.Read_DataBlock;
						sector.DataBlock[2].Write_DataBlock = value.Write_DataBlock;
						sector.DataBlock[2].Increment_DataBlock = value.Increment_DataBlock;
						sector.DataBlock[2].Decrement_DataBlock = value.Decrement_DataBlock;
						break;
				}

				encodeSectorTrailer(ref sector);
				
				RaisePropertyChanged("Selected_DataBlock_AccessCondition");
				RaisePropertyChanged("SectorTrailer");
			}
		}
		
		/// <summary>
		/// 
		/// </summary>
		public MifareClassicSectorTreeViewModel Selected_Sector_AccessCondition
		{
			get {
				return selected_Sector_AccessCondition;
			}
			set {
				//TODO: create sector string
				selected_Sector_AccessCondition = value;
				
				sector.Read_Access_Condition = value.Read_Access_Condition;
				sector.Write_Access_Condition = value.Write_Access_Condition;
				sector.Read_KeyA = value.Read_KeyA;
				sector.Write_KeyA = value.Write_KeyA;
				sector.Read_KeyB = value.Read_KeyB;
				sector.Write_KeyB = value.Write_KeyB;
				sector.Cx = value.Cx;
				
				encodeSectorTrailer(ref sector);
				
				RaisePropertyChanged("Selected_Sector_AccessCondition");
				RaisePropertyChanged("SectorTrailer");
			}
		} private MifareClassicSectorTreeViewModel selected_Sector_AccessCondition;
		
		/// <summary>
		/// 
		/// </summary>
		public bool IsValidSectorTrailer {
			get { return isValidSectorTrailer; }
			set {
				isValidSectorTrailer = value;
				RaisePropertyChanged("IsValidSectorTrailer");
			}
		} private bool isValidSectorTrailer;
		
		/// <summary>
		/// 
		/// </summary>
		public string SectorTrailer {
			get {
				return sector.AccessBitsAsString; //CustomConverter.encodeSectorTrailer(sector);
			}
			set{
				sector.AccessBitsAsString = value.ToUpper();
				IsValidSectorTrailer = !decodeSectorTrailer(sector.AccessBitsAsString, ref selected_Sector_AccessCondition);
				if(!IsValidSectorTrailer)
					return;
				RaisePropertyChanged("Selected_Sector_AccessCondition");
				RaisePropertyChanged("Selected_DataBlock_AccessCondition");
			}
		} private string sectorTrailer;

		#endregion
		
		#region KeySetup
		
		/// <summary>
		/// 
		/// </summary>
		public bool IsClassicAuthInfoEnabled {
			get { return isClassicAuthInfoEnabled; }
			set {
				isClassicAuthInfoEnabled = value;
				RaisePropertyChanged("IsClassicAuthInfoEnabled");
			}
		} private bool isClassicAuthInfoEnabled = false;
		
		/// <summary>
		/// 
		/// </summary>
		public bool IsClassicKeyEditingEnabled {
			get { return isClassicKeyEditingEnabled; }
			set {
				isClassicKeyEditingEnabled = value;
				RaisePropertyChanged("IsClassicKeyEditingEnabled");
			}
		} private bool isClassicKeyEditingEnabled;
		
		/// <summary>
		/// 
		/// </summary>
		public Task_Type SelectedKeySetupTaskType {
			get
			{
				//classicKeyAKeyCurrent = SectorTrailer.Split(',')[0];
				return selectedKeySetupTaskType;
			}
			set
			{
				selectedKeySetupTaskType = value;
				RaisePropertyChanged("SelectedKeySetupTaskType");
			}
		} private Task_Type selectedKeySetupTaskType;
		
		/// <summary>
		/// 
		/// </summary>
		public string SelectedKeySetupTaskIndex {
			get
			{
				//classicKeyAKeyCurrent = SectorTrailer.Split(',')[0];
				return selectedKeySetupTaskIndex;
			}
			set
			{
				selectedKeySetupTaskIndex = value;
				IsValidSelectedKeySetupTaskIndex = int.TryParse(value, out keySetupTaskIndex);
				RaisePropertyChanged("SelectedKeySetupTaskIndex");
			}
		} private string selectedKeySetupTaskIndex;
		
		/// <summary>
		/// 
		/// </summary>
		public string SelectedKeySetupTaskDescription {
			get
			{
				//classicKeyAKeyCurrent = SectorTrailer.Split(',')[0];
				return selectedKeySetupTaskDescription;
			}
			set
			{
				selectedKeySetupTaskDescription = value;
				RaisePropertyChanged("SelectedKeySetupTaskDescription");
			}
		} private string selectedKeySetupTaskDescription;
		
		/// <summary>
		/// 
		/// </summary>
		public bool IsValidSelectedKeySetupTaskIndex {
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
		} private bool isValidSelectedKeySetupTaskIndex;
		
		/// <summary>
		/// 
		/// </summary>
		public string ClassicKeyAKeyTargetRetyped {
			get
			{
				//classicKeyAKeyCurrent = SectorTrailer.Split(',')[0];
				return classicKeyAKeyTargetRetyped;
			}
			set
			{
				classicKeyAKeyTargetRetyped = value.ToUpper();
				IsValidClassicKeyAKeyTargetRetyped = (CustomConverter.IsInHexFormat(value) && value.Length == 12);
				RaisePropertyChanged("ClassicKeyAKeyTargetRetyped");
			}
		} private string classicKeyAKeyTargetRetyped;
		
		/// <summary>
		/// 
		/// </summary>
		public bool IsValidClassicKeyAKeyTargetRetyped {
			get
			{
				return isValidClassicKeyAKeyTargetRetyped;
			}
			set
			{
				isValidClassicKeyAKeyTargetRetyped = value;
				RaisePropertyChanged("IsValidClassicKeyAKeyTargetRetyped");
			}
		} private bool isValidClassicKeyAKeyTargetRetyped;
		
		/// <summary>
		/// 
		/// </summary>
		public string ClassicKeyBKeyTargetRetyped {
			get
			{
				//classicKeyAKeyCurrent = SectorTrailer.Split(',')[0];
				return classicKeyBKeyTargetRetyped;
			}
			set
			{
				classicKeyBKeyTargetRetyped = value.ToUpper();
				IsValidClassicKeyBKeyTargetRetyped = (CustomConverter.IsInHexFormat(value) && value.Length == 12);
				RaisePropertyChanged("ClassicKeyBKeyTargetRetyped");
			}
		} private string classicKeyBKeyTargetRetyped;
		
		/// <summary>
		/// 
		/// </summary>
		public bool IsValidClassicKeyBKeyTargetRetyped {
			get
			{
				return isValidClassicKeyBKeyTargetRetyped;
			}
			set
			{
				isValidClassicKeyBKeyTargetRetyped = value;
				RaisePropertyChanged("IsValidClassicKeyBKeyTargetRetyped");
			}
		} private bool isValidClassicKeyBKeyTargetRetyped;
		
		/// <summary>
		/// 
		/// </summary>
		public string ClassicKeyAKeyCurrent {
			get
			{
				return classicKeyAKeyCurrent;
			}
			set
			{
				classicKeyAKeyCurrent = value.ToUpper();
				
				IsValidClassicKeyAKeyCurrent = (CustomConverter.IsInHexFormat(value) && value.Length == 12);
				if(IsValidClassicKeyAKeyCurrent && SelectedAccessBitsTaskType == Task_Type.ChangeDefault)
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
				
				RaisePropertyChanged("ClassicKeyAKeyCurrent");
			}
		} private string classicKeyAKeyCurrent;
		
		/// <summary>
		/// 
		/// </summary>
		public bool IsValidClassicKeyAKeyCurrent {
			get
			{
				return isValidClassicKeyAKeyCurrent;
			}
			set
			{
				isValidClassicKeyAKeyCurrent = value;
				RaisePropertyChanged("IsValidClassicKeyAKeyCurrent");
			}
		} private bool isValidClassicKeyAKeyCurrent;
		
		/// <summary>
		/// 
		/// </summary>
		public string ClassicKeyAKeyTarget {
			get
			{ return classicKeyAKeyTarget; }
			set
			{
				classicKeyAKeyTarget = value;
				IsValidClassicKeyAKeyTarget = (CustomConverter.IsInHexFormat(value) && value.Length == 12);
				RaisePropertyChanged("ClassicKeyAKeyTarget");
			}
		} private string classicKeyAKeyTarget;

		/// <summary>
		/// 
		/// </summary>
		public bool IsValidClassicKeyAKeyTarget {
			get
			{
				return isValidClassicKeyAKeyTarget;
			}
			set
			{
				isValidClassicKeyAKeyTarget = value;
				RaisePropertyChanged("IsValidClassicKeyAKeyTarget");
			}
		} private bool isValidClassicKeyAKeyTarget;
		
		/// <summary>
		/// 
		/// </summary>
		public string ClassicKeyBKeyCurrent {
			get
			{
				return classicKeyBKeyCurrent;
			}
			set
			{
				classicKeyBKeyCurrent = value.ToUpper();
				
				IsValidClassicKeyBKeyCurrent = (CustomConverter.IsInHexFormat(value) && value.Length == 12);
				if(IsValidClassicKeyBKeyCurrent && SelectedAccessBitsTaskType == Task_Type.ChangeDefault)
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
				
				RaisePropertyChanged("ClassicKeyBKeyCurrent");
			}
		} private string classicKeyBKeyCurrent;
		
		/// <summary>
		/// 
		/// </summary>
		public bool IsValidClassicKeyBKeyCurrent {
			get
			{
				return isValidClassicKeyBKeyCurrent;
			}
			set
			{
				isValidClassicKeyBKeyCurrent = value;
				RaisePropertyChanged("IsValidClassicKeyBKeyCurrent");
			}
		} private bool isValidClassicKeyBKeyCurrent;
		
		/// <summary>
		/// 
		/// </summary>
		public string ClassicKeyBKeyTarget {
			get
			{ return classicKeyBKeyTarget; }
			set
			{
				classicKeyBKeyTarget = value;
				IsValidClassicKeyBKeyTarget = (CustomConverter.IsInHexFormat(value) && value.Length == 12);
				RaisePropertyChanged("ClassicKeyBKeyTarget");
			}
		} private string classicKeyBKeyTarget;
		
		/// <summary>
		/// 
		/// </summary>
		public bool IsValidClassicKeyBKeyTarget {
			get
			{
				return isValidClassicKeyBKeyTarget;
			}
			set
			{
				isValidClassicKeyBKeyTarget = value;
				RaisePropertyChanged("IsValidClassicKeyBKeyTarget");
			}
		} private bool isValidClassicKeyBKeyTarget;
		
		/// <summary>
		/// 
		/// </summary>
		public KeyType_MifareClassicKeyType SelectedClassicKeyANumberCurrent
		{
			get { return selectedClassicKeyANumberCurrent; }
			set
			{
				selectedClassicKeyANumberCurrent = value;
				ClassicKeyAKeyCurrent = settings.DefaultSpecification.MifareClassicDefaultSecuritySettings.
					Where(x => x.KeyType == SelectedClassicKeyANumberCurrent).
					Single().AccessBits.Split(new[] {',',';'})[0];
				
				RaisePropertyChanged("SelectedClassicKeyANumberCurrent");
			}
		} private KeyType_MifareClassicKeyType selectedClassicKeyANumberCurrent;
		
		/// <summary>
		/// 
		/// </summary>
		public KeyType_MifareClassicKeyType SelectedClassicKeyBNumberCurrent
		{
			get { return selectedClassicKeyBNumberCurrent; }
			set
			{
				selectedClassicKeyBNumberCurrent = value;
				
				ClassicKeyBKeyCurrent = settings.DefaultSpecification.MifareClassicDefaultSecuritySettings.
					Where(x => x.KeyType == SelectedClassicKeyBNumberCurrent).
					Single().AccessBits.Split(new[] {',',';'})[2];
				
				
				RaisePropertyChanged("SelectedClassicKeyBNumberCurrent");
			}
		} private KeyType_MifareClassicKeyType selectedClassicKeyBNumberCurrent;
		
		/// <summary>
		/// 
		/// </summary>
		public KeyType_MifareClassicKeyType SelectedClassicKeyANumberTarget
		{
			get { return selectedClassicKeyANumberTarget; }
			set
			{
				selectedClassicKeyANumberTarget = value;
				RaisePropertyChanged("SelectedClassicKeyANumberTarget");
			}
		} private KeyType_MifareClassicKeyType selectedClassicKeyANumberTarget;
		
		/// <summary>
		/// 
		/// </summary>
		public KeyType_MifareClassicKeyType SelectedClassicKeyBNumberTarget
		{
			get { return selectedClassicKeyBNumberTarget; }
			set
			{
				selectedClassicKeyBNumberTarget = value;
				RaisePropertyChanged("SelectedClassicKeyBNumberTarget");
			}
		} private KeyType_MifareClassicKeyType selectedClassicKeyBNumberTarget;
		
		/// <summary>
		/// 
		/// </summary>
		public KeyType_MifareClassicKeyType SelectedClassicSectorCurrent
		{
			get { return selectedClassicSectorCurrent; }
			set
			{
				selectedClassicSectorCurrent = value;
				RaisePropertyChanged("SelectedClassicSectorCurrent");
			}
		} private KeyType_MifareClassicKeyType selectedClassicSectorCurrent;
		
		public bool DataBlockIsCombinedToggleButtonIsChecked {
			get { return dataBlockIsCombinedToggleButtonIsChecked; }
			set {
				dataBlockIsCombinedToggleButtonIsChecked = value;

				if(value)
					Selected_DataBlockType = Data_Block.BlockAll;
				
				RaisePropertyChanged("SelectedDataBlockItem");
				RaisePropertyChanged("DataBlockIsCombinedToggleButtonIsChecked");
				RaisePropertyChanged("DataBlockSelectionComboBoxIsEnabled");
				RaisePropertyChanged("DataBlockSelection");
			}
		} private bool dataBlockIsCombinedToggleButtonIsChecked;
		
		#endregion

		#region General Properties
		
		public string StatusText
		{
			get { return statusText; }
			set {
				statusText = value;
				RaisePropertyChanged("StatusText");
			}
		} private string statusText;
		
		public SettingsReaderWriter Settings
		{
			get { return settings; }
		}
		
		public int KeySelectionComboboxIndex {
			get {
				if (ViewModelContext is TreeViewChildNodeViewModel)
					return (ViewModelContext as TreeViewChildNodeViewModel).sectorModel.mifareClassicSectorNumber;
				else
					return selectedKeyNumber;
			}
			set {
				selectedKeyNumber = value;
				RaisePropertyChanged("KeySelectionComboboxIndex");
				RaisePropertyChanged("selectedClassicKeyAKey");
				RaisePropertyChanged("selectedClassicKeyBKey");
			}
		} private int selectedKeyNumber;
		
		public bool DataBlockSelectionComboBoxIsEnabled {
			get { return !dataBlockIsCombinedToggleButtonIsChecked; }
		}
		
		public bool? IsFixedKeyNumber {
			get {
				if (ViewModelContext is TreeViewChildNodeViewModel)
					return false;
				else
					return true;
			}
		}
		
		#endregion
		
		#region Commands
		
		public ICommand AuthenticateCommand { get { return new RelayCommand(OnNewAuthenticateCommand); } }
		private void OnNewAuthenticateCommand() {
			
			using ( RFiDDevice device = new RFiDDevice(settings.DefaultSpecification.DefaultReaderProvider))
			{

				StatusText = "";
				
				Mouse.OverrideCursor = Cursors.Wait;

				
				if(device.ReadMiFareClassicSingleSector((int)SelectedClassicSectorCurrent, ClassicKeyAKeyCurrent, ClassicKeyBKeyCurrent) == ERROR.NoError)
				{
					StatusText = string.Format("{0}: Connection to Reader successfully established\n", DateTime.Now);
					
					
					foreach(bool b in device.DataBlockSuccesfullyAuth)
					{
						if(b)
							StatusText = StatusText + string.Format("{0}: Error\n", DateTime.Now);
						else
							StatusText = StatusText + string.Format("{0}: Success\n", DateTime.Now);
					}
					
				}

				else
					StatusText = "Unable to Auth";
				
				Mouse.OverrideCursor = Cursors.Arrow;
			}
		}
		
		#endregion
		
		
		#region IUserDialogViewModel Implementation

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
		
		public ICommand ExitCommand { get { return new RelayCommand(Cancel); } }
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
		
		public Action<MifareClassicSetupViewModel> OnOk { get; set; }
		public Action<MifareClassicSetupViewModel> OnCancel { get; set; }
		public Action<MifareClassicSetupViewModel> OnAuth { get; set; }
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

		private string _Caption;
		public string Caption {
			get { return _Caption; }
			set {
				_Caption = value;
				RaisePropertyChanged("Caption");
			}
		}
		
		#endregion
		
		#region Extensions
		
		/// <summary>
		/// turns a given byte or string sector trailer to a access bits selection
		/// </summary>
		/// <param name="st"></param>
		/// <param name="_sector"></param>
		/// <param name="_keyA"></param>
		/// <param name="_keyB"></param>
		/// <returns></returns>
		private bool decodeSectorTrailer(byte[] st, ref MifareClassicSectorTreeViewModel _sector)
		{
			uint C1x,C2x;
			
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
			
			if(C1x == 4)
				isTransportConfiguration = true;
			else
				isTransportConfiguration = false;
			
			_sector = sectorTrailer_AccessBits[(int)C1x];
			
			//sector_AccessCondition = new List<Access_Condition>(sab.d_sector_trailer_access_bits.c1,sab.d_sector_trailer_access_bits.c2, sab.d_sector_trailer_access_bits.c3);
			
			//sector.Sector_AccessCondition = sector_AccessCondition;
			//decodedSectorTrailerAccessBits = sectorTrailerAB[C1x];
			
			#endregion

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
			#endregion

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

			#endregion
			
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
			
			if(dataBlock0 == dataBlock1 && dataBlock1 == dataBlock2)
			{
				dataBlockCombined = dataBlock0;
				Selected_DataBlockType = Data_Block.BlockAll;
				DataBlockIsCombinedToggleButtonIsChecked = true;
			}
			else
			{
				Selected_DataBlockType = Data_Block.Block0;
				DataBlockIsCombinedToggleButtonIsChecked = false;
			}

			#endregion
			return false;
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="st"></param>
		/// <param name="_sector"></param>
		/// <returns></returns>
		private bool decodeSectorTrailer(string st, ref MifareClassicSectorTreeViewModel _sector)
		{
			
			byte[] _bytes = new byte[255];
			int discarded = 0;
			
			string[] sectorTrailer = st.Split(new[] {',',';'});
			if(sectorTrailer.Count() != 3 ||
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
		private bool encodeSectorTrailer(ref MifareClassicSectorTreeViewModel _sector)
		{
			byte[] st = new byte[4] { 0x00, 0x00, 0x00, 0xC3};
			
			uint sectorAccessBitsIndex = (uint)_sector.Cx; //_sector.Sector_AccessCondition.Cx;
			uint dataBlock0AccessBitsIndex = (uint)_sector.DataBlock[0].Cx; //(uint)dataBlock_AccessBits.IndexOf(_sector); //_sector.DataBlock0_AccessCondition.Cx;
			uint dataBlock1AccessBitsIndex = (uint)_sector.DataBlock[1].Cx; //_sector.DataBlock1_AccessCondition.Cx;
			uint dataBlock2AccessBitsIndex = (uint)_sector.DataBlock[2].Cx; //_sector.DataBlock2_AccessCondition.Cx;

			// DataBlock 0 = C1/0; C2/0; C3/0
			
			st[1] |= (byte)((dataBlock0AccessBitsIndex & 0x01) << 4); 	// C1/0
			st[2] |= (byte)((dataBlock0AccessBitsIndex & 0x02) >> 1);	// C2/0
			st[2] |= (byte)((dataBlock0AccessBitsIndex & 0x04) << 2);	// C3/0
			
			
			// DataBlock 1 = C1/1; C2/1; C3/1
			
			st[1] |= (byte)((dataBlock1AccessBitsIndex & 0x01) << 5); 	// C1/1
			st[2] |= (byte)(dataBlock1AccessBitsIndex & 0x02);			// C2/1
			st[2] |= (byte)((dataBlock1AccessBitsIndex & 0x04) << 3);	// C3/1

			
			// DataBlock 2 = C1/2; C2/2; C3/2
			
			st[1] |= (byte)((dataBlock2AccessBitsIndex & 0x01) << 6); 	// C1/2
			st[2] |= (byte)((dataBlock2AccessBitsIndex & 0x02) << 1);	// C2/2
			st[2] |= (byte)((dataBlock2AccessBitsIndex & 0x04) << 4);	// C3/2

			
			// SectorAccessBits = C1/3; C2/3; C3/3
			
			st[1] |= (byte)((sectorAccessBitsIndex & 0x01) << 7); 	// C1/3
			st[2] |= (byte)((sectorAccessBitsIndex & 0x02) << 2);	// C2/3
			st[2] |= (byte)((sectorAccessBitsIndex & 0x04) << 5);	// C3/3

			st = CustomConverter.buildSectorTrailerInvNibble(st);
			
			string[] stAsString = _sector.AccessBitsAsString.Split(new[] {',',';'});
			stAsString[1] = CustomConverter.HexToString(st);
			_sector.AccessBitsAsString = string.Join(",",stAsString);
			return false;
		}
		#endregion
	}
}
