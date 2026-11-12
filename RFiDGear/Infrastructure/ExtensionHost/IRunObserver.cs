#nullable enable
using RFiDGear.Contracts;
using RFiDGear.Models;

namespace RFiDGear.Infrastructure.ExtensionHost
{
    /// <summary>
    /// Told about the runs that an extension starts with <c>ITaskExecHost.ExecuteAllAsync</c>. Automatic mode needs it,
    /// because such a run works on the card that is on the reader just as one of the mode does: the mode must not run
    /// that card again, and it must stop when the run leaves the card in a state nobody knows.
    /// <para>
    /// Both members may be called from any thread, must not throw, and must be quick: they run inside the run, or right
    /// after it, on the thread that started it.
    /// </para>
    /// </summary>
    internal interface IRunObserver
    {
        /// <summary>
        /// Called once per run, with the chip the run's own read found, after that read and before the first task starts.
        /// Not called by a run that has no read of its own.
        /// </summary>
        /// <param name="chip">The chip the run's tasks work on.</param>
        void ChipHydrated(GenericChipModel chip);

        /// <summary>
        /// Called once per accepted run after it has ended: its slot is released, <c>IsRunningChanged</c> has been raised
        /// for it and so has <c>ExecutionCompleted</c>, so a stop that follows is announced after both.
        /// </summary>
        /// <param name="result">The result the caller of the run receives.</param>
        /// <param name="abandoned">Whether the per-task watchdog gave the run up.</param>
        void RunEnded(ExecResult result, bool abandoned);
    }
}
