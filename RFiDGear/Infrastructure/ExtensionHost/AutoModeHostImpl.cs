#nullable enable
using RFiDGear.Contracts;
using RFiDGear.Models;
using RFiDGear.Services.AutoMode;

using Serilog;

using System;
using System.Linq;

namespace RFiDGear.Infrastructure.ExtensionHost
{
    /// <summary>
    /// <see cref="IAutoModeHost"/> of the main application: a thin face on <see cref="AutoModeController"/>, the one
    /// state machine that the menu item and the <c>AUTOMODE=1</c> start-up argument drive as well. It adds nothing of its
    /// own to the way cards are found and processed, so a mode started through the host is the mode the user sees in the
    /// menu, and an extension that starts and stops it gets exactly what a click would.
    /// <para>
    /// It is also the <see cref="IRunObserver"/> of the execution host: a run that an extension starts with
    /// <c>ExecuteAllAsync</c> works on the card on the reader just as a run of the mode does, so the mode remembers that
    /// card and stops when the run leaves it in an unknown state.
    /// </para>
    /// </summary>
    internal sealed class AutoModeHostImpl : IAutoModeHost, IRunObserver
    {
        private static ILogger Logger => Log.ForContext<AutoModeHostImpl>();

        private readonly AutoModeController _controller;
        private readonly ExtensionOperationCoordinator _coordinator;

        /// <summary>Initializes the host.</summary>
        /// <param name="controller">The state machine of the main application.</param>
        /// <param name="coordinator">The admission gate the controller uses; it tells the busy refusals apart.</param>
        public AutoModeHostImpl(AutoModeController controller, ExtensionOperationCoordinator coordinator)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));

            // The controller and the host live for the whole session, so there is nothing to unsubscribe.
            _controller.StateChanged += (_, _) => RaiseStateChanged();
            _controller.RunCompleted += (_, result) => RaiseRunCompleted(result);
        }

        /// <inheritdoc/>
        public bool IsAutoModeEnabled => _controller.IsEnabled;

        /// <inheritdoc/>
        public AutoModeStopReason LastStopReason => _controller.LastStopReason;

        /// <inheritdoc/>
        public event EventHandler? AutoModeStateChanged;

        /// <inheritdoc/>
        public event EventHandler<ExecResult>? AutoModeRunCompleted;

        /// <inheritdoc/>
        public void Start()
        {
            // A start from the host insists on a reader and a project with a task (the menu does not), and a program task
            // %EXIT ends the run of one card instead of closing the application the extension lives in.
            switch (_controller.TryStart(requireReady: true, keepApplicationOpen: true))
            {
                case AutoModeStartResult.Started:
                case AutoModeStartResult.AlreadyEnabled:
                    return;

                case AutoModeStartResult.NoReader:
                    throw new InvalidOperationException(
                        "No reader is selected. Select a reader in the settings of the application before starting automatic mode.");

                case AutoModeStartResult.NoTasks:
                    throw new InvalidOperationException(
                        "The project has no task. Load a project that has tasks before starting automatic mode.");

                default:
                    throw new InvalidOperationException(BusyMessage());
            }
        }

        /// <inheritdoc/>
        public void Stop() => _controller.Stop();

        /// <inheritdoc/>
        public void ChipHydrated(GenericChipModel chip)
        {
            // As the polls report the card, so that the mode compares like with like.
            var uid = ReaderSnapshotBuilder.PrimaryUidOf(chip);
            if (uid != null)
            {
                _controller.RememberCard(uid);
            }
        }

        /// <inheritdoc/>
        public void RunEnded(ExecResult result, bool abandoned) =>
            _controller.OnOtherRunEnded(new AutoModeRunReport(admitted: true, result, abandoned));

        /// <summary>
        /// Picks the message that matches what the coordinator is busy with. The state can change between the refused
        /// attempt and this read, in which case the last message is used.
        /// </summary>
        private string BusyMessage() =>
            _coordinator.IsLoading ? "A project load is in progress. Wait for LoadAsync to complete before starting automatic mode."
            : _coordinator.IsMutating ? "A project edit is in progress. Wait for it to complete before starting automatic mode."
            : _coordinator.IsScanning ? "A reader scan is in progress. Wait for it to complete before starting automatic mode."
            : "A task run is in progress. Start automatic mode after it has completed.";

        /// <summary>
        /// Raises <see cref="AutoModeStateChanged"/> to each subscriber in isolation, with this host as the sender: a
        /// throwing subscriber is logged and must not turn a start or stop into an exception, nor starve the subscribers
        /// after it.
        /// </summary>
        private void RaiseStateChanged()
        {
            var handlers = AutoModeStateChanged;
            if (handlers == null)
            {
                return;
            }

            foreach (var handler in handlers.GetInvocationList().Cast<EventHandler>())
            {
                try
                {
                    handler(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    var error = new SanitizedException(ex);
                    Logger.Error(
                        "AutoModeStateChanged subscriber threw; the exception was not propagated. {ExceptionType}: {ExceptionMessage} {ExceptionStackTrace}",
                        error.TypeName, error.Message, error.StackTrace);
                }
            }
        }

        /// <summary>
        /// Raises <see cref="AutoModeRunCompleted"/> to each subscriber in isolation, with this host as the sender: a
        /// throwing subscriber is logged and must not stop the mode, nor starve the subscribers after it.
        /// </summary>
        private void RaiseRunCompleted(ExecResult result)
        {
            var handlers = AutoModeRunCompleted;
            if (handlers == null)
            {
                return;
            }

            foreach (var handler in handlers.GetInvocationList().Cast<EventHandler<ExecResult>>())
            {
                try
                {
                    handler(this, result);
                }
                catch (Exception ex)
                {
                    var error = new SanitizedException(ex);
                    Logger.Error(
                        "AutoModeRunCompleted subscriber threw for run {RunId}; the exception was not propagated. {ExceptionType}: {ExceptionMessage} {ExceptionStackTrace}",
                        result.RunId, error.TypeName, error.Message, error.StackTrace);
                }
            }
        }
    }
}
