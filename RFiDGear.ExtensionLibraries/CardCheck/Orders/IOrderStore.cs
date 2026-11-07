using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>
    /// The order table of the external order system (SQL Server, Windows authentication). The library reads the
    /// orders and writes exactly three things: the report of a check (as base64 text), the status
    /// <c>CheckFinished</c> and the date of the change. Every call opens its own connection and returns a
    /// <see cref="Failure"/> instead of throwing; a failure never names the server, the database or the table.
    /// </summary>
    public interface IOrderStore
    {
        /// <summary>Reads all orders, newest first (the creation date as text, in the current culture's order).</summary>
        /// <param name="cancellationToken">Cancels the call.</param>
        /// <returns>The orders, or why they could not be read.</returns>
        Task<Result<IReadOnlyList<OrderRecord>>> ListAsync(CancellationToken cancellationToken);

        /// <summary>Fetches the report stored with an order.</summary>
        /// <param name="orderId">The key of the order.</param>
        /// <param name="cancellationToken">Cancels the call.</param>
        /// <returns>The report (a PDF); <see cref="FailureKind.NotFound"/> if there is no such order or it has no report.</returns>
        Task<Result<byte[]>> DownloadReportAsync(string orderId, CancellationToken cancellationToken);

        /// <summary>Stores a report with an order and changes nothing else.</summary>
        /// <param name="orderId">The key of the order.</param>
        /// <param name="report">The report (a PDF).</param>
        /// <param name="cancellationToken">Cancels the call.</param>
        /// <returns>Success, or why nothing was written; <see cref="FailureKind.NotFound"/> if there is no such order.</returns>
        Task<Result> ReplaceReportAsync(string orderId, byte[] report, CancellationToken cancellationToken);

        /// <summary>
        /// Finishes a check: stores the report, sets the status <c>CheckFinished</c> and the date of the change, all
        /// in one transaction, so an order is never marked finished without its report.
        /// </summary>
        /// <param name="orderId">The key of the order.</param>
        /// <param name="report">The final report (a PDF).</param>
        /// <param name="cancellationToken">Cancels the call.</param>
        /// <returns>Success, or why nothing was written; <see cref="FailureKind.NotFound"/> if there is no such order.</returns>
        Task<Result> FinalizeAsync(string orderId, byte[] report, CancellationToken cancellationToken);

        /// <summary>Sets the date of the change of an order to now and changes nothing else.</summary>
        /// <param name="orderId">The key of the order.</param>
        /// <param name="cancellationToken">Cancels the call.</param>
        /// <returns>Success, or why nothing was written; <see cref="FailureKind.NotFound"/> if there is no such order.</returns>
        Task<Result> TouchChangedDateAsync(string orderId, CancellationToken cancellationToken);

        /// <summary>Checks that the server can be reached and that the table has the columns the library uses.</summary>
        /// <param name="cancellationToken">Cancels the call.</param>
        /// <returns>Success, or what is wrong, without naming the server, the database or the table.</returns>
        Task<Result> TestConnectionAsync(CancellationToken cancellationToken);
    }
}
