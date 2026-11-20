using System.Diagnostics;
using System.Text.Json.Serialization;

namespace RFiDGear.Bridge
{
    /// <summary>The parameters of <c>registerKeyProvider</c>. Every field is optional; the default is what every client got before the field existed.</summary>
    public sealed record RegisterKeyProviderRequest
    {
        /// <summary>
        /// Gets whether RFiDGear is to ask this client only for the keys of runs that the client started over this bridge (<see cref="RunSources.Bridge"/>).
        /// For every other run (the window, automatic mode, an extension, a button of a task dialog) RFiDGear answers
        /// <see cref="KeyResolveResults.NotAuthorized"/> itself and does not call the client. The default is <see langword="false"/>.
        /// Needs <see cref="BridgeCapabilities.KeysContext"/>; <see cref="RegisterKeyProviderResponse.OwnRunsOnly"/> says whether it is in force.
        /// The latest registration of a connection counts. It is left out of the message while it is <see langword="false"/>, so a
        /// registration that does not ask for it is the same message as before the field existed and a host that does not know the
        /// field (it rejects members it does not know) still accepts it.
        /// </summary>
        [JsonPropertyName("ownRunsOnly")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool OwnRunsOnly { get; init; }
    }

    /// <summary>The answer to <c>registerKeyProvider</c>.</summary>
    public sealed record RegisterKeyProviderResponse
    {
        /// <summary>
        /// Gets the id of the provider that stands for the client. A project names a key as this provider and a reference of the
        /// client's choosing (a name in its vault, for example); RFiDGear passes the reference on and never interprets it.
        /// </summary>
        [JsonPropertyName("providerId")]
        public string ProviderId { get; init; } = string.Empty;

        /// <summary>
        /// Gets how long the client has to answer <see cref="IBridgeClientKeys.ResolveKey"/> when no person is asked, in
        /// milliseconds. After that the request is cancelled (the token of the call), the task ends with
        /// <c>ERROR.KeyUnavailable</c>, and a key that arrives later is thrown away.
        /// </summary>
        [JsonPropertyName("timeoutMs")]
        public int TimeoutMs { get; init; }

        /// <summary>Gets how long the client has to answer when <see cref="ResolveKeyRequest.Interactive"/> is set, in milliseconds.</summary>
        [JsonPropertyName("interactiveTimeoutMs")]
        public int InteractiveTimeoutMs { get; init; }

        /// <summary>
        /// Gets whether RFiDGear applies <see cref="RegisterKeyProviderRequest.OwnRunsOnly"/>: <see langword="true"/> only when the
        /// client asked for it and the host offers <see cref="BridgeCapabilities.KeysContext"/>. A client that needs the
        /// restriction checks this and does not rely on the request alone. Left out of the message while it is
        /// <see langword="false"/>: nothing and <see langword="false"/> mean the same.
        /// </summary>
        [JsonPropertyName("ownRunsOnly")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool OwnRunsOnly { get; init; }
    }

    /// <summary>
    /// What RFiDGear asks the client for: one card key. It says which key is meant (the reference of the project) and for what
    /// (slot, algorithm, usage, scope), and which task and card it is for, so that a vault can decide and keep a record. It is
    /// never logged by RFiDGear, and its text form says nothing about it.
    /// </summary>
    [DebuggerDisplay("ResolveKeyRequest [redacted]")]
    public sealed record ResolveKeyRequest
    {
        /// <summary>Gets the id of the key slot of the task (for example the application master key of a DESFire command); informational.</summary>
        [JsonPropertyName("slotId")]
        public string SlotId { get; init; } = string.Empty;

        /// <summary>Gets the reference the project names for the key. Opaque to RFiDGear; the client decides what it means, and what it answers to.</summary>
        [JsonPropertyName("reference")]
        public string Reference { get; init; } = string.Empty;

        /// <summary>Gets the kind of key, one of <see cref="KeyAlgorithms"/>.</summary>
        [JsonPropertyName("algorithm")]
        public string Algorithm { get; init; } = string.Empty;

        /// <summary>Gets what the key is for, one of <see cref="KeyUsages"/>.</summary>
        [JsonPropertyName("usage")]
        public string Usage { get; init; } = string.Empty;

        /// <summary>Gets the DESFire application the key belongs to, when the task is about one.</summary>
        [JsonPropertyName("applicationId")]
        public long? ApplicationId { get; init; }

