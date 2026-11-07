namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>
    /// The statements the store sends, written once. The table name comes in already quoted by
    /// <see cref="SqlIdentifier"/>; the column names are fixed words of the order system's table, and every value
    /// (the order key, the report, the status, the date) is a parameter, never part of the text.
    /// </summary>
    internal sealed class OrderSql
    {
        private const string Id = "[CC-ID]";
        private const string Report = "[CC-ReportBase64]";
        private const string Status = "[CC-Status]";
        private const string Changed = "[CC-ChangedDate]";

        // The columns of an order, in the order the store reads them.
        private const string Columns =
            "[CC-ID], [CC-JobNumber], [CC-CardNumber], [CC-CreationDate], [CC-ChangedDate], "
            + "[CC-Customername], [CC-EditorName], [CC-Status], [CC-DealerName], [CC-SalesName]";

        /// <summary>Creates the statements for a table.</summary>
        /// <param name="quotedTable">The table name as returned by <see cref="SqlIdentifier.Quote"/>.</param>
        /// <param name="sqlServer">
        /// Whether the statements go to SQL Server. The existence check of a write then takes an update lock and keeps
        /// it (<c>UPDLOCK, HOLDLOCK</c>): with only a shared lock, a writer of the order system and this transaction
        /// each hold a lock the other needs to upgrade, and one of them is chosen as the victim of a deadlock. With the
        /// update lock the other writer waits until this transaction is over.
        /// </param>
        public OrderSql(string quotedTable, bool sqlServer)
        {
            SelectAll = "SELECT " + Columns + " FROM " + quotedTable;
            Probe = "SELECT " + Columns + ", " + Report + " FROM " + quotedTable + " WHERE 1 = 0";
            SelectReport = "SELECT " + Report + " FROM " + quotedTable + " WHERE " + Id + " = @id";
            Exists = "SELECT 1 FROM " + quotedTable + (sqlServer ? " WITH (UPDLOCK, HOLDLOCK)" : string.Empty) + " WHERE " + Id + " = @id";
            UpdateReport = "UPDATE " + quotedTable + " SET " + Report + " = @data WHERE " + Id + " = @id";
            UpdateStatus = "UPDATE " + quotedTable + " SET " + Status + " = @status WHERE " + Id + " = @id";
            UpdateChangedDate = "UPDATE " + quotedTable + " SET " + Changed + " = @changed WHERE " + Id + " = @id";
        }

        /// <summary>Reads every order (ten columns, not the report).</summary>
        public string SelectAll { get; }

        /// <summary>Selects no row but names every column the library uses, to check the table.</summary>
        public string Probe { get; }

        /// <summary>Reads the report of one order (<c>@id</c>).</summary>
        public string SelectReport { get; }

        /// <summary>Finds one order (<c>@id</c>).</summary>
        public string Exists { get; }

        /// <summary>Stores the report (<c>@data</c>) of one order (<c>@id</c>).</summary>
        public string UpdateReport { get; }

        /// <summary>Stores the status (<c>@status</c>) of one order (<c>@id</c>).</summary>
        public string UpdateStatus { get; }

        /// <summary>Stores the date of the change (<c>@changed</c>) of one order (<c>@id</c>).</summary>
        public string UpdateChangedDate { get; }
    }
}
