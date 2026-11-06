using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Contracts;

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

    internal sealed class FakeReaderHost : IReaderHost
    {
        private readonly SubscriberCounter _subscribers = new SubscriberCounter();
        private EventHandler? _readerStateChanged;

        private string? _readerName = "Test reader";

        /// <summary>Makes every read of the reader fail, as a faulty reader provider would.</summary>
        public bool ThrowOnRead { get; set; }

        public string? ReaderName
        {
            get => ThrowOnRead ? throw new InvalidOperationException("The reader provider failed.") : _readerName;
            set => _readerName = value;
        }

        public string? CurrentUID { get; set; }

        public string? ChipTypeName { get; set; }

        public bool IsConnected { get; set; } = true;

        public int ReaderStateChangedSubscribers => _subscribers.Count;

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
