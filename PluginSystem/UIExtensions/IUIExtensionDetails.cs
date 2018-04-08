using System.ComponentModel.Composition;

namespace MefMvvm.SharedContracts
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

