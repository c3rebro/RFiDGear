using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>An order as the stub store starts out with it: the row and the report stored with it, if any.</summary>
    /// <param name="Record">The row of the order table.</param>
    /// <param name="Report">The report stored with the order, or <c>null</c> if it has none.</param>
    internal sealed record StubOrder(OrderRecord Record, byte[]? Report);

    /// <summary>
    /// The orders the assistant's test mode starts out with. They are made up and say so: ids and job numbers start with
    /// <c>STUB-</c>, so they are never mistaken for an order of the order system, and the numbers are plain enough to
    /// become folder and file names. They stand for the stages an order goes through, newest first, so that a check can
    /// be tried on any of them with nothing but a reader and a card.
    /// </summary>
    internal static class StubOrders
    {
        /// <summary>The text stored as the report of the order that is already finished. It is not a real PDF.</summary>
        internal const string PlaceholderReport = "%PDF-1.4 stub report";

        /// <summary>Makes the starting set. Every call makes a new one, so a store never shares an order with another.</summary>
        /// <returns>The orders, newest first.</returns>
        internal static IReadOnlyList<StubOrder> CreateSeed() => new[]
        {
            Order(1, "2026-02-05", nameof(OrderStatus.Created), "Stub customer A"),
            Order(2, "2026-02-04", nameof(OrderStatus.OrderConfirmed), "Stub customer B"),
            Order(3, "2026-02-03", nameof(OrderStatus.InProgress), "Stub customer A"),
            Order(4, "2026-02-02", nameof(OrderStatus.CheckFinished), "Stub customer C", Encoding.ASCII.GetBytes(PlaceholderReport)),
            Order(5, "2026-02-01", nameof(OrderStatus.Closed), "Stub customer B"),
        };

        private static StubOrder Order(int number, string created, string status, string customer, byte[]? report = null) =>
            new StubOrder(
                new OrderRecord
                {
                    Id = "STUB-" + number,
                    JobNumber = "STUB-" + (1000 + number),
                    ChipNumber = number.ToString(CultureInfo.InvariantCulture),
                    DateCreated = created,
                    DateModified = created,
                    CustomerName = customer,
                    EditorName = "Stub operator",
                    StatusText = status,
                    DealerName = "Stub dealer",
                    SalesName = "Stub sales",
                },
                report);
    }
}
