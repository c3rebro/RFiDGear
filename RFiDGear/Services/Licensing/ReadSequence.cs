using System;
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

        /// <summary>
        /// Makes an action that runs only if its read is still the newest at the moment it is run. An answer is handed to another
        /// thread to be published, and a read can start while it waits there: the check has to be made when it is published, not
        /// before it is handed over.
        /// </summary>
        /// <param name="ticket">The number that <see cref="Begin"/> gave to the read.</param>
        /// <param name="publish">What publishes the answer.</param>
        /// <returns>The action that checks, and publishes when the read is still the newest.</returns>
        public Action Guard(int ticket, Action publish)
        {
            return () =>
            {
                if (IsLatest(ticket))
                {
                    publish();
                }
            };
        }
    }
}
