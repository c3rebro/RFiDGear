using System;

namespace RFiDGear.Contracts
{
    /// <summary>
    /// The answer of a <see cref="ISecureKeyProvider"/>: either a key (<see cref="Resolved"/>) or the reason there is none
    /// (<see cref="Failed"/>). Whoever holds a resolved answer owns the key and disposes the resolution, or the key, when
    /// the card operation is over.
    /// </summary>
    public sealed class SecureKeyResolution : IDisposable
    {
        private SecureKeyResolution(ResolvedSecureKey? key, SecureKeyFailureReason? failureReason)
        {
            Key = key;
            FailureReason = failureReason;
        }

        /// <summary>Gets whether the provider handed out a key.</summary>
        public bool IsResolved => Key != null;

        /// <summary>Gets the key, or <c>null</c> when the answer is a failure.</summary>
        public ResolvedSecureKey? Key { get; }

        /// <summary>Gets the reason there is no key, or <c>null</c> when the provider handed one out.</summary>
        public SecureKeyFailureReason? FailureReason { get; }

        /// <summary>Creates the answer "here is the key". The receiver owns <paramref name="key"/>.</summary>
        /// <param name="key">The key.</param>
        /// <returns>The answer.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
        public static SecureKeyResolution Resolved(ResolvedSecureKey key)
        {
            ArgumentNullException.ThrowIfNull(key);
            return new SecureKeyResolution(key, null);
        }

        /// <summary>Creates the answer "there is no key", with the reason.</summary>
        /// <param name="reason">Why there is no key.</param>
        /// <returns>The answer.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="reason"/> is not a defined value.</exception>
        public static SecureKeyResolution Failed(SecureKeyFailureReason reason)
        {
            if (!Enum.IsDefined(reason))
            {
                throw new ArgumentOutOfRangeException(nameof(reason), "Not a defined failure reason.");
            }

            return new SecureKeyResolution(null, reason);
        }

        /// <summary>Disposes the key, if there is one. Calling it more than once is allowed.</summary>
        public void Dispose() => Key?.Dispose();

        /// <inheritdoc />
        public override string ToString() => IsResolved ? "SecureKeyResolution [resolved]" : $"SecureKeyResolution [failed: {FailureReason}]";
    }
}
