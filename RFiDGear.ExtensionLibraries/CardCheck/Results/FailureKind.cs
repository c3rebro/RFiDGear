namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>
    /// What kind of problem stopped an operation. A caller decides what to show or retry from this, never from
    /// the text of a message.
    /// </summary>
    public enum FailureKind
    {
        /// <summary>The caller passed something that cannot be used (an empty name, a path outside its root).</summary>
        InvalidInput,

        /// <summary>A saved setting exists but is unusable (a malformed server name, an unknown table).</summary>
        InvalidConfiguration,

        /// <summary>A setting the operation needs has not been entered yet.</summary>
        NotConfigured,

        /// <summary>The thing asked for does not exist (no such order, no report stored for it).</summary>
        NotFound,

        /// <summary>A server or a share could not be reached.</summary>
        Unreachable,

        /// <summary>The operation ran out of time.</summary>
        Timeout,

        /// <summary>The operating system or the server refused access.</summary>
        AccessDenied,

        /// <summary>A file or a stored value exists but cannot be read (a damaged PDF, an unreadable settings file).</summary>
        Corrupt,

        /// <summary>A file operation failed for a reason other than the ones above.</summary>
        Io,

        /// <summary>The operation was cancelled by the caller.</summary>
        Cancelled,

        /// <summary>Anything else. The details went to the log, not into the failure.</summary>
        Unexpected,
    }
}
