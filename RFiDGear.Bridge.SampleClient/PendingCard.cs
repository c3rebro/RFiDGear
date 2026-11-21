#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RFiDGear.Bridge.SampleClient
{
    /// <summary>A card whose decision is under way: what was shown, the source of the token of the decision, and what the decision will have answered.</summary>
    internal sealed class PendingCard
    {
        private readonly CancellationTokenSource _cancel;
        private int _dropped;

        /// <summary>Creates the record of a card that was shown.</summary>
        /// <param name="uid">The UID that was shown.</param>
        /// <param name="cancel">The source of the token of the decision.</param>
        /// <param name="answer">What the decision will have answered: what was sent, or <c>none</c>.</param>
        internal PendingCard(string uid, CancellationTokenSource cancel, Task<string> answer)
        {
            Uid = uid;
            _cancel = cancel;
            Answer = answer;
        }

        /// <summary>Gets the UID that was shown.</summary>
        internal string Uid { get; }

        /// <summary>Gets what the decision will have answered: what was sent, or <c>none</c>.</summary>
        internal Task<string> Answer { get; }

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
