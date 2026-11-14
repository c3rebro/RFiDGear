using System;

namespace RFiDGear.Infrastructure.Ultralight
{
    /// <summary>How a task authenticates to the chip before it reads or writes pages.</summary>
    public enum UltralightAuth
    {
        /// <summary>No authentication: the chip is used as it answers.</summary>
        None = 0,

        /// <summary>Ultralight C: a 3DES authentication with a 16-byte key.</summary>
        UltralightC = 1,

        /// <summary>Ultralight EV1 and NTAG: the password (4 bytes) and the PACK that the chip must answer with (2 bytes).</summary>
        Password = 2
    }

    /// <summary>
    /// What a reader needs to know to read, write or authenticate on a chip of the Ultralight family: the chip the task expects, how
    /// to authenticate, and whether protected pages may be written. It holds the key and the password as text for the duration of one
    /// call only, and never shows them: <see cref="ToString"/> names the access without them. A reader turns the text into bytes,
    /// uses them once, and clears the bytes.
    /// </summary>
    public readonly struct UltralightAccess
    {
        /// <summary>Creates the access.</summary>
        /// <param name="expected">The chip the task expects; authentication and writing protected pages need a named chip.</param>
        /// <param name="auth">How to authenticate.</param>
        /// <param name="keyHex">The key (Ultralight C: 32 hexadecimal digits) or the password (EV1 and NTAG: 8 digits).</param>
        /// <param name="packHex">The PACK (EV1 and NTAG: 4 digits).</param>
        /// <param name="allowProtectedPages">Whether the task opted in to writing protected pages.</param>
        public UltralightAccess(UltralightExpectation expected, UltralightAuth auth = UltralightAuth.None, string keyHex = null, string packHex = null, bool allowProtectedPages = false)
        {
            Expected = expected;
            Auth = auth;
            KeyHex = keyHex;
            PackHex = packHex;
            AllowProtectedPages = allowProtectedPages;
        }

        /// <summary>Gets the chip the task expects.</summary>
        public UltralightExpectation Expected { get; }

        /// <summary>Gets how to authenticate.</summary>
        public UltralightAuth Auth { get; }

        /// <summary>Gets a value that says whether the task opted in to writing protected pages.</summary>
        public bool AllowProtectedPages { get; }

        // Not public: only a reader of this assembly turns the text into bytes.
        internal string KeyHex { get; }

        internal string PackHex { get; }

        /// <summary>
        /// An access without authentication.
        /// </summary>
        /// <param name="expected">The chip the task expects.</param>
        /// <returns>The access.</returns>
        public static UltralightAccess Open(UltralightExpectation expected = UltralightExpectation.Auto) => new UltralightAccess(expected);

        /// <inheritdoc />
        public override string ToString() =>
            $"UltralightAccess(expects {Expected}, authentication {Auth}, protected pages {(AllowProtectedPages ? "allowed" : "not allowed")}, secrets not shown)";
    }

    /// <summary>What a reader did for one call of the page operations.</summary>
    public sealed class UltralightResult
    {
        private static readonly byte[] NoData = Array.Empty<byte>();

        internal UltralightResult(ERROR error, byte[] data, UltralightVariant? variant, bool authenticated, int pagesDone, int? failedPage)
        {
            Error = error;
            Data = data ?? NoData;
            Variant = variant;
            Authenticated = authenticated;
            PagesDone = pagesDone;
            FailedPage = failedPage;
        }

        /// <summary>Gets the normalized error code: <see cref="ERROR.NoError"/> when everything was done.</summary>
        public ERROR Error { get; }

        /// <summary>Gets a value that says whether everything was done.</summary>
        public bool Succeeded => Error == ERROR.NoError;

        /// <summary>
        /// Gets the bytes that were read, 4 per page, in order; never <c>null</c>. After a failed read it holds the pages read before
        /// the failure. A write leaves it empty.
        /// </summary>
        public byte[] Data { get; }

        /// <summary>Gets the chip that was found, when the reader got as far as telling it.</summary>
        public UltralightVariant? Variant { get; }

        /// <summary>Gets a value that says whether the reader authenticated to the chip.</summary>
        public bool Authenticated { get; }

        /// <summary>
        /// Gets the number of pages done: read, or written (a write that was refused stops at the page before it). A write that was
        /// read back and found different keeps the number of pages that were written.
        /// </summary>
        public int PagesDone { get; }

        /// <summary>Gets the page at which a read, a write or its check failed; <c>null</c> when the failure is not at a page.</summary>
        public int? FailedPage { get; }

        /// <summary>
        /// A call that failed before anything was done.
        /// </summary>
        /// <param name="error">The error.</param>
        /// <returns>The result.</returns>
        public static UltralightResult Failed(ERROR error) => new UltralightResult(error, null, null, false, 0, null);
    }
}
