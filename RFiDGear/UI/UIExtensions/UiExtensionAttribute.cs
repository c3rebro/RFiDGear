using System;
using System.ComponentModel.Composition;
using RFiDGear.UI.UIExtensions.Interfaces;

namespace RFiDGear.UI.UIExtensions
{

    [MetadataAttribute()]
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class UiExtensionAttribute : ExportAttribute
    {

        public UiExtensionAttribute()
            : base(typeof(IUIExtension))
        {
        }

        public string Category { get; set; }
        public string IconUri { get; set; }
        public string Name { get; set; }
        public int SortOrder { get; set; }
        public string Uri { get; set; }
    }

}

