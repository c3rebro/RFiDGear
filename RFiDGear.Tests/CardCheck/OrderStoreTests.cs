using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Data.Sqlite;

using RFiDGear.ExtensionLibraries.CardCheck;

using Xunit;

namespace RFiDGear.Tests.CardCheck
{
    /// <summary>
    /// The order store against a real SQL engine (SQLite, in the tests only, on the same bracketed column names).
    /// Kept to what must hold whatever the extension looks like: the three writes happen in one transaction, values are
    /// parameters, a table name cannot add a statement, and no failure names the server, the database or the table.
    /// </summary>
    public sealed class OrderStoreTests : IDisposable
    {
        private const string Server = "SRV-SECRET";
        private const string Database = "DB-SECRET";

        private readonly TempFolder _folder = new TempFolder();
        private readonly RecordingCardCheckLog _log = new RecordingCardCheckLog();
        private readonly DateTime _clock = new DateTime(2026, 11, 7, 10, 30, 0);

        public OrderStoreTests()
        {
            Execute(
                "CREATE TABLE T_CardCheck ([CC-ID] TEXT, [CC-JobNumber] TEXT, [CC-CardNumber] TEXT, [CC-CreationDate] TEXT, "
                + "[CC-ChangedDate] TEXT, [CC-Customername] TEXT, [CC-EditorName] TEXT, [CC-Status] TEXT, [CC-DealerName] TEXT, "
                + "[CC-SalesName] TEXT, [CC-ReportBase64] TEXT)");
        }

        public void Dispose() => _folder.Dispose();

        private string Connection => "Data Source=" + Path.Combine(_folder.Path, "orders.db") + ";Pooling=False";

        private sealed class SqliteFactory : IDbConnectionFactory
        {
            private readonly string _connectionString;

            public SqliteFactory(string connectionString)
            {
                _connectionString = connectionString;
            }

            public int Created { get; private set; }

            // The isolation level of every transaction the store began, in order.
            public List<IsolationLevel> Isolation { get; } = new List<IsolationLevel>();

            public DbConnection Create()
            {
                Created++;

                return new RecordingConnection(_connectionString, Isolation.Add);
            }
        }

        private sealed class RecordingConnection : SqliteConnection
        {
            private readonly Action<IsolationLevel> _record;

            public RecordingConnection(string connectionString, Action<IsolationLevel> record)
                : base(connectionString)
            {
                _record = record;
            }

            protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
            {
                _record(isolationLevel);

                return base.BeginDbTransaction(isolationLevel);
            }
        }

        private OrderStore Store(SqliteFactory factory, DatabaseSettings? settings = null) =>
            new OrderStore(settings ?? new DatabaseSettings { Server = Server, Database = Database, Table = "T_CardCheck" }, factory, _log, () => _clock);

        private OrderStore Store() => Store(new SqliteFactory(Connection));

        private void Execute(string sql, params (string Name, object Value)[] parameters)
        {
            using var connection = new SqliteConnection(Connection);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;

            foreach (var (name, value) in parameters)
            {
                command.Parameters.AddWithValue(name, value);
            }

            command.ExecuteNonQuery();
        }

