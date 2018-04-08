/*
 * Created by SharpDevelop.
 * User: rotts
 * Date: 20.03.2018
 * Time: 13:52
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.ComponentModel.Composition;

namespace PluginSystem
{
	/// <summary>
	/// Description of ExportViewModel.
	/// </summary>
    [MetadataAttribute]
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class PluginBase : ExportAttribute
    {
        public string Name { get; private set; }

        public PluginBase(string name, bool isStatic)
            : base("ViewModel")
        {
            Name = name;
        }
    }

    public interface IViewModelMetadata
    {
        string Name { get; }
    }
}
