#nullable enable
using RFiDGear.Services.AutoMode;

namespace RFiDGear.Infrastructure.ExtensionHost
{
    /// <summary>
    /// An automatic mode host that says why a start was refused, as a value, and which start a mode is. The external control
    /// bridge needs the reason to tell a client apart "no reader", "no task" and "busy" (the public <c>IAutoModeHost.Start</c>
    /// reports them as the text of an exception, which a program must not parse), and the number of the start to stop exactly
    /// the mode a client started and not one that the user switched on after it ended. The public interface stays as it is (its
    /// snapshot is frozen).
    /// </summary>
    internal interface IAutoModeControl
    {
        /// <summary>
        /// Starts automatic mode the way <c>IAutoModeHost.Start</c> does: it insists on a reader and on a project with a task, and
        /// a program task <c>%EXIT</c> ends the run of one card instead of closing the application. It may be called from any
        /// thread and never waits; the state-changed event of the host is raised on the calling thread, before this returns.
        /// </summary>
        /// <param name="session">The number of the start the mode is in (the new one, or the running one); 0 when it was refused and the mode is off.</param>
        /// <returns>What came of it; only <see cref="AutoModeStartResult.Started"/> changed anything.</returns>
        AutoModeStartResult TryStart(out int session);

        /// <summary>Gets the number of the latest start, 0 if there was none. It grows with every start.</summary>
        int CurrentSession { get; }

        /// <summary>
        /// Switches the mode off only if it is still the start with this number, as one step against every other change of the state.
        /// </summary>
        /// <param name="session">The number of the start.</param>
        /// <returns><see langword="true"/> when this call switched the mode off.</returns>
        bool StopIfSession(int session);
    }
}
