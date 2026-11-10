using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Contracts;
using RFiDGear.Infrastructure;

namespace RFiDGear.Tests.CardCheckAssistant
{
    /// <summary>
    /// A host service written from scratch for the Card Check Assistant tests. It counts how many subscribers each event
    /// has, so a leak after the window closed is visible, lets a test hold a load or a run open, and raises events from
    /// any thread, as the real host does.
    /// </summary>
    internal sealed class FakeExtensionHost : IRFiDGearExtensionHost
    {
        public FakeExtensionHost(string cultureName = "en-US")
        {
            ProjectHost = new FakeProjectHost();
            ExecutionHost = new FakeExecHost();
            ReaderHost = new FakeReaderHost();
            Context = new FakeAppContext(cultureName);
            AutoModeHost = new FakeAutoModeHost();
        }

        public FakeProjectHost ProjectHost { get; }

        public FakeExecHost ExecutionHost { get; }

        public FakeReaderHost ReaderHost { get; }

        public FakeAppContext Context { get; }

        public FakeAutoModeHost AutoModeHost { get; }

        public IProjectHost Project => ProjectHost;

        public ITaskExecHost Execution => ExecutionHost;

        public IReaderHost Reader => ReaderHost;

        public IAutoModeHost AutoMode => AutoModeHost;

        public IAppContext AppContext => Context;

        /// <summary>The number of subscribers over all four events a page listens to.</summary>
        public int PageSubscriberCount =>
            ExecutionHost.IsRunningChangedSubscribers
            + ProjectHost.ProjectChangedSubscribers
            + ReaderHost.ReaderStateChangedSubscribers
            + Context.CultureChangedSubscribers;
    }

    /// <summary>Counts subscribers of an event that has custom accessors.</summary>
    internal sealed class SubscriberCounter
    {
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public void Added() => Interlocked.Increment(ref _count);

        public void Removed() => Interlocked.Decrement(ref _count);
    }

    internal sealed class FakeProjectHost : IProjectHost
    {
        private readonly SubscriberCounter _subscribers = new SubscriberCounter();
        private EventHandler? _projectChanged;

        public string? CurrentPath { get; set; }

        public int TaskCount { get; set; }

        /// <summary>What <see cref="LoadAsync"/> answers when nothing holds it open.</summary>
        public Func<string, ProjectOpenResult> LoadAnswer { get; set; } = path => ProjectOpenResult.Ok(path, 3);

        /// <summary>When set, <see cref="LoadAsync"/> waits for this before answering.</summary>
        public TaskCompletionSource<bool>? LoadGate { get; set; }

        public List<string> LoadedPaths { get; } = new List<string>();

        public List<CancellationToken> LoadTokens { get; } = new List<CancellationToken>();

        public int ProjectChangedSubscribers => _subscribers.Count;

        public event EventHandler ProjectChanged
        {
            add
            {
                _projectChanged += value;
                _subscribers.Added();
            }

            remove
            {
                _projectChanged -= value;
                _subscribers.Removed();
            }
        }

        public void RaiseProjectChanged() => _projectChanged?.Invoke(this, EventArgs.Empty);

        public async Task<ProjectOpenResult> LoadAsync(string path, CancellationToken ct = default)
        {
            LoadedPaths.Add(path);
            LoadTokens.Add(ct);

            if (LoadGate != null)
            {
                await LoadGate.Task.ConfigureAwait(true);
            }

            var answer = LoadAnswer(path);
            if (answer.Success)
            {
                CurrentPath = answer.CanonicalPath;
                TaskCount = answer.TaskCount;
                RaiseProjectChanged();
            }

            return answer;
        }

        [Obsolete("The synchronous load is not part of what the page uses.")]
        public void Load(string path) => throw new NotSupportedException();

        public void New() => throw new NotSupportedException();

        public void Save() => throw new NotSupportedException();

        public void SaveAs(string path) => throw new NotSupportedException();

        public void ReplaceTaskCollection(IEnumerable<object> tasks) => throw new NotSupportedException();
    }

    internal sealed class FakeExecHost : ITaskExecHost
    {
        private readonly SubscriberCounter _runningSubscribers = new SubscriberCounter();
        private readonly SubscriberCounter _completedSubscribers = new SubscriberCounter();
        private EventHandler? _isRunningChanged;
        private EventHandler<ExecResult>? _executionCompleted;
        private int _isRunning;

