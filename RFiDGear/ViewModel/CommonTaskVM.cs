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
using LibLogicalAccess;
using RFiDGear.DataAccessLayer;
using RFiDGear.Model;
using RFiDGear.ViewModel;
using System;
using System.ComponentModel.Composition;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Xml.Serialization;

namespace RFiDGear.ViewModel
{
	/// <summary>
	/// Description of CommonTaskVM.
	/// </summary>
	public class CommonTaskVM
	{
		private protected SettingsReaderWriter settings = new SettingsReaderWriter();

		public ERROR TaskErr { get; set; }

		#region general props

		/// <summary>
		/// 
		/// </summary>
		public SettingsReaderWriter Settings
		{
			get { return settings; }
		}

		/// <summary>
		///
		/// </summary>
		TaskType_MifareDesfireTask SelectedTaskType
        {
            get => selectedAccessBitsTaskType;
            set
            {
                selectedAccessBitsTaskType = value;
            }
        }
        private TaskType_MifareDesfireTask selectedAccessBitsTaskType;

		/// <summary>
		///
		/// </summary>
		public string SelectedTaskIndex
		{
			get
			{
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
			}
		}
		private bool? isValidSelectedTaskIndex;

		/// <summary>
		///
		/// </summary>
		public string SelectedTaskDescription
		{
			get
			{
				//classicKeyAKeyCurrent = SectorTrailer.Split(',')[0];
				return selectedAccessBitsTaskDescription;
			}
			set
			{
				selectedAccessBitsTaskDescription = value;
			}
		}
		private string selectedAccessBitsTaskDescription;

		/// <summary>
		///
		/// </summary>
		public bool? IsTaskCompletedSuccessfully
		{
			get { return isTaskCompletedSuccessfully; }
			set
			{
				isTaskCompletedSuccessfully = value;
			}
		}
		private bool? isTaskCompletedSuccessfully;

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
			}
		}
		private bool isValidSelectedAccessBitsTaskIndex;

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
			}
		}
		private string statusText;

		#endregion general props

		#region Localization

		public string LocalizationResourceSet { get; set; }
		
		[XmlIgnore]
		public string Caption
		{
			get { return _Caption; }
			set
			{
				_Caption = value;
			}
		} private string _Caption;

		#endregion Localization
	}
}