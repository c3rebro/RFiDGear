using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Extensions.CardCheckAssistant.Services;
using RFiDGear.Extensions.CardCheckAssistant.ViewModels;

using Xunit;

namespace RFiDGear.Tests.CardCheckAssistant
{
    /// <summary>What the list of orders remembers (the file) and how it sorts (the comparer); neither needs a window.</summary>
    public sealed class OrderViewStateTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "RFiDGear.Tests", "view-state-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, recursive: true);
            }
        }

        [Fact]
        public void WhatIsSaved_ComesBackAsItWas_AndNothingIsLeftBesideTheFile()
        {
            var store = new FileGuidedViewState(_folder);
            Assert.Null(store.Load().Grouping);   // nothing remembered yet, and no folder either

            store.Save(new GuidedViewStateData { Grouping = "Customer", Sort = "JobNumber", CollapsedGroups = new[] { "Customer:Acme", "Status:Created" } });
            var loaded = new FileGuidedViewState(_folder).Load();

            Assert.Equal("Customer", loaded.Grouping);
            Assert.Equal("JobNumber", loaded.Sort);
            Assert.Equal(new[] { "Customer:Acme", "Status:Created" }, loaded.CollapsedGroups);
            Assert.Equal(new[] { FileGuidedViewState.FileName }, Directory.GetFiles(_folder).Select(Path.GetFileName));
        }

        [Theory]
        [InlineData("this is not json")]
        [InlineData("{\"schemaVersion\": 99, \"grouping\": \"Customer\"}")]   // a version this one does not know
        [InlineData("null")]
        public void AFileThatCannotBeUsed_IsNothingRemembered_NotAnError(string content)
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllText(Path.Combine(_folder, FileGuidedViewState.FileName), content);

            var loaded = new FileGuidedViewState(_folder).Load();

            Assert.Null(loaded.Grouping);
            Assert.Null(loaded.Sort);
            Assert.Empty(loaded.CollapsedGroups);
        }

        [Fact]
        public void TooManyClosedGroups_AreCutAtTheLimit_AndEmptyOrRepeatedOnesAreLeftOut()
        {
            var store = new FileGuidedViewState(_folder);
            var keys = Enumerable.Range(0, FileGuidedViewState.MaxCollapsedGroups + 50).Select(i => "Customer:C" + i).Concat(new[] { "", "  ", "Customer:C0" });

            store.Save(new GuidedViewStateData { CollapsedGroups = keys.ToList() });

            var loaded = store.Load().CollapsedGroups;
            Assert.Equal(FileGuidedViewState.MaxCollapsedGroups, loaded.Count);
            Assert.Equal(loaded.Count, loaded.Distinct().Count());
            Assert.DoesNotContain(loaded, string.IsNullOrWhiteSpace);
        }

        [Fact]
        public void AFolderThatCannotBeWritten_LosesWhatWasToBeSaved_WithoutAnError()
        {
            // The folder name is a file, so nothing can be made in it.
            Directory.CreateDirectory(Path.GetDirectoryName(_folder)!);
            File.WriteAllText(_folder, "in the way");

            var store = new FileGuidedViewState(Path.Combine(_folder, "below"));
            store.Save(new GuidedViewStateData { Grouping = "Customer" });

            Assert.Null(store.Load().Grouping);
            File.Delete(_folder);
        }

        [Fact]
        public void ABlankFolder_IsRefused()
        {
            Assert.Throws<ArgumentException>(() => new FileGuidedViewState(" "));
        }

        // ── the order of the list ──

        private static OrderRowViewModel Row(string id, string customer = "", string status = "InProgress", string? created = null, string? modified = null, string? job = null) =>
            new OrderRowViewModel(new OrderRecord
            {
                Id = id,
                JobNumber = job ?? "J" + id,
                ChipNumber = "1",
                CustomerName = customer,
                StatusText = status,
                DateCreated = created ?? string.Empty,
                DateModified = modified ?? string.Empty,
            });

        private static string[] Sorted(OrderSort sort, OrderGrouping grouping, params OrderRowViewModel[] rows)
        {
            var list = rows.ToList();
            list.Sort(new OrderRowComparer(sort, grouping));
            return list.Select(row => row.Id).ToArray();
        }

        [Fact]
        public void TheNewestOrderComesFirst_AndAnOrderWithoutADateComesLast()
        {
            var rows = new[]
            {
                Row("none"),
                Row("old", created: "2026-01-05 08:00:00", modified: "2026-03-01 08:00:00"),
                Row("new", created: "2026-10-09 08:00:00", modified: "2026-10-09 09:00:00"),
                Row("mid", created: "2026-06-30 08:00:00", modified: "2026-11-01 09:00:00"),
            };

            Assert.Equal(new[] { "new", "mid", "old", "none" }, Sorted(OrderSort.CreatedNewest, OrderGrouping.Status, rows));
            Assert.Equal(new[] { "mid", "new", "old", "none" }, Sorted(OrderSort.ModifiedNewest, OrderGrouping.Status, rows));
        }

        [Fact]
        public void TextsAreSortedWithoutRegardToCase_AndAnEmptyOneComesLast_AndEqualOnesFollowTheirDate()
        {
            var rows = new[]
            {
                Row("1", customer: "zeta", created: "2026-01-01 08:00:00"),
                Row("2", customer: ""),
                Row("3", customer: "Acme", created: "2026-01-01 08:00:00"),
                Row("4", customer: "acme", created: "2026-05-01 08:00:00"),
            };

            // "acme" and "Acme" are the same customer: the newer of the two is first. The one without a customer is last.
            Assert.Equal(new[] { "4", "3", "1", "2" }, Sorted(OrderSort.Customer, OrderGrouping.Customer, rows));
            Assert.Equal(new[] { "1", "2", "3", "4" }, Sorted(OrderSort.JobNumber, OrderGrouping.Status, rows));
        }

        [Fact]
        public void TheMonthsKeepTheirOwnOrder_WhateverTheSortIs()
        {
            var rows = new[]
            {
                Row("oct", customer: "Acme", modified: "2026-10-20 08:00:00"),
                Row("nov", customer: "Zeta", modified: "2026-11-02 08:00:00"),
                Row("none", customer: "Beta"),
            };

            // Grouped by month, the newest month is first and the orders without a date are last, whatever the customer is.
            Assert.Equal(new[] { "nov", "oct", "none" }, Sorted(OrderSort.Customer, OrderGrouping.Month, rows));

            // Not grouped by month, the sort alone decides.
            Assert.Equal(new[] { "oct", "none", "nov" }, Sorted(OrderSort.Customer, OrderGrouping.Status, rows));
        }

        [Fact]
        public void TheSameRowsComeOutInTheSameOrder_WhateverOrderTheyWentIn()
        {
            var rows = new[] { Row("a", job: "J1"), Row("b", job: "J1"), Row("c", job: "J1") };

            var forward = Sorted(OrderSort.Status, OrderGrouping.None, rows);
            var backward = Sorted(OrderSort.Status, OrderGrouping.None, rows.Reverse().ToArray());

            Assert.Equal(forward, backward);
        }
    }
}
