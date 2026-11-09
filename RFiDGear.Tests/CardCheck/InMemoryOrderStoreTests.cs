using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.ExtensionLibraries.CardCheck;

using Xunit;

namespace RFiDGear.Tests.CardCheck
{
    /// <summary>
    /// The stand-in for the order table that the assistant's test mode uses: it answers as the store on the real table
    /// does (the same order of checks and the same failures), so a check tried against it behaves as it would against the
    /// order system, and it never leaves memory.
    /// </summary>
    public sealed class InMemoryOrderStoreTests
    {
        private static readonly DateTime Clock = new DateTime(2026, 11, 9, 8, 15, 0);
        private static readonly string ChangeDate = Clock.ToString(CultureInfo.CurrentCulture);

        private readonly RecordingCardCheckLog _log = new RecordingCardCheckLog();

        [Fact]
        public async Task TheFactory_GivesFiveStubOrders_NewestFirst_ThatSayTheyAreStubs()
        {
            var store = CardCheckServices.CreateStubOrderStore(_log);

            var orders = await ListOf(store);

            Assert.Equal(new[] { "STUB-1", "STUB-2", "STUB-3", "STUB-4", "STUB-5" }, orders.Select(order => order.Id));
            Assert.Equal(
                new[] { OrderStatus.Created, OrderStatus.OrderConfirmed, OrderStatus.InProgress, OrderStatus.CheckFinished, OrderStatus.Closed },
                orders.Select(order => order.Status));
            Assert.Equal(5, orders.Select(order => order.JobNumber).Distinct().Count());
            Assert.All(orders, order =>
            {
                Assert.StartsWith("STUB-", order.JobNumber, StringComparison.Ordinal);
                Assert.False(string.IsNullOrWhiteSpace(order.ChipNumber));
                Assert.False(string.IsNullOrWhiteSpace(order.DateCreated));
                Assert.False(string.IsNullOrWhiteSpace(order.DateModified));
                Assert.False(string.IsNullOrWhiteSpace(order.CustomerName));
                Assert.False(string.IsNullOrWhiteSpace(order.EditorName));
                Assert.False(string.IsNullOrWhiteSpace(order.DealerName));
                Assert.False(string.IsNullOrWhiteSpace(order.SalesName));
            });
        }

        [Fact]
        public async Task TheList_IsNewestFirst_WhateverOrderTheOrdersWereAddedIn()
        {
            var oldestFirst = StubOrders.CreateSeed().Reverse().ToList();

            var orders = await ListOf(new InMemoryOrderStore(oldestFirst));

            Assert.Equal(new[] { "STUB-1", "STUB-2", "STUB-3", "STUB-4", "STUB-5" }, orders.Select(order => order.Id));
        }

        [Fact]
        public async Task AReportWithoutContent_ReadsBackAsNoReport_ThereIsNoSuchThingAsAnEmptyFile()
        {
            var seed = new[] { new StubOrder(new OrderRecord { Id = "EMPTY" }, Array.Empty<byte>()) };

            var report = await new InMemoryOrderStore(seed).DownloadReportAsync("EMPTY", CancellationToken.None);

            AssertFailure(report.Failure, FailureKind.NotFound, "orders.report.notFound");
        }

        [Fact]
        public void TheFactory_NotesTheCreationWithACountOnly_AndNeedsALog()
        {
            CardCheckServices.CreateStubOrderStore(_log);

            var entry = Assert.Single(_log.Entries);
            Assert.StartsWith("I: ", entry, StringComparison.Ordinal);
            Assert.EndsWith("[5]", entry, StringComparison.Ordinal);
            Assert.Throws<ArgumentNullException>(() => CardCheckServices.CreateStubOrderStore(null!));
            Assert.Throws<ArgumentNullException>(() => new InMemoryOrderStore(null!));
        }

        [Fact]
        public async Task TheStubOrders_HaveNumbersThatBecomeFileNamesUnchanged()
        {
            using var folder = new TempFolder();
            var files = CardCheckServices.CreateReportFiles(_log);
            var orders = await ListOf(CardCheckServices.CreateStubOrderStore(_log));

            foreach (var order in orders)
            {
                var paths = files.Resolve(new ReportLocation(folder.Path, CreateSubdirectory: true), order.JobNumber, order.ChipNumber);

                Assert.True(paths.IsSuccess, order.JobNumber);
                Assert.Equal(order.JobNumber, Path.GetFileName(paths.Value.Folder));

                // The chip number is in the file name as it is, and a name that had to be changed would carry a hash.
                var fileName = Path.GetFileName(paths.Value.Final);
                Assert.StartsWith(order.JobNumber, fileName, StringComparison.Ordinal);
                Assert.Contains(order.ChipNumber, fileName, StringComparison.Ordinal);
                Assert.DoesNotContain("~", fileName, StringComparison.Ordinal);
            }
        }

