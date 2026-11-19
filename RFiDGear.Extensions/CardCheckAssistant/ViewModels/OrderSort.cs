using System;
using System.Collections;
using System.Collections.Generic;

using RFiDGear.Extensions.CardCheckAssistant.Localization;
using RFiDGear.Extensions.CardCheckAssistant.Mvvm;

namespace RFiDGear.Extensions.CardCheckAssistant.ViewModels
{
    /// <summary>What the list of orders is sorted by, inside each group.</summary>
    internal enum OrderSort
    {
        /// <summary>By the date the order was created, the newest first. This is the order the order system hands the list over in.</summary>
        CreatedNewest,

        /// <summary>By the date the order was last changed, the newest first.</summary>
        ModifiedNewest,

        /// <summary>By the job number.</summary>
        JobNumber,

        /// <summary>By the customer.</summary>
        Customer,

        /// <summary>By the status of the order.</summary>
        Status
    }

    /// <summary>One way to sort the list of orders, with its name in the language of the page.</summary>
    internal sealed class OrderSortOption : ObservableBase
    {
        private readonly string _key;
        private readonly LocalizedStrings _strings;

        /// <summary>Creates the option.</summary>
        /// <param name="sort">What it sorts by.</param>
        /// <param name="key">The text that names it.</param>
        /// <param name="strings">The page's texts.</param>
        public OrderSortOption(OrderSort sort, string key, LocalizedStrings strings)
        {
            Sort = sort;
            _key = key;
            _strings = strings;
        }

        /// <summary>Gets what the option sorts by.</summary>
        public OrderSort Sort { get; }

        /// <summary>Gets the name of the option, in the language of the page.</summary>
        public string Name => _strings[_key];

        /// <summary>Tells the page that the name changed because the language of the page did.</summary>
        public void Refresh() => OnPropertyChanged(nameof(Name));
    }

    /// <summary>
    /// Puts the orders of the list in the order the operator chose. It has no part of WPF in it, so the order can be tested
    /// as it is. A date that cannot be read, and a text that is empty, come last; whatever two orders have in common, the
    /// newer one comes first, then the job number, then the id, so the order of the list never depends on the sort that ran.
    /// </summary>
    internal sealed class OrderRowComparer : IComparer<OrderRowViewModel>, IComparer
    {
        private readonly OrderSort _sort;
        private readonly OrderGrouping _grouping;

        /// <summary>Creates the comparer.</summary>
        /// <param name="sort">What the orders are sorted by.</param>
        /// <param name="grouping">What the list is grouped by: months keep their own order (the newest first) whatever the sort is, so the groups do not jump around.</param>
        public OrderRowComparer(OrderSort sort, OrderGrouping grouping)
        {
            _sort = sort;
            _grouping = grouping;
        }

        /// <inheritdoc/>
        public int Compare(OrderRowViewModel? x, OrderRowViewModel? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x == null)
            {
                return -1;
            }

            if (y == null)
            {
                return 1;
            }

            // "2026-11" is bigger than "2026-10", and the dash of an order with no date is smaller than both: the newest month first, no date last.
            var result = _grouping == OrderGrouping.Month ? string.CompareOrdinal(y.MonthGroup, x.MonthGroup) : 0;
            if (result != 0)
            {
                return result;
            }

            result = _sort switch
            {
                OrderSort.CreatedNewest => NewestFirst(x.CreatedAt, y.CreatedAt),
                OrderSort.ModifiedNewest => NewestFirst(x.ModifiedAt, y.ModifiedAt),
                OrderSort.JobNumber => Text(x.JobNumber, y.JobNumber),
                OrderSort.Customer => Text(x.Customer, y.Customer),
                OrderSort.Status => Text(x.Status, y.Status),
                _ => 0
            };

            if (result != 0)
            {
                return result;
            }

            result = NewestFirst(x.CreatedAt, y.CreatedAt);
            if (result != 0)
            {
                return result;
            }

            result = Text(x.JobNumber, y.JobNumber);
            return result != 0 ? result : string.CompareOrdinal(x.Id, y.Id);
        }

        /// <inheritdoc/>
        int IComparer.Compare(object? x, object? y) => Compare(x as OrderRowViewModel, y as OrderRowViewModel);

        private static int NewestFirst(DateTime? x, DateTime? y)
        {
            if (x == null || y == null)
            {
                return x == y ? 0 : x == null ? 1 : -1;
            }

            return y.Value.CompareTo(x.Value);
        }

        private static int Text(string? x, string? y)
        {
            var xBlank = string.IsNullOrWhiteSpace(x);
            var yBlank = string.IsNullOrWhiteSpace(y);
            if (xBlank || yBlank)
            {
                return xBlank == yBlank ? 0 : xBlank ? 1 : -1;
            }

            return string.Compare(x, y, StringComparison.CurrentCultureIgnoreCase);
        }
    }
}
