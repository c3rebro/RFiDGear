#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;

using RFiDGear.Bridge;
using RFiDGear.Contracts;
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Services.ExternalControl;
using RFiDGear.Services.TaskExecution;
using RFiDGear.Tests.AutoMode;

using Xunit;

using static RFiDGear.Tests.AutoMode.AutoModeMainWindowTests;

namespace RFiDGear.Tests.ExternalControl
{
    /// <summary>
    /// The bridge on the host of the real main window: the real state machine of automatic mode, with a scripted reader and a
    /// scripted execution service, and the clock driven by hand. What the fake host promises about the order of the notices is
    /// shown here to be what the mode does. These tests need the same things as the other tests that build a main window.
    /// </summary>
    [Collection("SettingsFileAccess")]
    public class BridgeAutoModeRealHostTests
    {
        private const string CardA = "0123456789";
        private const string CardB = "9876543210";

        private static readonly Func<Action, Task> RunAtOnce = action =>
        {
            action();

            return Task.CompletedTask;
        };

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            var until = DateTime.UtcNow.AddSeconds(5);

            while (!condition())
            {
                Assert.True(DateTime.UtcNow < until, "The condition did not become true in time.");
                await Task.Delay(10);
            }
        }

        [Fact]
        public async Task ACardInAModeAClientStarted_IsReportedBeforeTheModeStops_AndTheFailureOfOneRunStopsNothingBeforeThat()
        {
            await AutoModeHostTests.WithHost(async (viewModel, host, provider, exec, _) =>
            {
                var pipe = "RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N");
                using var service = new ExternalControlService(viewModel.ExtensionHost, RunAtOnce, () => false, "2.0.10", pipe);
                Assert.True(service.Start());
                service.MarkStartupComplete();
                using var client = await BridgeTestClient.ConnectAsync(pipe, 5000);
                await client.HelloAsync(BridgeEvents.AutoModeChanged, BridgeEvents.AutoRunCompleted);

                // The first card ends with an ordinary failure of a task: the mode goes on. The second one faults: the mode stops after it.
                var started = await client.Service.StartAutoMode(new StartAutoModeRequest());
                exec.OnExecute = _ => new TaskExecutionResult { TerminalStatus = TaskLoopTerminalStatus.Failure };
                provider.Card = Card(CardA);
                await viewModel.AutoMode.TickAsync();
                await client.WaitForEventsAsync(2);
                var stillOn = host.IsAutoModeEnabled;

                provider.Card = new RFiDGear.Models.GenericChipModel();
                await viewModel.AutoMode.TickAsync();
                exec.OnExecute = _ => throw new InvalidOperationException("The reader failed.");
                provider.Card = Card(CardA);
                await viewModel.AutoMode.TickAsync();
                var events = await client.WaitForEventsAsync(4);
                await Task.Delay(200);
                var list = await client.Service.ListRunResults(new ListRunResultsRequest());

                Assert.True(started.Enabled);
                Assert.True(stillOn);
                Assert.False(host.IsAutoModeEnabled);
                Assert.Equal(4, client.Events.Count);

                // On, first run (not stopped by a failure of a task), second run, off, in that order: the result before the notice.
                Assert.IsType<AutoModeChangedEvent>(events[0]);
                var first = Assert.IsType<AutoRunCompletedEvent>(events[1]);
                var second = Assert.IsType<AutoRunCompletedEvent>(events[2]);
                var off = Assert.IsType<AutoModeChangedEvent>(events[3]);
                Assert.Equal(new long[] { 1, 2, 3, 4 }, events.Select(e => e.Seq));
                Assert.Equal(new long[] { 1, 2 }, new[] { first.Result.ResultSeq, second.Result.ResultSeq });
                Assert.All(new[] { first.Result, second.Result }, r => Assert.Equal(RunSources.AutoMode, r.Source));
                Assert.Equal(FailureReasons.Faulted, second.Result.FailureReason);
                Assert.False(off.Enabled);
                Assert.Equal(AutoModeStopReasons.RunAborted, off.LastStopReason);
                Assert.Equal(2, off.LastResultSeq);
                Assert.Equal(new long[] { 1, 2 }, list.Results.Select(r => r.ResultSeq));
                Assert.Equal(RunSources.AutoMode, list.Results[1].Source);
            });
        }

        [Fact]
        public async Task TheResultOfACardOfTheMode_SaysWhichStateOfTheProjectTheWindowTookTheRunWith()
        {
            await AutoModeHostTests.WithHost(async (viewModel, host, provider, exec, _) =>
            {
                var pipe = "RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N");
                using var service = new ExternalControlService(viewModel.ExtensionHost, RunAtOnce, () => false, "2.0.10", pipe, runFacts: viewModel.RunFacts);
                Assert.True(service.Start());
                service.MarkStartupComplete();
                using var client = await BridgeTestClient.ConnectAsync(pipe, 5000);
                await client.HelloAsync(BridgeEvents.AutoModeChanged, BridgeEvents.AutoRunCompleted);
                var expected = ((IProjectRevisionSource)viewModel.ExtensionHost.Project).GetProjectRevision();

                await client.Service.StartAutoMode(new StartAutoModeRequest());
                exec.OnExecute = _ => new TaskExecutionResult { TerminalStatus = TaskLoopTerminalStatus.Success };
                provider.Card = Card(CardA);
                await viewModel.AutoMode.TickAsync();
                var events = await client.WaitForEventsAsync(2);

                var completed = Assert.IsType<AutoRunCompletedEvent>(events[1]);
                Assert.Matches("^[0-9a-f]{32}$", completed.Result.ProjectRevision);
                Assert.Equal(expected, completed.Result.ProjectRevision);
                Assert.True(host.IsAutoModeEnabled);
            });
        }

        [Fact]
        public async Task WithoutAWindowThatNotesIt_TheResultOfACardOfTheMode_HasNoRevision()
        {
            await AutoModeHostTests.WithHost(async (viewModel, _, provider, exec, _) =>
            {
                var pipe = "RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N");
                using var service = new ExternalControlService(viewModel.ExtensionHost, RunAtOnce, () => false, "2.0.10", pipe);
                Assert.True(service.Start());
                service.MarkStartupComplete();
                using var client = await BridgeTestClient.ConnectAsync(pipe, 5000);
                await client.HelloAsync(BridgeEvents.AutoModeChanged, BridgeEvents.AutoRunCompleted);

                await client.Service.StartAutoMode(new StartAutoModeRequest());
                exec.OnExecute = _ => new TaskExecutionResult { TerminalStatus = TaskLoopTerminalStatus.Success };
                provider.Card = Card(CardA);
                await viewModel.AutoMode.TickAsync();
                var events = await client.WaitForEventsAsync(2);

                var completed = Assert.IsType<AutoRunCompletedEvent>(events[1]);
                Assert.Null(completed.Result.ProjectRevision);

                // Nobody takes the facts, so the window did not make a revision for them: a revision costs a look at the whole project.
                Assert.Null(viewModel.RunFacts.Take(completed.Result.RunId));
            });
        }

        private static StartAutoModeRequest WithApproval() =>
            new StartAutoModeRequest { StopOnDisconnect = true, RequireApproval = true };

        [Fact]
        public async Task ACardThatTheClientApproves_RunsWithTheVariablesOfThatApprovalOnly_AndItsResultNamesThePresence()
        {
            await AutoModeHostTests.WithHost(async (viewModel, _, provider, exec, _) =>
            {
                var pipe = "RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N");
                using var service = new ExternalControlService(viewModel.ExtensionHost, RunAtOnce, () => false, "2.0.10", pipe, runFacts: viewModel.RunFacts);
                Assert.True(service.Start());
                service.MarkStartupComplete();
                using var client = await BridgeTestClient.ConnectAsync(pipe, 5000);
                await client.HelloAsync(BridgeEvents.CardPresented, BridgeEvents.CardPresentationEnded, BridgeEvents.AutoRunCompleted);
                var revision = ((IProjectRevisionSource)viewModel.ExtensionHost.Project).GetProjectRevision();
                await client.Service.StartAutoMode(WithApproval());

                // The first card is approved with a variable, the second (the same card, put back) without any.
                provider.Card = Card(CardA);
                var firstTick = viewModel.AutoMode.TickAsync();
                var firstShown = Assert.IsType<CardPresentedEvent>((await client.WaitForEventsAsync(1))[0]);
                await client.Service.ApproveCard(new ApproveCardRequest
                {
                    PresenceId = firstShown.PresenceId,
                    ExpectProjectRevision = revision,
                    Variables = new[] { new VariablePair { Name = "$EXPECTEDUID", Value = CardA } },
                });
                await AutoModeMainWindowTests.Within(firstTick);

                provider.Card = new RFiDGear.Models.GenericChipModel();
                await AutoModeMainWindowTests.Within(viewModel.AutoMode.TickAsync());
                provider.Card = Card(CardA);
                var secondTick = viewModel.AutoMode.TickAsync();
                var secondShown = Assert.IsType<CardPresentedEvent>((await client.WaitForEventsAsync(4))[3]);
                await client.Service.ApproveCard(new ApproveCardRequest { PresenceId = secondShown.PresenceId });
                await AutoModeMainWindowTests.Within(secondTick);
                var events = await client.WaitForEventsAsync(6);

                Assert.Equal(2, exec.Requests.Count);
                Assert.Equal(CardA, exec.Requests[0].VariablesFromArgs["$EXPECTEDUID"]);
                Assert.False(exec.Requests[1].VariablesFromArgs.ContainsKey("$EXPECTEDUID"));
                Assert.NotEqual(firstShown.PresenceId, secondShown.PresenceId);
                Assert.Equal(revision, firstShown.ProjectRevision);

                // The presence ends with the run that was admitted for it, and the result of that run says for which presence it ran.
                var ended = Assert.IsType<CardPresentationEndedEvent>(events[1]);
                var completed = Assert.IsType<AutoRunCompletedEvent>(events[2]);
                Assert.Equal((firstShown.PresenceId, PresentationEndReasons.Approved, exec.Requests[0].RunId), (ended.PresenceId, ended.Reason, ended.RunId));
                Assert.Equal((exec.Requests[0].RunId, firstShown.PresenceId), (completed.Result.RunId, completed.Result.PresenceId));
                Assert.Equal(secondShown.PresenceId, Assert.IsType<AutoRunCompletedEvent>(events[5]).Result.PresenceId);
            });
        }

        [Fact]
        public async Task TheReportVariablesOfAnApproval_AreTheReportOfThatCardOnly()
        {
            await AutoModeHostTests.WithHost(async (viewModel, _, provider, exec, _) =>
            {
                var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "RFiDGear.Tests", Guid.NewGuid().ToString("N"));
                System.IO.Directory.CreateDirectory(folder);

                try
                {
                    var pipe = "RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N");
                    using var service = new ExternalControlService(viewModel.ExtensionHost, RunAtOnce, () => false, "2.0.10", pipe, runFacts: viewModel.RunFacts);
                    Assert.True(service.Start());
                    service.MarkStartupComplete();
                    using var client = await BridgeTestClient.ConnectAsync(pipe, 5000);
                    await client.HelloAsync(BridgeEvents.CardPresented, BridgeEvents.CardPresentationEnded);
                    await client.Service.StartAutoMode(WithApproval());
                    var target = System.IO.Path.Combine(folder, "report.pdf");

                    // What the real service does with the report state of its request: it keeps it in its result (and makes a writer when
                    // there is none), and the window keeps it for the next run unless it was the report of one card only.
                    var defaultFolder = new RFiDGear.Infrastructure.FileAccess.ProjectManager().AppDataPath;
                    var writerOfTheFirstRun = new RFiDGear.Infrastructure.FileAccess.ReportReaderWriter(
                        new RFiDGear.Infrastructure.FileAccess.ProjectManager(System.IO.Path.Combine(folder, "window")));
                    string workFolderOfTheApprovedRun = null;

                    exec.OnExecute = request =>
                    {
                        // The writer of the report that an approval named: where does it build its report, and does it use that folder?
                        if (request.ReportOutputPath != null && request.ReportReaderWriter != null)
                        {
                            var manager = (RFiDGear.Infrastructure.FileAccess.ProjectManager)typeof(RFiDGear.Infrastructure.FileAccess.ReportReaderWriter)
                                .GetField("projectManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                                .GetValue(request.ReportReaderWriter)!;
                            workFolderOfTheApprovedRun = manager.AppDataPath;

                            // Never in the folder of the application (a regression would write there for real).
                            if (!string.Equals(manager.AppDataPath, defaultFolder, StringComparison.OrdinalIgnoreCase))
                            {
                                System.IO.File.WriteAllText(manager.GetTemporaryReportTemplatePath(), "the report of the approved card");
                            }
                        }

                        return new TaskExecutionResult
                        {
                            TerminalStatus = TaskLoopTerminalStatus.Success,
                            ReportOutputPath = request.ReportOutputPath,
                            ReportReaderWriter = request.ReportReaderWriter ?? writerOfTheFirstRun,
                        };
                    };

                    // A card is put on the reader (after the card before it was taken away), shown, approved with the variables and run.
                    var shownBefore = 0;

                    async Task RunACardAsync(params VariablePair[] variables)
                    {
                        provider.Card = new RFiDGear.Models.GenericChipModel();
                        await AutoModeMainWindowTests.Within(viewModel.AutoMode.TickAsync());
                        provider.Card = Card(CardA);
                        var tick = viewModel.AutoMode.TickAsync();
                        var shown = Assert.IsType<CardPresentedEvent>((await client.WaitForEventsAsync(shownBefore + 1))[shownBefore]);
                        await client.Service.ApproveCard(new ApproveCardRequest { PresenceId = shown.PresenceId, Variables = variables });
                        await AutoModeMainWindowTests.Within(tick);

                        // The presented notice and the one that ends it.
                        await client.WaitForEventsAsync(shownBefore + 2);
                        shownBefore += 2;
                    }

                    await RunACardAsync();
                    await RunACardAsync(new VariablePair { Name = "REPORTTARGETPATH", Value = target });
                    await RunACardAsync();

                    // The report of the second card is its own: not what the window had, and not what the third card finds.
                    Assert.Equal(3, exec.Requests.Count);
                    Assert.Null(exec.Requests[0].ReportOutputPath);
                    Assert.Equal(target, exec.Requests[1].ReportOutputPath);
                    Assert.Null(exec.Requests[2].ReportOutputPath);
                    Assert.Same(writerOfTheFirstRun, exec.Requests[2].ReportReaderWriter);

                    // The report of the approved card is built by a writer of its own, in a work folder of its own (not the folder of the
                    // application and not the one of the writer of the window), and the folder is gone when the run is over.
                    Assert.NotNull(exec.Requests[1].ReportReaderWriter);
                    Assert.NotSame(writerOfTheFirstRun, exec.Requests[1].ReportReaderWriter);
                    Assert.NotNull(workFolderOfTheApprovedRun);
                    Assert.NotEqual(defaultFolder, workFolderOfTheApprovedRun, StringComparer.OrdinalIgnoreCase);
                    Assert.NotEqual(System.IO.Path.Combine(folder, "window"), workFolderOfTheApprovedRun, StringComparer.OrdinalIgnoreCase);
                    Assert.False(System.IO.Directory.Exists(workFolderOfTheApprovedRun));
                }
                finally
                {
                    System.IO.Directory.Delete(folder, recursive: true);
                }
            });
        }

        [Fact]
        public async Task ACardThatTheClientDeclines_IsNeverRun_AndNotAskedAgainWhileItStays()
        {
            await AutoModeHostTests.WithHost(async (viewModel, host, provider, exec, _) =>
            {
                var pipe = "RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N");
                using var service = new ExternalControlService(viewModel.ExtensionHost, RunAtOnce, () => false, "2.0.10", pipe, runFacts: viewModel.RunFacts);
                Assert.True(service.Start());
                service.MarkStartupComplete();
                using var client = await BridgeTestClient.ConnectAsync(pipe, 5000);
                await client.HelloAsync(BridgeEvents.CardPresented, BridgeEvents.CardPresentationEnded);
                await client.Service.StartAutoMode(WithApproval());

                provider.Card = Card(CardA);
                var tick = viewModel.AutoMode.TickAsync();
                var shown = Assert.IsType<CardPresentedEvent>((await client.WaitForEventsAsync(1))[0]);
                await client.Service.DeclineCard(new DeclineCardRequest { PresenceId = shown.PresenceId });
                await AutoModeMainWindowTests.Within(tick);
                await AutoModeMainWindowTests.Within(viewModel.AutoMode.TickAsync());
                await Task.Delay(200);

                Assert.Empty(exec.Requests);
                Assert.Equal(2, client.Events.Count);
                Assert.Equal(PresentationEndReasons.Declined, Assert.IsType<CardPresentationEndedEvent>(client.Events[1]).Reason);
                Assert.True(host.IsAutoModeEnabled);
            });
        }

        [Fact]
        public async Task AnApprovalForAnotherStateOfTheProject_RunsNothing_AndTheSameCardIsOfferedAgainWithANewPresence()
        {
            await AutoModeHostTests.WithHost(async (viewModel, host, provider, exec, _) =>
            {
                var pipe = "RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N");
                using var service = new ExternalControlService(viewModel.ExtensionHost, RunAtOnce, () => false, "2.0.10", pipe, runFacts: viewModel.RunFacts);
                Assert.True(service.Start());
                service.MarkStartupComplete();
                using var client = await BridgeTestClient.ConnectAsync(pipe, 5000);
                await client.HelloAsync(BridgeEvents.CardPresented, BridgeEvents.CardPresentationEnded);
                await client.Service.StartAutoMode(WithApproval());

                provider.Card = Card(CardA);
                var tick = viewModel.AutoMode.TickAsync();
                var first = Assert.IsType<CardPresentedEvent>((await client.WaitForEventsAsync(1))[0]);
                await client.Service.ApproveCard(new ApproveCardRequest { PresenceId = first.PresenceId, ExpectProjectRevision = "ffffffffffffffffffffffffffffffff" });
                await AutoModeMainWindowTests.Within(tick);
                var events = await client.WaitForEventsAsync(2);

                // Nothing ran. The card is still there and the mode is on, so the next look offers it again, as a presence of its own.
                var secondTick = viewModel.AutoMode.TickAsync();
                var second = Assert.IsType<CardPresentedEvent>((await client.WaitForEventsAsync(3))[2]);
                await client.Service.DeclineCard(new DeclineCardRequest { PresenceId = second.PresenceId });
                await AutoModeMainWindowTests.Within(secondTick);

                Assert.Empty(exec.Requests);
                var ended = Assert.IsType<CardPresentationEndedEvent>(events[1]);
                Assert.Equal((first.PresenceId, PresentationEndReasons.ProjectChanged, null), (ended.PresenceId, ended.Reason, ended.RunId));
                Assert.NotEqual(first.PresenceId, second.PresenceId);
                Assert.True(host.IsAutoModeEnabled);
            });
        }

        [Fact]
        public async Task ACardThatIsReplacedAfterTheApproval_IsNotRunOnIt_AndTheOtherCardIsOfferedNext()
        {
            await AutoModeHostTests.WithHost(async (viewModel, host, provider, exec, _) =>
            {
                var pipe = "RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N");
                using var service = new ExternalControlService(viewModel.ExtensionHost, RunAtOnce, () => false, "2.0.10", pipe, runFacts: viewModel.RunFacts);
                Assert.True(service.Start());
                service.MarkStartupComplete();
                using var client = await BridgeTestClient.ConnectAsync(pipe, 5000);
                await client.HelloAsync(BridgeEvents.CardPresented, BridgeEvents.CardPresentationEnded);
                await client.Service.StartAutoMode(WithApproval());

                // Another card is put on the reader before the answer, and no tick looked at the reader in between: the window looks
                // once more before it admits the run, and the approval was given for the card that was shown.
                provider.Card = Card(CardA);
                var tick = viewModel.AutoMode.TickAsync();
                var first = Assert.IsType<CardPresentedEvent>((await client.WaitForEventsAsync(1))[0]);
                provider.Card = Card(CardB);
                await client.Service.ApproveCard(new ApproveCardRequest { PresenceId = first.PresenceId });
                await AutoModeMainWindowTests.Within(tick);
                var events = await client.WaitForEventsAsync(2);

                var secondTick = viewModel.AutoMode.TickAsync();
                var second = Assert.IsType<CardPresentedEvent>((await client.WaitForEventsAsync(3))[2]);
                await client.Service.DeclineCard(new DeclineCardRequest { PresenceId = second.PresenceId });
                await AutoModeMainWindowTests.Within(secondTick);

                Assert.Empty(exec.Requests);
                var ended = Assert.IsType<CardPresentationEndedEvent>(events[1]);
                Assert.Equal((first.PresenceId, PresentationEndReasons.CardChanged, null), (ended.PresenceId, ended.Reason, ended.RunId));
                Assert.Equal(CardB, second.Uid);
                Assert.NotEqual(first.PresenceId, second.PresenceId);
                Assert.True(host.IsAutoModeEnabled);
            });
        }

        [Fact]
        public async Task AReaderThatCannotBeLookedAtBeforeTheRun_IsNoProofOfTheCard_NothingRunsAndTheCardIsOfferedAgain()
        {
            await AutoModeHostTests.WithHost(async (viewModel, host, provider, exec, _) =>
            {
                var pipe = "RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N");
                using var service = new ExternalControlService(viewModel.ExtensionHost, RunAtOnce, () => false, "2.0.10", pipe, runFacts: viewModel.RunFacts);
                Assert.True(service.Start());
                service.MarkStartupComplete();
                using var client = await BridgeTestClient.ConnectAsync(pipe, 5000);
                await client.HelloAsync(BridgeEvents.CardPresented, BridgeEvents.CardPresentationEnded);
                await client.Service.StartAutoMode(WithApproval());

                provider.Card = Card(CardA);
                var tick = viewModel.AutoMode.TickAsync();
                var first = Assert.IsType<CardPresentedEvent>((await client.WaitForEventsAsync(1))[0]);

                // The look that the window takes before it admits the run fails: a card that cannot be told is not the one that was approved.
                provider.Script(RFiDGear.Infrastructure.ERROR.NoError, failure: new InvalidOperationException("The reader failed."));
                await client.Service.ApproveCard(new ApproveCardRequest { PresenceId = first.PresenceId });
                await AutoModeMainWindowTests.Within(tick);
                var events = await client.WaitForEventsAsync(2);

                var secondTick = viewModel.AutoMode.TickAsync();
                var second = Assert.IsType<CardPresentedEvent>((await client.WaitForEventsAsync(3))[2]);
                await client.Service.DeclineCard(new DeclineCardRequest { PresenceId = second.PresenceId });
                await AutoModeMainWindowTests.Within(secondTick);

                Assert.Empty(exec.Requests);
                Assert.Equal(PresentationEndReasons.NotAdmitted, Assert.IsType<CardPresentationEndedEvent>(events[1]).Reason);
                Assert.Equal((CardA, true), (second.Uid, second.PresenceId != first.PresenceId));
                Assert.True(host.IsAutoModeEnabled);
            });
        }

        [Fact]
        public async Task AModeThatIsStoppedWhileTheWindowLooksAtTheReader_RunsNothing_EvenWhenTheLookSucceeds()
        {
            await AutoModeHostTests.WithHost(async (viewModel, host, provider, exec, _) =>
            {
                var pipe = "RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N");
                using var service = new ExternalControlService(viewModel.ExtensionHost, RunAtOnce, () => false, "2.0.10", pipe, runFacts: viewModel.RunFacts);
                Assert.True(service.Start());
                service.MarkStartupComplete();
                using var client = await BridgeTestClient.ConnectAsync(pipe, 5000);
                await client.HelloAsync(BridgeEvents.CardPresented, BridgeEvents.CardPresentationEnded);
                await client.Service.StartAutoMode(WithApproval());

                provider.Card = Card(CardA);
                var tick = viewModel.AutoMode.TickAsync();
                var shown = Assert.IsType<CardPresentedEvent>((await client.WaitForEventsAsync(1))[0]);

                // The look that the window takes before it admits the run is held; the mode is stopped while it is outstanding, and then
                // the look ends with the right card. The request is no longer wanted, so nothing may be admitted.
                var slowLook = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                provider.Script(RFiDGear.Infrastructure.ERROR.NoError, Card(CardA), slowLook.Task);
                var nextRead = provider.ReadCount + 1;
                await client.Service.ApproveCard(new ApproveCardRequest { PresenceId = shown.PresenceId });
                await AutoModeMainWindowTests.Within(provider.ReadStarted(nextRead));
                await client.Service.StopAutoMode(new StopAutoModeRequest());
                slowLook.SetResult(true);
                await AutoModeMainWindowTests.Within(tick);
                var events = await client.WaitForEventsAsync(2);

                Assert.Empty(exec.Requests);
                Assert.Equal(PresentationEndReasons.NotAdmitted, Assert.IsType<CardPresentationEndedEvent>(events[1]).Reason);
                Assert.False(host.IsAutoModeEnabled);
            });
        }

        [Fact]
        public async Task ACardThatIsReplacedWhileTheClientDecides_IsWithdrawnByTheNextTick_AndALateApprovalIsRefused()
        {
            await AutoModeHostTests.WithHost(async (viewModel, host, provider, exec, _) =>
            {
                var pipe = "RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N");
                using var service = new ExternalControlService(viewModel.ExtensionHost, RunAtOnce, () => false, "2.0.10", pipe, runFacts: viewModel.RunFacts);
                Assert.True(service.Start());
                service.MarkStartupComplete();
                using var client = await BridgeTestClient.ConnectAsync(pipe, 5000);
                await client.HelloAsync(BridgeEvents.CardPresented, BridgeEvents.CardPresentationEnded);
                await client.Service.StartAutoMode(WithApproval());

                provider.Card = Card(CardA);
                var tick = viewModel.AutoMode.TickAsync();
                var first = Assert.IsType<CardPresentedEvent>((await client.WaitForEventsAsync(1))[0]);

                // The clock goes on ticking while the client decides: the tick that waits holds the machine, and this one only looks.
                provider.Card = Card(CardB);
                await AutoModeMainWindowTests.Within(viewModel.AutoMode.TickAsync());
                await AutoModeMainWindowTests.Within(tick);
                var events = await client.WaitForEventsAsync(2);
                var late = await Assert.ThrowsAsync<StreamJsonRpc.RemoteInvocationException>(() => client.Service.ApproveCard(new ApproveCardRequest { PresenceId = first.PresenceId }));

                var secondTick = viewModel.AutoMode.TickAsync();
                var second = Assert.IsType<CardPresentedEvent>((await client.WaitForEventsAsync(3))[2]);
                await client.Service.DeclineCard(new DeclineCardRequest { PresenceId = second.PresenceId });
                await AutoModeMainWindowTests.Within(secondTick);

                Assert.Empty(exec.Requests);
                Assert.Equal(PresentationEndReasons.CardChanged, Assert.IsType<CardPresentationEndedEvent>(events[1]).Reason);
                Assert.Equal(BridgeErrorCodes.ApprovalNotPending, (int)late.ErrorCode);
                Assert.Equal(CardB, second.Uid);
                Assert.True(host.IsAutoModeEnabled);
            });
        }

        [Fact]
        public async Task ACardThatTheRunFindsReplacedAtItsOwnRead_FailsTheRunBeforeTheFirstTask_AndStopsTheMode()
        {
            await AutoModeHostTests.WithHost(async (viewModel, host, provider, exec, _) =>
            {
                var pipe = "RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N");
                using var service = new ExternalControlService(viewModel.ExtensionHost, RunAtOnce, () => false, "2.0.10", pipe, runFacts: viewModel.RunFacts);
                Assert.True(service.Start());
                service.MarkStartupComplete();
                using var client = await BridgeTestClient.ConnectAsync(pipe, 5000);
                await client.HelloAsync(BridgeEvents.CardPresented, BridgeEvents.AutoRunCompleted, BridgeEvents.AutoModeChanged);
                await client.Service.StartAutoMode(WithApproval());

                // The card is the right one when the window looks, and another one when the run reads it for itself: the run ends there,
                // before any task, and the mode does not go on with a card it cannot vouch for.
                var tasksStarted = false;
                exec.OnExecute = request =>
                {
                    request.ChipHydrated(Card(CardB));
                    tasksStarted = true;

                    return new TaskExecutionResult { TerminalStatus = TaskLoopTerminalStatus.Success };
                };
                provider.Card = Card(CardA);
                var tick = viewModel.AutoMode.TickAsync();
                var shown = Assert.IsType<CardPresentedEvent>((await client.WaitForEventsAsync(2))[1]);
                await client.Service.ApproveCard(new ApproveCardRequest { PresenceId = shown.PresenceId });
                await AutoModeMainWindowTests.Within(tick);
                var events = await client.WaitForEventsAsync(4);

                Assert.False(tasksStarted);
                var completed = Assert.IsType<AutoRunCompletedEvent>(events[2]);
                Assert.Equal(FailureReasons.Faulted, completed.Result.FailureReason);
                Assert.False(Assert.IsType<AutoModeChangedEvent>(events[3]).Enabled);
                Assert.False(host.IsAutoModeEnabled);
            });
        }

        [Fact]
        public async Task AModeAClientStarted_EndsWhenItsConnectionEnds_AndTheMenuAndTheModeAgree()
        {
            await AutoModeHostTests.WithHost(async (viewModel, host, _, _, _) =>
            {
                var pipe = "RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N");
                using var service = new ExternalControlService(viewModel.ExtensionHost, RunAtOnce, () => false, "2.0.10", pipe);
                Assert.True(service.Start());
                service.MarkStartupComplete();
                var client = await BridgeTestClient.ConnectAsync(pipe, 5000);
                await client.HelloAsync();

                await client.Service.StartAutoMode(new StartAutoModeRequest());
                Assert.True(viewModel.IsWriteToAllChipAutoChecked);
                client.Dispose();
                await WaitUntilAsync(() => !host.IsAutoModeEnabled);

                Assert.Equal(AutoModeStopReason.StopRequested, host.LastStopReason);
                Assert.False(viewModel.IsWriteToAllChipAutoChecked);
            });
        }

        [Fact]
        public async Task StartsAndRefusals_AreTheOnesOfTheRealMode_ForAMainWindowWithoutATask()
        {
            await WithViewModel(async (viewModel, _, _, _) =>
            {
                var pipe = "RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N");
                using var service = new ExternalControlService(viewModel.ExtensionHost, RunAtOnce, () => false, "2.0.10", pipe);
                Assert.True(service.Start());
                service.MarkStartupComplete();
                using var client = await BridgeTestClient.ConnectAsync(pipe, 5000);
                await client.HelloAsync();

                var failure = await Assert.ThrowsAsync<StreamJsonRpc.RemoteInvocationException>(() => client.Service.StartAutoMode(new StartAutoModeRequest()));

                Assert.Equal(BridgeErrorCodes.NoProjectLoaded, (int)failure.ErrorCode);
                Assert.False(viewModel.ExtensionHost.AutoMode.IsAutoModeEnabled);
            });
        }
    }
}
