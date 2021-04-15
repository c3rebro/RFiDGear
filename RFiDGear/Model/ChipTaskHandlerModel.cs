/*
 * Created by SharpDevelop.
 * Date: 04.11.2017
 * Time: 10:13
 *
 */

using GalaSoft.MvvmLight;
using RFiDGear.ViewModel;

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
    [XmlInclude(typeof(CreateCommonTaskView))]
    public class ChipTaskHandlerModel : ViewModelBase
    {
        private Version Version = Assembly.GetExecutingAssembly().GetName().Version;

        public ChipTaskHandlerModel()
        {
            TaskCollection = new ObservableCollection<object>();
            ManifestVersion = string.Format("{0}.{1}.{2}", Version.Major, Version.Minor, Version.Build);
        }

        public Type GetTaskType(int _index = 0) { return (TaskCollection != null && TaskCollection.Count > 0) ? TaskCollection[_index].GetType() : null; }

        /// <summary>
        ///
        /// </summary>
        public string ManifestVersion { get; set; }

        /// <summary>
        /// Exposing all Tasks as SetupViewModel
        /// </summary>
        public ObservableCollection<object> TaskCollection
        {
            get { return taskCollection; }
            set
            {
                taskCollection = value;
                RaisePropertyChanged("TaskCollection");
            }
        }

        private ObservableCollection<object> taskCollection;
    }
}