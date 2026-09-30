using RFiDGear.Contracts;

using System;

namespace RFiDGear.Infrastructure.ExtensionHost
{
    /// <summary>
    /// Stub <see cref="IAutoModeHost"/> for headless contexts where no WPF timer loop is running.
    /// Full implementation is wired when <see cref="HeadlessExtensionHost"/> is used inside the
    /// running application via <c>MainWindowViewModel</c>.
    /// </summary>
    internal sealed class NullAutoModeHost : IAutoModeHost
    {
        public bool IsAutoModeEnabled => false;

        public event EventHandler AutoModeStateChanged
        {
            add { }
            remove { }
        }

        public void Start() =>
            throw new NotSupportedException("Continuous auto mode requires the main application window.");

        public void Stop() { }
    }
}
