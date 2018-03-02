using RFiDGear.Plugins;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Ninject.Modules;

using PluginSystem;

using Ninject;

namespace RFiDGear.Plugins
{
    class MainModule : NinjectModule
    {
        public override void Load()
        {
			Bind<PluginBase>().To<VCNEditor>().Named("MifareClassicTaskPlugin");
        }
    }
}
