#nullable enable
using System;

using RFiDGear.Bridge;

using Serilog;

using StreamJsonRpc;
using StreamJsonRpc.Protocol;

namespace RFiDGear.Services.ExternalControl
{
    /// <summary>
    /// The JSON-RPC connection of the bridge. By default the library answers an unexpected exception of a call with its type,
    /// its message and its stack trace; here such an exception becomes an <see cref="BridgeErrorCodes.InternalError"/> with an id
    /// that also appears in the log, and nothing else. A <see cref="LocalRpcException"/> (the way the service answers on
    /// purpose) is left as it is.
    /// <para>
    /// This is the last line of defence: the service already turns what it expects into answers, but a defect in the library
    /// or in a handler must not become a message with a path or a name in it.
    /// </para>
    /// </summary>
    internal sealed class BridgeJsonRpc : JsonRpc
    {
        /// <summary>Creates the connection.</summary>
        /// <param name="handler">The message handler on the pipe.</param>
        internal BridgeJsonRpc(IJsonRpcMessageHandler handler)
            : base(handler)
        {
        }

        /// <inheritdoc />
        protected override JsonRpcError.ErrorDetail CreateErrorDetails(JsonRpcRequest request, Exception exception)
        {
            if (exception is LocalRpcException)
            {
                return base.CreateErrorDetails(request, exception);
            }

            var errorId = Guid.NewGuid().ToString("N")[..12];
            Log.ForContext<BridgeJsonRpc>().Error("The external control bridge failed unexpectedly ({ExceptionType}, errorId {ErrorId})", exception.GetType().FullName, errorId);

            return new JsonRpcError.ErrorDetail
            {
                Code = (JsonRpcErrorCode)BridgeErrorCodes.InternalError,
                Message = BridgeErrorKinds.InternalError,
                Data = new BridgeErrorData { Kind = BridgeErrorKinds.InternalError, ErrorId = errorId },
            };
        }
    }
}
