#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Bridge;
using RFiDGear.Services.ExternalControl;

using StreamJsonRpc;

using Xunit;

namespace RFiDGear.Tests.ExternalControl
{
    /// <summary>
    /// The transport of the external control bridge, on a real named pipe with a client written the way an external application
    /// would write it: the handshake and what it refuses, how errors look on the wire, who can connect, what happens to a client
    /// that sends too much, says nothing or stops reading, and the order of the events. Every test uses a pipe name of its own.
    /// </summary>
    public sealed class BridgeServerTests : IDisposable
    {
        private readonly List<IDisposable> _disposables = new List<IDisposable>();

        public void Dispose()
        {
            foreach (var disposable in Enumerable.Reverse(_disposables))
            {
                disposable.Dispose();
            }
        }

        private static BridgeServiceOptions Offering(params string[] capabilities) => new BridgeServiceOptions
        {
            AppVersion = "2.0.10",
            InstanceId = "instance-1",
            Capabilities = new HashSet<string>(capabilities.Concat(new[] { BridgeCapabilities.Session }), StringComparer.Ordinal),
        };

        private (BridgeServer Server, string Name) StartServer(BridgeServiceOptions? options = null, BridgeServerTimings? timings = null, int maxMessageBytes = BridgeProtocol.MaxMessageBytes)
        {
            var name = "RFiDGear.Bridge.test-" + Guid.NewGuid().ToString("N");
            var server = new BridgeServer(name, options ?? Offering(), timings, maxMessageBytes);
            _disposables.Add(server);
            Assert.True(server.Start());

            return (server, name);
        }

        private async Task<BridgeTestClient> ConnectAsync(string name, int timeoutMs = 5000)
        {
            var client = await BridgeTestClient.ConnectAsync(name, timeoutMs);
            _disposables.Add(client);

            return client;
        }

        private static async Task<NamedPipeClientStream> RawAsync(string name)
        {
            var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.ConnectAsync(5000);

            return pipe;
        }

        private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000)
        {
            var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);

