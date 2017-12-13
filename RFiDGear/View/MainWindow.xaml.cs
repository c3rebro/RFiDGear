using RFiDGear.ViewModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace RFiDGear
{
	//	public delegate void TreeViewNodeMouseAction(object sender, TreeNodeMouseClickEventArgs e);

	/// <summary>
	/// Description of MainForm.
	/// </summary>
	///
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow
	{
		public MainWindow()
		{
			InitializeComponent();
		}

		private void WindowMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			DragMove();
		}
		
		private void OnAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
		{
			e.Column.Header = ((PropertyDescriptor)e.PropertyDescriptor).DisplayName;
		}

		private void MainWindowTreeViewControlMouseButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (sender != null)
			{
				TreeView item = sender as TreeView;

				DependencyObject dep = (DependencyObject)e.OriginalSource;
				while ((dep != null) && !(dep is TreeViewItem))
				{
					dep = VisualTreeHelper.GetParent(dep);
				}
				if (dep == null)
				{
					foreach (object o in item.Items)
					{
						if (o is TreeViewParentNodeViewModel)
						{
							foreach (TreeViewChildNodeViewModel child in (o as TreeViewParentNodeViewModel).Children)
							{
								child.IsSelected = false;

								if (child.Children != null)
								{
									foreach (TreeViewGrandChildNodeViewModel grandChild in child.Children)
										grandChild.IsSelected = false;
								}
							}

							(o as TreeViewParentNodeViewModel).IsSelected = false;
						}

					}
					return;
				}
			}
		}
	}
}