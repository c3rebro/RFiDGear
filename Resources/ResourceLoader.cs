/*
 * Created by SharpDevelop.
 * User: rotts
 * Date: 07.12.2016
 * Time: 01:27
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
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
			
			cultureInfo = (settings._defaultLanguage == "german") ? new CultureInfo("de") : new CultureInfo("en");
		}
		
		public static string getResource(string resName){
			return resManager.GetString(resName, cultureInfo);
		}
		
		public static void setLanguage(string lang){
			settings.saveSettings(lang);
		}
		
		public static string getLanguage(){
			return settings._defaultLanguage;
		}
	}
}