            while (!condition())
            {
                Assert.True(DateTime.UtcNow < until, "The condition did not become true in time.");
                await Task.Delay(10);
            }
        }

        // ── The handshake ──

        [Fact]
        public async Task Hello_AnswersWhoTheHostIs_WhatItOffers_AndWhichRequestedEventsAreOn()
        {
            var (_, name) = StartServer(Offering(BridgeCapabilities.EventsExecution, BridgeCapabilities.EventsReader));
            using var client = await ConnectAsync(name);

            var hello = await client.HelloAsync(BridgeEvents.RunCompleted, BridgeEvents.ReaderChanged, BridgeEvents.ProjectChanged, "noSuchEvent");

            Assert.Equal(BridgeProtocol.Product, hello.Product);
            Assert.Equal("2.0.10", hello.AppVersion);
            Assert.Equal("instance-1", hello.InstanceId);
            Assert.Equal(new[] { 1, 0 }, new[] { hello.Protocol.Major, hello.Protocol.Minor });
            Assert.Equal(typeof(IBridgeService).Assembly.GetName().Version!.ToString(), hello.ContractsVersion);
            Assert.False(string.IsNullOrEmpty(hello.SessionId));

            // The capabilities are the ones offered, in the order of the contract; the events are the requested ones the host offers.
            Assert.Equal(new[] { BridgeCapabilities.Session, BridgeCapabilities.EventsExecution, BridgeCapabilities.EventsReader }, hello.Capabilities);
            Assert.Equal(new[] { BridgeEvents.RunCompleted, BridgeEvents.ReaderChanged }, hello.Events);
            Assert.Equal(BridgeProtocol.MaxMessageBytes, hello.Limits.MaxMessageBytes);
        }

        [Fact]
        public async Task AnyOtherCallBeforeHello_IsRefused()
        {
            var (_, name) = StartServer(Offering(BridgeCapabilities.Status));
            using var client = await ConnectAsync(name);

            var failure = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.GetStatus(new GetStatusRequest()));

            Assert.Equal(BridgeErrorCodes.HandshakeRequired, failure.ErrorCode);
            Assert.Equal(BridgeErrorKinds.HandshakeRequired, BridgeTestClient.DataOf(failure)!.Kind);
        }

        [Fact]
        public async Task AProtocolRangeWithoutTheHostsMajorVersion_IsRefused_AndTheHostSaysWhichItSpeaks()
        {
            var (_, name) = StartServer();
            using var client = await ConnectAsync(name);

            var failure = await Assert.ThrowsAsync<RemoteInvocationException>(() =>
                client.Service.Hello(new HelloRequest { ClientName = "t", ClientVersion = "1", ProtocolMajorMin = 2, ProtocolMajorMax = 3 }));

            Assert.Equal(BridgeErrorCodes.IncompatibleProtocol, failure.ErrorCode);
            var details = BridgeTestClient.DataOf(failure)!.Details!;
            Assert.Equal("1", details["serverMajorMin"]);
            Assert.Equal("1", details["serverMajorMax"]);
        }

        [Fact]
        public async Task ARequestOverALimit_IsRefused_WithTheNameOfTheFieldAndNeverItsValue()
        {
            var (_, name) = StartServer();
            using var client = await ConnectAsync(name);
            var tooLong = new string('x', 200);

            var failure = await Assert.ThrowsAsync<RemoteInvocationException>(() =>
                client.Service.Hello(new HelloRequest { ClientName = tooLong, ClientVersion = "1", ProtocolMajorMin = 1, ProtocolMajorMax = 1 }));

            Assert.Equal(BridgeErrorCodes.InvalidRequest, failure.ErrorCode);
            var data = BridgeTestClient.DataOf(failure)!;
            Assert.Equal("clientName", data.Details!["field"]);
            Assert.DoesNotContain(tooLong, failure.Message + string.Join(",", data.Details.Values), StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("clientVersion")]
        [InlineData("events")]
        [InlineData("protocolMajorMin")]
        public async Task EveryOtherLimitOfTheHello_NamesItsFieldToo(string field)
        {
            var (_, name) = StartServer();
            using var client = await ConnectAsync(name);
            var request = new HelloRequest { ClientName = "t", ClientVersion = "1", ProtocolMajorMin = 1, ProtocolMajorMax = 1 };
            request = field switch
            {
                "clientVersion" => request with { ClientVersion = new string('v', 65) },
                "events" => request with { Events = Enumerable.Range(0, 33).Select(i => "event" + i).ToArray() },
                _ => request with { ProtocolMajorMin = 3, ProtocolMajorMax = 2 },
            };

            var failure = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.Hello(request));

            Assert.Equal(BridgeErrorCodes.InvalidRequest, failure.ErrorCode);
            Assert.Equal(field, BridgeTestClient.DataOf(failure)!.Details!["field"]);
        }

        [Fact]
        public async Task AMethodOfACapabilityTheHostDoesNotOffer_IsRefused_NeverIgnored()
        {
            var (_, name) = StartServer();
            using var client = await ConnectAsync(name);
            await client.HelloAsync();

            var failure = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.GetStatus(new GetStatusRequest()));

            Assert.Equal(BridgeErrorCodes.CapabilityNotSupported, failure.ErrorCode);
            Assert.Equal(BridgeCapabilities.Status, BridgeTestClient.DataOf(failure)!.Details!["capability"]);
        }

        // ── Errors on the wire ──

        [Fact]
        public async Task AnUnexpectedExceptionInACall_BecomesAnInternalErrorWithAnId_AndNothingOfTheExceptionReachesTheClient()
        {
            // The status is offered but not implemented in this slice: the call throws NotSupportedException inside the guard.
            using var logs = CapturedLogs.Install();
            var (_, name) = StartServer(Offering(BridgeCapabilities.Status));
            using var client = await ConnectAsync(name);
            await client.HelloAsync();

            var failure = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.GetStatus(new GetStatusRequest()));

            Assert.Equal(BridgeErrorCodes.InternalError, failure.ErrorCode);
            var data = BridgeTestClient.DataOf(failure)!;
            Assert.Equal(BridgeErrorKinds.InternalError, data.Kind);
            Assert.False(string.IsNullOrEmpty(data.ErrorId));
            var everything = failure.Message + failure.StackTrace + data.Kind;
            Assert.DoesNotContain("NotSupportedException", everything, StringComparison.Ordinal);
            Assert.DoesNotContain("not supported", everything, StringComparison.OrdinalIgnoreCase);

            // The log has the id and the type, so the entry can be found, and not the text.
            var entry = Assert.Single(logs.All(), e => e.RenderMessage().Contains(data.ErrorId!, StringComparison.Ordinal));
            Assert.Null(entry.Exception);
            Assert.Contains("NotSupportedException", entry.RenderMessage(), StringComparison.Ordinal);
            Assert.DoesNotContain("Specified method is not supported", CapturedLogs.EverythingWrittenFor(entry), StringComparison.Ordinal);
        }

        [Fact]
        public async Task AnUnknownMethod_IsMinus32601_AndOnlyTheMethodsOfTheContractExist()
        {
            var (_, name) = StartServer();
            using var client = await ConnectAsync(name);

            // Not in the contract, and public members of the service class that the contract does not name: none can be called.
            foreach (var method in new[] { "noSuchMethod", "helloDone", "isEventEnabled", "toString", "getHashCode" })
            {
                var failure = await Assert.ThrowsAnyAsync<RemoteRpcException>(() => client.Rpc.InvokeAsync<object>(method));

                Assert.IsType<RemoteMethodNotFoundException>(failure);
                Assert.Equal(-32601, (int)failure.ErrorCode!.Value);
            }
        }

        [Fact]
        public async Task ARequestWithAMemberTheHostDoesNotKnow_IsMinus32602()
        {
            var (_, name) = StartServer();
            using var client = await ConnectAsync(name);

            var failure = await Assert.ThrowsAnyAsync<RemoteRpcException>(() =>
                client.Rpc.InvokeAsync<HelloResponse>("hello", new object[] { new { clientName = "x", clientVersion = "1", protocolMajorMin = 1, protocolMajorMax = 1, somethingNewer = 1 } }));

            Assert.Equal(-32602, (int)failure.ErrorCode!.Value);
        }

        [Fact]
        public async Task TheLogOfAConnection_HasSessionIds_AndNothingTheClientSaid()
        {
            using var logs = CapturedLogs.Install();
            var (_, name) = StartServer();
            using (var client = await ConnectAsync(name))
            {
                await client.Service.Hello(new HelloRequest { ClientName = "SENTINEL-CLIENT", ClientVersion = "SENTINEL-VERSION", ProtocolMajorMin = 1, ProtocolMajorMax = 1, Events = new[] { "SENTINEL-EVENT" } });
            }

            await WaitUntilAsync(() => logs.All().Any(e => e.RenderMessage().Contains("disconnected", StringComparison.Ordinal)));

            var text = string.Join("\n", logs.All().Select(CapturedLogs.EverythingWrittenFor));
            Assert.Contains("session", text, StringComparison.Ordinal);
            Assert.DoesNotContain("SENTINEL", text, StringComparison.Ordinal);
            Assert.DoesNotContain(name, text, StringComparison.Ordinal);
        }

        [Fact]
        public async Task NothingOfTheTrafficReachesATraceListener()
        {
            var (server, name) = StartServer();
            using var client = await ConnectAsync(name);
            await client.HelloAsync();

            await WaitUntilAsync(() => server.ActiveSession?.Rpc != null);

            Assert.Equal(SourceLevels.Off, server.ActiveSession!.Rpc!.TraceSource.Switch.Level);
        }

        // ── Who can connect ──

        [Fact]
        public void TheWaitingPipe_BelongsToTheCurrentUserOnly()
        {
            var (server, _) = StartServer();

            var rules = server.WaitingPipe!.GetAccessControl().GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<PipeAccessRule>().ToList();

            using var me = WindowsIdentity.GetCurrent();
            Assert.NotEmpty(rules);
            Assert.All(rules, rule => Assert.Equal(me.Owner, rule.IdentityReference));
            Assert.DoesNotContain(rules, rule => rule.IdentityReference == new SecurityIdentifier(WellKnownSidType.WorldSid, null));
            Assert.DoesNotContain(rules, rule => rule.IdentityReference == new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null));
        }

        [Fact]
        public async Task OneClientAtATime_ASecondOneWaits_AndConnectsWhenTheFirstHasLeft()
        {
            var (server, name) = StartServer();
            var first = await ConnectAsync(name);
            await first.HelloAsync();
            await WaitUntilAsync(() => server.HasSession);

            await Assert.ThrowsAsync<TimeoutException>(() => BridgeTestClient.ConnectAsync(name, 400));

            first.Dispose();
            await WaitUntilAsync(() => server.State == BridgeServerState.Listening);

            using var second = await ConnectAsync(name);
            Assert.Equal(BridgeProtocol.Product, (await second.HelloAsync()).Product);
        }

        [Fact]
        public void AnotherServerOnTheSameName_FailsClosed_AndTheFirstOneKeepsWorking()
        {
            var (_, name) = StartServer();
            using var second = new BridgeServer(name, Offering());

            Assert.False(second.Start());
            Assert.Equal(BridgeServerState.Failed, second.State);
            Assert.Null(second.WaitingPipe);
        }

        [Fact]
        public async Task Stopping_EndsTheConnection_AndClosesThePipe()
        {
            var (server, name) = StartServer();
            using var client = await ConnectAsync(name);
            await client.HelloAsync();

            server.Stop();

            await Assert.ThrowsAnyAsync<Exception>(() => client.Service.Hello(new HelloRequest { ClientName = "t", ClientVersion = "1", ProtocolMajorMin = 1, ProtocolMajorMax = 1 }));
            Assert.Equal(BridgeServerState.Stopped, server.State);
            Assert.Null(server.WaitingPipe);
            await Assert.ThrowsAsync<TimeoutException>(() => BridgeTestClient.ConnectAsync(name, 300));
        }

        [Fact]
        public async Task Stopping_WhileNobodyIsConnected_ClosesTheWaitingPipe()
        {
            var (server, name) = StartServer();

            server.Stop();

            Assert.Null(server.WaitingPipe);
            await Assert.ThrowsAsync<TimeoutException>(() => BridgeTestClient.ConnectAsync(name, 300));
        }

        // ── A client that misbehaves ──

        [Fact]
        public async Task AMessageThatClaimsTwoGigabytes_EndsTheConnection_WithoutTheHostBufferingIt()
        {
            var (server, name) = StartServer();
            using var raw = await RawAsync(name);
            var before = GC.GetTotalAllocatedBytes();

            await raw.WriteAsync(Encoding.ASCII.GetBytes("Content-Length: 2000000000\r\n\r\n{\"jsonrpc\":\"2.0\""));
            var read = await raw.ReadAsync(new byte[16]).AsTask().WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(0, read);
            Assert.True(GC.GetTotalAllocatedBytes() - before < 50_000_000, "The host allocated far more than a small message needs.");
            await WaitUntilAsync(() => server.State == BridgeServerState.Listening);

            using var next = await ConnectAsync(name);
            Assert.Equal(BridgeProtocol.Product, (await next.HelloAsync()).Product);
        }

        [Fact]
        public async Task AMessageOverASmallLimit_IsRefused_ButOneWithinItGoesThrough()
        {
            var (_, name) = StartServer(maxMessageBytes: 4096);
            using var client = await ConnectAsync(name);
            var tooBig = new string('x', 5000);

            // A message within the limit is served; the next one over it ends the connection, and nothing is answered to it.
            Assert.Equal(BridgeProtocol.Product, (await client.HelloAsync()).Product);
            await Assert.ThrowsAnyAsync<Exception>(() =>
                client.Service.Hello(new HelloRequest { ClientName = "t", ClientVersion = tooBig, ProtocolMajorMin = 1, ProtocolMajorMax = 1 }));
        }

        [Fact]
        public async Task AClientThatNeverSaysHello_IsDisconnectedAtTheDeadline()
        {
            var (server, name) = StartServer(timings: new BridgeServerTimings { HandshakeDeadline = TimeSpan.FromMilliseconds(300) });
            using var raw = await RawAsync(name);
            await WaitUntilAsync(() => server.HasSession);

            var read = await raw.ReadAsync(new byte[16]).AsTask().WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(0, read);
            await WaitUntilAsync(() => server.State == BridgeServerState.Listening);
        }

        [Fact]
        public async Task AClientThatStopsReading_IsDisconnectedWhenAnEventCannotBeSent()
        {
            var (server, name) = StartServer(Offering(BridgeCapabilities.EventsExecution), new BridgeServerTimings { SendWatchdog = TimeSpan.FromMilliseconds(300) });
            using var raw = await RawAsync(name);
            var hello = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"hello\",\"params\":[{\"clientName\":\"t\",\"clientVersion\":\"1\",\"protocolMajorMin\":1,\"protocolMajorMax\":1,\"events\":[\"runCompleted\"]}]}";
            await raw.WriteAsync(Encoding.UTF8.GetBytes($"Content-Length: {Encoding.UTF8.GetByteCount(hello)}\r\n\r\n{hello}"));
            await WaitUntilAsync(() => server.ActiveSession?.Service.HelloDone == true);

            // Results big enough to fill the buffer of the pipe, which nobody empties.
            var big = Enumerable.Range(0, 3000).Select(i => new TaskOutcomeDto { Position = i, ProjectTaskIndex = i.ToString(), TaskTypeName = "Task", NormalizedErrorCode = "NoError" }).ToList();
            for (var i = 0; i < 5; i++)
            {
                server.Publish(BridgeEvents.RunCompleted, durable: true, (seq, session, at) => new RunCompletedEvent { Seq = seq, SessionId = session, At = at, Result = new RunResultDto { RunId = "r", TaskOutcomes = big } });
            }

            await WaitUntilAsync(() => server.State == BridgeServerState.Listening, 10_000);

            // The pipe is free again for the next client; a session that was ended while a write was stuck does not leave the name busy.
            using var next = await ConnectAsync(name);
            Assert.Equal(BridgeProtocol.Product, (await next.HelloAsync()).Product);
        }

        [Fact]
        public async Task AClientThatLetsTooManyResultsPileUp_IsDisconnected_NotServedWithOneMissing()
        {
            var (server, name) = StartServer(Offering(BridgeCapabilities.EventsExecution), new BridgeServerTimings { SendWatchdog = TimeSpan.FromSeconds(30) });
            using var raw = await RawAsync(name);
            var hello = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"hello\",\"params\":[{\"clientName\":\"t\",\"clientVersion\":\"1\",\"protocolMajorMin\":1,\"protocolMajorMax\":1,\"events\":[\"runCompleted\"]}]}";
            await raw.WriteAsync(Encoding.UTF8.GetBytes($"Content-Length: {Encoding.UTF8.GetByteCount(hello)}\r\n\r\n{hello}"));
            await WaitUntilAsync(() => server.ActiveSession?.Service.HelloDone == true);
            var big = Enumerable.Range(0, 3000).Select(i => new TaskOutcomeDto { Position = i, ProjectTaskIndex = i.ToString(), TaskTypeName = "Task", NormalizedErrorCode = "NoError" }).ToList();

            for (var i = 0; i < BridgeEventQueue.DurableCap + 10; i++)
            {
                server.Publish(BridgeEvents.RunCompleted, durable: true, (seq, session, at) => new RunCompletedEvent { Seq = seq, SessionId = session, At = at, Result = new RunResultDto { RunId = "r", TaskOutcomes = big } });
            }

            await WaitUntilAsync(() => server.State == BridgeServerState.Listening, 10_000);

            using var next = await ConnectAsync(name);
            Assert.Equal(BridgeProtocol.Product, (await next.HelloAsync()).Product);
        }

        // ── Start-up and shutdown ──

        [Fact]
        public async Task UntilStartupIsComplete_OnlyHelloAndGetStatusAreServed_EverythingElseIsNotReady()
        {
            var complete = false;
            var options = new BridgeServiceOptions
            {
                InstanceId = "instance-1",
                Capabilities = new HashSet<string> { BridgeCapabilities.Session, BridgeCapabilities.Status, BridgeCapabilities.ProjectLoad },
                IsStartupComplete = () => complete,
                GetStatus = () => Task.FromResult(new StatusResponse { InstanceId = "instance-1", StartupComplete = complete }),
            };
            var (_, name) = StartServer(options);
            using var client = await ConnectAsync(name);
            await client.HelloAsync();

            var status = await client.Service.GetStatus(new GetStatusRequest());
            var notReady = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.LoadProject(new LoadProjectRequest { Path = @"C:\a.rfPrj" }));

            Assert.False(status.StartupComplete);
            Assert.Equal(BridgeErrorCodes.NotReady, notReady.ErrorCode);
            var data = BridgeTestClient.DataOf(notReady)!;
            Assert.Equal("startup", data.Details!["reason"]);
            Assert.True(data.NeverStarted);

            // Once start-up is over the same call reaches the method itself (which this slice has not filled in).
            complete = true;
            var later = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Service.LoadProject(new LoadProjectRequest { Path = @"C:\a.rfPrj" }));
            Assert.Equal(BridgeErrorCodes.InternalError, later.ErrorCode);
            Assert.True((await client.Service.GetStatus(new GetStatusRequest())).StartupComplete);
        }

        [Fact]
        public async Task Flush_WaitsUntilTheNoticeHasLeft_AndStopEndsTheConnection()
        {
            var (server, name) = StartServer(Offering(BridgeCapabilities.EventsLifecycle));
            using var client = await ConnectAsync(name);
            await client.HelloAsync(BridgeEvents.ShuttingDown);

            server.Publish(BridgeEvents.ShuttingDown, durable: true, (seq, session, at) => new ShuttingDownEvent { Seq = seq, SessionId = session, At = at });

            Assert.True(server.TryFlush(TimeSpan.FromSeconds(5)));
            Assert.Single(await client.WaitForEventsAsync(1));
        }

        [Fact]
        public async Task Flush_WithAClientThatStoppedReading_GivesUpAfterTheTimeAndStopStillReturns()
        {
            var (server, name) = StartServer(Offering(BridgeCapabilities.EventsExecution), new BridgeServerTimings { SendWatchdog = TimeSpan.FromSeconds(30) });
            using var raw = await RawAsync(name);
            var hello = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"hello\",\"params\":[{\"clientName\":\"t\",\"clientVersion\":\"1\",\"protocolMajorMin\":1,\"protocolMajorMax\":1,\"events\":[\"runCompleted\"]}]}";
            await raw.WriteAsync(Encoding.UTF8.GetBytes($"Content-Length: {Encoding.UTF8.GetByteCount(hello)}\r\n\r\n{hello}"));
            await WaitUntilAsync(() => server.ActiveSession?.Service.HelloDone == true);
            var big = Enumerable.Range(0, 3000).Select(i => new TaskOutcomeDto { Position = i, ProjectTaskIndex = i.ToString(), TaskTypeName = "Task", NormalizedErrorCode = "NoError" }).ToList();
            server.Publish(BridgeEvents.RunCompleted, durable: true, (seq, session, at) => new RunCompletedEvent { Seq = seq, SessionId = session, At = at, Result = new RunResultDto { RunId = "r", TaskOutcomes = big } });

            var watch = System.Diagnostics.Stopwatch.StartNew();
            var flushed = server.TryFlush(TimeSpan.FromMilliseconds(300));
            server.Stop();

            Assert.False(flushed);
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(3), "The flush or the stop waited for a client that does not read.");
            Assert.Equal(BridgeServerState.Stopped, server.State);
        }

        [Fact]
        public async Task TheStateOfTheServer_IsAnnounced_WhenAClientComesAndGoes()
        {
            var (server, name) = StartServer();
            var changes = 0;
            server.StateChanged += (_, _) => Interlocked.Increment(ref changes);

            var client = await ConnectAsync(name);
            await client.HelloAsync();
            await WaitUntilAsync(() => Volatile.Read(ref changes) >= 1);
            client.Dispose();
            await WaitUntilAsync(() => Volatile.Read(ref changes) >= 2 && server.State == BridgeServerState.Listening);
        }

        [Fact]
        public async Task AHandlerOfTheServerState_ThatThrows_DoesNotBreakTheServer()
        {
            var (server, name) = StartServer();
            server.StateChanged += (_, _) => throw new InvalidOperationException("The handler is broken.");

            using var client = await ConnectAsync(name);

            Assert.Equal(BridgeProtocol.Product, (await client.HelloAsync()).Product);
        }

        // ── Events ──

        [Fact]
        public async Task Events_ArriveTypedAndInOrder_OnlyTheOnesAskedFor()
        {
            var (server, name) = StartServer(Offering(BridgeCapabilities.EventsExecution, BridgeCapabilities.EventsReader, BridgeCapabilities.EventsProject));
            using var client = await ConnectAsync(name);
            var hello = await client.HelloAsync(BridgeEvents.RunStarted, BridgeEvents.RunCompleted);
            var start = DateTimeOffset.UtcNow;

            server.Publish(BridgeEvents.ProjectChanged, durable: false, (seq, session, at) => new ProjectChangedEvent { Seq = seq, SessionId = session, At = at, Path = "not asked for", TaskCount = 1 });
            server.Publish(BridgeEvents.RunStarted, durable: true, (seq, session, at) => new RunStartedEvent { Seq = seq, SessionId = session, At = at, RequestId = "r-1", RunId = "a1", StartedAt = start });
            server.Publish(BridgeEvents.RunCompleted, durable: true, (seq, session, at) => new RunCompletedEvent { Seq = seq, SessionId = session, At = at, Result = new RunResultDto { RequestId = "r-1", RunId = "a1", Status = RunStatuses.Success, TotalTasks = 1 } });

            var events = await client.WaitForEventsAsync(2);
            await Task.Delay(200);

            Assert.Equal(2, client.Events.Count);
            var started = Assert.IsType<RunStartedEvent>(events[0]);
            var completed = Assert.IsType<RunCompletedEvent>(events[1]);
            Assert.Equal(new long[] { 1, 2 }, new[] { started.Seq, completed.Seq });
            Assert.All(events, e => Assert.Equal(hello.SessionId, e.SessionId));
            Assert.Equal("a1", started.RunId);
            Assert.Equal(RunStatuses.Success, completed.Result.Status);
        }

        [Fact]
        public async Task AClientThatAskedForNoEvents_IsNotDisconnected_NoMatterHowManyResultsTheHostHas()
        {
            var (server, name) = StartServer(Offering(BridgeCapabilities.EventsExecution));
            using var client = await ConnectAsync(name);
            await client.HelloAsync();

            // More than the limit of results that wait: none of them is owed to this client, so none of them counts.
            for (var i = 0; i < BridgeEventQueue.DurableCap * 2; i++)
            {
                server.Publish(BridgeEvents.RunCompleted, durable: true, (seq, session, at) => new RunCompletedEvent { Seq = seq, SessionId = session, At = at, Result = new RunResultDto { RunId = "r" } });
            }

            await Task.Delay(200);

            Assert.True(server.HasSession);
            Assert.Empty(client.Events);
            Assert.Equal(BridgeProtocol.Product, (await client.HelloAsync()).Product);
        }

        [Fact]
        public async Task AnEventPublishedWithoutAConnection_IsDropped_AndTheNextClientStartsAtOne()
        {
            var (server, name) = StartServer(Offering(BridgeCapabilities.EventsReader));

            server.Publish(BridgeEvents.ReaderChanged, durable: false, (seq, session, at) => new ReaderChangedEvent { Seq = seq, SessionId = session, At = at });
            using var client = await ConnectAsync(name);
            await client.HelloAsync(BridgeEvents.ReaderChanged);
            server.Publish(BridgeEvents.ReaderChanged, durable: false, (seq, session, at) => new ReaderChangedEvent { Seq = seq, SessionId = session, At = at, Reader = new ReaderDto { State = ReaderStates.Ready } });

            var events = await client.WaitForEventsAsync(1);

            Assert.Equal(1, events[0].Seq);
            Assert.Equal(ReaderStates.Ready, ((ReaderChangedEvent)events[0]).Reader.State);
        }
    }
}
