#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using RFiDGear.Bridge;
using RFiDGear.Contracts;
using RFiDGear.Services.AutoMode;
using RFiDGear.Services.ExternalControl;
using RFiDGear.Tests.CardCheckAssistant;

using StreamJsonRpc;

using Xunit;

namespace RFiDGear.Tests.ExternalControl
{
    /// <summary>
    /// Automatic mode that waits for the decision of the client that started it, over a real pipe: what a start with approval
    /// promises and refuses, the notices that show a card and say how its presence ended, who may answer and when, and what each
    /// way out of the wait runs (nothing). The machine is played by the test against the approval the host was given.
    /// </summary>
    public sealed class BridgeAutoModeApprovalTests : IDisposable
    {
        private readonly List<IDisposable> _disposables = new List<IDisposable>();

        public void Dispose()
        {
            foreach (var disposable in Enumerable.Reverse(_disposables))
            {
                disposable.Dispose();
            }
        }

        private static readonly Func<Action, Task> RunAtOnce = action =>
        {
            action();

            return Task.CompletedTask;
        };

        private (ExternalControlService Service, string PipeName, FakeExtensionHost Host) Start(RunFacts? facts = null)
        {
            var name = "RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N");
            var host = new FakeExtensionHost();
            host.ProjectHost.CurrentPath = @"C:\projects\one.rfPrj";
            host.ProjectHost.TaskCount = 2;
            host.ProjectHost.Revision = "rev-1";
            var service = new ExternalControlService(host, RunAtOnce, () => false, "2.0.10", name, runFacts: facts);
            _disposables.Add(service);
            Assert.True(service.Start());
            service.MarkStartupComplete();

            return (service, name, host);
        }

        private async Task<BridgeTestClient> ConnectAsync(string pipeName, params string[] events)
        {
            var client = await BridgeTestClient.ConnectAsync(pipeName, 5000);
            _disposables.Add(client);
            await client.HelloAsync(events);

            return client;
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            var until = DateTime.UtcNow.AddSeconds(5);

            while (!condition())
            {
                Assert.True(DateTime.UtcNow < until, "The condition did not become true in time.");
                await Task.Delay(10);
            }
        }

        private static (int Code, BridgeErrorData Data) ErrorOf(RemoteInvocationException failure) => ((int)failure.ErrorCode, Assert.IsType<BridgeErrorData>(BridgeTestClient.DataOf(failure)));

        private static string Json(object value) => System.Text.Json.JsonSerializer.Serialize(value, value.GetType(), BridgeJson.CreateClientOptions());

        private static StartAutoModeRequest WithApproval(int? timeoutMs = null) =>
            new StartAutoModeRequest { StopOnDisconnect = true, RequireApproval = true, ApprovalTimeoutMs = timeoutMs };

        private static Task<AutoModeApproval?> Present(FakeExtensionHost host, string presenceId = "p-1", string uid = "04AA") =>
            Task.Run(() => host.AutoModeHost.Approval!.RequestAsync(new AutoModePresence(presenceId, uid)));

        // ── What is offered ──

        [Fact]
        public async Task TheApproval_IsOffered_WithItsTwoNotices_AndAPlainStartIsStillAPlainStart()
        {
            var (_, pipe, host) = Start();
            var client = await BridgeTestClient.ConnectAsync(pipe, 5000);
            _disposables.Add(client);

            var hello = await client.HelloAsync(BridgeEvents.CardPresented, BridgeEvents.CardPresentationEnded);
            var response = await client.Service.StartAutoMode(new StartAutoModeRequest());
            var status = await client.Service.GetStatus(new GetStatusRequest());

            Assert.Contains(BridgeCapabilities.AutoModeApproval, hello.Capabilities);
            Assert.Contains(BridgeEvents.CardPresented, hello.Events);
            Assert.Contains(BridgeEvents.CardPresentationEnded, hello.Events);
            Assert.False(response.RequireApproval);
            Assert.Null(response.ApprovalTimeoutMs);
            Assert.Null(host.AutoModeHost.Approval);
            Assert.False(status.AutoMode!.RequireApproval);
            Assert.Null(status.AutoMode.PendingPresenceId);
        }

        [Fact]
        public void ARequestWithoutApproval_SaysNothingAboutIt_SoThatAnOlderHostStillAcceptsIt()
        {
            var plain = Json(new StartAutoModeRequest());
            var approval = Json(new ApproveCardRequest { PresenceId = "p-1" });

            Assert.DoesNotContain("requireApproval", plain);
            Assert.DoesNotContain("approvalTimeoutMs", plain);
            Assert.DoesNotContain("expectProjectRevision", approval);
            Assert.DoesNotContain("variables", approval);
        }

