using RFiDGear.UI.UIExtensions;
using RFiDGear.UI.UIExtensions.Interfaces;

namespace VCNEditor.Model
{
    /// <summary>
    /// UI extension entry point for the VCN editor view.
    /// </summary>
    [UiExtension(Name = "VCNEditor",
        Uri = "pack://application:,,,/VCNEditor;component/View/VCNEditorView.xaml",
        Category = "VCNEditor", SortOrder = 0)]
    public class VCNEditor : IUIExtension
    {

    }
}
