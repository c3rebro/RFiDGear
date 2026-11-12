using System;

namespace RFiDGear.Contracts
{
    /// <summary>
    /// The key a provider hands out, together with the duty to dispose it. It is an abstract base on purpose: today the
    /// only kind of key is <see cref="SecureKeyLease"/>, bytes in the host's memory, and the host treats every other
    /// subclass as <see cref="SecureKeyFailureReason.Unsupported"/> (and disposes it). That leaves room for kinds of key
    /// that never leave a secure element, without changing what a provider that hands out bytes has to do.
    /// <para>
    /// The receiver owns the key and disposes it as soon as the card operation is over. Disposing is idempotent.
    /// </para>
    /// </summary>
    public abstract class ResolvedSecureKey : IDisposable
    {
        /// <summary>Releases the key. Calling it more than once is allowed.</summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>Releases the key; called once by <see cref="Dispose()"/> and safe to call again.</summary>
        /// <param name="disposing">Always <c>true</c>: no finalizer calls this.</param>
        protected abstract void Dispose(bool disposing);
    }
}
