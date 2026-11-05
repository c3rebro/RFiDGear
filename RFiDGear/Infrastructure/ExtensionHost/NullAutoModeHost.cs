using RFiDGear.Contracts;

using System;

namespace RFiDGear.Infrastructure.ExtensionHost
{
    /// <summary>
    /// Stub <see cref="IAutoModeHost"/> for headless contexts where no WPF timer loop is running.
    /// <para>
    /// <strong>Known limitation:</strong> <see cref="Start"/> throws <see cref="NotSupportedException"/>
    /// in this implementation. Full auto-mode wiring requires the main application window and is
    /// tracked in issue #192. This stub is used by both <see cref="HeadlessExtensionHost"/> (HIL tests)
    /// and the main-app path until #192 is implemented.
    /// </para>
    /// </summary>
    internal sealed class NullAutoModeHost : IAutoModeHost
    {
        /// <inheritdoc/>
        public bool IsAutoModeEnabled => false;

        /// <inheritdoc/>
        public event EventHandler AutoModeStateChanged
        {
            add { }
            remove { }
        }

        /// <summary>Not supported. Throws <see cref="NotSupportedException"/>.</summary>
        /// <exception cref="NotSupportedException">Always thrown. Continuous auto mode requires the main application window (#192).</exception>
        public void Start() =>
            throw new NotSupportedException("Continuous auto mode requires the main application window. Full implementation is tracked in issue #192.");

        /// <inheritdoc/>
        public void Stop() { }
    }
}
