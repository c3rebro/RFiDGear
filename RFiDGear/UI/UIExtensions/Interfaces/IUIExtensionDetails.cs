using System.ComponentModel.Composition;

namespace RFiDGear.UI.UIExtensions.Interfaces
{

    public interface IUIExtensionDetails
    {

        string Category { get; }
        string IconUri { get; }
        string Name { get; }
        string Uri { get; }

        int SortOrder { get; }
    }

}

