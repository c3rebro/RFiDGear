using System;
using System.Collections.Generic;

namespace RFiDGear.Extensions.CardCheckAssistant.Workflow
{
    /// <summary>
    /// Which orders a window is checking. The reports of an order are files with fixed names, so two windows that
    /// check the same order would overwrite each other's reports; a window reserves the reports of its order from the
    /// start of the check until it lets go of it.
    /// </summary>
    internal interface IOrderReservations
    {
        /// <summary>Reserves the reports that belong to a key (the path of the finished report of the order).</summary>
        /// <param name="key">What is reserved.</param>
        /// <returns>The reservation, to be disposed when the window lets go of the order; <c>null</c> if another window holds it.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
        IDisposable? TryReserve(string key);
    }

    /// <summary>
    /// The reservations of one process. RFiDGear runs once, and all its windows of the assistant are in it, so one
    /// instance shared by them all is enough. Paths are compared without regard to the case, as Windows does.
    /// </summary>
    internal sealed class OrderReservations : IOrderReservations
    {
        private readonly object _gate = new object();
        private readonly HashSet<string> _held = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The instance the windows of the assistant share.</summary>
        internal static OrderReservations Shared { get; } = new OrderReservations();

        /// <inheritdoc />
        public IDisposable? TryReserve(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            lock (_gate)
            {
                return _held.Add(key) ? new Lease(this, key) : null;
            }
        }

        private void Release(string key)
        {
            lock (_gate)
            {
                _held.Remove(key);
            }
        }

        private sealed class Lease : IDisposable
        {
            private readonly OrderReservations _owner;
            private readonly string _key;
            private int _released;

            public Lease(OrderReservations owner, string key)
            {
                _owner = owner;
                _key = key;
            }

            // Letting go twice must not release a reservation that someone else holds by then.
            public void Dispose()
            {
                if (System.Threading.Interlocked.Exchange(ref _released, 1) == 0)
                {
                    _owner.Release(_key);
                }
            }
        }
    }
}
