#nullable enable
using RFiDGear.Services.AutoMode;

namespace RFiDGear.Infrastructure.ExtensionHost
{
    /// <summary>
    /// An automatic mode host that says why a start was refused, as a value. The external control bridge needs the reason to
    /// tell a client apart "no reader", "no task" and "busy"; the public <c>IAutoModeHost.Start</c> reports them as the text of
    /// an exception, which a program must not parse, and stays as it is (its snapshot is frozen).
    /// </summary>
    internal interface IAutoModeControl
    {
        /// <summary>
        /// Starts automatic mode the way <c>IAutoModeHost.Start</c> does: it insists on a reader and on a project with a task, and
        /// a program task <c>%EXIT</c> ends the run of one card instead of closing the application. It may be called from any
        /// thread and never waits; the state-changed event of the host is raised on the calling thread, before this returns.
        /// </summary>
        /// <returns>What came of it; only <see cref="AutoModeStartResult.Started"/> changed anything.</returns>
        AutoModeStartResult TryStart();
    }
}
