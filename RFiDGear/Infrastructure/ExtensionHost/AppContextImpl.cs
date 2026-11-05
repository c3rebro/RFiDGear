#nullable enable
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
        // Returns "german" or "english" from settings, or null when not available (headless/test).
        private readonly Func<string?>? _getConfiguredLanguage;

        /// <summary>
        /// Creates an instance that reports OS UI culture (<see cref="CultureInfo.CurrentUICulture"/>).
        /// Used by headless/test hosts where no settings object is available.
        /// </summary>
        public AppContextImpl() { }

        /// <summary>
        /// Creates an instance that reads the configured language from the application settings.
        /// </summary>
        /// <param name="getConfiguredLanguage">
        /// Delegate returning <c>"german"</c> or <c>"english"</c> (the values stored in
        /// <c>settings.DefaultSpecification.DefaultLanguage</c>). May return <c>null</c> when
        /// settings are not yet initialized; the OS UI culture is used as fallback in that case.
        /// </param>
        public AppContextImpl(Func<string?> getConfiguredLanguage)
        {
            _getConfiguredLanguage = getConfiguredLanguage
                ?? throw new ArgumentNullException(nameof(getConfiguredLanguage));
        }

        /// <inheritdoc/>
        public string RFiDGearVersion { get; } =
            Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "0.0.0.0";

        /// <inheritdoc/>
        public string UICultureName
        {
            get
            {
                if (_getConfiguredLanguage != null)
                {
                    var lang = _getConfiguredLanguage();
                    if (lang != null)
                        return lang == "german" ? "de-DE" : "en-US";
                }
                return CultureInfo.CurrentUICulture.Name;
            }
        }

        /// <inheritdoc/>
        public event EventHandler CultureChanged
        {
            // Culture changes at runtime are not supported; this event is never raised.
            add { }
            remove { }
        }
    }
}
