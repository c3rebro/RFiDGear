using System.Collections.Generic;

namespace RFiDGear.Helpers.Selection.Interfaces
{
    /// <summary>
    /// Represents a selectable tree node for TreeView operations.
    /// </summary>
    public interface ITreeSelectionNode
    {
        bool IsSelected { get; set; }

        IEnumerable<ITreeSelectionNode> SelectionChildren { get; }
    }
}
