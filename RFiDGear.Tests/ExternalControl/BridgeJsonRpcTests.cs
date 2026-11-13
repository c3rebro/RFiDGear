#nullable enable
using System;
using System.IO.Pipelines;
using System.Linq;
using System.Threading.Tasks;

using Nerdbank.Streams;

using RFiDGear.Bridge;
using RFiDGear.Services.ExternalControl;

using StreamJsonRpc;

using Xunit;

namespace RFiDGear.Tests.ExternalControl
{
    /// <summary>
    /// The last line of defence of the connection: what the library would answer for an exception that no guard of the
    /// service caught. The service turns what it expects into answers on its own, so this is shown with a target that throws
    /// the way a defect would.
    /// </summary>
    public class BridgeJsonRpcTests
    {
        private const string Secret = @"C:\Users\someone\SECRET-project.rfPrj";

        public class Throwing
        {
            public Task<string> Boom() => throw new InvalidOperationException(Secret);

            public Task<string> Deliberate() => throw new LocalRpcException("on purpose") { ErrorCode = BridgeErrorCodes.Busy, ErrorData = new BridgeErrorData { Kind = BridgeErrorKinds.Busy } };
        }

        private static async Task<(JsonRpc Client, BridgeJsonRpc Server)> ConnectAsync()
        {
            var (serverStream, clientStream) = FullDuplexStream.CreatePair();
            var server = new BridgeJsonRpc(new HeaderDelimitedMessageHandler(serverStream, new SystemTextJsonFormatter { JsonSerializerOptions = BridgeJson.CreateServerOptions() }));
            server.AddLocalRpcTarget(new Throwing(), new JsonRpcTargetOptions { MethodNameTransform = CommonMethodNameTransforms.CamelCase });
            server.StartListening();
            var client = new JsonRpc(new HeaderDelimitedMessageHandler(clientStream, new SystemTextJsonFormatter { JsonSerializerOptions = BridgeJson.CreateClientOptions() }));
            client.StartListening();
            await Task.CompletedTask;

            return (client, server);
        }

        [Fact]
        public async Task AnExceptionNobodyCaught_BecomesAnInternalErrorWithAnId_AndNeitherItsTextNorItsStackIsSent()
        {
            using var logs = CapturedLogs.Install();
            var (client, server) = await ConnectAsync();
            using var _ = server;
            using var __ = client;

            var failure = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.InvokeAsync<string>("boom"));

            Assert.Equal(BridgeErrorCodes.InternalError, failure.ErrorCode);
            // What came over the wire: the message and the data of the error (the stack trace of the client's own exception is not it).
            var everything = failure.Message + System.Text.Json.JsonSerializer.Serialize(failure.ErrorData) + System.Text.Json.JsonSerializer.Serialize(failure.DeserializedErrorData);
            Assert.DoesNotContain("SECRET", everything, StringComparison.Ordinal);
            Assert.DoesNotContain("InvalidOperationException", everything, StringComparison.Ordinal);
            Assert.DoesNotContain("   at ", everything, StringComparison.Ordinal);

            var entry = Assert.Single(logs.All());
            Assert.Contains("InvalidOperationException", entry.RenderMessage(), StringComparison.Ordinal);
            Assert.DoesNotContain("SECRET", string.Join("\n", logs.All().Select(CapturedLogs.EverythingWrittenFor)), StringComparison.Ordinal);
            Assert.Null(entry.Exception);
        }

        [Fact]
        public async Task AnAnswerTheServiceGaveOnPurpose_IsLeftAsItIs()
        {
            var (client, server) = await ConnectAsync();
            using var _ = server;
            using var __ = client;

            var failure = await Assert.ThrowsAsync<RemoteInvocationException>(() => client.InvokeAsync<string>("deliberate"));

            Assert.Equal(BridgeErrorCodes.Busy, failure.ErrorCode);
        }
    }
}
