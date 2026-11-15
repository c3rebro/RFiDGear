#nullable enable
using System;

using RFiDGear.Infrastructure.ExtensionHost;

namespace RFiDGear.Services.Sam
{
    /// <summary>
    /// Holds the reader for as long as the SAM setup is open: the slot of the coordinator (as for a reader scan of an extension: no
    /// load, run, project edit or scan is admitted meanwhile, and the setup is not admitted while one is in progress) and the gate
    /// of the reader (the reads of the main window and the polls of the automatic mode). The setup keeps a host session of the
    /// SAM open between its steps, and any other connect to the same reader (a task, the automatic mode, a read) could reset the
    /// SAM and end that session, so nothing else may use the reader meanwhile.
    /// </summary>
    internal static class SamSetupGate
    {
        /// <summary>
        /// Runs <paramref name="run"/> while the slot of the coordinator and the gate of the reader are held, if both are free.
        /// Both are given back whatever happens.
        /// </summary>
        /// <param name="gate">The gate of the reader.</param>
        /// <param name="coordinator">The coordinator of the operations of the main window; its scan slot is taken.</param>
        /// <param name="setBusy">Tells the main window that the reader is in use (<c>true</c>) and that it is free again (<c>false</c>).</param>
        /// <param name="run">What to do while the reader is held.</param>
        /// <returns><see langword="false"/> when something else was in progress or held the gate, and nothing was run.</returns>
        internal static bool RunExclusive(ReaderAccessGate gate, ExtensionOperationCoordinator coordinator, Action<bool?> setBusy, Action run)
        {
            ArgumentNullException.ThrowIfNull(gate);
            ArgumentNullException.ThrowIfNull(coordinator);
            ArgumentNullException.ThrowIfNull(setBusy);
            ArgumentNullException.ThrowIfNull(run);

            // A run of tasks does not hold the gate while it works (it holds the coordinator), so the coordinator comes first.
            if (!coordinator.TryBeginScan())
            {
                return false;
            }

            try
            {
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
            finally
            {
                coordinator.EndScan();
            }
        }
    }
}
