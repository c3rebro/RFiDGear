#nullable enable
using System;
using System.Collections.Generic;

using RFiDGear.Bridge;

namespace RFiDGear.Tests.ExternalControl
{
    /// <summary>
    /// One filled-in value of every type that crosses the pipe, with fixed values. The contract tests write each of them as
    /// JSON and compare it with the golden form, so that a changed name, a changed order of members or a changed encoding is
    /// seen as a diff of the wire and not as a surprise in a client.
    /// </summary>
    internal static class BridgeSamples
    {
        private static readonly DateTimeOffset Start = new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);

        /// <summary>Gets a reader with one card on it.</summary>
        public static ReaderDto Reader { get; } = new ReaderDto
        {
            Name = "Elatec TWN4",
            State = ReaderStates.Ready,
            HasCard = true,
            Credentials = new[]
            {
                new CredentialDto { Uid = "04A1B2C3D4E5F6", TypeCode = 16385, DisplayName = "MIFARE DESFire EV2", IsPrimary = true },
            },
        };

        /// <summary>Gets a result with a task that ran and a task that was not reached.</summary>
        public static RunResultDto Result { get; } = new RunResultDto
        {
            RequestId = "r-1",
            RunId = "a1b2c3d4e5f60718293a4b5c6d7e8f90",
            Source = RunSources.Bridge,
            Status = RunStatuses.Failure,
            FailureReason = FailureReasons.Cancelled,
            TotalTasks = 2,
            FailedTasks = 1,
            StartedAt = Start,
            CompletedAt = Start.AddSeconds(3),
            CardUid = "04A1B2C3D4E5F6",
            ResultSeq = 7,
            TaskOutcomes = new[]
            {
                new TaskOutcomeDto { Position = 0, ProjectTaskIndex = "0", TaskTypeName = "MifareDesfireSetupViewModel", IsSuccess = false, NormalizedErrorCode = "KeyUnavailable" },
                new TaskOutcomeDto { Position = 1, ProjectTaskIndex = "1", TaskTypeName = "MifareDesfireSetupViewModel", IsSuccess = null, NormalizedErrorCode = "Empty", SkipReason = "NotReached" },
            },
        };

        /// <summary>Gets the result of a run of automatic mode: no request id, the card it processed.</summary>
        public static RunResultDto AutoResult { get; } = new RunResultDto
        {
            RunId = "b2c3d4e5f60718293a4b5c6d7e8f90a1",
            Source = RunSources.AutoMode,
            Status = RunStatuses.Success,
            FailureReason = FailureReasons.None,
            TotalTasks = 1,
            FailedTasks = 0,
            StartedAt = Start,
            CompletedAt = Start.AddSeconds(2),
            CardUid = "04A1B2C3D4E5F6",
            ResultSeq = 8,
            TaskOutcomes = new[]
            {
                new TaskOutcomeDto { Position = 0, ProjectTaskIndex = "0", TaskTypeName = "MifareDesfireSetupViewModel", IsSuccess = true, NormalizedErrorCode = "Empty" },
            },
        };

