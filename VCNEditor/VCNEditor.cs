using System;
using VCNEditor.ViewModel;
using VCNEditor.View;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using PluginSystem;
using System.Windows;
using GalaSoft.MvvmLight;

namespace VCNEditor
{
	[Export(typeof(VCNEditor))]
	public class VCNEditor : ViewModelBase, IPluginBase
	{
		//private VCNEditorViewModel vm;
		private VCNEditorView view;
		
		public VCNEditor()
		{
			//Name = "VCNEditor";
			//MainPart = new VCNEditorView();
			//MainPart.DataContext = new VCNEditorViewModel(this);
			view = new VCNEditorView();
			view.Name = "VCNEditor";
			view.DataContext = new VCNEditorViewModel();
			
		}
		
		public FrameworkElement MainPart { get { return view;}}
		
		public string Name { get { return view.Name; }}
		
		/// <summary>
		/// Expose translated strings from ResourceLoader
		/// </summary>
		public string LocalizationResourceSet { get; set; }

	}
}
