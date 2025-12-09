using System;
using System.ComponentModel.Composition;
using System.Windows;

namespace RFiDGear.Infrastructure.DI
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class ImportProperty : ImportAttribute
    {

        public ImportProperty()
            : base("ViewModel")
        {
        }

        public ImportProperty(string name)
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
