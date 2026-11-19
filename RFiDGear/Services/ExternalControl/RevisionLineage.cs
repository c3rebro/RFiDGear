#nullable enable
using System;
using System.Collections.Generic;

namespace RFiDGear.Services.ExternalControl
{
    /// <summary>
    /// Remembers which project states are the same project because only a run lies between them. A run changes the project it
    /// runs: a DESFire task writes the application it created, the key version it read and the size it found into its own
    /// settings, and a read replaces what a task shows. Some of these fields are inputs as well as results, so no list of them
    /// can say what a run writes and what a person edits. What can be said is when: the state at the end of a run that the bridge
    /// started is the state it started with, and the run alone made the difference, because the project cannot be edited while
    /// a run goes on.
    /// <para>
    /// So when such a run ends, its end state is recorded as a descendant of its start state. A client that approved the
    /// revision of the start state can run the next card with it; a change made in the window after the run is another state,
    /// which no run led to, and is refused. Only the runs of the bridge are followed: a run started in the window, by automatic
    /// mode or by an extension changes the project without the bridge seeing where it started, and what it leaves is refused until
    /// the client has read the revision again (which is the safe way to be wrong).
    /// </para>
    /// </summary>
    internal sealed class RevisionLineage
    {
        private const int Limit = 64;

        private readonly object _gate = new object();
        private readonly Dictionary<string, string> _origin = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Queue<string> _order = new Queue<string>();

        /// <summary>Records that a run which started with one state ended with another.</summary>
        /// <param name="start">The revision the run started with.</param>
        /// <param name="end">The revision of the project right after the run.</param>
        internal void Absorb(string start, string end)
        {
            ArgumentNullException.ThrowIfNull(start);
            ArgumentNullException.ThrowIfNull(end);

            lock (_gate)
            {
                var origin = OriginOf(start);

                if (string.Equals(origin, end, StringComparison.Ordinal))
                {
                    return;
                }

                if (!_origin.ContainsKey(end))
                {
                    _order.Enqueue(end);
                }

                _origin[end] = origin;

                while (_order.Count > Limit)
                {
                    _origin.Remove(_order.Dequeue());
                }
            }
        }

        /// <summary>Says whether two states are the same project: they are, or only runs lie between them.</summary>
        /// <param name="current">The revision of the project now.</param>
        /// <param name="expected">The revision a client expects.</param>
        /// <returns><see langword="true"/> when a run of the bridge may have made the difference and nothing else.</returns>
        internal bool Matches(string current, string expected)
        {
            ArgumentNullException.ThrowIfNull(current);
            ArgumentNullException.ThrowIfNull(expected);

            lock (_gate)
            {
                return string.Equals(OriginOf(current), OriginOf(expected), StringComparison.Ordinal);
            }
        }

        // Follows the chain to the state that no run led to. The step count only guards against a loop that cannot be made.
        private string OriginOf(string revision)
        {
            for (var steps = 0; steps <= Limit && _origin.TryGetValue(revision, out var next) && !string.Equals(next, revision, StringComparison.Ordinal); steps++)
            {
                revision = next;
            }

            return revision;
        }
    }
}
