#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Bridge;

namespace RFiDGear.Services.ExternalControl
{
    /// <summary>Makes an event when it is about to be sent, so that its content is the latest and its sequence number is in order.</summary>
    /// <param name="seq">The sequence number on this connection.</param>
    /// <param name="sessionId">The id of the connection.</param>
    /// <param name="at">The time of sending.</param>
    /// <returns>The event.</returns>
    internal delegate BridgeEvent BridgeEventFactory(long seq, string sessionId, DateTimeOffset at);

    /// <summary>An event waiting to be sent: its wire name and the way to make it.</summary>
    /// <param name="Name">The wire name (<see cref="BridgeEvents"/>).</param>
    /// <param name="Create">Makes the event at the time of sending.</param>
    internal sealed record PendingBridgeEvent(string Name, BridgeEventFactory Create);

    /// <summary>
    /// The events that wait for a connection to take them. Host events arrive on any thread, inside the admission of a run, so
    /// the handlers only put something here; one consumer sends, in order.
    /// <para>
    /// There are two kinds. A <em>state</em> event (the reader changed, a run started or ended) says "look again": while one of
    /// the same name is waiting, a newer one replaces it, because only the latest picture matters, and its content is made at
    /// the time of sending. A <em>durable</em> event (a run's result) is never merged or dropped: they wait in order, up to
    /// <see cref="DurableCap"/>; when a client is so slow that they pile up beyond it, the connection is ended and not an event
    /// lost (the results stay in the history and can be asked for).
    /// </para>
    /// </summary>
    internal sealed class BridgeEventQueue
    {
        /// <summary>How many durable events may wait.</summary>
        internal const int DurableCap = 64;

        private readonly object _gate = new object();
        private readonly LinkedList<Entry> _items = new LinkedList<Entry>();
        private readonly Dictionary<string, LinkedListNode<Entry>> _stateByName = new Dictionary<string, LinkedListNode<Entry>>(StringComparer.Ordinal);
        private TaskCompletionSource<bool> _signal = NewSignal();
        private int _durable;
        private bool _completed;

        /// <summary>Puts a state event in, or replaces the one of the same name that is waiting.</summary>
        /// <param name="name">The wire name.</param>
        /// <param name="create">Makes the event at the time of sending.</param>
        /// <returns><see langword="false"/> when the queue has ended.</returns>
        internal bool EnqueueState(string name, BridgeEventFactory create)
        {
            lock (_gate)
            {
                if (_completed)
                {
                    return false;
                }

                if (_stateByName.TryGetValue(name, out var waiting))
                {
                    waiting.Value = new Entry(name, create, Durable: false);
                }
                else
                {
                    _stateByName[name] = _items.AddLast(new Entry(name, create, Durable: false));
                }

                Wake();

                return true;
            }
        }

        /// <summary>Puts a durable event in.</summary>
        /// <param name="name">The wire name.</param>
        /// <param name="create">Makes the event at the time of sending.</param>
        /// <returns><see langword="false"/> when the queue has ended or is full: the caller ends the connection.</returns>
        internal bool EnqueueDurable(string name, BridgeEventFactory create)
        {
            lock (_gate)
            {
                if (_completed || _durable >= DurableCap)
                {
                    return false;
                }

                _durable++;
                _items.AddLast(new Entry(name, create, Durable: true));
                Wake();

                return true;
            }
        }

        /// <summary>Waits for the next event.</summary>
        /// <param name="cancellationToken">Cancelled when the connection ends.</param>
        /// <returns>The next event in the order they were first put in, or <see langword="null"/> when the queue has ended and is empty.</returns>
        internal async ValueTask<PendingBridgeEvent?> DequeueAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                Task wait;

                lock (_gate)
                {
                    var first = _items.First;

                    if (first != null)
                    {
                        _items.RemoveFirst();
                        var entry = first.Value;

                        if (entry.Durable)
                        {
                            _durable--;
                        }
                        else
                        {
                            _stateByName.Remove(entry.Name);
                        }

                        return new PendingBridgeEvent(entry.Name, entry.Create);
                    }

                    if (_completed)
                    {
                        return null;
                    }

                    wait = _signal.Task;
                }

                await wait.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>Ends the queue: what is waiting can still be taken, then <see cref="DequeueAsync"/> answers <see langword="null"/>.</summary>
        internal void Complete()
        {
            lock (_gate)
            {
                _completed = true;
                Wake();
            }
        }

        private static TaskCompletionSource<bool> NewSignal() => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        private void Wake()
        {
            _signal.TrySetResult(true);
            _signal = NewSignal();
        }

        private sealed record Entry(string Name, BridgeEventFactory Create, bool Durable);
    }
}
