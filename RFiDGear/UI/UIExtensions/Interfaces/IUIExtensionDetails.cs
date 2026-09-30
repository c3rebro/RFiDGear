using RFiDGear.UI.UIExtensions;

namespace RFiDGear.UI.UIExtensions.Interfaces
{

    public interface IUIExtensionDetails
    {

        string Category { get; }
        string IconUri { get; }
        string Name { get; }
        string Uri { get; }

        int SortOrder { get; }

        /// <summary>
        /// Host region the extension targets. Defaults to <see cref="UIExtensions.HostPlacement.CardTask"/>
        /// for extensions compiled against earlier versions of the attribute.
        /// </summary>
        HostPlacement HostPlacement { get; }
    }

}

