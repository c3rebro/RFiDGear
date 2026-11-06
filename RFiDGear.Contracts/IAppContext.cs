using System;

namespace RFiDGear.Contracts
{
    /// <summary>
    /// Read-only application context available to extensions via <see cref="IRFiDGearExtensionHost.AppContext"/>.
    /// Use this to select language-specific project files and format UI strings correctly.
    /// <para>
    /// Threading: every member may be read from any thread. Sensitive data: nothing here is secret.
    /// </para>
    /// </summary>
    public interface IAppContext
    {
        /// <summary>
        /// RFiDGear assembly version (e.g. <c>"2.0.10.0"</c>).
        /// </summary>
        string RFiDGearVersion { get; }

        /// <summary>
        /// BCP-47 culture name of the active UI culture (e.g. <c>"de-DE"</c> or <c>"en-US"</c>).
        /// Use this to select language-specific default project files.
        /// <para>
        /// In the main application this is the language in RFiDGear's settings as the application last read it:
        /// German reads as <c>"de-DE"</c> and any other value as <c>"en-US"</c>. The language is read at start-up
        /// and again whenever the user opens a project from the main window (the start-up auto-load and the Open
        /// dialog); opening a project through <see cref="IProjectHost.LoadAsync(string, System.Threading.CancellationToken)"/>
        /// does not refresh it. A headless host without settings reports the calling thread's
        /// <see cref="System.Globalization.CultureInfo.CurrentUICulture"/>, which can differ between threads.
        /// </para>
        /// </summary>
        string UICultureName { get; }

        /// <summary>
        /// Raised when the active UI culture changes. In this release it is never raised.
        /// <para>
        /// The Options &gt; Language menu saves the new language and asks for a restart. If the user declines the
        /// restart, <see cref="UICultureName"/> keeps the old language until the next project is opened from the main
        /// window, and then changes without this event being raised. Read <see cref="UICultureName"/> when you
        /// need it instead of caching it for the process lifetime.
        /// </para>
        /// </summary>
        event EventHandler CultureChanged;
    }
}
