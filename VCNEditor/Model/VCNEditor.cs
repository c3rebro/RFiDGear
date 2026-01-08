using RFiDGear.UI.UIExtensions;
using RFiDGear.UI.UIExtensions.Interfaces;

namespace VCNEditor.Model
{
    /// <summary>
    /// MEF-exported UI extension that registers the VCN editor view and points to the
    /// pack URI entry point for its XAML view.
    /// </summary>
    [UiExtension(Name = "VCNEditor",
        Uri = "pack://application:,,,/VCNEditor;component/View/VCNEditorView.xaml",
        Category = "VCNEditor", SortOrder = 0)]
    public class VCNEditor : IUIExtension
    {

    }
}
