using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RFiDGear.Extensions.CardCheckAssistant.Services
{
    /// <summary>One job the page offers to pick, as plain values of the extension's own.</summary>
    /// <param name="Id">The key of the job in its source.</param>
    /// <param name="JobNumber">The job number; becomes the value of <c>$JOBNUMBER</c>.</param>
    /// <param name="ChipNumber">The chip number; becomes the value of <c>$CHIPNUMBER</c>.</param>
    /// <param name="Customer">The customer the job is for.</param>
    /// <param name="Status">The status of the job as its source keeps it.</param>
    internal sealed record JobEntry(string Id, string JobNumber, string ChipNumber, string Customer, string Status);

    /// <summary>
    /// Where the jobs the page offers come from. A seam of the extension's own (no library type in it), so the view model
    /// is tested without a store. For now the only source is the made-up jobs of the test mode; the order queue follows
    /// behind the same seam.
    /// </summary>
    internal interface IJobSource
    {
        /// <summary>Lists the jobs. It may take time, so call it off the UI thread; it does not throw.</summary>
        /// <param name="cancellationToken">Cancels the call.</param>
        /// <returns>The jobs, newest first; none if they cannot be listed.</returns>
        Task<IReadOnlyList<JobEntry>> ListAsync(CancellationToken cancellationToken);
    }

    /// <summary>
    /// Where the report of a test run goes: a folder of its own, apart from the reports of real jobs. A seam of the
    /// extension's own, like <see cref="IJobSource"/>.
    /// </summary>
    internal interface ITestReportTarget
    {
        /// <summary>
        /// Makes the folder for the report of a job and says where the report goes. It touches the disk, so call it off the
        /// UI thread; it does not throw because of a folder.
        /// </summary>
        /// <param name="job">The job the report is for.</param>
        /// <returns>The full path of the report file; empty if the folder could not be made or the job's numbers cannot be file names.</returns>
        string Prepare(JobEntry job);
    }
}
