using System;
using System.Globalization;
using System.Resources;

namespace RFiDGear.ViewModel
{
	/// <summary>
	/// Description of ResourceLoaderViewModel.
	/// </summary>
	public static class ResourceLoader
	{
		
		static readonly SettingsReaderWriter settings;
		static readonly CultureInfo cultureInfo;
		static readonly ResourceManager resManager;
		
		
		static ResourceLoader()
		{
			settings = new SettingsReaderWriter();
			resManager = new ResourceManager("RFiDGear.Resources.Manifest", System.Reflection.Assembly.GetExecutingAssembly());
			settings.readSettings();
			
			cultureInfo = (settings.DefaultLanguage == "german") ? new CultureInfo("de") : new CultureInfo("en");
		}
		
		public static string getResource(string resName){
			return resManager.GetString(resName, cultureInfo);
		}
		
		public static void setLanguage(string lang){
			settings.saveSettings(lang);
		}
		
		public static string getLanguage(){
			return settings.DefaultLanguage;
		}
	}
}
