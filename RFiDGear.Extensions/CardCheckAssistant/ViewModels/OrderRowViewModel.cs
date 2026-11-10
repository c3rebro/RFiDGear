using System;

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

        /// <summary>Gets a value indicating whether the order can be checked: only an order in progress can.</summary>
        public bool IsStartable { get; }
    }
}
