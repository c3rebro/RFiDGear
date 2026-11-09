using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>
    /// An <see cref="IOrderStore"/> that keeps its orders in memory, for the assistant's test mode: a check can be tried
    /// with only a reader and a card, and nothing is read from or written to an order database. It answers as the store on
    /// the real order table does, in the same order of checks and with the same failures (<see cref="OrderFailures"/>), so
    /// what works against it works against the real one. The orders and every change to them last as long as the store.
    /// </summary>
    internal sealed class InMemoryOrderStore : IOrderStore
    {
        private readonly object _gate = new object();
        private readonly List<StoredOrder> _orders;
        private readonly Func<DateTime> _now;

        /// <summary>Creates the store.</summary>
        /// <param name="orders">The orders it starts out with; copied, so the caller's array stays as it is.</param>
        /// <param name="now">The clock for the date of a change; the local time unless a test sets another.</param>
        /// <exception cref="ArgumentNullException"><paramref name="orders"/> is null.</exception>
        public InMemoryOrderStore(IEnumerable<StubOrder> orders, Func<DateTime>? now = null)
        {
            if (orders == null)
            {
                throw new ArgumentNullException(nameof(orders));
            }

            // A report without content is no report, as with the table, where it reads back as "not found".
            _orders = orders.Select(order => new StoredOrder(order.Record, order.Report is { Length: 0 } ? null : Copy(order.Report))).ToList();
            _now = now ?? (() => DateTime.Now);
        }

        /// <inheritdoc />
        public Task<Result<IReadOnlyList<OrderRecord>>> ListAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(Result<IReadOnlyList<OrderRecord>>.Fail(OrderFailures.Cancelled()));
            }

            lock (_gate)
            {
                // Newest first, by the creation date as text in the current culture's order, as the store on the table does.
                var orders = _orders
                    .Select(order => order.Record)
                    .OrderByDescending(record => record.DateCreated, StringComparer.CurrentCulture)
                    .ToList();

                return Task.FromResult(Result<IReadOnlyList<OrderRecord>>.Ok(orders));
            }
        }

        /// <inheritdoc />
        public Task<Result<byte[]>> DownloadReportAsync(string orderId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                return Task.FromResult(Result<byte[]>.Fail(OrderFailures.MissingId()));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(Result<byte[]>.Fail(OrderFailures.Cancelled()));
            }

            lock (_gate)
            {
                // No such order and no report are the same answer, as with the table.
                var report = Find(orderId)?.Report;

                return Task.FromResult(report == null ? Result<byte[]>.Fail(OrderFailures.NoReport()) : Result<byte[]>.Ok(Copy(report)!));
            }
        }

        /// <inheritdoc />
        public Task<Result> ReplaceReportAsync(string orderId, byte[] report, CancellationToken cancellationToken)
        {
            var refused = RefuseReport(report);

            return Task.FromResult(refused ?? Write(orderId, cancellationToken, order => order.Report = Copy(report)));
        }

        /// <inheritdoc />
        public Task<Result> FinalizeAsync(string orderId, byte[] report, CancellationToken cancellationToken)
        {
            var refused = RefuseReport(report);

            // The report, the status and the date together: an order is never marked finished while it has no report.
            return Task.FromResult(refused ?? Write(orderId, cancellationToken, order =>
            {
                order.Report = Copy(report);
                order.Record = order.Record with
                {
                    StatusText = OrderStatusText.CheckFinished,
                    DateModified = ChangeDate(),
                };
            }));
        }

        /// <inheritdoc />
        public Task<Result> TouchChangedDateAsync(string orderId, CancellationToken cancellationToken) =>
            Task.FromResult(Write(orderId, cancellationToken, order => order.Record = order.Record with { DateModified = ChangeDate() }));

        /// <inheritdoc />
        public Task<Result> TestConnectionAsync(CancellationToken cancellationToken) =>
            Task.FromResult(cancellationToken.IsCancellationRequested ? Result.Fail(OrderFailures.Cancelled()) : Result.Ok());

        // A report that is missing throws, and one without content is refused, before the order is looked at.
        private static Result? RefuseReport(byte[] report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            return report.Length == 0 ? Result.Fail(OrderFailures.EmptyReport()) : null;
        }

        // The checks of a write in the order the store on the table makes them; the change itself is made under the lock.
        private Result Write(string orderId, CancellationToken cancellationToken, Action<StoredOrder> change)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                return Result.Fail(OrderFailures.MissingId());
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Result.Fail(OrderFailures.Cancelled());
            }

            lock (_gate)
            {
                var order = Find(orderId);

                if (order == null)
                {
                    return Result.Fail(OrderFailures.NoOrder());
                }

                change(order);

                return Result.Ok();
            }
        }

        // Call under the lock.
        private StoredOrder? Find(string orderId) =>
            _orders.FirstOrDefault(order => string.Equals(order.Record.Id, orderId, StringComparison.Ordinal));

        private string ChangeDate() => _now().ToString(CultureInfo.CurrentCulture);

        private static byte[]? Copy(byte[]? bytes) => bytes == null ? null : (byte[])bytes.Clone();

        // One order and the report stored with it. Only used under the lock.
        private sealed class StoredOrder
        {
            public StoredOrder(OrderRecord record, byte[]? report)
            {
                Record = record;
                Report = report;
            }

            public OrderRecord Record { get; set; }

            public byte[]? Report { get; set; }
        }
    }
}
