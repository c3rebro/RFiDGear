using System;
using System.Linq;

using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Extensions.CardCheckAssistant.Workflow;

namespace RFiDGear.Extensions.CardCheckAssistant.ViewModels
{
    /// <summary>One order of the list, as the page shows it.</summary>
    internal sealed class OrderRowViewModel
    {
        /// <summary>Creates the row.</summary>
        /// <param name="order">The order.</param>
        /// <exception cref="ArgumentNullException"><paramref name="order"/> is null.</exception>
        public OrderRowViewModel(OrderRecord order)
        {
            Order = order ?? throw new ArgumentNullException(nameof(order));
            IsStartable = CardCheckWorkflow.IsStartable(order);
        }

        /// <summary>Gets the order this row shows.</summary>
        public OrderRecord Order { get; }

        /// <summary>Gets the id of the order, which the workflow is started with.</summary>
        public string Id => Order.Id;

        /// <summary>Gets the job number.</summary>
        public string JobNumber => Order.JobNumber;

        /// <summary>Gets the chip number.</summary>
        public string ChipNumber => Order.ChipNumber;

        /// <summary>Gets the customer.</summary>
        public string Customer => Order.CustomerName;

        /// <summary>Gets the status as the order system writes it.</summary>
        public string Status => Order.StatusText;

        /// <summary>Gets the date the order was created, as the operator reads it.</summary>
        public string Created => OrderDates.Show(Order.DateCreated);

        /// <summary>Gets the date the order was last changed, as the operator reads it.</summary>
        public string Modified => OrderDates.Show(Order.DateModified);

        /// <summary>Gets the group of the order when the list is grouped by status.</summary>
        public string StatusGroup => OrderDates.NamedOrDash(Order.StatusText);

        /// <summary>Gets the group of the order when the list is grouped by customer.</summary>
        public string CustomerGroup => OrderDates.NamedOrDash(Order.CustomerName);

        /// <summary>Gets the group of the order when the list is grouped by the month of the last change (<c>2026-11</c>).</summary>
        public string MonthGroup => OrderDates.NamedOrDash(OrderDates.Month(Order.DateModified));

        /// <summary>Tells whether the order answers a search: every word is part of the job, chip, customer or status.</summary>
        /// <param name="search">What the operator typed; blank matches every order.</param>
        /// <returns><c>true</c> if the order is to be shown.</returns>
        public bool Matches(string? search)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                return true;
            }

            var haystack = JobNumber + " " + ChipNumber + " " + Customer + " " + Status;
            return search.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .All(word => haystack.IndexOf(word, StringComparison.CurrentCultureIgnoreCase) >= 0);
        }

        /// <summary>Gets a value indicating whether the order can be checked: only an order in progress can.</summary>
        public bool IsStartable { get; }

        /// <summary>The row as one line, which is also what a screen reader announces for the item.</summary>
        /// <returns>Job number, chip number, customer and status, without the ones that are empty.</returns>
        public override string ToString() =>
            string.Join(", ", new[] { JobNumber, ChipNumber, Customer, Status }.Where(part => !string.IsNullOrWhiteSpace(part)));
    }
}
