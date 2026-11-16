#nullable enable
using System;

using RFiDGear.Infrastructure.ExtensionHost;

namespace RFiDGear.Services.Sam
{
    /// <summary>
    /// Holds the reader and the SAM for as long as the SAM setup is open: the slot of the coordinator (as for a reader scan of an extension: no
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
        /// <param name="isReaderBusy">
        /// Whether the main window says that the reader is in use. Some commands of the window (the quick check, a read) work on the
        /// reader without the coordinator and without the gate for most of the time and say so only with this flag.
        /// </param>
        /// <param name="setBusy">Tells the main window that the reader is in use (<c>true</c>) and that it is free again (<c>false</c>).</param>
        /// <param name="run">What to do while the reader is held.</param>
        /// <returns><see langword="false"/> when something else was in progress or held the gate or said that it uses the reader, and nothing was run.</returns>
        internal static bool RunExclusive(ReaderAccessGate gate, ExtensionOperationCoordinator coordinator, Func<bool?> isReaderBusy, Action<bool?> setBusy, Action run)
        {
            ArgumentNullException.ThrowIfNull(gate);
            ArgumentNullException.ThrowIfNull(coordinator);
            ArgumentNullException.ThrowIfNull(isReaderBusy);
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

                // The SAM itself: a command that uses a key of the SAM holds it too, and two connections would share its state.
                if (!SamPortGate.TryEnter())
                {
                    gate.Exit();

                    return false;
                }

                var flagSet = false;

                try
                {
                    // Asked after the slot and the gate are ours, so that nothing starts between the question and the first use.
                    if (isReaderBusy() == true)
                    {
                        return false;
                    }

                    setBusy(true);
                    flagSet = true;
                    run();

                    return true;
                }
                finally
                {
                    // Only a flag that the setup set is cleared: when the window said that it uses the reader, that flag is the
                    // window's, and the setup must not tell it that the reader is free.
                    if (flagSet)
                    {
                        setBusy(false);
                    }

                    SamPortGate.Exit();
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
