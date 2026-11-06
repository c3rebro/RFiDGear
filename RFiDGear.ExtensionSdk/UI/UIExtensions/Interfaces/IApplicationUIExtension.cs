#nullable enable
using System.Windows;

using RFiDGear.Contracts;

namespace RFiDGear.UI.UIExtensions.Interfaces
{
    /// <summary>
    /// An application-level extension (<c>HostPlacement = HostPlacement.Application</c>) that builds its own page.
    /// RFiDGear creates one shared instance of the exported class the first time the user opens the extension, then
    /// calls <see cref="CreateContent"/> every time the user opens it.
    /// <para>
    /// The host service arrives as an argument, never through a MEF <c>[Import]</c>: the host creates the service
    /// while the main window starts, which can be after MEF has composed the extension, so an import would have been
    /// empty or <c>null</c>. By the time <see cref="CreateContent"/> runs, the service is fully initialized and is
    /// the same instance for the whole session.
    /// </para>
    /// <para>
    /// Do not set <c>Uri</c> in the <c>[UiExtension]</c> attribute of an extension that implements this interface: a
    /// <c>Uri</c> is the older way to name a page, and when it is set RFiDGear opens that page and never creates the
    /// class or calls <see cref="CreateContent"/>.
    /// </para>
    /// <para>
    /// Instance lifetime: the exported instance is shared and long-lived, so keep it stateless. Put everything that
    /// belongs to one opened window in the content (or its <see cref="FrameworkElement.DataContext"/>), which is
    /// created fresh by every call.
    /// </para>
    /// <para>
    /// Cleanup: when the window closes, the host disposes the returned content if it implements
    /// <see cref="System.IDisposable"/>, and also its <see cref="FrameworkElement.DataContext"/> if that is a different
    /// <see cref="System.IDisposable"/> object. Each is disposed once, and an exception from <c>Dispose</c> is logged
    /// and ignored. Unsubscribe from the host's events there; the host outlives every window.
    /// </para>
    /// </summary>
    public interface IApplicationUIExtension : IUIExtension
    {
        /// <summary>
        /// Builds the page shown in a new window. Called on the UI thread.
        /// </summary>
        /// <param name="host">The initialized host service. Never <c>null</c>.</param>
        /// <returns>
        /// A new control or panel for the window to host (for example a <c>UserControl</c>). It must not be a
        /// <c>Page</c> or a <c>Window</c>, and must not be returned by more than one call. It must not be <c>null</c>:
        /// the host then reports the extension as failed to open.
        /// </returns>
        FrameworkElement CreateContent(IRFiDGearExtensionHost host);
    }
}