        [Fact]
        public async Task Finalize_StoresTheReport_TheStatusAndTheDate_OfThatOrderOnly()
        {
            var store = Store();
            var before = await ListOf(store);

            var result = await store.FinalizeAsync("STUB-2", Report("finished"), CancellationToken.None);

            Assert.True(result.IsSuccess);
            var after = await ListOf(store);
            Assert.Equal(
                before.Single(order => order.Id == "STUB-2") with { StatusText = "CheckFinished", DateModified = ChangeDate },
                after.Single(order => order.Id == "STUB-2"));
            Assert.Equal(before.Where(order => order.Id != "STUB-2"), after.Where(order => order.Id != "STUB-2"));
            Assert.Equal(Report("finished"), (await store.DownloadReportAsync("STUB-2", CancellationToken.None)).Value);
        }

        [Fact]
        public async Task ReplaceReport_ChangesOnlyTheReport_AndTouch_OnlyTheDate()
        {
            var store = Store();
            var before = await ListOf(store);

            Assert.True((await store.ReplaceReportAsync("STUB-3", Report("replaced"), CancellationToken.None)).IsSuccess);
            Assert.Equal(before, await ListOf(store));
            Assert.Equal(Report("replaced"), (await store.DownloadReportAsync("STUB-3", CancellationToken.None)).Value);

            Assert.True((await store.TouchChangedDateAsync("STUB-3", CancellationToken.None)).IsSuccess);
            var touched = (await ListOf(store)).Single(order => order.Id == "STUB-3");
            Assert.Equal(before.Single(order => order.Id == "STUB-3") with { DateModified = ChangeDate }, touched);
            Assert.Equal(Report("replaced"), (await store.DownloadReportAsync("STUB-3", CancellationToken.None)).Value);
        }

        [Fact]
        public async Task AReport_IsCopied_SoNeitherTheCallerNorTheReaderCanChangeWhatIsStored()
        {
            var store = Store();
            var given = Report("original");
            await store.FinalizeAsync("STUB-1", given, CancellationToken.None);

            given[0] = (byte)'X';
            var fetched = (await store.DownloadReportAsync("STUB-1", CancellationToken.None)).Value;
            fetched[0] = (byte)'Y';

            Assert.Equal(Report("original"), (await store.DownloadReportAsync("STUB-1", CancellationToken.None)).Value);
        }

        [Fact]
        public async Task DownloadReport_AnOrderWithoutOne_AndAnUnknownOrder_AreNotFound_NeverAnEmptyFile()
        {
            var store = Store();

            AssertFailure((await store.DownloadReportAsync("STUB-1", CancellationToken.None)).Failure, FailureKind.NotFound, "orders.report.notFound");
            AssertFailure((await store.DownloadReportAsync("NOPE", CancellationToken.None)).Failure, FailureKind.NotFound, "orders.report.notFound");
            Assert.Equal(StubOrders.PlaceholderReport, Encoding.ASCII.GetString((await store.DownloadReportAsync("STUB-4", CancellationToken.None)).Value));
        }

        [Fact]
        public async Task Writes_ToAnOrderThatIsNotThere_AreNotFound_AndChangeNothing()
        {
            var store = Store();
            var before = await ListOf(store);

            foreach (var write in Writes())
            {
                // The id is matched exactly as given. That is stricter than the default collation of SQL Server, which
                // ignores case; an id taken from the list matches with either.
                AssertFailure((await write(store, "NOPE", Report("x"), CancellationToken.None)).Failure, FailureKind.NotFound, "orders.notFound");
                AssertFailure((await write(store, "stub-1", Report("x"), CancellationToken.None)).Failure, FailureKind.NotFound, "orders.notFound");
            }

            Assert.Equal(before, await ListOf(store));
        }

