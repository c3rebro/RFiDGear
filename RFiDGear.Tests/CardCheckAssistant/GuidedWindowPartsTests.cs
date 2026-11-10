using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Extensions.CardCheckAssistant.Services;
using RFiDGear.Extensions.CardCheckAssistant.Views;
using RFiDGear.Extensions.CardCheckAssistant.Workflow;
using RFiDGear.Tests.CardCheck;

using Xunit;

namespace RFiDGear.Tests.CardCheckAssistant
{
    public sealed class GuidedWindowPartsTests : IDisposable
    {
        private readonly TempFolder _folder = new TempFolder();
        private readonly RecordingCardCheckLog _log = new RecordingCardCheckLog();

        public void Dispose() => _folder.Dispose();

        [Fact]
        public Task TheRealWorkflow_ReadsTheMadeUpOrdersInTheTestMode_AndDoesNotTouchTheOrderSystem() => StaTestRunner.RunOnStaThreadAsync(async () =>
        {
            var settings = CardCheckServices.CreateSettingsStore(Path.Combine(_folder.Path, "settings"), _log);
            Assert.True(settings.Save(new CardCheckSettings { UseStubJobs = true, Report = new ReportSettings { OutputPath = _folder.Path } }).IsSuccess);
            var host = new FakeExtensionHost();

            using var workflow = (CardCheckWorkflow)GuidedWorkflowFactory.Create(host, settings, _log, new OrderReservations(), Path.Combine(_folder.Path, "fallback"));
            Assert.Equal(WorkflowState.Idle, workflow.Current.State);

            Assert.True(workflow.RefreshOrders());
            await workflow.CurrentOperation.WaitAsync(TimeSpan.FromSeconds(30));

            Assert.Null(workflow.Current.OrdersFailure);
            Assert.True(workflow.Current.IsTestMode);
            Assert.Equal(5, workflow.Current.Orders.Count);
            Assert.True(Directory.Exists(Path.Combine(_folder.Path, "Test")));
        });

        [Fact]
        public Task TheRealWorkflow_RunsTheWholeCheckOnTheHost_ToAFinishedOrder() => StaTestRunner.RunOnStaThreadAsync(async () =>
        {
            using var area = new TempArea();
            var settings = CardCheckServices.CreateSettingsStore(Path.Combine(area.Directory, "settings"), _log);
            var reports = Path.Combine(area.Directory, "reports");
            Directory.CreateDirectory(reports);
            Assert.True(settings.Save(new CardCheckSettings
            {
                UseStubJobs = true,
                DefaultProjectPath = area.ProjectPath,
                Report = new ReportSettings { OutputPath = reports },
            }).IsSuccess);

            // What RFiDGear does with a good run: the project writes the report it was given. A chip is on the reader.
            var host = new FakeExtensionHost();
            host.ExecutionHost.OnExecute = request => ReportFixtures.WriteFirstReport(request.Variables["REPORTTARGETPATH"], "No", "No", "N/A");
            host.ReaderHost.Show(Snapshots.Card("04AABBCC"), raise: false);

            using var workflow = (CardCheckWorkflow)GuidedWorkflowFactory.Create(host, settings, _log, new OrderReservations(), Path.Combine(area.Directory, "fallback"));
            Task Done() => workflow.CurrentOperation.WaitAsync(TimeSpan.FromSeconds(60));

            Assert.True(workflow.RefreshOrders());
            await Done();
            Assert.True(workflow.Start("STUB-3", ReportLanguage.German));
            await Done();
            Assert.Equal(WorkflowState.WaitingForCard, workflow.Current.State);
            Assert.Equal("04AABBCC", workflow.Current.Card!.Credentials[0].Uid);

            Assert.True(workflow.StartCheck());
            await Done();
            Assert.Equal(WorkflowState.Assessing, workflow.Current.State);
            Assert.Equal(ChipVerdict.NotSupported, workflow.Current.Selection!.Verdict);
            var request = Assert.Single(host.ExecutionHost.Requests);
            Assert.Equal("STUB-1003", request.Variables["$JOBNUMBER"]);
            Assert.Single(host.ProjectHost.LoadedPaths);
            Assert.True(File.Exists(workflow.Current.Check!.ReportPath));

            Assert.True(workflow.Finish());
            await Done();
            Assert.Equal(WorkflowState.Completed, workflow.Current.State);
        });

        [Fact]
        public Task AWorkflowWithNothingToWorkFrom_SaysSo_WhenTheListIsRead() => StaTestRunner.RunOnStaThreadAsync(async () =>
        {
            using var workflow = (CardCheckWorkflow)GuidedWorkflowFactory.CreateUnavailable(new FakeExtensionHost(), _log, new OrderReservations());

            Assert.True(workflow.RefreshOrders());
            await workflow.CurrentOperation.WaitAsync(TimeSpan.FromSeconds(30));

            Assert.Equal(WorkflowState.Idle, workflow.Current.State);
            Assert.Equal(ContextFailureCodes.Unavailable, workflow.Current.OrdersFailure!.Code);
            Assert.Empty(workflow.Current.Orders);
        });

        [Fact]
        public void TheFactory_NeedsItsParts()
        {
            var host = new FakeExtensionHost();
            var settings = CardCheckServices.CreateSettingsStore(Path.Combine(_folder.Path, "settings"), _log);
            var reservations = new OrderReservations();

            Assert.Throws<ArgumentNullException>(() => GuidedWorkflowFactory.Create(null!, settings, _log, reservations, _folder.Path));
            Assert.Throws<ArgumentNullException>(() => GuidedWorkflowFactory.Create(host, null!, _log, reservations, _folder.Path));
            Assert.Throws<ArgumentNullException>(() => GuidedWorkflowFactory.Create(host, settings, null!, reservations, _folder.Path));
            Assert.Throws<ArgumentNullException>(() => GuidedWorkflowFactory.CreateUnavailable(null!, _log, reservations));
            Assert.Throws<ArgumentNullException>(() => GuidedWorkflowFactory.CreateUnavailable(host, null!, reservations));
        }

        [Fact]
        public Task TheDispatcherTimer_TicksOnTheWindowsThread_UntilItIsDisposed() => StaTestRunner.RunOnStaThreadAsync(async () =>
        {
            var ticks = 0;
            var threads = new System.Collections.Concurrent.ConcurrentBag<int>();
            var enough = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var start = DispatcherTimerStarter.For(Dispatcher.CurrentDispatcher);

            var timer = start(
                TimeSpan.FromMilliseconds(20),
                () =>
                {
                    threads.Add(Environment.CurrentManagedThreadId);
                    if (Interlocked.Increment(ref ticks) == 2)
                    {
                        enough.TrySetResult(true);
                    }
                });
            await enough.Task.WaitAsync(TimeSpan.FromSeconds(30));
            timer.Dispose();
            var stopped = Volatile.Read(ref ticks);
            await Task.Delay(150);

            Assert.Equal(stopped, Volatile.Read(ref ticks));
            Assert.All(threads, id => Assert.Equal(Environment.CurrentManagedThreadId, id));
            Assert.Throws<ArgumentNullException>(() => DispatcherTimerStarter.For(null!));
        });
    }
}
