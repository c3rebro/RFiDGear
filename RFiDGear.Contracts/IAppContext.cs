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
        /// and again each time the main window's Open dialog is shown (even if the user then cancels it) and whenever
        /// the start-up logic opens a project; opening a project through
        /// <see cref="IProjectHost.LoadAsync(string, System.Threading.CancellationToken)"/> does not refresh it.
        /// A headless host without settings reports the calling thread's
        /// <see cref="System.Globalization.CultureInfo.CurrentUICulture"/>, which can differ between threads.
        /// </para>
        /// </summary>
        string UICultureName { get; }

        /// <summary>
        /// Raised when <see cref="UICultureName"/> starts reporting a different language than before.
        /// <para>
        /// In the main application that happens when it re-reads the language from its settings (see
        /// <see cref="UICultureName"/>) and finds a different one. The usual case: the user picks a language under
        /// Options &gt; Language, which saves the choice and asks for a restart. If the user declines the restart,
        /// <see cref="UICultureName"/> keeps the old language until the next re-read; the event is raised then,
        /// once. A re-read that finds the same language raises nothing, and a headless host never raises it.
        /// </para>
        /// <para>
        /// Threading: raised synchronously on the thread that changed the language, which in the main application
        /// is the UI thread, before the operation that triggered the re-read (showing the Open dialog, opening the
        /// project) continues, so keep handlers short. An exception thrown by an ordinary (non-async) handler is
        /// logged and is <em>not</em> propagated to the application or to the other handlers. The host cannot
        /// catch exceptions from an <c>async void</c> handler, so handle them inside such a handler.
        /// </para>
        /// <para>
        /// Read <see cref="UICultureName"/> when you need it, or subscribe here and read it again, instead of caching
        /// it for the process lifetime.
        /// </para>
        /// </summary>
        event EventHandler CultureChanged;
    }
}
