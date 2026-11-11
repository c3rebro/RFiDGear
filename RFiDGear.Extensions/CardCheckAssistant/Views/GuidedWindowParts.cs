using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Extensions.CardCheckAssistant.Services;

namespace RFiDGear.Extensions.CardCheckAssistant.Views
{
    /// <summary>
    /// The questions of the guided page as message boxes, and the viewer for a report as the shell's: the page's window is
    /// the owner of the question, and the report is opened by the guarded opener on a worker thread.
    /// </summary>
    internal sealed class GuidedPageServices : IGuidedPageServices
    {
        private readonly IReportOpener _opener;

        /// <summary>Creates the services.</summary>
        /// <param name="opener">Opens a PDF file or a folder; nothing else.</param>
        /// <exception cref="ArgumentNullException"><paramref name="opener"/> is null.</exception>
        public GuidedPageServices(IReportOpener opener) => _opener = opener ?? throw new ArgumentNullException(nameof(opener));

        /// <inheritdoc/>
        /// <remarks>The opener reads the disk and cannot be interrupted: a cancel ends the wait, and the request is left to finish by itself.</remarks>
        public Task<Failure?> OpenReportAsync(string path, CancellationToken ct) =>
            Task.Run(
                () =>
                {
                    var opened = _opener.Open(path);

                    return opened.IsSuccess ? null : opened.Failure;
                },
                ct).WaitAsync(ct);

        /// <inheritdoc/>
        public bool Confirm(string title, string text)
        {
            var owner = ImportDialogs.ActiveWindow();
            var answer = owner == null
                ? MessageBox.Show(text, title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No)
                : MessageBox.Show(owner, text, title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);

            return answer == MessageBoxResult.Yes;
        }
    }

    /// <summary>The repeating timer of the page, as a dispatcher timer: its tick runs on the window's thread.</summary>
    internal static class DispatcherTimerStarter
    {
        /// <summary>Makes the function the page starts its timer with.</summary>
        /// <param name="dispatcher">The dispatcher of the window.</param>
        /// <returns>A function that starts a timer and returns what stops it.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="dispatcher"/> is null.</exception>
        public static Func<TimeSpan, Action, IDisposable> For(Dispatcher dispatcher)
        {
            if (dispatcher == null)
            {
                throw new ArgumentNullException(nameof(dispatcher));
            }

            return (interval, tick) => new Running(dispatcher, interval, tick);
        }

        private sealed class Running : IDisposable
        {
            private readonly DispatcherTimer _timer;

            public Running(Dispatcher dispatcher, TimeSpan interval, Action tick)
            {
                // This constructor starts the timer.
                _timer = new DispatcherTimer(interval, DispatcherPriority.Background, (_, _) => tick(), dispatcher);
            }

            public void Dispose() => _timer.Stop();
        }
    }
}
