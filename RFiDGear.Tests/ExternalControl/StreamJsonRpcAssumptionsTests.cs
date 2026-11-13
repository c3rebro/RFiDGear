#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using Nerdbank.Streams;

using RFiDGear.Bridge;

using StreamJsonRpc;
using StreamJsonRpc.Protocol;

using Xunit;

namespace RFiDGear.Tests.ExternalControl
{
    /// <summary>
    /// What the external control bridge assumes about StreamJsonRpc, written as tests. The design (names of methods, shape of
    /// errors, strict reading of requests, one DTO per call) rests on these behaviours of version 2.26.150. A test that fails
    /// after an upgrade of the library says which assumption no longer holds, before a client finds out.
    /// </summary>
    public class StreamJsonRpcAssumptionsTests
    {
        private static JsonRpc Server(Stream stream, IBridgeService target, bool sanitize = false)
        {
            var formatter = new SystemTextJsonFormatter { JsonSerializerOptions = BridgeJson.CreateServerOptions() };
            var handler = new HeaderDelimitedMessageHandler(stream, formatter);
            var rpc = sanitize ? new SanitizingRpc(handler) : new JsonRpc(handler);
            rpc.AddLocalRpcTarget(target, new JsonRpcTargetOptions { MethodNameTransform = CommonMethodNameTransforms.CamelCase, NotifyClientOfEvents = false });
            rpc.StartListening();

            return rpc;
        }

        private static JsonRpc Client(Stream stream)
        {
            var formatter = new SystemTextJsonFormatter { JsonSerializerOptions = BridgeJson.CreateClientOptions() };
            var rpc = new BridgeClientRpc(new HeaderDelimitedMessageHandler(stream, formatter));

            return rpc;
        }

        // Without this the client reads error.data as the library's own CommonErrorData and our BridgeErrorData is lost.
        private sealed class BridgeClientRpc : JsonRpc
        {
            public BridgeClientRpc(IJsonRpcMessageHandler handler)
                : base(handler)
            {
            }

            protected override Type? GetErrorDetailsDataType(JsonRpcError error)
            {
                ArgumentNullException.ThrowIfNull(error);

                return typeof(BridgeErrorData);
            }
        }

        private static string Everything(RemoteInvocationException failure) =>
            failure.Message + " | " + (failure.DeserializedErrorData == null ? string.Empty : JsonSerializer.Serialize(failure.DeserializedErrorData, failure.DeserializedErrorData.GetType()));

        private static IBridgeService Proxy(JsonRpc client)
        {
            var proxy = client.Attach<IBridgeService>(new JsonRpcProxyOptions { MethodNameTransform = CommonMethodNameTransforms.CamelCase });
            client.StartListening();

            return proxy;
        }

