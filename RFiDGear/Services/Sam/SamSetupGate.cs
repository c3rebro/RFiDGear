#nullable enable
using System;

using RFiDGear.Infrastructure.ExtensionHost;

namespace RFiDGear.Services.Sam
{
    /// <summary>
    /// Holds the gate of the reader for as long as the SAM setup is open. The setup keeps a host session of the SAM open between
    /// its steps, and any other connect to the same reader (the monitor, the automatic mode, a task) could reset the SAM and end
    /// that session, so nothing else may use the reader meanwhile.
    /// </summary>
    internal static class SamSetupGate
    {
        /// <summary>Runs <paramref name="run"/> while the gate is held, if it is free. The gate is given back whatever happens.</summary>
        /// <param name="gate">The gate of the reader.</param>
        /// <param name="setBusy">Tells the main window that the reader is in use (<c>true</c>) and that it is free again (<c>false</c>).</param>
        /// <param name="run">What to do while the gate is held.</param>
        /// <returns><see langword="false"/> when somebody else held the gate and nothing was run.</returns>
        internal static bool RunExclusive(ReaderAccessGate gate, Action<bool?> setBusy, Action run)
        {
            ArgumentNullException.ThrowIfNull(gate);
            ArgumentNullException.ThrowIfNull(setBusy);
            ArgumentNullException.ThrowIfNull(run);

            if (!gate.TryEnter())
            {
                return false;
            }

            try
            {
                setBusy(true);
                run();

                return true;
            }
            finally
            {
                setBusy(false);
                gate.Exit();
            }
        }
    }
}