        /// <summary>Gets every sample, by name. The name is the key of the golden form.</summary>
        public static IReadOnlyDictionary<string, object> All { get; } = new Dictionary<string, object>
        {
            ["HelloRequest"] = new HelloRequest
            {
                ClientName = "SIE Encode",
                ClientVersion = "3.1.0",
                ProtocolMajorMin = 1,
                ProtocolMajorMax = 1,
                Events = new[] { BridgeEvents.RunCompleted, BridgeEvents.ReaderChanged },
            },
            ["HelloResponse"] = new HelloResponse
            {
                Product = BridgeProtocol.Product,
                AppVersion = "2.0.10",
                ContractsVersion = "1.0.0",
                InstanceId = "9f8e7d6c5b4a39281706f5e4d3c2b1a0",
                Protocol = new ProtocolVersionDto { Major = 1, Minor = 0 },
                SessionId = "s1",
                Capabilities = new[] { BridgeCapabilities.Session, BridgeCapabilities.Status },
                Events = new[] { BridgeEvents.RunCompleted },
                Limits = new BridgeLimitsDto
                {
                    MaxMessageBytes = BridgeProtocol.MaxMessageBytes,
                    MaxVariables = BridgeProtocol.MaxVariables,
                    MaxVariableNameLength = BridgeProtocol.MaxVariableNameLength,
                    MaxVariableValueLength = BridgeProtocol.MaxVariableValueLength,
                    MaxPathLength = BridgeProtocol.MaxPathLength,
                    MaxRequestIdLength = BridgeProtocol.MaxRequestIdLength,
                    ResultHistory = BridgeProtocol.ResultHistory,
                    RequestIdHistory = BridgeProtocol.RequestIdHistory,
                    MaxWaitMs = BridgeProtocol.MaxWaitMs,
                },
            },
            ["GetStatusRequest"] = new GetStatusRequest(),
            ["StatusResponse"] = new StatusResponse
            {
                InstanceId = "9f8e7d6c5b4a39281706f5e4d3c2b1a0",
                StartupComplete = true,
                ReaderInitializing = false,
                UiResponsive = true,
                Stale = false,
                Project = new ProjectStatusDto { Loaded = true, Path = @"C:\Projects\order.rfPrj", TaskCount = 3 },
                IsRunning = true,
                ActiveRun = new ActiveRunDto { RequestId = "r-1", RunId = "a1b2", Source = RunSources.Bridge, StartedAt = Start },
                Reader = Reader,
                LastResultSeq = 7,
                AutoMode = new AutoModeStatusDto { Enabled = true, StartedBy = RunSources.Bridge, StopOnDisconnect = true, LastStopReason = AutoModeStopReasons.RunAborted },
            },
            ["LoadProjectRequest"] = new LoadProjectRequest { Path = @"C:\Projects\order.rfPrj" },
            ["LoadProjectResponse"] = new LoadProjectResponse { Path = @"C:\Projects\order.rfPrj", TaskCount = 3 },
            ["StartRunRequest"] = new StartRunRequest
            {
                RequestId = "r-1",
                Variables = new[] { new VariablePair { Name = "$JOBNUMBER", Value = "4711" } },
                ExpectProjectPath = @"C:\Projects\order.rfPrj",
                ExpectTaskCount = 3,
            },
            ["StartRunResponse"] = new StartRunResponse { RequestId = "r-1", RunId = "a1b2c3d4e5f60718293a4b5c6d7e8f90", State = RunStates.Running, Duplicate = false },
            ["CancelRunRequest"] = new CancelRunRequest { RequestId = "r-1" },
            ["CancelRunResponse"] = new CancelRunResponse { State = RunStates.Running, CancelRequested = true },
            ["GetRunResultRequest"] = new GetRunResultRequest { RequestId = "r-1", WaitMs = 5000 },
            ["GetRunResultResponse"] = new GetRunResultResponse { State = RunStates.Completed, Result = Result },
            ["ScanReaderRequest"] = new ScanReaderRequest(),
            ["ScanReaderResponse"] = new ScanReaderResponse { Reader = Reader },
            ["RunStartedEvent"] = new RunStartedEvent { Seq = 1, SessionId = "s1", At = Start, RequestId = "r-1", RunId = "a1b2", StartedAt = Start },
            ["RunCompletedEvent"] = new RunCompletedEvent { Seq = 2, SessionId = "s1", At = Start.AddSeconds(3), Result = Result },
            ["RunningChangedEvent"] = new RunningChangedEvent { Seq = 3, SessionId = "s1", At = Start, IsRunning = true, OwnRequestId = "r-1", OwnRunId = "a1b2" },
            ["ReaderChangedEvent"] = new ReaderChangedEvent { Seq = 4, SessionId = "s1", At = Start, Reader = Reader },
            ["ProjectChangedEvent"] = new ProjectChangedEvent { Seq = 5, SessionId = "s1", At = Start, Path = @"C:\Projects\order.rfPrj", TaskCount = 3 },
            ["ShuttingDownEvent"] = new ShuttingDownEvent { Seq = 6, SessionId = "s1", At = Start, Reason = ShutdownReasons.UserClose, RunInFlight = true },
            ["StartAutoModeRequest"] = new StartAutoModeRequest { StopOnDisconnect = false },
            ["StartAutoModeResponse"] = new StartAutoModeResponse { Enabled = true, AlreadyEnabled = false, StopOnDisconnect = false },
            ["StopAutoModeRequest"] = new StopAutoModeRequest(),
            ["StopAutoModeResponse"] = new StopAutoModeResponse { WasEnabled = true },
            ["ListRunResultsRequest"] = new ListRunResultsRequest { AfterResultSeq = 6, Limit = 10 },
            ["ListRunResultsResponse"] = new ListRunResultsResponse
            {
                InstanceId = "9f8e7d6c5b4a39281706f5e4d3c2b1a0",
                Results = new[] { Result, AutoResult },
                Gap = false,
                LastResultSeq = 8,
            },
            ["AutoModeChangedEvent"] = new AutoModeChangedEvent { Seq = 7, SessionId = "s1", At = Start, Enabled = false, StartedBy = null, LastStopReason = AutoModeStopReasons.ReaderLost, LastResultSeq = 8 },
            ["AutoRunCompletedEvent"] = new AutoRunCompletedEvent { Seq = 8, SessionId = "s1", At = Start.AddSeconds(2), Result = AutoResult },
            ["RegisterKeyProviderRequest"] = new RegisterKeyProviderRequest(),
            ["RegisterKeyProviderResponse"] = new RegisterKeyProviderResponse { ProviderId = "bridge-client", TimeoutMs = 10000, InteractiveTimeoutMs = 300000 },
            ["ResolveKeyRequest"] = new ResolveKeyRequest
            {
                SlotId = "desfire.app.master",
                Reference = "vault/order-4711",
                Algorithm = KeyAlgorithms.Aes128,
                Usage = KeyUsages.Authenticate,
                ApplicationId = 3,
                KeyNumber = 0,
                RunId = "a1b2c3d4e5f60718293a4b5c6d7e8f90",
                TaskIndex = "2",
                CardUid = "04A1B2C3D4E5F6",
                Interactive = false,
            },
            ["ResolveKeyResponse"] = new ResolveKeyResponse { Result = KeyResolveResults.Resolved, Key = "AQIDBAUGBwg=" },
            ["BridgeErrorData"] = new BridgeErrorData
            {
                Kind = BridgeErrorKinds.Busy,
                ErrorId = "e-42",
                NeverStarted = true,
                Details = new Dictionary<string, string> { ["activity"] = "run" },
            },
        };
    }
}
