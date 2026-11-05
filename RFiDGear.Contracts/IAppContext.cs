using System;

namespace RFiDGear.Contracts
{
    /// <summary>
    /// Read-only application context available to extensions via <see cref="IRFiDGearExtensionHost.AppContext"/>.
    /// Use this to select language-specific project files and format UI strings correctly.
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
        /// </summary>
        string UICultureName { get; }

        /// <summary>
        /// Raised when the active UI culture changes.
        /// In this implementation the culture is fixed for the process lifetime and this event is never raised.
        /// Extensions must not depend on culture changes at runtime.
        /// </summary>
        event EventHandler CultureChanged;
    }
}
