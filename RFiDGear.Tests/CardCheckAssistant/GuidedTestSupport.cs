using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Extensions.CardCheckAssistant.Localization;
using RFiDGear.Extensions.CardCheckAssistant.Services;
using RFiDGear.Extensions.CardCheckAssistant.ViewModels;
using RFiDGear.Extensions.CardCheckAssistant.Workflow;

namespace RFiDGear.Tests.CardCheckAssistant
{
    /// <summary>
    /// A workflow that shows what a test pushes, in the order and on the thread the test chooses, and records the
    /// commands it is given. The real engine always delivers in order; this one can deliver an old picture late.
    /// </summary>
    internal sealed class ScriptedWorkflow : IGuidedWorkflow
    {
        private WorkflowSnapshot _current;
        private long _version;

        public ScriptedWorkflow(WorkflowState state = WorkflowState.Idle, long firstVersion = 1)
        {
            _version = firstVersion - 1;
            _current = Make(state);
        }

        public event EventHandler<WorkflowSnapshot>? Changed;

        public WorkflowSnapshot Current => _current;

        public List<string> Calls { get; } = new List<string>();

        public bool Accept { get; set; } = true;

        public int DisposeCount { get; private set; }

        public int ChangedSubscribers => Changed?.GetInvocationList().Length ?? 0;

        /// <summary>A snapshot as the engine would compose it for the state: the controls of the state, or only Cancel while busy.</summary>
        public WorkflowSnapshot Make(WorkflowState state, Func<WorkflowSnapshot, WorkflowSnapshot>? edit = null, bool busy = false, bool cancelling = false)
        {
            var controls = busy ? WorkflowControls.Cancel : WorkflowTables.ControlsOf(state);
            if (cancelling)
            {
                controls &= ~WorkflowControls.Cancel;
            }

            var snapshot = new WorkflowSnapshot
            {
                State = state,
                IsBusy = busy,
                IsCancelling = cancelling,
                Controls = controls,
                Close = WorkflowTables.CloseAdviceOf(state),
                Version = ++_version,
            };
            var edited = edit == null ? snapshot : edit(snapshot);

            // A test that sets the controls itself has made its choice; otherwise they are narrowed as the engine does.
            return !busy && edited.Controls == snapshot.Controls ? edited with { Controls = Narrowed(edited) } : edited;
        }

        // What CardCheckWorkflow.Compose takes away from the controls of a state when nothing runs, by what the snapshot holds.
        private static WorkflowControls Narrowed(WorkflowSnapshot s)
        {
            var controls = s.Controls;
            if (s.State == WorkflowState.Assessing)
            {
                // Either the check is finished, or the chip is to be programmed (unless that is not possible).
                controls &= s.Selection?.Verdict == ChipVerdict.Programmable ? ~WorkflowControls.Finish : ~WorkflowControls.BeginProgramming;
                if (s.ProgrammingProblem != null)
                {
                    controls &= ~WorkflowControls.BeginProgramming;
                }
            }

            if (s.State == WorkflowState.Programming)
            {
                // Only a chip that was put back can go on, and only a wait that ran out can be made again.
                var outcome = s.Move?.Outcome;
                if (outcome != CardWaitOutcome.Done)
                {
                    controls &= ~WorkflowControls.ContinueProgramming;
                }

                if (outcome != CardWaitOutcome.TimedOut)
                {
                    controls &= ~WorkflowControls.Retry;
                }
            }

            return controls;
        }

        /// <summary>Makes the next snapshot, makes it current and raises the event on the calling thread.</summary>
        public WorkflowSnapshot Push(WorkflowState state, Func<WorkflowSnapshot, WorkflowSnapshot>? edit = null, bool busy = false, bool cancelling = false)
        {
            var snapshot = Make(state, edit, busy, cancelling);
            _current = snapshot;
            Changed?.Invoke(this, snapshot);

            return snapshot;
        }

        /// <summary>Raises the event with a snapshot as it is, without making it current (a late or repeated one).</summary>
        public void Raise(WorkflowSnapshot snapshot) => Changed?.Invoke(this, snapshot);

        public bool RefreshOrders() => Record("Refresh");

        public bool Start(string orderId, ReportLanguage language) => Record("Start:" + orderId + ":" + language);

        public bool ConfirmReplace(bool replace) => Record("ConfirmReplace:" + replace);

        public bool StartCheck() => Record("StartCheck");

        public bool Finish() => Record("Finish");

        public bool BeginProgramming() => Record("BeginProgramming");

        public bool ContinueProgramming(LsmTemplate template, LsmAnswers answers) => Record("ContinueProgramming:" + template.Name);

        public bool FinishWithoutReadOut() => Record("FinishWithoutReadOut");