        public bool IsRunning => Volatile.Read(ref _isRunning) == 1;

        /// <summary>What <see cref="ExecuteAllAsync(ExecutionRequest, CancellationToken)"/> answers.</summary>
        public ExecResult Result { get; set; } = FakeResults.Success(2);

        /// <summary>When set, a run waits for this before answering.</summary>
        public TaskCompletionSource<bool>? RunGate { get; set; }

        /// <summary>When set, a run throws it instead of answering.</summary>
        public Exception? ThrowOnExecute { get; set; }

        /// <summary>When set, a run calls this with its request before it answers, as the project does when it writes its report.</summary>
        public Action<ExecutionRequest>? OnExecute { get; set; }

        /// <summary>Whether a run also moves <see cref="IsRunning"/> and raises its event, as the real host does.</summary>
        public bool SimulateRunningState { get; set; }

        public List<ExecutionRequest> Requests { get; } = new List<ExecutionRequest>();

        public List<CancellationToken> RunTokens { get; } = new List<CancellationToken>();

        public int IsRunningChangedSubscribers => _runningSubscribers.Count;

        public int ExecutionCompletedSubscribers => _completedSubscribers.Count;

        public event EventHandler IsRunningChanged
        {
            add
            {
                _isRunningChanged += value;
                _runningSubscribers.Added();
            }

            remove
            {
                _isRunningChanged -= value;
                _runningSubscribers.Removed();
            }
        }

        public event EventHandler<ExecResult> ExecutionCompleted
        {
            add
            {
                _executionCompleted += value;
                _completedSubscribers.Added();
            }

            remove
            {
                _executionCompleted -= value;
                _completedSubscribers.Removed();
            }
        }

        /// <summary>Moves <see cref="IsRunning"/> and raises the event, on the calling thread.</summary>
        public void SetRunning(bool running)
        {
            Volatile.Write(ref _isRunning, running ? 1 : 0);
            _isRunningChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Moves <see cref="IsRunning"/> without raising the event.</summary>
        public void SetRunningQuietly(bool running) => Volatile.Write(ref _isRunning, running ? 1 : 0);

        public async Task<ExecResult> ExecuteAllAsync(ExecutionRequest request, CancellationToken ct = default)
        {
            Requests.Add(request);
            RunTokens.Add(ct);

            if (ThrowOnExecute != null)
            {
                throw ThrowOnExecute;
            }

            OnExecute?.Invoke(request);

            if (SimulateRunningState)
            {
                SetRunning(true);
            }

            try
            {
                if (RunGate != null)
                {
                    await RunGate.Task.ConfigureAwait(true);
                }

                return Result;
            }
            finally
            {
                if (SimulateRunningState)
                {
                    SetRunning(false);
                }
            }
        }

        [Obsolete("The page always supplies a request.")]
        public Task<ExecResult> ExecuteAllAsync(CancellationToken ct = default) =>
            ExecuteAllAsync(ExecutionRequest.Empty, ct);

        public void ResetStatus() => throw new NotSupportedException();
    }

    /// <summary>
    /// A reader the test scripts. What it shows can be set as one snapshot or through the older single values, the next
    /// scans can be scripted (a result, a failure, a result that waits for a gate), and it keeps the contract of the real
    /// host: a scan that finds a different picture raises <c>ReaderStateChanged</c> once (the first picture always counts
    /// as different, and the reader name is not part of the comparison), and a scan that was busy or failed leaves the
    /// picture as it was.
    /// </summary>
    internal sealed class FakeReaderHost : IReaderHost
    {
        private readonly object _sync = new object();
        private readonly SubscriberCounter _subscribers = new SubscriberCounter();
        private readonly Queue<ScriptedScan> _scans = new Queue<ScriptedScan>();
        private EventHandler? _readerStateChanged;

        private string? _readerName = "Test reader";
        private string? _currentUid;
        private string? _chipTypeName;
        private bool _isConnected = true;
        private ReaderSnapshot? _snapshot;
        private bool _known;
        private int _scanCount;

        /// <summary>Makes every read of the reader fail, as a faulty reader provider would.</summary>
        public bool ThrowOnRead { get; set; }

