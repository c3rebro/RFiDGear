#nullable enable
using System;
using System.Threading.Tasks;

using RFiDGear.Bridge;
using RFiDGear.Contracts;

using Serilog;

namespace RFiDGear.Services.ExternalControl
{
    /// <summary>
    /// Answers <c>getStatus</c>. Some of what it reports belongs to the main window (the project, whether the reader has been
    /// looked at) and may only be read on its thread; a window that is busy or blocked by a dialog must not make the answer
    /// wait, so that part is read with a time limit and, when the window does not answer, the last values are reported with
    /// <see cref="StatusResponse.Stale"/> set. The rest (is a run going on, the reader, has start-up finished) is read from
    /// thread-safe sources at once.
    /// </summary>
    internal sealed class BridgeStatusProvider
    {
        private readonly IRFiDGearExtensionHost _host;
        private readonly string _instanceId;
        private readonly Func<bool> _isStartupComplete;
        private readonly Func<bool> _isReaderInitializing;
        private readonly Func<Action, Task> _dispatchToUi;
        private readonly TimeSpan _uiTimeout;
        private readonly Func<long> _lastResultSeq;
        private readonly object _gate = new object();
        private UiValues? _lastUi;
        private Task? _outstandingRead;

        /// <summary>Creates the provider.</summary>
        /// <param name="host">The host service of RFiDGear.</param>
        /// <param name="instanceId">The id of this process.</param>
        /// <param name="isStartupComplete">Says whether RFiDGear has finished starting.</param>
        /// <param name="isReaderInitializing">Says whether the reader has not been looked at yet; read on the UI thread.</param>
        /// <param name="dispatchToUi">Runs an action on the thread of the main window.</param>
        /// <param name="uiTimeout">How long to wait for the main window.</param>
        /// <param name="lastResultSeq">The number of the latest result the host holds; 0 when there is none.</param>
        internal BridgeStatusProvider(
            IRFiDGearExtensionHost host,
            string instanceId,
            Func<bool> isStartupComplete,
            Func<bool> isReaderInitializing,
            Func<Action, Task> dispatchToUi,
            TimeSpan uiTimeout,
            Func<long>? lastResultSeq = null)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _instanceId = instanceId ?? throw new ArgumentNullException(nameof(instanceId));
            _isStartupComplete = isStartupComplete ?? throw new ArgumentNullException(nameof(isStartupComplete));
            _isReaderInitializing = isReaderInitializing ?? throw new ArgumentNullException(nameof(isReaderInitializing));
            _dispatchToUi = dispatchToUi ?? throw new ArgumentNullException(nameof(dispatchToUi));
            _uiTimeout = uiTimeout;
            _lastResultSeq = lastResultSeq ?? (() => 0);
        }

        /// <summary>Reads the status.</summary>
        /// <returns>The status; it never waits longer than the time limit for the main window.</returns>
        internal async Task<StatusResponse> GetStatusAsync()
        {
            var responsive = await TryReadUiValuesAsync().ConfigureAwait(false);

            UiValues? ui;

            lock (_gate)
            {
                ui = _lastUi;
            }

            return new StatusResponse
            {
                InstanceId = _instanceId,
                StartupComplete = _isStartupComplete(),

                // Not known yet means not looked at yet: a missing card must not be taken for an empty reader.
                ReaderInitializing = ui?.ReaderInitializing ?? true,
                UiResponsive = responsive,
                Stale = !responsive,
                Project = ui?.Project ?? new ProjectStatusDto(),
                IsRunning = _host.Execution.IsRunning,
                Reader = BridgeMapper.ToDto(_host.Reader.CurrentSnapshot),
                LastResultSeq = _lastResultSeq(),
            };
        }

        // Asks the main window for its values. The action may run late, after the answer was given with the old values; it
        // stores what it read, so the next question gets it. While one such action is still waiting for a window that does
        // not answer, further questions share it instead of queueing another: a client that polls must not leave a growing
        // backlog behind for the window to work off when it recovers.
        private async Task<bool> TryReadUiValuesAsync()
        {
            try
            {
                await StartOrJoinRead().WaitAsync(_uiTimeout).ConfigureAwait(false);

                return true;
            }
            catch (Exception e) when (e is TimeoutException || e is OperationCanceledException || e is InvalidOperationException)
            {
                // A window that does not answer (a dialog is open, the application is closing) is a state to report, not a fault.
                Log.ForContext<BridgeStatusProvider>().Debug("External control: the main window did not answer in time ({ExceptionType})", e.GetType().FullName);

                return false;
            }
        }

        private Task StartOrJoinRead()
        {
            lock (_gate)
            {
                if (_outstandingRead == null || _outstandingRead.IsCompleted)
                {
                    _outstandingRead = _dispatchToUi(ReadUiValues);
                }

                return _outstandingRead;
            }
        }

        // Runs on the thread of the main window.
        private void ReadUiValues()
        {
            var path = _host.Project.CurrentPath;
            var taskCount = _host.Project.TaskCount;
            var values = new UiValues(
                new ProjectStatusDto { Loaded = path != null || taskCount > 0, Path = path, TaskCount = taskCount },
                _isReaderInitializing());

            lock (_gate)
            {
                _lastUi = values;
            }
        }

        private sealed record UiValues(ProjectStatusDto Project, bool ReaderInitializing);
    }
}
