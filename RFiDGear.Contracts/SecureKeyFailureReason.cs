namespace RFiDGear.Contracts
{
    /// <summary>
    /// Why a provider did not hand out a key. The host never touches the card after a failure, and it never falls back
    /// to another key: the task fails with the normalized error code <c>KeyUnavailable</c>, whatever the reason. The
    /// reason goes to the log and helps the person who has to fix it. There is deliberately no member for "wrong key":
    /// only the card can say that.
    /// </summary>
    public enum SecureKeyFailureReason
    {
        /// <summary>The provider is reachable, but knows no key for the reference.</summary>
        NotFound = 1,

        /// <summary>The provider knows the reference and refuses to give the key to this caller.</summary>
        NotAuthorized = 2,

        /// <summary>
        /// The provider cannot answer now: the vault is down or locked, or the connection failed. The host also uses this
        /// reason when the provider named in a project is not installed, or when a provider throws.
        /// </summary>
        ProviderUnavailable = 3,

        /// <summary>The provider did not answer in time. Set by the host, which enforces the time limit.</summary>
        Timeout = 4,

        /// <summary>The request was cancelled before an answer arrived.</summary>
        Cancelled = 5,

        /// <summary>The provider has a value for the reference, but it is not a usable key for the request.</summary>
        InvalidKey = 6,

        /// <summary>The provider cannot serve this request: an algorithm, a usage or an interactive prompt it does not support.</summary>
        Unsupported = 7,
    }
}
