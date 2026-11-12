using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading;

namespace RFiDGear.Contracts
{
    /// <summary>
    /// A key as bytes, held in memory that the garbage collector does not move (the pinned object heap) and that is
    /// overwritten with zeros when the lease is disposed. The lease never shows the key outside <see cref="Key"/> and
    /// <see cref="ToHexString"/>: <see cref="ToString"/> and the debugger display are redacted, and it has no equality, no
    /// serialization and no finalizer.
    /// <para>
    /// What a lease cannot do: bytes that were already copied out (a hex string from <see cref="ToHexString"/>, a copy
    /// of <see cref="Key"/>) are not under its control, and an immutable string stays in memory until the next garbage
    /// collection. Disposing a lease while another thread still reads <see cref="Key"/> makes that thread read zeros.
    /// </para>
    /// </summary>
    [DebuggerDisplay("SecureKeyLease [redacted]")]
    public sealed class SecureKeyLease : ResolvedSecureKey
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly byte[] _key;

        private int _disposed;

        /// <summary>
        /// Creates a lease over a copy of <paramref name="key"/>. The caller keeps its own buffer and has to zero it.
        /// </summary>
        /// <param name="key">The key bytes; at least one.</param>
        /// <exception cref="ArgumentException"><paramref name="key"/> is empty.</exception>
        public SecureKeyLease(ReadOnlySpan<byte> key)
        {
            if (key.IsEmpty)
            {
                throw new ArgumentException("A key has at least one byte.", nameof(key));
            }

            _key = GC.AllocateArray<byte>(key.Length, pinned: true);
            key.CopyTo(_key);
            Length = key.Length;
        }

        /// <summary>Gets the number of bytes in the key. Still valid after the lease is disposed.</summary>
        public int Length { get; }

        /// <summary>Gets the key bytes. Do not keep the span, and do not copy it into memory that outlives the operation.</summary>
        /// <exception cref="ObjectDisposedException">The lease has been disposed.</exception>
        public ReadOnlySpan<byte> Key
        {
            get
            {
                ThrowIfDisposed();
                return _key;
            }
        }

        /// <summary>
        /// Returns the key as upper-case hexadecimal digits without separators. The string is immutable, so the lease
        /// cannot zero it: it stays in memory until the garbage collector reclaims it. For the reader libraries that take
        /// a key as text.
        /// </summary>
        /// <returns>The hexadecimal digits of the key.</returns>
        /// <exception cref="ObjectDisposedException">The lease has been disposed.</exception>
        public string ToHexString()
        {
            ThrowIfDisposed();
            return Convert.ToHexString(_key);
        }

        /// <inheritdoc />
        public override string ToString() => "SecureKeyLease [redacted]";

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                CryptographicOperations.ZeroMemory(_key);
            }
        }

        private void ThrowIfDisposed() =>
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
    }
}
