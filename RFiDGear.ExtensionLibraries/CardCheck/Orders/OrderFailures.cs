namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>
    /// The failures an <see cref="IOrderStore"/> answers with for a mistake of the caller. The store on the real order
    /// table and the stand-in for the test mode take them from here, so the same mistake gives the same code and the same
    /// text with either of them.
    /// </summary>
    internal static class OrderFailures
    {
        /// <summary>No order id was given (null, empty or blanks).</summary>
        /// <returns>The failure.</returns>
        internal static Failure MissingId() =>
            new Failure(FailureKind.InvalidInput, "orders.id.missing", "No order was given.");

        /// <summary>A report to store has no content.</summary>
        /// <returns>The failure.</returns>
        internal static Failure EmptyReport() =>
            new Failure(FailureKind.InvalidInput, "orders.report.empty", "A report has no content.");

        /// <summary>There is no order with the id that was given.</summary>
        /// <returns>The failure.</returns>
        internal static Failure NoOrder() =>
            new Failure(FailureKind.NotFound, "orders.notFound", "There is no such order.");

        /// <summary>The order has no stored report (or there is no such order to fetch one from).</summary>
        /// <returns>The failure.</returns>
        internal static Failure NoReport() =>
            new Failure(FailureKind.NotFound, "orders.report.notFound", "The order has no stored report.");

        /// <summary>The caller cancelled the call.</summary>
        /// <returns>The failure.</returns>
        internal static Failure Cancelled() =>
            new Failure(FailureKind.Cancelled, "orders.cancelled", "The call was cancelled.");
    }
}
