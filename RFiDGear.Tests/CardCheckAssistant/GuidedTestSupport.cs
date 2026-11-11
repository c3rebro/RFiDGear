using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Extensions.CardCheckAssistant.Localization;
using RFiDGear.Extensions.CardCheckAssistant.Services;
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

            return edit == null ? snapshot : edit(snapshot);
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

    internal sealed class FakeCloseConfirmation : ICloseConfirmation
    {
        public bool Answer { get; set; }

        public List<(string Title, string Text)> Asked { get; } = new List<(string, string)>();

        public Action? WhileAsking { get; set; }

        public bool Confirm(string title, string text)
        {
            Asked.Add((title, text));
            WhileAsking?.Invoke();

            return Answer;
        }
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
