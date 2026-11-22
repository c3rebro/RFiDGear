using System.Threading;

namespace RFiDGear.Services.Licensing
{
    /// <summary>
    /// Numbers the reads of the license, so that of several reads that are under way at the same time only the newest one counts:
    /// the Store may answer them in another order than it was asked, and an older "expired" must not be published after a newer
    /// "bought".
    /// </summary>
    public sealed class ReadSequence
    {
        private int _latest;

        /// <summary>
        /// Starts a read.
        /// </summary>
        /// <returns>The number of the read, which is now the newest.</returns>
        public int Begin() => Interlocked.Increment(ref _latest);

        /// <summary>
        /// Tells whether a read is still the newest one.
        /// </summary>
        /// <param name="ticket">The number that <see cref="Begin"/> gave to the read.</param>
        /// <returns><see langword="true"/> when no read has started since.</returns>
        public bool IsLatest(int ticket) => Volatile.Read(ref _latest) == ticket;
    }
}