        /// <summary>Gets the number of the key in its application, when the task is about one.</summary>
        [JsonPropertyName("keyNumber")]
        public int? KeyNumber { get; init; }

        /// <summary>Gets the id of the run the task belongs to (the one in <see cref="RunResultDto.RunId"/>), when there is one.</summary>
        [JsonPropertyName("runId")]
        public string? RunId { get; init; }

        /// <summary>Gets the position of the task in the project, as the project writes it; free text.</summary>
        [JsonPropertyName("taskIndex")]
        public string TaskIndex { get; init; } = string.Empty;

        /// <summary>Gets the UID of the card the task works on, as upper-case hexadecimal digits, when it is known; the same UID that the result of the run carries.</summary>
        [JsonPropertyName("cardUid")]
        public string? CardUid { get; init; }

        /// <summary>Gets whether a person may be asked (the task was started by a click, not by a run without anybody there); the time limit is then <see cref="RegisterKeyProviderResponse.InteractiveTimeoutMs"/>.</summary>
        [JsonPropertyName("interactive")]
        public bool Interactive { get; init; }

        /// <summary>
        /// Gets the request id the client chose for the run, when the client started the run over this bridge (<see cref="Source"/>
        /// is <see cref="RunSources.Bridge"/>); nothing for any other run. It is what the client matches against the requests it
        /// sent with <c>startRun</c>, without waiting for the <c>runId</c> in the answer. Needs <see cref="BridgeCapabilities.KeysContext"/>.
        /// </summary>
        [JsonPropertyName("requestId")]
        public string? RequestId { get; init; }

        /// <summary>
        /// Gets who started the run the key is for, one of <see cref="RunSources"/>: <see cref="RunSources.Bridge"/> for a run
        /// of a client of this bridge (any client; compare <see cref="RequestId"/> to know whether it was this one),
        /// <see cref="RunSources.AutoMode"/>, <see cref="RunSources.Gui"/> for a run that the user started in the main window or
        /// with a start-up argument, and <see cref="RunSources.Other"/> for everything else (an extension, a button of a task
        /// dialog, a run that RFiDGear cannot place). A host that does not offer <see cref="BridgeCapabilities.KeysContext"/>
        /// sends nothing, which reads as <see cref="RunSources.Other"/>: a missing value is never taken for a run of the client.
        /// </summary>
        [JsonPropertyName("source")]
        public string Source { get; init; } = RunSources.Other;

        /// <summary>
        /// Gets the revision of the project the run started with, when <see cref="Source"/> is <see cref="RunSources.Bridge"/> and
        /// the host could tell it (see <see cref="BridgeCapabilities.ProjectRevision"/>); nothing otherwise. Opaque, and only to be
        /// compared with a revision that the client approved.
        /// </summary>
        [JsonPropertyName("projectRevision")]
        public string? ProjectRevision { get; init; }

        /// <summary>Gets a text form that holds nothing of the request.</summary>
        /// <returns>A fixed text.</returns>
        public override string ToString() => "ResolveKeyRequest [redacted]";
    }

    /// <summary>
    /// The client's answer to a request for a key. A key travels as base64 text in a JSON message: the strings and buffers of
    /// the JSON library cannot be erased by RFiDGear or by the client, only the copy RFiDGear keeps for the card operation is.
    /// </summary>
    [DebuggerDisplay("ResolveKeyResponse [redacted]")]
    public sealed record ResolveKeyResponse
    {
        /// <summary>Gets how the client answered, one of <see cref="KeyResolveResults"/>. Anything else is taken as a failure of the client.</summary>
        [JsonPropertyName("result")]
        public string Result { get; init; } = string.Empty;

        /// <summary>
        /// Gets the key as base64 text, 1 to <see cref="BridgeProtocol.MaxKeyBytes"/> bytes of it, when <see cref="Result"/> is
        /// <see cref="KeyResolveResults.Resolved"/>; nothing otherwise. A key of the wrong length for the algorithm is
        /// refused by the task, not here.
        /// </summary>
        [JsonPropertyName("key")]
        public string? Key { get; init; }

        /// <summary>Gets a text form that holds nothing of the answer.</summary>
        /// <returns>A fixed text.</returns>
        public override string ToString() => "ResolveKeyResponse [redacted]";
    }
}
