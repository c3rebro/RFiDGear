using System.Threading;
using System.Threading.Tasks;

using RFiDGear.ExtensionLibraries.CardCheck;

namespace RFiDGear.Extensions.CardCheckAssistant.Services
{
    /// <summary>
    /// What the guided page asks of the world outside of it: a question to the operator, and the viewer for a report.
    /// They are one seam because the page takes them together (its constructor is at the limit of its parameters), and
    /// because both are things a test must be able to answer by hand.
    /// </summary>
    internal interface IGuidedPageServices
    {
        /// <summary>
        /// Asks the operator a yes-or-no question. The page supplies the texts, already in the language of the page. It
        /// owns a message box: call it on the window's thread only.
        /// </summary>
        /// <param name="title">The title of the question.</param>
        /// <param name="text">What answering yes means for the order in its present state.</param>
        /// <returns><c>true</c> if the operator answered yes; the default answer is no.</returns>
        bool Confirm(string title, string text);

        /// <summary>
        /// Opens a report or the folder it is in with the program the operator has set up for it. It reads the disk (a
        /// share that does not answer takes time), so it does that work off the caller's thread and returns at once; the
        /// task ends when the shell has taken the request.
        /// </summary>
        /// <param name="path">The full path of a PDF file or a folder.</param>
        /// <param name="ct">Cancels the wait for the result (the request may have been handed to the shell all the same).</param>
        /// <returns><c>null</c> if it was opened, otherwise why not. The task does not fail for a path that cannot be opened.</returns>
        Task<Failure?> OpenReportAsync(string path, CancellationToken ct);
    }
}
