#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RFiDGear.Bridge.SampleClient
{
    /// <summary>A card whose decision is under way: what was shown, the source of the token of the decision, what the decision will have answered, and whether an answer was sent.</summary>
    internal sealed class PendingCard
    {
        private readonly CancellationTokenSource _cancel;
        private int _dropped;
        private int _sent;

        /// <summary>Creates the record of a card that was shown.</summary>
        /// <param name="uid">The UID that was shown.</param>
        /// <param name="cancel">The source of the token of the decision.</param>
        internal PendingCard(string uid, CancellationTokenSource cancel)
        {
            Uid = uid;
            _cancel = cancel;
            Token = cancel.Token;
        }

        /// <summary>Gets the UID that was shown.</summary>
        internal string Uid { get; }

        /// <summary>Gets the token of the decision, which is cancelled when the decision is dropped. It stays valid when the source is let go of.</summary>
        internal CancellationToken Token { get; }

        /// <summary>Gets or sets what the decision will have answered: what was sent, or <c>none</c> (a card that was never decided about has that answer from the start).</summary>
        internal Task<string> Answer { get; set; } = Task.FromResult("none");

        /// <summary>Gets a value indicating whether an answer was sent to RFiDGear for this card, whether or not RFiDGear has replied to it.</summary>
        internal bool Sent => Volatile.Read(ref _sent) != 0;

        /// <summary>Notes that an answer is being sent: it is noted before the request goes out, because the request cannot be called back.</summary>
        internal void MarkSent() => Volatile.Write(ref _sent, 1);

        /// <summary>
        /// Drops the decision: its token is cancelled when this returns, and the callbacks that the program registered on it run
        /// elsewhere, without anybody waiting for them. <c>Cancel</c> would run them on the calling thread, where one that blocks would
        /// hold up the events and the stop of the mode, and one that throws would end the loop. The source is let go of once the
        /// callbacks have run. Doing it twice does nothing.
        /// </summary>
        internal void Drop()
        {
            if (Interlocked.Exchange(ref _dropped, 1) != 0)
            {
                return;
            }

            _ = _cancel.CancelAsync().ContinueWith(
                task =>
                {
                    // What the callbacks threw is of no interest to anybody; the token is cancelled all the same.
                    _ = task.Exception;
                    _cancel.Dispose();
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }
}
