using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using RFiDGear.Contracts;
using RFiDGear.Extensions.CardCheckAssistant.ViewModels;

using Xunit;

using static RFiDGear.Tests.CardCheckAssistant.ViewModelTestSupport;

namespace RFiDGear.Tests.CardCheckAssistant
{
    /// <summary>
    /// Opening, using, closing and reopening the page: the host's four events are subscribed once and unsubscribed once,
    /// events that arrive on other threads or after the window closed do no harm, and nothing of a closed page is kept.
    /// </summary>
    public class CardCheckAssistantLifecycleTests
    {
        [Fact]
        public async Task OpeningSubscribesOncePerEvent_AndDisposeRemovesAll()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var host = new FakeExtensionHost();
                Assert.Equal(0, host.PageSubscriberCount);

                var viewModel = Create(host);
                Assert.Equal(1, host.ExecutionHost.IsRunningChangedSubscribers);
                Assert.Equal(1, host.ProjectHost.ProjectChangedSubscribers);
                Assert.Equal(1, host.ReaderHost.ReaderStateChangedSubscribers);
                Assert.Equal(1, host.Context.CultureChangedSubscribers);

                viewModel.Dispose();
                Assert.Equal(0, host.PageSubscriberCount);
                Assert.True(viewModel.IsDisposed);
            });
        }

        [Fact]
        public async Task Dispose_CanBeCalledAgain_WithoutUnsubscribingTwice()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var host = new FakeExtensionHost();
                var first = Create(host);
                var second = Create(host);   // another page open at the same time keeps its own subscriptions
                Assert.Equal(2, host.ExecutionHost.IsRunningChangedSubscribers);

                first.Dispose();
                first.Dispose();
                first.Dispose();

                Assert.Equal(1, host.ExecutionHost.IsRunningChangedSubscribers);
                Assert.Equal(1, host.Context.CultureChangedSubscribers);
                second.Dispose();
                Assert.Equal(0, host.PageSubscriberCount);
            });
        }

        [Fact]
        public async Task OpeningClosingAndReopening_GivesOneWorkingPage_WithoutTheOldOnesState()
        {
            using var area = new TempArea();
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var host = new FakeExtensionHost();

                var firstPage = Create(host);
                firstPage.ProjectPath = area.ProjectPath;
                firstPage.JobNumber = "first window";
                await firstPage.RunCommand.ExecuteAsync();
                Assert.True(firstPage.HasResult);
                firstPage.Dispose();
                Assert.Equal(0, host.PageSubscriberCount);

                using var secondPage = Create(host);

                Assert.Equal(1, host.ExecutionHost.IsRunningChangedSubscribers);
                Assert.Equal(1, host.ProjectHost.ProjectChangedSubscribers);
                Assert.Equal(1, host.ReaderHost.ReaderStateChangedSubscribers);
                Assert.Equal(1, host.Context.CultureChangedSubscribers);
                Assert.Equal(string.Empty, secondPage.ProjectPath);
                Assert.Equal(string.Empty, secondPage.JobNumber);
                Assert.False(secondPage.HasResult);
                Assert.Empty(secondPage.Rows);

                // And it works: exactly one handler reacts to one event.
                var changes = 0;
                secondPage.PropertyChanged += (_, e) => changes += e.PropertyName == nameof(CardCheckAssistantViewModel.IsExecutionRunning) ? 1 : 0;
                host.ExecutionHost.SetRunning(true);
                await Flush();
                Assert.Equal(1, changes);
                Assert.True(secondPage.IsExecutionRunning);
            });
        }

        [Fact]
        public async Task DisposeDropsTheResultAndTheRows()
        {
            using var area = new TempArea();
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var host = new FakeExtensionHost();
                var viewModel = Create(host);
                viewModel.ProjectPath = area.ProjectPath;
                await viewModel.RunCommand.ExecuteAsync();
                Assert.NotEmpty(viewModel.Rows);

                viewModel.Dispose();

                Assert.Empty(viewModel.Rows);
                Assert.False(viewModel.HasResult);
                Assert.False(viewModel.CanRun);
            });
        }

        [Fact]
        public async Task AClosedPage_IsNotKeptAliveByTheHost()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var host = new FakeExtensionHost();
                var weak = OpenAndCloseOnePage(host);

                for (var attempt = 0; attempt < 3 && weak.IsAlive; attempt++)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }

                Assert.False(weak.IsAlive, "the host still holds a closed page through one of its events");
            });
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference OpenAndCloseOnePage(FakeExtensionHost host)
        {
            var viewModel = Create(host);
            viewModel.Dispose();
            return new WeakReference(viewModel);
        }

        [Fact]
        public async Task EventsFromOtherThreads_ChangeThePageOnlyOnItsOwnThread()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var host = new FakeExtensionHost();
                using var viewModel = Create(host);
                var uiThread = Environment.CurrentManagedThreadId;
                var threads = new List<int>();
                viewModel.PropertyChanged += (_, _) => threads.Add(Environment.CurrentManagedThreadId);
                viewModel.Strings.PropertyChanged += (_, _) => threads.Add(Environment.CurrentManagedThreadId);

                await Task.Run(() =>
                {
                    host.ExecutionHost.SetRunning(true);
                    host.ReaderHost.RaiseReaderStateChanged();
                    host.ProjectHost.RaiseProjectChanged();
                    host.Context.ChangeCulture("de-DE");
                });
                await Flush();

                Assert.NotEmpty(threads);
                Assert.All(threads, thread => Assert.Equal(uiThread, thread));
                Assert.True(viewModel.IsExecutionRunning);
                Assert.Equal("Einmal ausführen", viewModel.Strings["RunButton"]);
            });
        }

        [Fact]
        public async Task TheRunningStateIsReadWhenTheEventIsHandled_NotTakenFromTheEvent()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var host = new FakeExtensionHost();
                using var viewModel = Create(host);

                // The host went true and false again before the page got to look: only the state now counts.
                host.ExecutionHost.SetRunning(true);
                host.ExecutionHost.SetRunningQuietly(false);
                await Flush();

                Assert.False(viewModel.IsExecutionRunning);
                Assert.True(viewModel.InputsEnabled);
            });
        }

        [Fact]
        public async Task AnEventThatIsQueuedWhenThePageIsDisposed_IsIgnored()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var host = new FakeExtensionHost();
                var viewModel = Create(host);
                var changesAfterDispose = 0;

                host.ExecutionHost.SetRunning(true);   // the page has queued its reaction
                host.Context.ChangeCulture("de-DE");
                viewModel.Dispose();
                viewModel.PropertyChanged += (_, _) => changesAfterDispose++;
                viewModel.Strings.PropertyChanged += (_, _) => changesAfterDispose++;
                await Flush();

                Assert.Equal(0, changesAfterDispose);
                Assert.False(viewModel.IsExecutionRunning);
                Assert.Equal("Run once", viewModel.Strings["RunButton"]);
            });
        }

        [Fact]
        public async Task EventsAfterDispose_ReachNoOne()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var host = new FakeExtensionHost();
                var viewModel = Create(host);
                viewModel.Dispose();

                host.ExecutionHost.SetRunning(true);
                host.ReaderHost.RaiseReaderStateChanged();
                host.ProjectHost.RaiseProjectChanged();
                host.Context.ChangeCulture("de-DE");
                await Flush();

                Assert.False(viewModel.IsExecutionRunning);
                Assert.Equal(0, host.PageSubscriberCount);
            });
        }

        [Fact]
        public async Task ClosingDuringTheLoad_CancelsIt_AndTheRunNeverStarts()
        {
            using var area = new TempArea();
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var host = new FakeExtensionHost();
                host.ProjectHost.LoadGate = NewGate();
                var viewModel = Create(host);
                viewModel.ProjectPath = area.ProjectPath;
                var run = viewModel.RunCommand.ExecuteAsync();

                viewModel.Dispose();
                Assert.True(host.ProjectHost.LoadTokens[0].IsCancellationRequested, "the load was told to stop");

                host.ProjectHost.LoadGate.SetResult(true);
                await run;

                Assert.Empty(host.ExecutionHost.Requests);
                Assert.False(viewModel.HasResult);
                Assert.Empty(viewModel.Rows);
            });
        }

        [Fact]
        public async Task ClosingDuringTheRun_CancelsIt_AndTheResultIsNotShown()
        {
            using var area = new TempArea();
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var host = new FakeExtensionHost();
                host.ExecutionHost.RunGate = NewGate();
                var viewModel = Create(host);
                viewModel.ProjectPath = area.ProjectPath;
                var run = viewModel.RunCommand.ExecuteAsync();
                await Flush();
                Assert.Single(host.ExecutionHost.Requests);

                viewModel.Dispose();
                Assert.True(host.ExecutionHost.RunTokens[0].IsCancellationRequested, "the run was told to stop at its next safe point");

                host.ExecutionHost.RunGate.SetResult(true);
                await run;

                Assert.False(viewModel.HasResult);
                Assert.Empty(viewModel.Rows);
            });
        }

        [Fact]
        public async Task TheWindowMayCloseWhileIdle_ButNotWhileLoadingOrRunning()
        {
            using var area = new TempArea();
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var host = new FakeExtensionHost();
                host.ProjectHost.LoadGate = NewGate();
                host.ExecutionHost.RunGate = NewGate();
                using var viewModel = Create(host);
                viewModel.ProjectPath = area.ProjectPath;

                Assert.True(viewModel.TryRequestClose());
                Assert.False(viewModel.ShowCloseHint);

                var run = viewModel.RunCommand.ExecuteAsync();
                Assert.False(viewModel.TryRequestClose());   // loading
                Assert.True(viewModel.ShowCloseHint);
                Assert.Equal("A run is in progress. Wait until it has finished before closing this window.", viewModel.CloseHintText);

                host.ProjectHost.LoadGate.SetResult(true);
                await Flush();
                Assert.False(viewModel.TryRequestClose());   // running

                host.ExecutionHost.RunGate.SetResult(true);
                await run;
                Assert.False(viewModel.ShowCloseHint, "the hint goes away when the run ends");
                Assert.True(viewModel.TryRequestClose());
            });
        }

        [Fact]
        public async Task AnotherRunsBusyHost_DoesNotStopThePageFromClosing()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var host = new FakeExtensionHost();
                using var viewModel = Create(host);

                host.ExecutionHost.SetRunning(true);   // the main window's run, not this page's
                await Flush();

                Assert.True(viewModel.TryRequestClose());
            });
        }

        [Fact]
        public async Task ChangingTheLanguage_RewritesEveryTextOnShow_IncludingAResultAndAnError()
        {
            using var area = new TempArea();
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var host = new FakeExtensionHost("de-DE");
                host.ExecutionHost.Result = FakeResults.WithOutcomes(
                    ExecutionOutcome.Failure,
                    ExecutionFailureReason.Cancelled,
                    new TaskOutcome(0, "1", "A", false, "AuthFailure"));
                using var viewModel = Create(host);
                Assert.Equal("Einmal ausführen", viewModel.Strings["RunButton"]);
                Assert.Equal("Leser: Test reader (verbunden)", viewModel.ReaderText);
                viewModel.ProjectPath = area.ProjectPath;
                viewModel.ReportTargetPath = "relative.pdf";
                await viewModel.RunCommand.ExecuteAsync();
                Assert.Equal("Die Berichtsdatei muss ein vollständiger Pfad sein, zum Beispiel C:\\Berichte\\bericht.pdf.", viewModel.ReportTargetError);

                viewModel.ReportTargetPath = string.Empty;
                await viewModel.RunCommand.ExecuteAsync();
                Assert.Equal("Fehlgeschlagen", viewModel.OutcomeText);
                Assert.Equal("Fehlgeschlagen", viewModel.Rows[0].ResultText);
                Assert.Equal("Grund: der Lauf wurde abgebrochen", viewModel.ReasonText);

                host.Context.ChangeCulture("en-US");
                await Flush();

                Assert.Equal("Run once", viewModel.Strings["RunButton"]);
                Assert.Equal("Reader: Test reader (connected)", viewModel.ReaderText);
                Assert.Equal("Failed", viewModel.OutcomeText);
                Assert.Equal("Failed", viewModel.Rows[0].ResultText);
                Assert.Equal("Reason: the run was cancelled", viewModel.ReasonText);
                Assert.Equal("1 of 1 tasks failed", viewModel.SummaryText);
            });
        }

        [Fact]
        public async Task ChangingTheLanguage_TellsEveryRowOnShowToReadItsTextAgain()
        {
            using var area = new TempArea();
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var host = new FakeExtensionHost("de-DE");
                using var viewModel = Create(host);
                viewModel.ProjectPath = area.ProjectPath;
                await viewModel.RunCommand.ExecuteAsync();
                Assert.Equal(2, viewModel.Rows.Count);

                // The rows work out their text when it is read, so only the notification makes the list redraw it.
                var told = new List<string?>();
                foreach (var row in viewModel.Rows)
                {
                    row.PropertyChanged += (_, e) => told.Add(e.PropertyName);
                }

                host.Context.ChangeCulture("en-US");
                await Flush();

                Assert.Equal(new[] { "ResultText", "ResultText" }, told);
            });
        }

        [Fact]
        public async Task AnErrorThatIsOnShow_IsWrittenAgainInTheNewLanguage()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var host = new FakeExtensionHost("en-US");
                using var viewModel = Create(host);
                await viewModel.RunCommand.ExecuteAsync();
                Assert.Equal("Select a project file.", viewModel.ProjectError);

                host.Context.ChangeCulture("de-DE");
                await Flush();

                Assert.Equal("Wählen Sie eine Projektdatei aus.", viewModel.ProjectError);
            });
        }

        [Fact]
        public async Task AnUnknownLanguage_FallsBackToEnglish()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var host = new FakeExtensionHost("fr-FR");
                using var viewModel = Create(host);

                Assert.Equal("Run once", viewModel.Strings["RunButton"]);
            });
        }
    }
}