        private string? Value(string id, string column)
        {
            using var connection = new SqliteConnection(Connection);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT [" + column + "] FROM T_CardCheck WHERE [CC-ID] = @id";
            command.Parameters.AddWithValue("@id", id);
            var value = command.ExecuteScalar();

            return value == null || value is DBNull ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private void Insert(string id, string? status = "Created", string created = "2026-01-15", string? report = null)
        {
            Execute(
                "INSERT INTO T_CardCheck VALUES (@id, 'J-' || @id, 'C-' || @id, @created, 'old date', 'Customer', 'Editor', @status, 'Dealer', 'Sales', @report)",
                ("@id", id),
                ("@created", created),
                ("@status", status ?? (object)DBNull.Value),
                ("@report", report ?? (object)DBNull.Value));
        }

        private static byte[] Pdf(string text) => Encoding.ASCII.GetBytes("%PDF-1.4 " + text);

        // ── reading ─────────────────────────────────────────────────────────

        [Fact]
        public async Task List_ReadsEveryColumn_NewestFirst_AndAMissingStatusIsNA()
        {
            Insert("A", status: "InProgress", created: "2026-01-01");
            Insert("B", status: null, created: "2026-03-01");
            Insert("C", status: "Closed", created: "2026-02-01");
            Execute("UPDATE T_CardCheck SET [CC-Customername] = NULL WHERE [CC-ID] = 'B'");

            var orders = (await Store().ListAsync(CancellationToken.None)).Value;

            Assert.Equal(new[] { "B", "C", "A" }, orders.Select(o => o.Id));
            Assert.Equal("NA", orders[0].StatusText);
            Assert.Equal(OrderStatus.NA, orders[0].Status);
            Assert.Equal(string.Empty, orders[0].CustomerName);
            var a = orders[2];
            Assert.Equal(("J-A", "C-A", "old date", "Editor", "Dealer", "Sales"), (a.JobNumber, a.ChipNumber, a.DateModified, a.EditorName, a.DealerName, a.SalesName));
            Assert.Equal(OrderStatus.InProgress, a.Status);
        }

        [Fact]
        public async Task DownloadReport_GivesTheBytes_AndNoReportIsNotFound_NeverAnEmptyFile()
        {
            var report = Pdf("the report");
            Insert("A", report: Convert.ToBase64String(report));
            Insert("B");
            Insert("C", report: string.Empty);
            Insert("D", report: "this is not base64!");
            var store = Store();

            Assert.Equal(report, (await store.DownloadReportAsync("A", CancellationToken.None)).Value);
            Assert.Equal(FailureKind.NotFound, (await store.DownloadReportAsync("B", CancellationToken.None)).Failure!.Kind);
            Assert.Equal(FailureKind.NotFound, (await store.DownloadReportAsync("C", CancellationToken.None)).Failure!.Kind);
            Assert.Equal(FailureKind.NotFound, (await store.DownloadReportAsync("nobody", CancellationToken.None)).Failure!.Kind);
            Assert.Equal(FailureKind.Corrupt, (await store.DownloadReportAsync("D", CancellationToken.None)).Failure!.Kind);
        }

        // ── writing ─────────────────────────────────────────────────────────

        [Fact]
        public async Task Finalize_StoresTheReport_TheStatusAndTheDate_OfThatOrderOnly()
        {
            Insert("A", status: "InProgress");
            Insert("B", status: "InProgress");
            var report = Pdf("final");

            var result = await Store().FinalizeAsync("A", report, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(Convert.ToBase64String(report), Value("A", "CC-ReportBase64"));
            Assert.Equal("CheckFinished", Value("A", "CC-Status"));
            Assert.Equal(_clock.ToString(CultureInfo.CurrentCulture), Value("A", "CC-ChangedDate"));
            Assert.Equal("InProgress", Value("B", "CC-Status"));
            Assert.Null(Value("B", "CC-ReportBase64"));
        }

        [Fact]
        public async Task Finalize_WhenALaterStepFails_NothingIsKept()
        {
            Insert("A", status: "InProgress");
            Execute("CREATE TRIGGER refuse_date BEFORE UPDATE OF [CC-ChangedDate] ON T_CardCheck BEGIN SELECT RAISE(ABORT, 'refused'); END");

            var result = await Store().FinalizeAsync("A", Pdf("final"), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Null(Value("A", "CC-ReportBase64"));
            Assert.Equal("InProgress", Value("A", "CC-Status"));
        }

        [Fact]
        public async Task Writes_ToAnOrderThatIsNotThere_AreNotFound_AndWriteNothing_AnIdIsNeverSql()
        {
            Insert("A", status: "InProgress");
            var store = Store();

            foreach (var id in new[] { "nobody", "' OR '1'='1", "A' --" })
            {
                Assert.Equal(FailureKind.NotFound, (await store.FinalizeAsync(id, Pdf("x"), CancellationToken.None)).Failure!.Kind);
                Assert.Equal(FailureKind.NotFound, (await store.ReplaceReportAsync(id, Pdf("x"), CancellationToken.None)).Failure!.Kind);
                Assert.Equal(FailureKind.NotFound, (await store.TouchChangedDateAsync(id, CancellationToken.None)).Failure!.Kind);
            }

            Assert.Equal("InProgress", Value("A", "CC-Status"));
            Assert.Null(Value("A", "CC-ReportBase64"));
            Assert.Equal("old date", Value("A", "CC-ChangedDate"));
        }

        [Fact]
        public async Task AnEmptyReport_IsRefused_AndWritesNothing()
        {
            Insert("A", status: "InProgress");
            var store = Store();

            Assert.Equal("orders.report.empty", (await store.FinalizeAsync("A", Array.Empty<byte>(), CancellationToken.None)).Failure!.Code);
            Assert.Equal("orders.report.empty", (await store.ReplaceReportAsync("A", Array.Empty<byte>(), CancellationToken.None)).Failure!.Code);
            Assert.Equal("InProgress", Value("A", "CC-Status"));
            Assert.Null(Value("A", "CC-ReportBase64"));
        }

        [Fact]
        public async Task EveryWrite_RunsSerializable_SoTheExistenceCheckHoldsUntilTheCommit()
        {
            Insert("A", status: "InProgress");
            var factory = new SqliteFactory(Connection);
            var store = Store(factory);

            await store.FinalizeAsync("A", Pdf("x"), CancellationToken.None);
            await store.ReplaceReportAsync("A", Pdf("y"), CancellationToken.None);
            await store.TouchChangedDateAsync("A", CancellationToken.None);

            Assert.Equal(3, factory.Isolation.Count);
            Assert.All(factory.Isolation, level => Assert.Equal(IsolationLevel.Serializable, level));
        }

        [Fact]
        public async Task ReplaceReport_ChangesOnlyTheReport_AndTouch_OnlyTheDate()
        {
            Insert("A", status: "InProgress");
            var store = Store();

            Assert.True((await store.ReplaceReportAsync("A", Pdf("edited"), CancellationToken.None)).IsSuccess);
            Assert.Equal(Convert.ToBase64String(Pdf("edited")), Value("A", "CC-ReportBase64"));
            Assert.Equal("old date", Value("A", "CC-ChangedDate"));
            Assert.Equal("InProgress", Value("A", "CC-Status"));

            Assert.True((await store.TouchChangedDateAsync("A", CancellationToken.None)).IsSuccess);
            Assert.Equal(_clock.ToString(CultureInfo.CurrentCulture), Value("A", "CC-ChangedDate"));
            Assert.Equal("InProgress", Value("A", "CC-Status"));
        }

        // ── configuration, failures, secrets ────────────────────────────────

        [Fact]
        public async Task TestConnection_ChecksTheTableAndItsColumns()
        {
            Assert.True((await Store().TestConnectionAsync(CancellationToken.None)).IsSuccess);

            Execute("CREATE TABLE T_NoReport ([CC-ID] TEXT, [CC-JobNumber] TEXT, [CC-CardNumber] TEXT, [CC-CreationDate] TEXT, [CC-ChangedDate] TEXT, "
                + "[CC-Customername] TEXT, [CC-EditorName] TEXT, [CC-Status] TEXT, [CC-DealerName] TEXT, [CC-SalesName] TEXT)");
            var narrow = new DatabaseSettings { Server = Server, Database = Database, Table = "T_NoReport" };

            Assert.False((await Store(new SqliteFactory(Connection), narrow).TestConnectionAsync(CancellationToken.None)).IsSuccess);
        }

        [Fact]
        public async Task NotConfigured_OrAnInvalidTableName_FailsBeforeAnyConnectionIsMade()
        {
            var factory = new SqliteFactory(Connection);

            var empty = await Store(factory, new DatabaseSettings()).ListAsync(CancellationToken.None);
            var invalid = await Store(factory, new DatabaseSettings { Server = Server, Database = Database, Table = "T_CardCheck; DROP TABLE T_CardCheck" }).ListAsync(CancellationToken.None);

            Assert.Equal(FailureKind.NotConfigured, empty.Failure!.Kind);
            Assert.Equal("orders.table.invalid", invalid.Failure!.Code);
            Assert.Equal(0, factory.Created);
        }

        [Fact]
        public async Task ACancelledCall_IsCancelled_WithoutConnecting()
        {
            var factory = new SqliteFactory(Connection);
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();

            var result = await Store(factory).ListAsync(cancelled.Token);

            Assert.Equal(FailureKind.Cancelled, result.Failure!.Kind);
            Assert.Equal(0, factory.Created);
        }

        [Fact]
        public async Task NoFailureNamesTheServerTheDatabaseTheTableOrThePath()
        {
            var unreachable = Store(new SqliteFactory("Data Source=" + Path.Combine(_folder.Path, "no-such-folder", "T_Hidden.db") + ";Mode=ReadOnly;Pooling=False"));
            var missingTable = new DatabaseSettings { Server = Server, Database = Database, Table = "T_Hidden" };
            var failures = new[]
            {
                (await unreachable.ListAsync(CancellationToken.None)).Failure!,
                (await Store(new SqliteFactory(Connection), missingTable).ListAsync(CancellationToken.None)).Failure!,
                (await Store(new SqliteFactory(Connection), missingTable).FinalizeAsync("A", Pdf("x"), CancellationToken.None)).Failure!,
            };

            Assert.Equal(FailureKind.Unreachable, failures[0].Kind);

            foreach (var failure in failures)
            {
                var text = failure.Code + failure.SafeMessage + _log.Everything;
                Assert.DoesNotContain(Server, text, StringComparison.Ordinal);
                Assert.DoesNotContain(Database, text, StringComparison.Ordinal);
                Assert.DoesNotContain("T_Hidden", text, StringComparison.Ordinal);
                Assert.DoesNotContain(_folder.Path, text, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Theory]
        [InlineData(-2, false, FailureKind.Timeout)]
        [InlineData(18456, true, FailureKind.AccessDenied)]
        [InlineData(4060, true, FailureKind.AccessDenied)]
        [InlineData(208, false, FailureKind.InvalidConfiguration)]
        [InlineData(53, true, FailureKind.Unreachable)]
        [InlineData(53, false, FailureKind.Io)]
        public void ServerErrors_AreMappedByTheirNumber(int number, bool opening, FailureKind expected)
        {
            Assert.Equal(expected, OrderStore.FromSqlError(number, opening).Kind);
        }

        // ── the table name and the connection ───────────────────────────────

        [Theory]
        [InlineData("T_CardCheck", "[T_CardCheck]")]
        [InlineData("dbo.T_CardCheck", "[dbo].[T_CardCheck]")]
        [InlineData("  db.dbo.T  ", "[db].[dbo].[T]")]
        [InlineData("[dbo].[T Card Check]", "[dbo].[T Card Check]")]
        [InlineData("[a]]b].c", "[a]]b].[c]")]
        public void Quote_WritesAValidNameWithEveryPartInBrackets(string name, string expected)
        {
            Assert.Equal(expected, SqlIdentifier.Quote(name).Value);
        }

        [Theory]
        [InlineData("")]
        [InlineData("  ")]
        [InlineData("a;b")]
        [InlineData("a b")]
        [InlineData("a'--")]
        [InlineData("1a")]
        [InlineData("a.b.c.d")]
        [InlineData("a..b")]
        [InlineData(".a")]
        [InlineData("a.")]
        [InlineData("[a")]
        [InlineData("[]")]
        [InlineData("a]")]
        [InlineData("[a]b")]
        public void Quote_RefusesAnythingElse(string name)
        {
            Assert.Equal("orders.table.invalid", SqlIdentifier.Quote(name).Failure!.Code);
        }

        [Fact]
        public void Quote_RefusesAControlCharacterInBrackets()
        {
            Assert.False(SqlIdentifier.Quote("[a" + (char)1 + "b]").IsSuccess);
        }

        [Fact]
        public void ConnectionString_UsesTheWindowsAccount_AndNamesTheInstanceOnlyWhenThereIsOne()
        {
            var withInstance = new SqlConnectionStringBuilder(SqlServerConnectionFactory.BuildConnectionString(new DatabaseSettings { Server = "SRV\\INST", Database = "Db" }));
            var plain = SqlServerConnectionFactory.BuildConnectionString(new DatabaseSettings { Server = "SRV", Database = "Db" });
            var trailing = SqlServerConnectionFactory.BuildConnectionString(new DatabaseSettings { Server = "SRV\\", Database = "Db" });

            Assert.Equal(("SRV\\INST", "Db", true, true, "INST"), (withInstance.DataSource, withInstance.InitialCatalog, withInstance.IntegratedSecurity, withInstance.TrustServerCertificate, withInstance.ApplicationName));
            Assert.Equal((2, 10, 30), (withInstance.ConnectRetryCount, withInstance.ConnectRetryInterval, withInstance.ConnectTimeout));
            Assert.DoesNotContain("Application Name", plain, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Application Name", trailing, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Password", plain, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("User ID", plain, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Arguments_AreChecked()
        {
            Assert.Throws<ArgumentNullException>(() => CardCheckServices.CreateOrderStore(null!, _log));
            Assert.Throws<ArgumentNullException>(() => CardCheckServices.CreateOrderStore(new DatabaseSettings(), null!));
            Assert.Throws<ArgumentNullException>(() => { _ = Store().FinalizeAsync("A", null!, CancellationToken.None); });
        }
    }
}
