/*
 * Created by SharpDevelop.
 * Date: 04.11.2017
 * Time: 10:13
 *
 */

using CommunityToolkit.Mvvm.ComponentModel;
using RFiDGear.ViewModel.TaskSetupViewModels;

using System;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Xml.Serialization;

namespace RFiDGear.Model
{
    /// <summary>
    /// Description of ChipTaskHandlerModel.
    /// </summary>
    [XmlInclude(typeof(MifareDesfireSetupViewModel))]
    [XmlInclude(typeof(MifareClassicSetupViewModel))]
    [XmlInclude(typeof(CommonTaskViewModel))]
    [XmlInclude(typeof(GenericChipTaskViewModel))]
    public class ChipTaskHandlerModel : ObservableObject
    {
        private readonly Version Version = Assembly.GetExecutingAssembly().GetName().Version;

        public ChipTaskHandlerModel()
        {
            TaskCollection = new ObservableCollection<IGenericTaskModel>();
            ManifestVersion = string.Format("{0}.{1}.{2}", Version.Major, Version.Minor, Version.Build);
        }

        public Type GetTaskType(int _index) { return (TaskCollection != null && TaskCollection.Count > 0) ? TaskCollection[_index].GetType() : null; }
        public Type GetTaskType(IGenericTaskModel _object) { return (TaskCollection != null && TaskCollection.Count > 0) ? TaskCollection[TaskCollection.IndexOf(_object)].GetType() : null; }

        /// <summary>
        ///
        /// </summary>
        public string ManifestVersion { get; set; }

        /// <summary>
        /// Exposing all Tasks as SetupViewModel
        /// </summary>
        public ObservableCollection<IGenericTaskModel> TaskCollection
        {
            get => taskCollection;
            set
            {
                taskCollection = value;
                
                OnPropertyChanged(nameof(TaskCollection));
            }
        }

        private ObservableCollection<IGenericTaskModel> taskCollection;
    }
}
