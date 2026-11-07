using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>The SQL implementation of <see cref="IOrderStore"/>.</summary>
    internal sealed class OrderStore : IOrderStore
    {
        private readonly DatabaseSettings _settings;
        private readonly IDbConnectionFactory _factory;
        private readonly ICardCheckLog _log;
        private readonly Func<DateTime> _now;

        /// <summary>Creates the store.</summary>
        /// <param name="settings">Where the order database and its table are.</param>
        /// <param name="factory">Makes the connections.</param>
        /// <param name="log">Where an unexpected fault is noted (by type only).</param>
        /// <param name="now">The clock for the date of a change; the local time unless a test sets another.</param>
        public OrderStore(DatabaseSettings settings, IDbConnectionFactory factory, ICardCheckLog log, Func<DateTime>? now = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _now = now ?? (() => DateTime.Now);
        }

        /// <inheritdoc />
        public Task<Result<IReadOnlyList<OrderRecord>>> ListAsync(CancellationToken cancellationToken) =>
            RunAsync<IReadOnlyList<OrderRecord>>("orders.list", cancellationToken, async (connection, sql) =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = sql.SelectAll;
                using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                var orders = new List<OrderRecord>();

                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    orders.Add(new OrderRecord
                    {
                        Id = Text(reader, 0, string.Empty),
                        JobNumber = Text(reader, 1, string.Empty),
                        ChipNumber = Text(reader, 2, string.Empty),
                        DateCreated = Text(reader, 3, string.Empty),
                        DateModified = Text(reader, 4, string.Empty),
                        CustomerName = Text(reader, 5, string.Empty),
                        EditorName = Text(reader, 6, string.Empty),
                        StatusText = Text(reader, 7, nameof(OrderStatus.NA)),
                        DealerName = Text(reader, 8, string.Empty),
                        SalesName = Text(reader, 9, string.Empty),
                    });
                }

                // Newest first, by the creation date as text in the current culture's order, as the old assistant did.
                return Result<IReadOnlyList<OrderRecord>>.Ok(orders.OrderByDescending(order => order.DateCreated, StringComparer.CurrentCulture).ToList());
            });

        /// <inheritdoc />
        public Task<Result<byte[]>> DownloadReportAsync(string orderId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                return Task.FromResult(Result<byte[]>.Fail(MissingId()));
            }

            return RunAsync<byte[]>("orders.download", cancellationToken, async (connection, sql) =>
            {
                using var command = Command(connection, null, sql.SelectReport, orderId);

                // Sequential access lets the driver stream a large text instead of buffering the whole row.
                using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);

                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false) || await reader.IsDBNullAsync(0, cancellationToken).ConfigureAwait(false))
                {
                    return Result<byte[]>.Fail(NoReport());
                }

                var text = reader.GetString(0);

                if (string.IsNullOrWhiteSpace(text))
                {
                    return Result<byte[]>.Fail(NoReport());
                }

                try
                {
                    return Result<byte[]>.Ok(Convert.FromBase64String(text));
                }
                catch (FormatException)
                {
                    return Result<byte[]>.Fail(new Failure(FailureKind.Corrupt, "orders.report.corrupt", "The report stored with the order is not valid."));
                }
            });
        }

        /// <inheritdoc />
        public Task<Result> ReplaceReportAsync(string orderId, byte[] report, CancellationToken cancellationToken)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            if (report.Length == 0)
            {
                return Task.FromResult(Result.Fail(EmptyReport()));
            }

            return WriteAsync("orders.replace", orderId, cancellationToken, sql => new[] { Update.ForReport(sql, report) });
        }

        /// <inheritdoc />
        public Task<Result> FinalizeAsync(string orderId, byte[] report, CancellationToken cancellationToken)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            // An empty report would be stored and then read back as "no report", on an order marked finished.
            if (report.Length == 0)
            {
                return Task.FromResult(Result.Fail(EmptyReport()));
            }

            // The report first, then the status: an order is never marked finished while it has no report.
            return WriteAsync("orders.finalize", orderId, cancellationToken, sql => new[]
            {
                Update.ForReport(sql, report),
                new Update(sql.UpdateStatus, "@status", OrderStatusText.CheckFinished, large: false),
                Update.ForChangeDate(sql, _now()),
            });
        }

        /// <inheritdoc />
        public Task<Result> TouchChangedDateAsync(string orderId, CancellationToken cancellationToken) =>
            WriteAsync("orders.touch", orderId, cancellationToken, sql => new[] { Update.ForChangeDate(sql, _now()) });

        /// <inheritdoc />
        public async Task<Result> TestConnectionAsync(CancellationToken cancellationToken)
        {
            var probed = await RunAsync<bool>("orders.test", cancellationToken, async (connection, sql) =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = sql.Probe;
                using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

                return Result<bool>.Ok(true);
            }).ConfigureAwait(false);

            return probed.IsSuccess ? Result.Ok() : Result.Fail(probed.Failure!);
        }

        /// <summary>What a failed SQL Server call means, by the server's error number.</summary>
        /// <param name="number">The error number of the SQL Server exception.</param>
        /// <param name="opening">Whether the connection was still being opened.</param>
        /// <returns>The failure.</returns>
        internal static Failure FromSqlError(int number, bool opening)
        {
            switch (number)
            {
                case -2:
                    return new Failure(FailureKind.Timeout, "orders.timeout", "The order database did not answer in time.");
                case 229:
                case 230:
                case 262:
                case 297:
                case 300:
                case 916:
                case 4060:
                case 18452:
                case 18456:
                    return new Failure(FailureKind.AccessDenied, "orders.denied", "The order database refused the access of this Windows account.");
                case 207:
                case 208:
                    return new Failure(FailureKind.InvalidConfiguration, "orders.schema", "The order table or one of its columns was not found.");
                default:
                    return opening
                        ? new Failure(FailureKind.Unreachable, "orders.unreachable", "The order database cannot be reached.")
                        : new Failure(FailureKind.Io, "orders.failed", "The order database could not complete the call.");
            }
        }

        // Text of a column, whatever type the column has; empty or the given fallback if it holds no value.
        private static string Text(DbDataReader reader, int column, string fallback) =>
            reader.IsDBNull(column) ? fallback : Convert.ToString(reader.GetValue(column), CultureInfo.CurrentCulture) ?? fallback;

        private static Failure MissingId() =>
            new Failure(FailureKind.InvalidInput, "orders.id.missing", "No order was given.");

        private static Failure EmptyReport() =>
            new Failure(FailureKind.InvalidInput, "orders.report.empty", "A report has no content.");

        private static Failure NoOrder() =>
            new Failure(FailureKind.NotFound, "orders.notFound", "There is no such order.");

        private static Failure NoReport() =>
            new Failure(FailureKind.NotFound, "orders.report.notFound", "The order has no stored report.");

        private static DbCommand Command(DbConnection connection, DbTransaction? transaction, string text, string orderId)
        {
            var command = connection.CreateCommand();
            command.CommandText = text;
            command.Transaction = transaction;
            AddParameter(command, "@id", orderId, large: false);

            return command;
        }

        private static void AddParameter(DbCommand command, string name, string value, bool large)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.DbType = DbType.String;
            parameter.Value = value;

            if (large)
            {
                // -1 is "as long as it needs to be" (nvarchar(max)); a report is far longer than a normal column.
                parameter.Size = -1;
            }

            command.Parameters.Add(parameter);
        }

        private Result<OrderSql> Prepare()
        {
            if (string.IsNullOrWhiteSpace(_settings.Server) || string.IsNullOrWhiteSpace(_settings.Database))
            {
                return Result<OrderSql>.Fail(new Failure(FailureKind.NotConfigured, "orders.notConfigured", "The order database is not set up yet."));
            }

            var table = SqlIdentifier.Quote(_settings.Table);

            return table.IsSuccess ? Result<OrderSql>.Ok(new OrderSql(table.Value, _factory.UsesSqlServerDialect)) : Result<OrderSql>.Fail(table.Failure!);
        }

        // Opens a connection of its own, runs the work and turns whatever goes wrong into a failure.
        private async Task<Result<T>> RunAsync<T>(string operation, CancellationToken cancellationToken, Func<DbConnection, OrderSql, Task<Result<T>>> work)
        {
            var prepared = Prepare();

            if (!prepared.IsSuccess)
            {
                return Result<T>.Fail(prepared.Failure!);
            }

            var opening = true;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var connection = _factory.Create();
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                opening = false;

                return await work(connection, prepared.Value).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException || cancellationToken.IsCancellationRequested)
            {
                return Result<T>.Fail(new Failure(FailureKind.Cancelled, "orders.cancelled", "The call was cancelled."));
            }
            catch (Exception ex)
            {
                return Result<T>.Fail(Classify(ex, operation, opening));
            }
        }

        // One transaction: the order has to exist, then each update in turn; nothing is kept unless all of them worked.
        private async Task<Result> WriteAsync(string operation, string orderId, CancellationToken cancellationToken, Func<OrderSql, Update[]> updates)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                return Result.Fail(MissingId());
            }

            var written = await RunAsync<bool>(operation, cancellationToken, async (connection, sql) =>
            {
                // Serializable, so the lock of the existence check below is kept until the commit: at the default
                // (read committed) the order system could delete the row between the check and the first update, and
                // every update would then change nothing and still "succeed". On SQL Server the check also takes an
                // update lock (see OrderSql), so a writer of the order system queues behind this transaction instead
                // of deadlocking with the shared lock being upgraded. The transaction is short.
                // Disposing it without committing rolls it back, on every path out of here.
                await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);

                // Asked for with a query of its own: the number of rows an UPDATE reports is not reliable on a
                // table that has triggers.
                using (var exists = Command(connection, transaction, sql.Exists, orderId))
                {
                    if (await exists.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) == null)
                    {
                        return Result<bool>.Fail(NoOrder());
                    }
                }

                foreach (var update in updates(sql))
                {
                    using var command = Command(connection, transaction, update.Statement, orderId);
                    AddParameter(command, update.Parameter, update.Value, update.Large);
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

                return Result<bool>.Ok(true);
            }).ConfigureAwait(false);

            return written.IsSuccess ? Result.Ok() : Result.Fail(written.Failure!);
        }

        private Failure Classify(Exception ex, string operation, bool opening)
        {
            switch (ex)
            {
                // The driver is a file beside the extension; if it is not there the first use of it throws one of
                // these. That must be a failure like any other, so nothing in this method may need the driver itself.
                case FileNotFoundException _:
                case FileLoadException _:
                case BadImageFormatException _:
                case DllNotFoundException _:
                    return new Failure(FailureKind.InvalidConfiguration, "orders.driver.missing", "The database driver is missing or does not fit this version.");
                case TimeoutException _:
                    return FromSqlError(-2, opening);
                case ArgumentException _ when opening:
                    return new Failure(FailureKind.InvalidConfiguration, "orders.connection.invalid", "The server or database name cannot be used.");
                case DbException db:
                    return FromDatabaseError(db, opening);
                default:
                    _log.Unexpected(operation, UnexpectedFault.From(ex));

                    return new Failure(FailureKind.Unexpected, operation + ".unexpected", "The order database call failed unexpectedly.");
            }
        }

        // The only place that names the driver's exception type, in a method of its own that is never inlined: it is
        // compiled only when a database exception has actually been thrown, which means the driver is loaded.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Failure FromDatabaseError(DbException ex, bool opening) =>
            ex is SqlException sql ? FromSqlError(sql.Number, opening) : FromSqlError(0, opening);

        // One UPDATE of a column of the order, with the value it sets.
        private sealed class Update
        {
            public Update(string statement, string parameter, string value, bool large)
            {
                Statement = statement;
                Parameter = parameter;
                Value = value;
                Large = large;
            }

            public string Statement { get; }

            public string Parameter { get; }

            public string Value { get; }

            public bool Large { get; }

            public static Update ForReport(OrderSql sql, byte[] content) =>
                new Update(sql.UpdateReport, "@data", Convert.ToBase64String(content), large: true);

            public static Update ForChangeDate(OrderSql sql, DateTime when) =>
                new Update(sql.UpdateChangedDate, "@changed", when.ToString(CultureInfo.CurrentCulture), large: false);
        }
    }
}