        [Fact]
        public async Task AnEmptyReport_IsRefusedBeforeTheOrderIsLookedAt_AndANullOneThrows()
        {
            var store = Store();
            var before = await ListOf(store);

            foreach (var id in new[] { "STUB-1", "NOPE", string.Empty })
            {
                AssertFailure((await store.FinalizeAsync(id, Array.Empty<byte>(), CancellationToken.None)).Failure, FailureKind.InvalidInput, "orders.report.empty");
                AssertFailure((await store.ReplaceReportAsync(id, Array.Empty<byte>(), CancellationToken.None)).Failure, FailureKind.InvalidInput, "orders.report.empty");
            }

            Assert.Throws<ArgumentNullException>(() => { _ = store.FinalizeAsync("STUB-1", null!, CancellationToken.None); });
            Assert.Throws<ArgumentNullException>(() => { _ = store.ReplaceReportAsync("STUB-1", null!, CancellationToken.None); });
            Assert.Equal(before, await ListOf(store));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task ABlankId_IsRefused_ByEveryCallThatTakesOne(string? id)
        {
            var store = Store();

            AssertFailure((await store.DownloadReportAsync(id!, CancellationToken.None)).Failure, FailureKind.InvalidInput, "orders.id.missing");

            foreach (var write in Writes())
            {
                AssertFailure((await write(store, id!, Report("x"), CancellationToken.None)).Failure, FailureKind.InvalidInput, "orders.id.missing");
            }
        }

        [Fact]
        public async Task ACancelledCall_IsCancelled_AndChangesNothing()
        {
            var store = Store();
            var before = await ListOf(store);
            using var source = new CancellationTokenSource();
            source.Cancel();

            AssertFailure((await store.ListAsync(source.Token)).Failure, FailureKind.Cancelled, "orders.cancelled");
            AssertFailure((await store.DownloadReportAsync("STUB-4", source.Token)).Failure, FailureKind.Cancelled, "orders.cancelled");
            AssertFailure((await store.TestConnectionAsync(source.Token)).Failure, FailureKind.Cancelled, "orders.cancelled");

            // A blank id and a report without content are refused before the cancellation is looked at, as with the table.
            AssertFailure((await store.DownloadReportAsync(string.Empty, source.Token)).Failure, FailureKind.InvalidInput, "orders.id.missing");
            AssertFailure((await store.FinalizeAsync("STUB-1", Array.Empty<byte>(), source.Token)).Failure, FailureKind.InvalidInput, "orders.report.empty");
            AssertFailure((await store.ReplaceReportAsync("STUB-1", Array.Empty<byte>(), source.Token)).Failure, FailureKind.InvalidInput, "orders.report.empty");

            foreach (var write in Writes())
            {
                // Cancelled is answered before the order is looked at, and after a blank id is refused.
                AssertFailure((await write(store, "STUB-1", Report("x"), source.Token)).Failure, FailureKind.Cancelled, "orders.cancelled");
                AssertFailure((await write(store, "NOPE", Report("x"), source.Token)).Failure, FailureKind.Cancelled, "orders.cancelled");
                AssertFailure((await write(store, string.Empty, Report("x"), source.Token)).Failure, FailureKind.InvalidInput, "orders.id.missing");
            }

            Assert.Equal(before, await ListOf(store));
        }

        [Fact]
        public async Task TestConnection_Succeeds_BecauseThereIsNothingToConnectTo()
        {
            Assert.True((await Store().TestConnectionAsync(CancellationToken.None)).IsSuccess);
        }

        [Fact]
        public async Task TwoStores_ShareNothing()
        {
            var first = CardCheckServices.CreateStubOrderStore(_log);
            var second = CardCheckServices.CreateStubOrderStore(_log);

            await first.FinalizeAsync("STUB-2", Report("done"), CancellationToken.None);

            Assert.Equal(OrderStatus.CheckFinished, (await ListOf(first)).Single(order => order.Id == "STUB-2").Status);
            Assert.Equal(OrderStatus.OrderConfirmed, (await ListOf(second)).Single(order => order.Id == "STUB-2").Status);
            Assert.False((await second.DownloadReportAsync("STUB-2", CancellationToken.None)).IsSuccess);
        }

        // The three calls that write, with the same shape, so the rules they share are checked for each of them.
        private static IEnumerable<Func<IOrderStore, string, byte[], CancellationToken, Task<Result>>> Writes()
        {
            yield return (store, id, report, token) => store.ReplaceReportAsync(id, report, token);
            yield return (store, id, report, token) => store.FinalizeAsync(id, report, token);
            yield return (store, id, _, token) => store.TouchChangedDateAsync(id, token);
        }

        private static InMemoryOrderStore Store() => new InMemoryOrderStore(StubOrders.CreateSeed(), () => Clock);

        private static byte[] Report(string text) => Encoding.ASCII.GetBytes("%PDF-1.4 " + text);

        private static async Task<IReadOnlyList<OrderRecord>> ListOf(IOrderStore store) =>
            (await store.ListAsync(CancellationToken.None)).Value;

        private static void AssertFailure(Failure? failure, FailureKind kind, string code)
        {
            Assert.NotNull(failure);
            Assert.Equal(kind, failure!.Kind);
            Assert.Equal(code, failure.Code);
        }
    }
}
