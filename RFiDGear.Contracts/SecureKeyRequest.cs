namespace RFiDGear.Contracts
{
    /// <summary>
    /// What the host asks a <see cref="ISecureKeyProvider"/> for: one key, named by a reference the provider understands,
    /// for one slot of one task. Immutable. It contains no secret, so it may be logged, but the card UID identifies a card
    /// and <see cref="ToString"/> leaves it out.
    /// </summary>
    public sealed class SecureKeyRequest
    {
        /// <summary>
        /// Gets the stable identifier of the key slot in the task, for example <c>desfire.app.current</c>. For logs and for a
        /// provider that keeps a separate key per slot; the host does not interpret what a provider does with it.
        /// </summary>
        public string SlotId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the reference the project stores instead of the key. Opaque to the host, which only checks that it is
        /// a short string of letters, digits and <c>._:/-</c>. A provider decides what it means: a name in a vault, an
        /// identifier of a key on a secure element, a key to diversify.
        /// </summary>
        public string Reference { get; init; } = string.Empty;

        /// <summary>Gets the algorithm the key is used with, which also fixes how many bytes the answer has.</summary>
        public SecureKeyAlgorithm Algorithm { get; init; }

        /// <summary>Gets the role of the key in the operation.</summary>
        public SecureKeyUsage Usage { get; init; }

        /// <summary>Gets where on the card the key is used. Never <c>null</c>.</summary>
        public SecureKeyScope Scope { get; init; } = SecureKeyScope.None;

        /// <summary>
        /// Gets the identifier of the execution run, the same value as <see cref="ExecResult.RunId"/>, or <c>null</c> when
        /// the key is asked for outside of a run (a single command in a task dialog).
        /// </summary>
        public string? RunId { get; init; }

        /// <summary>
        /// Gets the task index as stored in the project (see <see cref="TaskOutcome.ProjectTaskIndex"/>), or an empty
        /// string when the key is not asked for by a task.
        /// </summary>
        public string TaskIndex { get; init; } = string.Empty;

        /// <summary>
        /// Gets the UID of the card at the start of the run, in upper case, as <see cref="CredentialInfo.Uid"/> reports it,
        /// or <c>null</c> when the host does not know a card (a run that has not read one, a dialog command). A provider
        /// that derives its key from the card has to fail with <see cref="SecureKeyFailureReason.Unsupported"/> when this is
        /// <c>null</c>, and never guess.
        /// </summary>
        public string? CardUid { get; init; }

        /// <summary>
        /// Gets whether the provider may ask a person (a password prompt, a PIN). <c>false</c> for service runs and the
        /// automatic mode, where nobody is there: a provider then has to answer from what it has, or fail with
        /// <see cref="SecureKeyFailureReason.Unsupported"/>.
        /// </summary>
        public bool Interactive { get; init; }

        /// <inheritdoc />
        public override string ToString() =>
            $"SecureKeyRequest {{ SlotId = {SlotId}, Reference = {Reference}, Algorithm = {Algorithm}, Usage = {Usage}, " +
            $"RunId = {RunId ?? "-"}, TaskIndex = {TaskIndex}, Interactive = {Interactive} }}";
    }
}