        [Fact]
        public void TheLibrary_AllowsWhatTheBridgeDerivesFrom()
        {
            // The server subclasses JsonRpc to re-code library errors and wraps the read stream instead of the handler.
            Assert.False(typeof(JsonRpc).IsSealed);
            Assert.True(typeof(JsonRpc).GetMethod("CreateErrorDetails", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.IsVirtual);
            Assert.False(typeof(HeaderDelimitedMessageHandler).IsSealed);
        }

        [Fact]
        public async Task ATypedProxy_CallsTheServerByTheCamelCaseNames_WithOneDtoAsTheParameter()
        {
            var (serverStream, clientStream) = FullDuplexStream.CreatePair();
            using var server = Server(serverStream, new Stub());
            using var client = Client(clientStream);
            var proxy = Proxy(client);

            var response = await proxy.Hello(new HelloRequest { ClientName = "test", ClientVersion = "1", ProtocolMajorMin = 1, ProtocolMajorMax = 1 });

            Assert.Equal("RFiDGear", response.Product);
            Assert.Equal(new[] { "session" }, response.Capabilities);
        }

        [Fact]
        public async Task AnErrorOfTheHandler_ReachesTheClientWithItsCodeAndItsData()
        {
            var (serverStream, clientStream) = FullDuplexStream.CreatePair();
            using var server = Server(serverStream, new Stub { OnGetStatus = () => throw new LocalRpcException("busy")
            {
                ErrorCode = BridgeErrorCodes.Busy,
                ErrorData = new BridgeErrorData { Kind = BridgeErrorKinds.Busy, NeverStarted = true },
            } });
            using var client = Client(clientStream);
            var proxy = Proxy(client);

            var failure = await Assert.ThrowsAsync<RemoteInvocationException>(() => proxy.GetStatus(new GetStatusRequest()));

            Assert.Equal(BridgeErrorCodes.Busy, failure.ErrorCode);
            var data = Assert.IsType<BridgeErrorData>(failure.DeserializedErrorData);
            Assert.Equal(BridgeErrorKinds.Busy, data.Kind);
            Assert.True(data.NeverStarted);
        }

        [Fact]
        public async Task AnUnknownMethod_IsMinus32601()
        {
            var (serverStream, clientStream) = FullDuplexStream.CreatePair();
            using var server = Server(serverStream, new Stub());
            using var client = Client(clientStream);
            client.StartListening();

            var failure = await Assert.ThrowsAnyAsync<RemoteRpcException>(() => client.InvokeAsync<object>("noSuchMethod"));

            Assert.IsType<RemoteMethodNotFoundException>(failure);
            Assert.Equal((int)JsonRpcErrorCode.MethodNotFound, (int)failure.ErrorCode!.Value);
        }

        [Fact]
        public async Task ARequestWithAMemberTheServerDoesNotKnow_IsAnArgumentError()
        {
            var (serverStream, clientStream) = FullDuplexStream.CreatePair();
            using var server = Server(serverStream, new Stub());
            using var client = Client(clientStream);
            client.StartListening();

            var failure = await Assert.ThrowsAnyAsync<RemoteRpcException>(() =>
                client.InvokeAsync<HelloResponse>("hello", new object[] { new { clientName = "x", bogus = 1 } }));

            // The same exception type as an unknown method: the code is what tells them apart.
            Assert.IsType<RemoteMethodNotFoundException>(failure);
            Assert.Equal((int)JsonRpcErrorCode.InvalidParams, (int)failure.ErrorCode!.Value);
        }

        [Fact]
        public async Task AnUnexpectedExceptionOfTheHandler_ReachesTheClientWithItsTextByDefault()
        {
            // The hazard this pins: without care, the text and the stack trace of any exception go to the client.
            var (serverStream, clientStream) = FullDuplexStream.CreatePair();
            using var server = Server(serverStream, new Stub { OnGetStatus = () => throw new InvalidOperationException("secret-detail-42") });
            using var client = Client(clientStream);
            var proxy = Proxy(client);

            var failure = await Assert.ThrowsAsync<RemoteInvocationException>(() => proxy.GetStatus(new GetStatusRequest()));

            Assert.Contains("secret-detail-42", Everything(failure), StringComparison.Ordinal);
        }

        [Fact]
        public async Task ASubclass_CanReplaceTheErrorOfAnUnexpectedException()
        {
            var (serverStream, clientStream) = FullDuplexStream.CreatePair();
            using var server = Server(serverStream, new Stub { OnGetStatus = () => throw new InvalidOperationException("secret-detail-42") }, sanitize: true);
            using var client = Client(clientStream);
            var proxy = Proxy(client);

            var failure = await Assert.ThrowsAsync<RemoteInvocationException>(() => proxy.GetStatus(new GetStatusRequest()));

            Assert.Equal(BridgeErrorCodes.InternalError, failure.ErrorCode);
            var text = Everything(failure);
            Assert.DoesNotContain("secret-detail-42", text, StringComparison.Ordinal);
            Assert.DoesNotContain("InvalidOperationException", text, StringComparison.Ordinal);
        }

        [Fact]
        public async Task TheLibrarysOwnArgumentError_IsNotRoutedThroughTheSubclass_SoClientsMapTheLibraryCodes()
        {
            // An unknown member or a wrong type is answered by the library before any code of the bridge runs: the code is
            // -32602 (invalid params), not our 1004. A client treats -32601 like capabilityNotSupported and -32602 like
            // invalidRequest; the contract says so.
            var (serverStream, clientStream) = FullDuplexStream.CreatePair();
            using var server = Server(serverStream, new Stub(), sanitize: true);
            using var client = Client(clientStream);
            client.StartListening();

            var failure = await Assert.ThrowsAnyAsync<RemoteRpcException>(() =>
                client.InvokeAsync<HelloResponse>("hello", new object[] { new { clientName = "x", bogus = 1 } }));

            Assert.Equal((int)JsonRpcErrorCode.InvalidParams, (int)failure.ErrorCode!.Value);
            Assert.DoesNotContain(BridgeErrorKinds.InternalError, failure.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task AWrongTypedValue_IsRefused_WithoutTheValueInTheMessage()
        {
            var (serverStream, clientStream) = FullDuplexStream.CreatePair();
            using var server = Server(serverStream, new Stub());
            using var client = Client(clientStream);
            client.StartListening();

            var failure = await Assert.ThrowsAnyAsync<RemoteRpcException>(() =>
                client.InvokeAsync<HelloResponse>("hello", new object[] { new { clientName = 918273645 } }));

            Assert.Equal((int)JsonRpcErrorCode.InvalidParams, (int)failure.ErrorCode!.Value);
            Assert.DoesNotContain("918273645", failure.Message, StringComparison.Ordinal);
        }
        private sealed class SanitizingRpc : JsonRpc
        {
            public SanitizingRpc(IJsonRpcMessageHandler handler)
                : base(handler)
            {
            }

            protected override JsonRpcError.ErrorDetail CreateErrorDetails(JsonRpcRequest request, Exception exception)
            {
                if (exception is LocalRpcException)
                {
                    return base.CreateErrorDetails(request, exception);
                }

                return new JsonRpcError.ErrorDetail
                {
                    Code = (JsonRpcErrorCode)BridgeErrorCodes.InternalError,
                    Message = BridgeErrorKinds.InternalError,
                    Data = new BridgeErrorData { Kind = BridgeErrorKinds.InternalError },
                };
            }
        }

        private sealed class Stub : IBridgeService
        {
            public Func<StatusResponse>? OnGetStatus { get; init; }

            public Task<HelloResponse> Hello(HelloRequest request)
            {
                ArgumentNullException.ThrowIfNull(request);

                return Task.FromResult(new HelloResponse { Product = BridgeProtocol.Product, Capabilities = new[] { BridgeCapabilities.Session } });
            }

            public Task<StatusResponse> GetStatus(GetStatusRequest request)
            {
                ArgumentNullException.ThrowIfNull(request);

                return Task.FromResult(OnGetStatus!());
            }

            public Task<LoadProjectResponse> LoadProject(LoadProjectRequest request) => throw NotUsed(request);

            public Task<StartRunResponse> StartRun(StartRunRequest request) => throw NotUsed(request);

            public Task<CancelRunResponse> CancelRun(CancelRunRequest request) => throw NotUsed(request);

            public Task<GetRunResultResponse> GetRunResult(GetRunResultRequest request) => throw NotUsed(request);

            public Task<ScanReaderResponse> ScanReader(ScanReaderRequest request) => throw NotUsed(request);

            public Task<StartAutoModeResponse> StartAutoMode(StartAutoModeRequest request) => throw NotUsed(request);

            public Task<StopAutoModeResponse> StopAutoMode(StopAutoModeRequest request) => throw NotUsed(request);

            public Task<ListRunResultsResponse> ListRunResults(ListRunResultsRequest request) => throw NotUsed(request);

            // The methods of the contract that these tests do not call.
            private static NotSupportedException NotUsed(object request)
            {
                ArgumentNullException.ThrowIfNull(request);

                return new NotSupportedException();
            }
        }
    }
}
