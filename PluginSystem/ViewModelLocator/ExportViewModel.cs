using System;
using System.ComponentModel.Composition;

namespace MefMvvm.SharedContracts.ViewModel
{

    [MetadataAttribute()]
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class ExportViewModel : ExportAttribute
    {

        public ExportViewModel()
            : base("ViewModel")
        {
        }

        public ExportViewModel(string name, bool isStatic)
            : base("ViewModel")
        {
            this.Name = name;
        }

        private string _Name;
        public string Name
        {
            get { return _Name; }
            private set { _Name = value; }
        }

    }

}
