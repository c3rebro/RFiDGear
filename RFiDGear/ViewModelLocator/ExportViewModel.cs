using System;
using System.ComponentModel.Composition;
using System.Windows;

namespace MefMvvm.SharedContracts.ViewModel
{

    [MetadataAttribute()]
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class ExportViewModel : ExportAttribute
    {

        public ExportViewModel()
            : base("ViewModel")
        {
        }

        public ExportViewModel(string name, bool isStatic = false)
            : base("ViewModel")
        {
            Name = name;
        }

        private string _Name;
        public string Name
        {
            get => _Name;
            private set => _Name = value;
        }

    }

}
