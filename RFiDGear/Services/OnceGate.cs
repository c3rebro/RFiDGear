using System.Threading;

namespace RFiDGear.Services
{
    /// <summary>
    /// Lets exactly one of several triggers do a piece of work: the first <see cref="TryEnter"/> wins, every later one is told
    /// to stand back. The start-up of the main window has two triggers (the window being activated, and a fallback for a window
    /// that is never activated) and must run once.
    /// </summary>
    internal sealed class OnceGate
    {
        private int _entered;

        /// <summary>Gets whether some caller has already won the gate.</summary>
        internal bool HasEntered => Volatile.Read(ref _entered) != 0;

        /// <summary>Tries to be the one that does the work.</summary>
        /// <returns><see langword="true"/> for the first caller only.</returns>
        internal bool TryEnter() => Interlocked.Exchange(ref _entered, 1) == 0;
    }
}
