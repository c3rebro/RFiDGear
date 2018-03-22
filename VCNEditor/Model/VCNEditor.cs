using System;
using MefMvvm.SharedContracts;

namespace VCNEditor.Model
{
    [UiExtension(Name = "VCNEditor",
        Uri = "pack://application:,,,/VCNEditor;component/View/VCNEditorView.xaml",
        Category = "VCNEditor", SortOrder = 0)]
    public class VCNEditor : IUIExtension
    {
    
    }
}
