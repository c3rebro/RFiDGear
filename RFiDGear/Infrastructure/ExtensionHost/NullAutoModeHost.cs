using RFiDGear.Contracts;

using System;

namespace RFiDGear.Infrastructure.ExtensionHost
{
    /// <summary>
    /// <see cref="IAutoModeHost"/> for headless contexts where no WPF timer loop is running.
    /// <para>
    /// <strong>Known limitation:</strong> <see cref="Start"/> throws <see cref="NotSupportedException"/>
    /// in this implementation, because continuous automatic mode needs the reader loop of the main application window.
    /// This is the host that <see cref="HeadlessExtensionHost"/> (the HIL tests) uses. The main application hands its
    /// own <see cref="AutoModeHostImpl"/> to the host instead.
    /// </para>
    /// </summary>
    internal sealed class NullAutoModeHost : IAutoModeHost
    {
        /// <inheritdoc/>
        public bool IsAutoModeEnabled => false;

        /// <inheritdoc/>
        public AutoModeStopReason LastStopReason => AutoModeStopReason.None;

        /// <inheritdoc/>
        public event EventHandler AutoModeStateChanged
        {
            add { }
            remove { }
        }

        /// <inheritdoc/>
        public event EventHandler<ExecResult> AutoModeRunCompleted
        {
            add { }
            remove { }
        }

        /// <summary>Not supported. Throws <see cref="NotSupportedException"/>.</summary>
        /// <exception cref="NotSupportedException">Always thrown. Continuous auto mode requires the main application window.</exception>
        public void Start() =>
            throw new NotSupportedException("Continuous auto mode requires the main application window.");

        /// <inheritdoc/>
        public void Stop() { }
    }
}
