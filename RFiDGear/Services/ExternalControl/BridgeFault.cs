#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using RFiDGear.Bridge;

using StreamJsonRpc;

namespace RFiDGear.Services.ExternalControl
{
    /// <summary>
    /// A refusal that the bridge answers to a client with an error code of its own (<see cref="BridgeErrorCodes"/>). Code that
    /// serves a call throws it; the guard around every call turns it into the error of the wire. It carries no text for the
    /// client: the kind and the code say what happened, and the details never hold a value the client sent or the text of an
    /// exception.
    /// </summary>
    [SuppressMessage("Major Code Smell", "S3871:Exception types should be \"public\"", Justification = "It only carries a refusal from the code that serves a call to the guard around it, in this assembly; it never crosses an assembly boundary and is no part of any contract.")]
    internal sealed class BridgeFault : Exception
    {
        /// <summary>Creates a fault.</summary>
        /// <param name="code">The code, one of <see cref="BridgeErrorCodes"/>.</param>
        /// <param name="kind">The short name, one of <see cref="BridgeErrorKinds"/>.</param>
        /// <param name="neverStarted">Whether the request is known not to have touched a card; <see langword="null"/> when that does not apply.</param>
        /// <param name="details">Further facts for the client.</param>
        internal BridgeFault(int code, string kind, bool? neverStarted = null, IReadOnlyDictionary<string, string>? details = null)
            : base(kind)
        {
            Code = code;
            Kind = kind;
            NeverStarted = neverStarted;
            Details = details;
        }

        /// <summary>Gets the code of the error.</summary>
        internal int Code { get; }

        /// <summary>Gets the short name of the error.</summary>
        internal string Kind { get; }

        /// <summary>Gets whether the request is known not to have touched a card.</summary>
        internal bool? NeverStarted { get; }

        /// <summary>Gets the further facts.</summary>
        internal IReadOnlyDictionary<string, string>? Details { get; }

        /// <summary>The first call was not <c>hello</c>.</summary>
        /// <returns>The fault.</returns>
        internal static BridgeFault HandshakeRequired() => new BridgeFault(BridgeErrorCodes.HandshakeRequired, BridgeErrorKinds.HandshakeRequired);

        /// <summary>The ranges of protocol major versions do not overlap.</summary>
        /// <param name="serverMajorMin">The lowest major version the host speaks.</param>
        /// <param name="serverMajorMax">The highest major version the host speaks.</param>
        /// <returns>The fault.</returns>
        internal static BridgeFault IncompatibleProtocol(int serverMajorMin, int serverMajorMax) =>
            new BridgeFault(
                BridgeErrorCodes.IncompatibleProtocol,
                BridgeErrorKinds.IncompatibleProtocol,
                details: new Dictionary<string, string>
                {
                    ["serverMajorMin"] = serverMajorMin.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["serverMajorMax"] = serverMajorMax.ToString(System.Globalization.CultureInfo.InvariantCulture),
                });

        /// <summary>The method belongs to a capability the host does not offer.</summary>
        /// <param name="capability">The capability.</param>
        /// <returns>The fault.</returns>
        internal static BridgeFault CapabilityNotSupported(string capability) =>
            new BridgeFault(BridgeErrorCodes.CapabilityNotSupported, BridgeErrorKinds.CapabilityNotSupported, details: new Dictionary<string, string> { ["capability"] = capability });

        /// <summary>A request breaks a rule or a limit.</summary>
        /// <param name="field">The name of the field, never its value.</param>
        /// <returns>The fault.</returns>
        internal static BridgeFault InvalidRequest(string field) =>
            new BridgeFault(BridgeErrorCodes.InvalidRequest, BridgeErrorKinds.InvalidRequest, details: new Dictionary<string, string> { ["field"] = field });

        /// <summary>RFiDGear cannot serve the call yet.</summary>
        /// <param name="reason">Why, a short word (<c>startup</c>).</param>
        /// <returns>The fault; the call did nothing, so it is marked as never started.</returns>
        internal static BridgeFault NotReady(string reason) =>
            new BridgeFault(BridgeErrorCodes.NotReady, BridgeErrorKinds.NotReady, neverStarted: true, details: new Dictionary<string, string> { ["reason"] = reason });

        /// <summary>A run, a project load, an edit or a reader scan is in the way.</summary>
        /// <param name="activity">What RFiDGear is busy with, one of <see cref="BusyActivities"/>.</param>
        /// <returns>The fault; the call did nothing, so it is marked as never started.</returns>
        internal static BridgeFault Busy(string activity) =>
            new BridgeFault(BridgeErrorCodes.Busy, BridgeErrorKinds.Busy, neverStarted: true, details: new Dictionary<string, string> { ["activity"] = activity });

        /// <summary>Automatic mode is on.</summary>
        /// <returns>The fault; the call did nothing, so it is marked as never started.</returns>
        internal static BridgeFault AutoModeActive() =>
            new BridgeFault(BridgeErrorCodes.AutoModeActive, BridgeErrorKinds.AutoModeActive, neverStarted: true);

        /// <summary>The project file does not exist.</summary>
        /// <returns>The fault.</returns>
        internal static BridgeFault ProjectNotFound() => new BridgeFault(BridgeErrorCodes.ProjectNotFound, BridgeErrorKinds.ProjectNotFound);

        /// <summary>The file is not a project this version can open.</summary>
        /// <returns>The fault.</returns>
        internal static BridgeFault ProjectUnsupportedFormat() => new BridgeFault(BridgeErrorCodes.ProjectUnsupportedFormat, BridgeErrorKinds.ProjectUnsupportedFormat);

        /// <summary>The project could not be loaded for another reason.</summary>
        /// <returns>The fault.</returns>
        internal static BridgeFault ProjectLoadFailed() => new BridgeFault(BridgeErrorCodes.ProjectLoadFailed, BridgeErrorKinds.ProjectLoadFailed);

        /// <summary>Turns the fault into the error of the wire.</summary>
        /// <returns>The exception that StreamJsonRpc answers with.</returns>
        internal LocalRpcException ToRpc() => new LocalRpcException(Kind)
        {
            ErrorCode = Code,
            ErrorData = new BridgeErrorData { Kind = Kind, NeverStarted = NeverStarted, Details = Details },
        };
    }
}
