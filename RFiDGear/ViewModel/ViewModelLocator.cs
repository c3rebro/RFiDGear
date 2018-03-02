/*
  In App.xaml:
  <Application.Resources>
      <vm:ViewModelLocator xmlns:vm="clr-namespace:RFiDGear"
                           x:Key="Locator" />
  </Application.Resources>

  In the View:
  DataContext="{Binding Source={StaticResource Locator}, Path=ViewModelName}"

  You can also use Blend to do all this with the tool's support.
  See http://www.galasoft.ch/mvvm
 */

using GalaSoft.MvvmLight.Ioc;
using CommonServiceLocator;
using PluginSystem;

namespace RFiDGear.ViewModel
{
	/// <summary>
	/// This class contains static references to all the view models in the
	/// application and provides an entry point for the bindings.
	/// </summary>
	public class ViewModelLocator
	{
		/// <summary>
		/// Initializes a new instance of the ViewModelLocator class.
		/// </summary>
		public ViewModelLocator()
		{
			ServiceLocator.SetLocatorProvider(() => SimpleIoc.Default);

			// Create run time view services and models
			//SimpleIoc.Default.Register<IDataService, DataService>();

			SimpleIoc.Default.Register<MainWindowViewModel>();
			//SimpleIoc.Default.Register<Messenger, MainWindowViewModel>(true);
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance",
		                                                 "CA1822:MarkMembersAsStatic",
		                                                 Justification = "This non-static member is needed for data binding purposes.")]
		public MainWindowViewModel Main
		{
			get
			{
				return ServiceLocator.Current.GetInstance<MainWindowViewModel>();
			}
		}
		
		public static void Cleanup()
		{
			// TODO Clear the ViewModels
		}
	}
}