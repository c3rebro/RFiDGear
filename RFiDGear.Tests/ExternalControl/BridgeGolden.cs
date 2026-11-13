#nullable enable
using System.Collections.Generic;

namespace RFiDGear.Tests.ExternalControl
{
    /// <summary>
    /// The JSON of every sample in <see cref="BridgeSamples"/>, as it goes over the pipe. This is the wire format of version 1
    /// written down: a client that was built against it must keep working, so a change here is a change of the protocol
    /// (an added member is a minor version, anything else a major one) and is made on purpose, never to make a test pass.
    /// </summary>
    internal static class BridgeGolden
    {
        /// <summary>Gets the golden JSON by sample name.</summary>
        public static IReadOnlyDictionary<string, string> Json { get; } = new Dictionary<string, string>
        {
            ["HelloRequest"] = """{"clientName":"SIE Encode","clientVersion":"3.1.0","protocolMajorMin":1,"protocolMajorMax":1,"events":["runCompleted","readerChanged"]}""",
            ["HelloResponse"] = """{"product":"RFiDGear","appVersion":"2.0.10","contractsVersion":"1.0.0","instanceId":"9f8e7d6c5b4a39281706f5e4d3c2b1a0","protocol":{"major":1,"minor":0},"sessionId":"s1","capabilities":["session","status"],"events":["runCompleted"],"limits":{"maxMessageBytes":1048576,"maxVariables":64,"maxVariableNameLength":128,"maxVariableValueLength":2048,"maxPathLength":4096,"maxRequestIdLength":64,"resultHistory":64,"requestIdHistory":1024,"maxWaitMs":30000}}""",
            ["GetStatusRequest"] = """{}""",
            ["StatusResponse"] = """{"instanceId":"9f8e7d6c5b4a39281706f5e4d3c2b1a0","startupComplete":true,"readerInitializing":false,"uiResponsive":true,"stale":false,"project":{"loaded":true,"path":"C:\\Projects\\order.rfPrj","taskCount":3},"isRunning":true,"activeRun":{"requestId":"r-1","runId":"a1b2","source":"bridge","startedAt":"2026-11-12T10:00:00+00:00"},"reader":{"name":"Elatec TWN4","state":"ready","hasCard":true,"credentials":[{"uid":"04A1B2C3D4E5F6","typeCode":16385,"displayName":"MIFARE DESFire EV2","isPrimary":true}]},"lastResultSeq":7,"autoMode":{"enabled":true,"startedBy":"bridge","stopOnDisconnect":true,"lastStopReason":"runAborted"}}""",
            ["LoadProjectRequest"] = """{"path":"C:\\Projects\\order.rfPrj"}""",
            ["LoadProjectResponse"] = """{"path":"C:\\Projects\\order.rfPrj","taskCount":3}""",
            ["StartRunRequest"] = """{"requestId":"r-1","variables":[{"name":"$JOBNUMBER","value":"4711"}],"expectProjectPath":"C:\\Projects\\order.rfPrj","expectTaskCount":3}""",
            ["StartRunResponse"] = """{"requestId":"r-1","runId":"a1b2c3d4e5f60718293a4b5c6d7e8f90","state":"running","duplicate":false}""",
            ["CancelRunRequest"] = """{"requestId":"r-1"}""",
            ["CancelRunResponse"] = """{"state":"running","cancelRequested":true}""",
            ["GetRunResultRequest"] = """{"requestId":"r-1","waitMs":5000}""",
            ["GetRunResultResponse"] = """{"state":"completed","result":{"requestId":"r-1","runId":"a1b2c3d4e5f60718293a4b5c6d7e8f90","source":"bridge","status":"failure","failureReason":"cancelled","totalTasks":2,"failedTasks":1,"startedAt":"2026-11-12T10:00:00+00:00","completedAt":"2026-11-12T10:00:03+00:00","cardUid":"04A1B2C3D4E5F6","resultSeq":7,"taskOutcomes":[{"position":0,"projectTaskIndex":"0","taskTypeName":"MifareDesfireSetupViewModel","isSuccess":false,"normalizedErrorCode":"KeyUnavailable"},{"position":1,"projectTaskIndex":"1","taskTypeName":"MifareDesfireSetupViewModel","normalizedErrorCode":"Empty","skipReason":"NotReached"}]}}""",
            ["ScanReaderRequest"] = """{}""",
            ["ScanReaderResponse"] = """{"reader":{"name":"Elatec TWN4","state":"ready","hasCard":true,"credentials":[{"uid":"04A1B2C3D4E5F6","typeCode":16385,"displayName":"MIFARE DESFire EV2","isPrimary":true}]}}""",
            ["RunStartedEvent"] = """{"seq":1,"sessionId":"s1","at":"2026-11-12T10:00:00+00:00","requestId":"r-1","runId":"a1b2","startedAt":"2026-11-12T10:00:00+00:00"}""",
            ["RunCompletedEvent"] = """{"seq":2,"sessionId":"s1","at":"2026-11-12T10:00:03+00:00","result":{"requestId":"r-1","runId":"a1b2c3d4e5f60718293a4b5c6d7e8f90","source":"bridge","status":"failure","failureReason":"cancelled","totalTasks":2,"failedTasks":1,"startedAt":"2026-11-12T10:00:00+00:00","completedAt":"2026-11-12T10:00:03+00:00","cardUid":"04A1B2C3D4E5F6","resultSeq":7,"taskOutcomes":[{"position":0,"projectTaskIndex":"0","taskTypeName":"MifareDesfireSetupViewModel","isSuccess":false,"normalizedErrorCode":"KeyUnavailable"},{"position":1,"projectTaskIndex":"1","taskTypeName":"MifareDesfireSetupViewModel","normalizedErrorCode":"Empty","skipReason":"NotReached"}]}}""",
            ["RunningChangedEvent"] = """{"seq":3,"sessionId":"s1","at":"2026-11-12T10:00:00+00:00","isRunning":true,"ownRequestId":"r-1","ownRunId":"a1b2"}""",
            ["ReaderChangedEvent"] = """{"seq":4,"sessionId":"s1","at":"2026-11-12T10:00:00+00:00","reader":{"name":"Elatec TWN4","state":"ready","hasCard":true,"credentials":[{"uid":"04A1B2C3D4E5F6","typeCode":16385,"displayName":"MIFARE DESFire EV2","isPrimary":true}]}}""",
            ["ProjectChangedEvent"] = """{"seq":5,"sessionId":"s1","at":"2026-11-12T10:00:00+00:00","path":"C:\\Projects\\order.rfPrj","taskCount":3}""",
            ["ShuttingDownEvent"] = """{"seq":6,"sessionId":"s1","at":"2026-11-12T10:00:00+00:00","reason":"userClose","runInFlight":true}""",
            ["StartAutoModeRequest"] = """{"stopOnDisconnect":false}""",
            ["StartAutoModeResponse"] = """{"enabled":true,"alreadyEnabled":false,"stopOnDisconnect":false}""",
            ["StopAutoModeRequest"] = """{}""",
            ["StopAutoModeResponse"] = """{"wasEnabled":true}""",
            ["ListRunResultsRequest"] = """{"afterResultSeq":6,"limit":10}""",
            ["ListRunResultsResponse"] = """{"instanceId":"9f8e7d6c5b4a39281706f5e4d3c2b1a0","results":[{"requestId":"r-1","runId":"a1b2c3d4e5f60718293a4b5c6d7e8f90","source":"bridge","status":"failure","failureReason":"cancelled","totalTasks":2,"failedTasks":1,"startedAt":"2026-11-12T10:00:00+00:00","completedAt":"2026-11-12T10:00:03+00:00","cardUid":"04A1B2C3D4E5F6","resultSeq":7,"taskOutcomes":[{"position":0,"projectTaskIndex":"0","taskTypeName":"MifareDesfireSetupViewModel","isSuccess":false,"normalizedErrorCode":"KeyUnavailable"},{"position":1,"projectTaskIndex":"1","taskTypeName":"MifareDesfireSetupViewModel","normalizedErrorCode":"Empty","skipReason":"NotReached"}]},{"runId":"b2c3d4e5f60718293a4b5c6d7e8f90a1","source":"autoMode","status":"success","failureReason":"none","totalTasks":1,"failedTasks":0,"startedAt":"2026-11-12T10:00:00+00:00","completedAt":"2026-11-12T10:00:02+00:00","cardUid":"04A1B2C3D4E5F6","resultSeq":8,"taskOutcomes":[{"position":0,"projectTaskIndex":"0","taskTypeName":"MifareDesfireSetupViewModel","isSuccess":true,"normalizedErrorCode":"Empty"}]}],"gap":false,"lastResultSeq":8}""",
            ["AutoModeChangedEvent"] = """{"seq":7,"sessionId":"s1","at":"2026-11-12T10:00:00+00:00","enabled":false,"lastStopReason":"readerLost","lastResultSeq":8}""",
            ["AutoRunCompletedEvent"] = """{"seq":8,"sessionId":"s1","at":"2026-11-12T10:00:02+00:00","result":{"runId":"b2c3d4e5f60718293a4b5c6d7e8f90a1","source":"autoMode","status":"success","failureReason":"none","totalTasks":1,"failedTasks":0,"startedAt":"2026-11-12T10:00:00+00:00","completedAt":"2026-11-12T10:00:02+00:00","cardUid":"04A1B2C3D4E5F6","resultSeq":8,"taskOutcomes":[{"position":0,"projectTaskIndex":"0","taskTypeName":"MifareDesfireSetupViewModel","isSuccess":true,"normalizedErrorCode":"Empty"}]}}""",
            ["BridgeErrorData"] = """{"kind":"busy","errorId":"e-42","neverStarted":true,"details":{"activity":"run"}}""",
        };
    }
}
