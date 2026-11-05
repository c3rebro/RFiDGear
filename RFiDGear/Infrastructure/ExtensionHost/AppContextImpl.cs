using RFiDGear.Contracts;

using System;
using System.Globalization;
using System.Reflection;

namespace RFiDGear.Infrastructure.ExtensionHost
{
    /// <summary>
    /// <see cref="IAppContext"/> implementation that reads version and culture from the running application.
    /// The UI culture is fixed for the process lifetime; <see cref="CultureChanged"/> is never raised.
    /// </summary>
    internal sealed class AppContextImpl : IAppContext
    {
        /// <inheritdoc/>
        public string RFiDGearVersion { get; } =
            Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "0.0.0.0";

        /// <inheritdoc/>
        public string UICultureName => CultureInfo.CurrentUICulture.Name;

        /// <inheritdoc/>
        public event EventHandler CultureChanged
        {
            // Culture changes at runtime are not supported; this event is never raised.
            add { }
            remove { }
        }
    }
}