        public string? ReaderName
        {
            get => ThrowOnRead ? throw new InvalidOperationException("The reader provider failed.") : _readerName;
            set => Change(() => _readerName = value);
        }

        public string? CurrentUID
        {
            get => _currentUid;
            set => Change(() => _currentUid = value);
        }

        public string? ChipTypeName
        {
            get => _chipTypeName;
            set => Change(() => _chipTypeName = value);
        }

        public bool IsConnected
        {
            get => _isConnected;
            set => Change(() => _isConnected = value);
        }

        /// <summary>
        /// The picture of the reader. Unless a test set a snapshot, it is made from the single values above, so a test
        /// that only sets <see cref="CurrentUID"/> still sees a card.
        /// </summary>
        public ReaderSnapshot CurrentSnapshot
        {
            get
            {
                if (ThrowOnRead)
                {
                    throw new InvalidOperationException("The reader provider failed.");
                }

                lock (_sync)
                {
                    return PictureLocked();
                }
            }
        }

        /// <summary>How many times <see cref="ScanAsync"/> was called, scans that were cancelled before they started excluded.</summary>
        public int ScanCount => Volatile.Read(ref _scanCount);

        public int ReaderStateChangedSubscribers => _subscribers.Count;

        /// <summary>
        /// Shows this picture from now on, and keeps the single values in step with it. Raises the event once if the
        /// picture is a different one, unless <paramref name="raise"/> is off.
        /// </summary>
        public void Show(ReaderSnapshot snapshot, bool raise = true)
        {
            bool changed;
            lock (_sync)
            {
                changed = !_known || !SameCard(PictureLocked(), snapshot);
                _known = true;
                _snapshot = snapshot;
                _readerName = snapshot.ReaderName;
                _isConnected = snapshot.State != ReaderScanState.NoReader;
                _currentUid = snapshot.Credentials.Count > 0 ? snapshot.Credentials[0].Uid : null;
                _chipTypeName = snapshot.Credentials.Count > 0 ? snapshot.Credentials[0].DisplayName : null;
            }

            if (changed && raise)
            {
                RaiseReaderStateChanged();
            }
        }

        /// <summary>
        /// The next scan finds this. Scans are answered in the order they were scripted. With <paramref name="store"/> off
        /// the reader changed while the scan ran, as the real host documents it: the answer is returned, but it does not
        /// become the picture and raises no event.
        /// </summary>
        public void ScriptScan(ReaderSnapshot result, TaskCompletionSource<bool>? gate = null, bool store = true) =>
            Enqueue(new ScriptedScan(result, null, gate, store));

        /// <summary>The next scan fails with this exception, as a faulty reader provider would.</summary>
        public void ScriptScanFailure(Exception failure, TaskCompletionSource<bool>? gate = null) => Enqueue(new ScriptedScan(null, failure, gate));

        /// <summary>
        /// Answers with the next scripted result; with nothing scripted the picture is as it was. A result in the state
        /// <c>Ready</c> or <c>NoReader</c> becomes the picture (and raises the event if it differs), unless it was
        /// scripted as not stored; one in the state <c>ReadFailed</c> or <c>Busy</c> is only returned.
        /// </summary>
        public async Task<ReaderSnapshot> ScanAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (ThrowOnRead)
            {
                throw new InvalidOperationException("The reader provider failed.");
            }

            ScriptedScan? next;
            lock (_sync)
            {
                _scanCount++;
                next = _scans.Count > 0 ? _scans.Dequeue() : null;
            }

            if (next == null)
            {
                return CurrentSnapshot;
            }

            if (next.Gate != null)
            {
                await next.Gate.Task.ConfigureAwait(false);
            }

            if (next.Failure != null)
            {
                throw next.Failure;
            }

            var result = next.Result!;
            if (next.Store && (result.State == ReaderScanState.Ready || result.State == ReaderScanState.NoReader))
            {
                Show(result);
            }

            return result;
        }

        private void Enqueue(ScriptedScan scan)
        {
            lock (_sync)
            {
                _scans.Enqueue(scan);
            }
        }

        // A single value was set: the picture is made from the single values again.
        private void Change(Action set)
        {
            lock (_sync)
            {
                set();
                _snapshot = null;
            }
        }

