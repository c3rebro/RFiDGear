#nullable enable
using System;
using System.Collections.Generic;

namespace RFiDGear.Services.ExternalControl
{
    /// <summary>What the main window knew about a run when it took it, and nobody else can say afterwards.</summary>
    /// <param name="ProjectRevision">The revision of the project the run started with, or <see langword="null"/> when it could not be made.</param>
    internal sealed record RunFact(string? ProjectRevision);

    /// <summary>
    /// A small table of what the main window knows about the runs that it makes (the run of a card of automatic mode, by hand): the
    /// window takes the execution slot, so at that moment nothing can change the project, and the result of the run, which is made
    /// later and elsewhere, takes the fact from here by the id of the run. A fact is taken once. The table holds the last few only:
    /// a run whose result nobody asked for, because the bridge was off or no client was there, must not keep it for ever.
    /// </summary>
    internal sealed class RunFacts
    {
        private const int Limit = 16;

        private readonly object _gate = new object();
        private readonly Dictionary<string, RunFact> _facts = new Dictionary<string, RunFact>(StringComparer.Ordinal);
        private readonly Queue<string> _order = new Queue<string>();
        private int _wanted;

        /// <summary>
        /// Gets or sets whether somebody takes the facts (the bridge is listening for the results of automatic mode). The window notes
        /// a fact only while this is on, so that a revision, which costs a look at the whole project, is not made for nobody.
        /// </summary>
        internal bool IsWanted
        {
            get => System.Threading.Volatile.Read(ref _wanted) != 0;
            set => System.Threading.Volatile.Write(ref _wanted, value ? 1 : 0);
        }

        /// <summary>Notes what is known about a run.</summary>
        /// <param name="runId">The id of the run, as its result will carry it.</param>
        /// <param name="fact">What is known.</param>
        internal void Remember(string runId, RunFact fact)
        {
            ArgumentException.ThrowIfNullOrEmpty(runId);
            ArgumentNullException.ThrowIfNull(fact);

            lock (_gate)
            {
                if (!_facts.ContainsKey(runId))
                {
                    _order.Enqueue(runId);
                }

                _facts[runId] = fact;

                while (_order.Count > Limit)
                {
                    _facts.Remove(_order.Dequeue());
                }
            }
        }

        /// <summary>Takes what is known about a run, and forgets it.</summary>
        /// <param name="runId">The id of the run.</param>
        /// <returns>The fact, or <see langword="null"/> when nothing is known (it was taken, it is too old, or the run was not noted).</returns>
        internal RunFact? Take(string runId)
        {
            lock (_gate)
            {
                // The id stays in the queue until it is pushed out: it is cheap, and the queue is as short as the table.
                return _facts.Remove(runId, out var fact) ? fact : null;
            }
        }
    }
}
