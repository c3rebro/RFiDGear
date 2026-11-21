#nullable enable
using System;
using System.Collections.Generic;

namespace RFiDGear.Services.ExternalControl
{
    /// <summary>
    /// Remembers which connection approved the card that a run of automatic mode was made for, by the id of the run. The window runs a
    /// card of the mode, so it is a run of the mode and not a run of the client; but the client decided that it should run, and it is
    /// the one that the run is answered to, so the restriction "my own runs only" counts it as its own. It holds a run id and a
    /// connection id, nothing else, and forgets the oldest when there are more than <see cref="Limit"/>: a run is only asked about
    /// while it is going on, and the origin of a run that is over is never looked up (the window has let go of it).
    /// </summary>
    internal sealed class ApprovedRunTable
    {
        /// <summary>How many runs are remembered.</summary>
        internal const int Limit = 64;

        private readonly object _gate = new object();
        private readonly Dictionary<string, string> _sessionByRun = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Queue<string> _order = new Queue<string>();

        /// <summary>Remembers that a connection approved the card of a run.</summary>
        /// <param name="runId">The id of the run that was admitted for the approved card.</param>
        /// <param name="sessionId">The connection that approved it.</param>
        internal void Add(string runId, string sessionId)
        {
            if (string.IsNullOrEmpty(runId) || string.IsNullOrEmpty(sessionId))
            {
                return;
            }

            lock (_gate)
            {
                if (_sessionByRun.ContainsKey(runId))
                {
                    _sessionByRun[runId] = sessionId;

                    return;
                }

                _sessionByRun[runId] = sessionId;
                _order.Enqueue(runId);

                while (_order.Count > Limit)
                {
                    _sessionByRun.Remove(_order.Dequeue());
                }
            }
        }

        /// <summary>Says which connection approved the card of a run.</summary>
        /// <param name="runId">The id of a run, or <see langword="null"/>.</param>
        /// <returns>The connection, or <see langword="null"/> when the run is not one that a client approved.</returns>
        internal string? SessionOf(string? runId)
        {
            if (string.IsNullOrEmpty(runId))
            {
                return null;
            }

            lock (_gate)
            {
                return _sessionByRun.TryGetValue(runId, out var sessionId) ? sessionId : null;
            }
        }
    }
}