        public bool Retry() => Record("Retry");

        public bool Cancel() => Record("Cancel");

        public bool Back() => Record("Back");

        public void Dispose() => DisposeCount++;

        private bool Record(string call)
        {
            Calls.Add(call);

            return Accept;
        }
    }

    /// <summary>
    /// The questions and the viewer of the page, answered by the test: the answer to every question, a record of what was
    /// asked, and the viewer's answer held back until the test completes it (no sleeps, no thread ids).
    /// </summary>
    internal sealed class FakePageServices : IGuidedPageServices
    {
        public bool Answer { get; set; }

        public List<(string Title, string Text)> Asked { get; } = new List<(string, string)>();

        public Action? WhileAsking { get; set; }

        /// <summary>The paths the page asked the viewer to open, in order.</summary>
        public List<string> Opened { get; } = new List<string>();

        /// <summary>The answer of the next open: completed by the test, or <c>null</c> for an answer at once ("opened").</summary>
        public TaskCompletionSource<Failure?>? NextOpen { get; set; }

        public bool Confirm(string title, string text)
        {
            Asked.Add((title, text));
            WhileAsking?.Invoke();

            return Answer;
        }

        public Task<Failure?> OpenReportAsync(string path, CancellationToken ct)
        {
            Opened.Add(path);
            var pending = NextOpen;
            if (pending == null)
            {
                return Task.FromResult<Failure?>(null);
            }

            // A cancelled wait ends the task, as the real one does.
            ct.Register(() => pending.TrySetCanceled(ct));

            return pending.Task;
        }
    }

    /// <summary>The page over a scripted workflow and the fakes it needs, for the tests of the view model.</summary>
    internal sealed class GuidedPage : IDisposable
    {
        public GuidedPage(WorkflowState state, bool withSettings, string culture, long firstVersion)
        {
            Host = new FakeExtensionHost(culture);
            Workflow = new ScriptedWorkflow(state, firstVersion);
            Vm = new GuidedCheckViewModel(
                Host,
                Dispatcher.CurrentDispatcher,
                Workflow,
                withSettings ? Settings : null,
                Offer,
                Close,
                Timer.Start);
        }

        public FakeExtensionHost Host { get; }

        public ScriptedWorkflow Workflow { get; }

        public StubSettingsDialog Settings { get; } = new StubSettingsDialog();

        public StubImportOffer Offer { get; } = new StubImportOffer();

        public FakePageServices Close { get; } = new FakePageServices();

        public ManualTimer Timer { get; } = new ManualTimer();

        public GuidedCheckViewModel Vm { get; }

        public static Task Run(
            Func<GuidedPage, Task> body,
            WorkflowState state = WorkflowState.Idle,
            bool settings = true,
            string culture = "en-US",
            long firstVersion = 1) =>
            StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var page = new GuidedPage(state, settings, culture, firstVersion);
                await body(page).WaitAsync(TimeSpan.FromSeconds(30));
            });

        public async Task PushAsync(WorkflowState state, Func<WorkflowSnapshot, WorkflowSnapshot>? edit = null, bool busy = false)
        {
            Workflow.Push(state, edit, busy);
            await ViewModelTestSupport.Flush();
        }

        public void Dispose() => Vm.Dispose();
    }

    /// <summary>The refresh timer, ticked by hand.</summary>
    internal sealed class ManualTimer
    {
        public int Starts { get; private set; }

        public TimeSpan Interval { get; private set; }

        public Action? Tick { get; private set; }

        public bool Disposed { get; private set; }

        public IDisposable Start(TimeSpan interval, Action tick)
        {
            Starts++;
            Interval = interval;
            Tick = tick;

            return new Handle(this);
        }

        private sealed class Handle : IDisposable
        {
            private readonly ManualTimer _owner;

            public Handle(ManualTimer owner) => _owner = owner;

            public void Dispose() => _owner.Disposed = true;
        }
    }

    internal sealed class StubImportOffer : IImportOffer
    {
        public int Calls { get; private set; }

        public Exception? Throws { get; set; }

        public Action? WhileOffering { get; set; }

        public CancellationToken SeenToken { get; private set; }

        public Task<bool> OfferAsync(LocalizedStrings strings, CancellationToken cancellationToken)
        {
            Calls++;
            SeenToken = cancellationToken;
            WhileOffering?.Invoke();

            return Throws != null ? throw Throws : Task.FromResult(false);
        }
    }

    internal sealed class StubSettingsDialog : ISettingsDialog
    {
        public bool Answer { get; set; }

        public int Shows { get; private set; }

        public Action? WhileOpen { get; set; }

        public bool Show(LocalizedStrings strings)
        {
            Shows++;
            WhileOpen?.Invoke();

            return Answer;
        }
    }
}
