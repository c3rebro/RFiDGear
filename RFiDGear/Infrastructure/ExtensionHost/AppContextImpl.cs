#nullable enable
using RFiDGear.Contracts;

using Serilog;

using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace RFiDGear.Infrastructure.ExtensionHost
{
    /// <summary>
    /// <see cref="IAppContext"/> implementation that reads version and culture from the running application.
    /// <para>
    /// The application tells this object when it has just changed the language it works with
    /// (<see cref="NotifyCultureMayHaveChanged"/>); <see cref="CultureChanged"/> is raised then, and only if
    /// <see cref="UICultureName"/> really reports something different from before. Nothing polls the language, so a
    /// host that is never notified, such as a headless one, never raises it.
    /// </para>
    /// </summary>
    internal sealed class AppContextImpl : IAppContext
    {
        // Resolved on each use, not cached, so the logger configured at startup (and the one tests install)
        // is the one that is used even if this type was touched before logging was configured.
        private static ILogger Logger => Log.ForContext<AppContextImpl>();

        // Returns "german" or "english" from settings, or null when not available (headless/test).
        private readonly Func<string?>? _getConfiguredLanguage;

        // The culture name subscribers were last told about (or that was current when this object was created).
        private string _lastKnownCultureName;

        /// <summary>
        /// Creates an instance that reports OS UI culture (<see cref="CultureInfo.CurrentUICulture"/>).
        /// Used by headless/test hosts where no settings object is available.
        /// </summary>
        public AppContextImpl()
        {
            _lastKnownCultureName = UICultureName;
        }

        /// <summary>
        /// Creates an instance that reads the configured language from the application settings.
        /// </summary>
        /// <param name="getConfiguredLanguage">
        /// Delegate returning <c>"german"</c> or <c>"english"</c> (the values stored in
        /// <c>settings.DefaultSpecification.DefaultLanguage</c>). May return <c>null</c> when
        /// settings are not yet initialized; the OS UI culture is used as fallback in that case. It is called once
        /// here, on the constructing thread, to record the language <see cref="CultureChanged"/> is measured against.
        /// </param>
        public AppContextImpl(Func<string?> getConfiguredLanguage)
        {
            _getConfiguredLanguage = getConfiguredLanguage
                ?? throw new ArgumentNullException(nameof(getConfiguredLanguage));
            _lastKnownCultureName = UICultureName;
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
                    {
                        return lang == "german" ? "de-DE" : "en-US";
                    }
                }
                return CultureInfo.CurrentUICulture.Name;
            }
        }

        /// <inheritdoc/>
        public event EventHandler? CultureChanged;

        /// <summary>
        /// Called by the application right after it changed the language it works with. Compares
        /// <see cref="UICultureName"/> with the value subscribers last knew and raises <see cref="CultureChanged"/>
        /// once if it differs; does nothing if it is the same. Subscribers run synchronously on the calling thread.
        /// </summary>
        public void NotifyCultureMayHaveChanged()
        {
            var current = UICultureName;
            var previous = Interlocked.Exchange(ref _lastKnownCultureName, current);

            if (string.Equals(previous, current, StringComparison.Ordinal))
            {
                return;
            }

            RaiseCultureChanged();
        }

        /// <summary>
        /// Raises <see cref="CultureChanged"/> to each subscriber in isolation: a throwing subscriber is logged and
        /// must not fail the operation that changed the language, nor starve the subscribers after it.
        /// </summary>
        private void RaiseCultureChanged()
        {
            var handlers = CultureChanged;
            if (handlers == null)
            {
                return;
            }

            foreach (var handler in handlers.GetInvocationList().Cast<EventHandler>())
            {
                try
                {
                    handler(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    var error = new SanitizedException(ex);
                    Logger.Error(
                        "CultureChanged subscriber threw; the exception was not propagated. {ExceptionType}: {ExceptionMessage} {ExceptionStackTrace}",
                        error.TypeName, error.Message, error.StackTrace);
                }
            }
        }
    }
}