        // ── Start ──

        [Fact]
        public async Task AStartWithApproval_SwitchesTheModeOn_WithTheApprovalThatTheMachineAsks_AndEchoesTheLimit()
        {
            var (_, pipe, host) = Start();
            var client = await ConnectAsync(pipe);

            var response = await client.Service.StartAutoMode(WithApproval());
            var status = await client.Service.GetStatus(new GetStatusRequest());

            Assert.True(response.Enabled);
            Assert.True(response.StopOnDisconnect);
            Assert.True(response.RequireApproval);
            Assert.Equal(BridgeProtocol.DefaultApprovalTimeoutMs, response.ApprovalTimeoutMs);
            Assert.NotNull(host.AutoModeHost.Approval);
            Assert.True(status.AutoMode!.RequireApproval);
            Assert.Null(status.AutoMode.PendingPresenceId);
        }

        [Fact]
        public async Task ATimeLimitOfTheClient_IsKept()
        {
            var (_, pipe, _) = Start();
            var client = await ConnectAsync(pipe);

            var response = await client.Service.StartAutoMode(WithApproval(30000));

            Assert.Equal(30000, response.ApprovalTimeoutMs);
        }

        [Theory]
        [InlineData(false, 5000, "approvalTimeoutMs")]
        [InlineData(true, 1999, "approvalTimeoutMs")]
        [InlineData(true, 120001, "approvalTimeoutMs")]
        public async Task AStartThatBreaksTheRulesOfTheApproval_IsRefused_AndSwitchesNothingOn(bool requireApproval, int timeoutMs, string field)
        {
            var (_, pipe, host) = Start();
            var client = await ConnectAsync(pipe);

            var failure = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.StartAutoMode(
                new StartAutoModeRequest { StopOnDisconnect = true, RequireApproval = requireApproval, ApprovalTimeoutMs = timeoutMs }));

