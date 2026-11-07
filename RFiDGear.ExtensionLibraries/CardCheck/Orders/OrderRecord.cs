namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>
    /// One row of the order table, as the order system keeps it. Every value is text and none is <c>null</c>: a
    /// missing value is empty, and a missing status is <c>NA</c>. The dates are shown as stored; this library does not
    /// interpret them.
    /// </summary>
    public sealed record OrderRecord
    {
        /// <summary>The key of the order (<c>CC-ID</c>).</summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>The job number (<c>CC-JobNumber</c>).</summary>
        public string JobNumber { get; init; } = string.Empty;

        /// <summary>The number of the card or chip order (<c>CC-CardNumber</c>).</summary>
        public string ChipNumber { get; init; } = string.Empty;

        /// <summary>When the order was created (<c>CC-CreationDate</c>).</summary>
        public string DateCreated { get; init; } = string.Empty;

        /// <summary>When the order was last changed (<c>CC-ChangedDate</c>).</summary>
        public string DateModified { get; init; } = string.Empty;

        /// <summary>The customer (<c>CC-Customername</c>).</summary>
        public string CustomerName { get; init; } = string.Empty;

        /// <summary>Who edits the order (<c>CC-EditorName</c>).</summary>
        public string EditorName { get; init; } = string.Empty;

        /// <summary>The status as stored (<c>CC-Status</c>); <c>NA</c> if there is none.</summary>
        public string StatusText { get; init; } = nameof(OrderStatus.NA);

        /// <summary>The dealer (<c>CC-DealerName</c>).</summary>
        public string DealerName { get; init; } = string.Empty;

        /// <summary>The sales person (<c>CC-SalesName</c>).</summary>
        public string SalesName { get; init; } = string.Empty;

        /// <summary>The status as a value; <see cref="OrderStatus.NA"/> for a text the library does not know.</summary>
        public OrderStatus Status => OrderStatusText.Parse(StatusText);
    }
}
