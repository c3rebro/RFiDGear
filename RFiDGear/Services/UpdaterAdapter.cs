using System;
using System.Threading;
using System.Threading.Tasks;
using RedCell.Diagnostics.Update;

namespace RFiDGear.Services
{
    /// <summary>
    /// Per-chunk progress snapshot delivered during an update download.
    /// </summary>
    public readonly struct DownloadProgress
    {
        /// <summary>Zero-based index of the file currently being downloaded.</summary>
        public int FileIndex { get; init; }
        /// <summary>Total number of payload files in this update.</summary>
        public int FileCount { get; init; }
        /// <summary>File name (not full path) of the payload being downloaded.</summary>
        public string FileName { get; init; }
        /// <summary>Bytes received so far for the current file.</summary>
        public long BytesReceived { get; init; }
        /// <summary>Total size of the current file, or -1 when unknown.</summary>
        public long TotalBytes { get; init; }

        /// <summary>
        /// Fractional progress (0–1) for the current file, or -1 when size is unknown.
        /// </summary>
        public double FileFraction => TotalBytes > 0 ? (double)BytesReceived / TotalBytes : -1;

        /// <summary>
        /// Overall progress (0–100) across all files, assuming equal file sizes.
        /// </summary>
        public double OverallPercent =>
            FileCount <= 0 ? 0
            : TotalBytes > 0
                ? (FileIndex + FileFraction) / FileCount * 100.0
                : (double)FileIndex / FileCount * 100.0;
    }

    public interface IUpdaterAdapter
    {
        bool UpdateAvailable { get; }
        string UpdateInfoText { get; }
        bool AllowUpdate { get; set; }
        Task StartMonitoringAsync();
        Task StopMonitoringAsync();
        /// <summary>Applies the update without progress reporting.</summary>
        Task ApplyUpdateAsync();
        /// <summary>Applies the update with per-chunk progress and cancellation support.</summary>
        Task ApplyUpdateAsync(IProgress<DownloadProgress> progress, CancellationToken cancellationToken);
    }

    public class UpdaterAdapter : IUpdaterAdapter
    {
        private readonly Updater updater;

        public UpdaterAdapter()
        {
            updater = new Updater();
        }

        public bool UpdateAvailable => updater.UpdateAvailable;
        public string UpdateInfoText => updater.UpdateInfoText;
        public bool AllowUpdate
        {
            get => updater.AllowUpdate;
            set => updater.AllowUpdate = value;
        }

        public Task ApplyUpdateAsync()
        {
            return updater.Update();
        }

        public Task ApplyUpdateAsync(IProgress<DownloadProgress> progress, CancellationToken cancellationToken)
        {
            Action<int, int, string, long, long> onProgress = progress == null
                ? null
                : (fileIndex, fileCount, fileName, bytesReceived, totalBytes) =>
                    progress.Report(new DownloadProgress
                    {
                        FileIndex = fileIndex,
                        FileCount = fileCount,
                        FileName = fileName,
                        BytesReceived = bytesReceived,
                        TotalBytes = totalBytes
                    });

            return updater.Update(onProgress, cancellationToken);
        }

        public Task StartMonitoringAsync()
        {
            return updater.StartMonitoring();
        }

        public Task StopMonitoringAsync()
        {
            return updater.StopMonitoring();
        }
    }
}