            var (code, data) = ErrorOf(failure);
            Assert.Equal(BridgeErrorCodes.InvalidRequest, code);
            Assert.Equal(field, data.Details!["field"]);
            Assert.Equal(0, host.AutoModeHost.StartCalls);
            Assert.False(host.AutoModeHost.IsAutoModeEnabled);
        }

        [Fact]
        public async Task AnApprovalWithoutTheAgreementToEndTheModeWithTheConnection_IsRefused_ForNobodyWouldBeLeftToAnswer()
        {
            var (_, pipe, host) = Start();
            var client = await ConnectAsync(pipe);

            var failure = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.StartAutoMode(
                new StartAutoModeRequest { StopOnDisconnect = false, RequireApproval = true }));

            var (code, data) = ErrorOf(failure);
            Assert.Equal(BridgeErrorCodes.InvalidRequest, code);
            Assert.Equal("stopOnDisconnect", data.Details!["field"]);
            Assert.Equal(0, host.AutoModeHost.StartCalls);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public async Task AModeThatIsOnIsNeverAgreedOnAgain_WhenOneOfTheTwoStartsWaitsForApproval(bool firstWithApproval, bool secondWithApproval)
        {
            var (_, pipe, host) = Start();
            var client = await ConnectAsync(pipe);
            await client.Service.StartAutoMode(firstWithApproval ? WithApproval() : new StartAutoModeRequest());

            var failure = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.StartAutoMode(
                secondWithApproval ? WithApproval() : new StartAutoModeRequest()));

            Assert.Equal(BridgeErrorCodes.AutoModeActive, ErrorOf(failure).Code);
            Assert.True(host.AutoModeHost.IsAutoModeEnabled);
        }

        [Fact]
        public async Task AModeTheUserSwitchedOn_IsNotTakenOverByAnApprovalStart()
        {
            var (_, pipe, host) = Start();
            var client = await ConnectAsync(pipe);
            host.AutoModeHost.TryStart();

            var failure = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.StartAutoMode(WithApproval()));

            Assert.Equal(BridgeErrorCodes.AutoModeActive, ErrorOf(failure).Code);
            Assert.Null(host.AutoModeHost.Approval);
        }

        // ── The answer ──

        [Fact]
        public async Task ACardIsShown_ApprovedOnceWithItsVariables_AndTheNoticeThatEndsItNamesTheRun()
        {
            var (_, pipe, host) = Start();
            var client = await ConnectAsync(pipe, BridgeEvents.CardPresented, BridgeEvents.CardPresentationEnded);
            await client.Service.StartAutoMode(WithApproval());

            var waiting = Present(host, "p-1", "04AA");
            var shown = Assert.IsType<CardPresentedEvent>((await client.WaitForEventsAsync(1))[0]);
            var status = await client.Service.GetStatus(new GetStatusRequest());
            var answer = await client.Service.ApproveCard(new ApproveCardRequest
            {
                PresenceId = "p-1",
                ExpectProjectRevision = "rev-1",
                Variables = new[] { new VariablePair { Name = "$EXPECTEDUID", Value = "04AA" } },
            });
            var approval = await waiting;
            approval!.NotifyRunAdmitted("run-5");
            var events = await client.WaitForEventsAsync(2);

            Assert.Equal(("p-1", "04AA", "rev-1"), (shown.PresenceId, shown.Uid, shown.ProjectRevision));
            Assert.True(shown.ExpiresAt > shown.At);
            Assert.Equal("p-1", status.AutoMode!.PendingPresenceId);
            Assert.Equal("p-1", answer.PresenceId);
            Assert.Equal("04AA", approval.Variables["$EXPECTEDUID"]);
            Assert.Equal("rev-1", approval.ExpectProjectRevision);
            var ended = Assert.IsType<CardPresentationEndedEvent>(events[1]);
            Assert.Equal(("p-1", PresentationEndReasons.Approved, "run-5"), (ended.PresenceId, ended.Reason, ended.RunId));
            Assert.Null((await client.Service.GetStatus(new GetStatusRequest())).AutoMode!.PendingPresenceId);
        }

        [Fact]
        public async Task ADeclinedCard_RunsNothing_AndTheNoticeSaysSo()
        {
            var (_, pipe, host) = Start();
            var client = await ConnectAsync(pipe, BridgeEvents.CardPresented, BridgeEvents.CardPresentationEnded);
            await client.Service.StartAutoMode(WithApproval());

            var waiting = Present(host);
            await client.WaitForEventsAsync(1);
            var answer = await client.Service.DeclineCard(new DeclineCardRequest { PresenceId = "p-1" });
            var events = await client.WaitForEventsAsync(2);

            Assert.Equal("p-1", answer.PresenceId);
            Assert.Null(await waiting);
            Assert.Equal(PresentationEndReasons.Declined, Assert.IsType<CardPresentationEndedEvent>(events[1]).Reason);
            Assert.True(host.AutoModeHost.IsAutoModeEnabled);
        }

        [Fact]
        public async Task ACardNobodyAnswersFor_RunsNothingWhenTheTimeIsUp_AndALateApprovalIsRefused()
        {
            var (_, pipe, host) = Start();
            var client = await ConnectAsync(pipe, BridgeEvents.CardPresented, BridgeEvents.CardPresentationEnded);
            await client.Service.StartAutoMode(WithApproval(BridgeProtocol.MinApprovalTimeoutMs));

            var waiting = Present(host);
            var events = await client.WaitForEventsAsync(2);
            var late = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.ApproveCard(new ApproveCardRequest { PresenceId = "p-1" }));

            Assert.Null(await waiting);
            Assert.Equal(PresentationEndReasons.Expired, Assert.IsType<CardPresentationEndedEvent>(events[1]).Reason);
            var (code, data) = ErrorOf(late);
            Assert.Equal(BridgeErrorCodes.ApprovalNotPending, code);
            Assert.Equal(BridgeErrorKinds.ApprovalNotPending, data.Kind);
            Assert.True(data.NeverStarted);
        }

        [Fact]
        public async Task AnAnswerForAnotherPresence_IsRefused_AndTheCardKeepsWaiting()
        {
            var (_, pipe, host) = Start();
            var client = await ConnectAsync(pipe, BridgeEvents.CardPresented);
            await client.Service.StartAutoMode(WithApproval());
            var waiting = Present(host);
            await client.WaitForEventsAsync(1);

            var refused = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.ApproveCard(new ApproveCardRequest { PresenceId = "p-0" }));
            var stillWaiting = !waiting.IsCompleted;
            await client.Service.ApproveCard(new ApproveCardRequest { PresenceId = "p-1" });

            Assert.Equal(BridgeErrorCodes.ApprovalNotPending, ErrorOf(refused).Code);
            Assert.True(stillWaiting);
            Assert.NotNull(await waiting);
        }

        [Fact]
        public async Task AnAnswerThatBreaksTheRules_IsRefusedAsInvalid_AndTheCardKeepsWaiting()
        {
            var (_, pipe, host) = Start();
            var client = await ConnectAsync(pipe, BridgeEvents.CardPresented);
            await client.Service.StartAutoMode(WithApproval());
            var waiting = Present(host);
            await client.WaitForEventsAsync(1);

            var refused = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.ApproveCard(
                new ApproveCardRequest { PresenceId = "p-1", ExpectProjectRevision = "has space!" }));
            var stillWaiting = !waiting.IsCompleted;
            await client.Service.DeclineCard(new DeclineCardRequest { PresenceId = "p-1" });

            var (code, data) = ErrorOf(refused);
            Assert.Equal(BridgeErrorCodes.InvalidRequest, code);
            Assert.Equal("expectProjectRevision", data.Details!["field"]);
            Assert.True(stillWaiting);
            Assert.Null(await waiting);
        }

        [Fact]
        public async Task WithoutAnyCardWaitedFor_OrWithoutTheModeOrWithAModeThatRunsEveryCard_AnAnswerIsNotPending()
        {
            var (_, pipe, host) = Start();
            var client = await ConnectAsync(pipe);

            var modeOff = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.ApproveCard(new ApproveCardRequest { PresenceId = "p-1" }));
            await client.Service.StartAutoMode(new StartAutoModeRequest());
            var plainMode = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.DeclineCard(new DeclineCardRequest { PresenceId = "p-1" }));
            await client.Service.StopAutoMode(new StopAutoModeRequest());
            await client.Service.StartAutoMode(WithApproval());
            var noCard = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.ApproveCard(new ApproveCardRequest { PresenceId = "p-1" }));

            Assert.All(new[] { modeOff, plainMode, noCard }, failure => Assert.Equal(BridgeErrorCodes.ApprovalNotPending, ErrorOf(failure).Code));
            Assert.True(host.AutoModeHost.IsAutoModeEnabled);
        }

        // ── The ways out of the wait that are not an answer ──

        [Fact]
        public async Task AModeThatIsStoppedWhileACardIsWaitedFor_RunsNothing_AndTheNoticeSaysStopped()
        {
            var (_, pipe, host) = Start();
            var client = await ConnectAsync(pipe, BridgeEvents.CardPresented, BridgeEvents.CardPresentationEnded);
            await client.Service.StartAutoMode(WithApproval());
            var waiting = Present(host);
            await client.WaitForEventsAsync(1);

            await client.Service.StopAutoMode(new StopAutoModeRequest());
            var events = await client.WaitForEventsAsync(2);

            Assert.Null(await waiting);
            Assert.Equal(PresentationEndReasons.Stopped, Assert.IsType<CardPresentationEndedEvent>(events[1]).Reason);
            var late = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.ApproveCard(new ApproveCardRequest { PresenceId = "p-1" }));
            Assert.Equal(BridgeErrorCodes.ApprovalNotPending, ErrorOf(late).Code);
        }

        [Fact]
        public async Task AConnectionThatEndsWhileACardIsWaitedFor_RunsNothing_AndTheModeEndsWithIt()
        {
            var (service, pipe, host) = Start();
            var client = await ConnectAsync(pipe, BridgeEvents.CardPresented);
            await client.Service.StartAutoMode(WithApproval());
            var waiting = Present(host);
            await client.WaitForEventsAsync(1);

            client.Dispose();
            var approval = await waiting;
            await WaitUntilAsync(() => !host.AutoModeHost.IsAutoModeEnabled);

            Assert.Null(approval);
            Assert.Equal(AutoModeStopReason.StopRequested, host.AutoModeHost.LastStopReason);
            await WaitUntilAsync(() => !service.IsConnected);
        }

        // ── The results ──

        [Fact]
        public async Task TheResultOfACardThatWasApproved_SaysForWhichPresenceItRan_AndWithWhichRevision()
        {
            var facts = new RunFacts();
            var (_, pipe, host) = Start(facts);
            var client = await ConnectAsync(pipe, BridgeEvents.AutoRunCompleted);
            facts.Remember("run-a", new RunFact("rev-7", "p-1"));

            host.AutoModeHost.RaiseRunCompleted(FakeResults.Success(2) with { RunId = "run-a", CardUid = "04AA" });
            host.AutoModeHost.RaiseRunCompleted(FakeResults.Success(2) with { RunId = "run-b", CardUid = "04AA" });
            var events = await client.WaitForEventsAsync(2);
            var list = await client.Service.ListRunResults(new ListRunResultsRequest());

            Assert.Equal(("rev-7", "p-1"), (Assert.IsType<AutoRunCompletedEvent>(events[0]).Result.ProjectRevision, Assert.IsType<AutoRunCompletedEvent>(events[0]).Result.PresenceId));
            Assert.Null(Assert.IsType<AutoRunCompletedEvent>(events[1]).Result.PresenceId);
            Assert.Equal(new string?[] { "p-1", null }, list.Results.Select(r => r.PresenceId));
        }
    }
}