        private ReaderSnapshot PictureLocked()
        {
            if (_snapshot != null)
            {
                return _snapshot;
            }

            var name = _readerName ?? string.Empty;
            if (!_isConnected)
            {
                return new ReaderSnapshot(name, ReaderScanState.NoReader, Array.Empty<CredentialInfo>());
            }

            // As the real builder does: a blank UID is no card, a UID is trimmed and in upper case, and a name that is a card
            // type gives its code (any other name is shown as it is, with code 0).
            var uid = _currentUid?.Trim();
            var credentials = string.IsNullOrEmpty(uid)
                ? Array.Empty<CredentialInfo>()
                : new[] { new CredentialInfo(uid.ToUpperInvariant(), CodeOf(_chipTypeName), _chipTypeName ?? "Unknown", true) };

            return new ReaderSnapshot(name, ReaderScanState.Ready, credentials);
        }

        private static bool SameCard(ReaderSnapshot a, ReaderSnapshot b) =>
            a.State == b.State && a.Credentials.SequenceEqual(b.Credentials);

        private static int CodeOf(string? typeName) =>
            Enum.TryParse<CARD_TYPE>(typeName, out var type) ? (int)type : 0;

        private sealed record ScriptedScan(ReaderSnapshot? Result, Exception? Failure, TaskCompletionSource<bool>? Gate, bool Store = true);

        public event EventHandler ReaderStateChanged
        {
            add
            {
                _readerStateChanged += value;
                _subscribers.Added();
            }

            remove
            {
                _readerStateChanged -= value;
                _subscribers.Removed();
            }
        }

        public void RaiseReaderStateChanged() => _readerStateChanged?.Invoke(this, EventArgs.Empty);
    }

    internal sealed class FakeAppContext : IAppContext
    {
        private readonly SubscriberCounter _subscribers = new SubscriberCounter();
        private EventHandler? _cultureChanged;
        private string _cultureName;

        public FakeAppContext(string cultureName)
        {
            _cultureName = cultureName;
        }

        public string RFiDGearVersion => "test";

        public string UICultureName => Volatile.Read(ref _cultureName);

        public int CultureChangedSubscribers => _subscribers.Count;

        public event EventHandler CultureChanged
        {
            add
            {
                _cultureChanged += value;
                _subscribers.Added();
            }

            remove
            {
                _cultureChanged -= value;
                _subscribers.Removed();
            }
        }

        /// <summary>Changes the language and raises the event, on the calling thread.</summary>
        public void ChangeCulture(string cultureName)
        {
            Volatile.Write(ref _cultureName, cultureName);
            _cultureChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Changes the language without raising the event.</summary>
        public void ChangeCultureQuietly(string cultureName) => Volatile.Write(ref _cultureName, cultureName);
    }

    internal sealed class FakeAutoModeHost : IAutoModeHost
    {
        public bool IsAutoModeEnabled => false;

        public event EventHandler AutoModeStateChanged
        {
            add => AutoModeStateChangedHandlers += value;
            remove => AutoModeStateChangedHandlers -= value;
        }

        private EventHandler? AutoModeStateChangedHandlers { get; set; }

        public void Start() => throw new NotSupportedException();

        public void Stop() => throw new NotSupportedException();
    }

    /// <summary>Ready-made results.</summary>
    internal static class FakeResults
    {
        public static ExecResult Success(int taskCount)
        {
            var outcomes = new List<TaskOutcome>();
            for (var i = 0; i < taskCount; i++)
            {
                outcomes.Add(new TaskOutcome(i, (i + 1).ToString(), "MifareDesfireSetupViewModel", true, "NoError"));
            }

            return new ExecResult(
                "run-1",
                ExecutionOutcome.Success,
                taskCount,
                0,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                outcomes);
        }

        public static ExecResult WithOutcomes(
            ExecutionOutcome outcome,
            ExecutionFailureReason reason,
            params TaskOutcome[] outcomes)
        {
            var failed = 0;
            foreach (var task in outcomes)
            {
                if (task.IsSuccess == false)
                {
                    failed++;
                }
            }

            return new ExecResult(
                "run-2",
                outcome,
                outcomes.Length,
                failed,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                outcomes,
                reason);
        }
    }
}
