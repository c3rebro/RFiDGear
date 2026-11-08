using System;
using System.Threading.Tasks;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ReaderProviders;
using Serilog;

namespace RFiDGear.Services
{
    /// <summary>
    /// What a look at the reader changes in the main window.
    /// </summary>
    internal sealed class ReaderProbeResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ReaderProbeResult"/> class.
        /// </summary>
        /// <param name="changesBusy">Whether <paramref name="busy"/> replaces the busy state of the reader.</param>
        /// <param name="busy"><see langword="false"/> ready, <see langword="null"/> unknown.</param>
        /// <param name="label">The text for the status bar.</param>
        public ReaderProbeResult(bool changesBusy, bool? busy, string label)
        {
            ChangesBusy = changesBusy;
            Busy = busy;
            Label = label;
        }

        /// <summary>Gets a value indicating whether <see cref="Busy"/> replaces the busy state of the reader.</summary>
        public bool ChangesBusy { get; }

        /// <summary>Gets the new busy state: <see langword="false"/> when ready, <see langword="null"/> when unknown.</summary>
        public bool? Busy { get; }

        /// <summary>Gets the text for the status bar.</summary>
        public string Label { get; }
    }

    /// <summary>
    /// Looks at the reader once, as the reader monitor does every few seconds, and says what the status bar should
    /// show. It holds no window state, so it can be tested without a main window.
    /// </summary>
    internal sealed class ReaderConnectionProbe
    {
        // The monitor looks every few seconds; the log gets one line per distinct failure, not one per look.
        private string lastFailure;

        /// <summary>
        /// Connects to the reader if it is not connected and describes the result. A reader that cannot be opened (its COM
        /// port is held by another program or another RFiDGear window) is shown as such, not as a reader that is not
        /// attached, and a reader that throws never escapes.
        /// </summary>
        /// <param name="device">The reader; nothing is done when there is none.</param>
        /// <param name="provider">The reader provider that is selected in the settings.</param>
        /// <param name="isReaderBusy">The current busy state; nothing is done while the reader is busy.</param>
        /// <returns>What to show, or <see langword="null"/> when there was nothing to look at.</returns>
        public async Task<ReaderProbeResult> ProbeAsync(ReaderDevice device, ReaderTypes provider, bool? isReaderBusy)
        {
            if (device == null || isReaderBusy == true)
            {
                return null;
            }

            try
            {
                if (device.IsConnected)
                {
                    return new ReaderProbeResult(false, null, DescribeReader(provider, device));
                }

                if (await device.ConnectAsync() == ERROR.NoError)
                {
                    lastFailure = null;

                    return new ReaderProbeResult(true, false, DescribeReader(provider, device));
                }

                // A reason is only a reason while the reader is not open: one that failed after its port was opened is not
                // a port that cannot be reached.
                return new ReaderProbeResult(true, null, DescribeUnavailableReader(provider, !device.IsConnected && device.LastConnectError != null));
            }
            catch (Exception e)
            {
                ReportUnexpectedFailure(e);

                // Only the Elatec reader is reached through a COM port.
                return new ReaderProbeResult(true, null, DescribeUnavailableReader(provider, provider == ReaderTypes.Elatec));
            }
        }

        /// <summary>
        /// Logs a failure that nobody expected while looking at the reader, once per distinct kind of failure.
        /// </summary>
        /// <param name="e">The exception.</param>
        public void ReportUnexpectedFailure(Exception e)
        {
            var reason = e.GetType().Name;

            if (!string.Equals(reason, lastFailure, StringComparison.Ordinal))
            {
                lastFailure = reason;
                Log.ForContext<ReaderConnectionProbe>().Warning(e, "Looking at the reader failed: {Reason}", reason);
            }
        }

        private static string DescribeReader(ReaderTypes provider, ReaderDevice device)
        {
            return provider == ReaderTypes.None
                ? "N/A"
                : provider + " " + device.ReaderUnitName + device.ReaderUnitVersion;
        }

        private static string DescribeUnavailableReader(ReaderTypes provider, bool portNotAccessible)
        {
            if (provider == ReaderTypes.None)
            {
                return "N/A";
            }

            return portNotAccessible
                ? provider + " " + ResourceLoader.GetResource("labelReaderPortNotAccessible")
                : provider.ToString();
        }
    }
}
