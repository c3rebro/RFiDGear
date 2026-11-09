#nullable enable
using RFiDGear.Contracts;
using RFiDGear.Infrastructure.ReaderProviders;

using Serilog;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RFiDGear.Infrastructure.ExtensionHost
{
    /// <summary>
    /// <see cref="IReaderHost"/> implementation that reads from <see cref="ReaderDevice.Instance"/>.
    /// </summary>
    internal sealed class ReaderHostImpl : IReaderHost
    {
        private static ILogger Logger => Log.ForContext<ReaderHostImpl>();

        private readonly Func<string?> _getReaderName;
        private readonly Func<ReaderSnapshot?>? _getSnapshot;

        /// <param name="readerName">Display name of the configured reader (from settings).</param>
        public ReaderHostImpl(string readerName)
            : this(() => readerName)
        {
        }

        /// <param name="getReaderName">
        /// Returns the display name of the configured reader. Called each time <see cref="ReaderName"/> is read, so the
        /// name follows the reader the user selects while the application runs.
        /// </param>
        /// <param name="getSnapshot">
        /// Returns the snapshot of the reader and the card. Called each time <see cref="CurrentSnapshot"/> is read. When
        /// omitted the snapshot is built from <see cref="ReaderDevice.Instance"/>; a delegate that returns <c>null</c>
        /// gives a snapshot without a reader.
        /// </param>
        public ReaderHostImpl(Func<string?> getReaderName, Func<ReaderSnapshot?>? getSnapshot = null)
        {
            _getReaderName = getReaderName ?? throw new ArgumentNullException(nameof(getReaderName));
            _getSnapshot = getSnapshot;
        }

        /// <inheritdoc/>
        public string? ReaderName => _getReaderName();

        /// <inheritdoc/>
        public string? CurrentUID => ReaderDevice.Instance?.GenericChip?.UID;

        /// <inheritdoc/>
        public string? ChipTypeName => ReaderDevice.Instance?.GenericChip?.CardType.ToString();

        /// <inheritdoc/>
        public bool IsConnected => ReaderDevice.Instance?.IsConnected ?? false;

        /// <inheritdoc/>
        public ReaderSnapshot CurrentSnapshot
        {
            get
            {
                if (_getSnapshot != null)
                {
                    return _getSnapshot() ?? ReaderSnapshotBuilder.NoReader(ReaderName);
                }

                var device = ReaderDevice.Instance;

                return ReaderSnapshotBuilder.Build(ReaderName, device?.IsConnected ?? false, device?.GenericChip);
            }
        }

        /// <inheritdoc/>
        /// <remarks>Reading the reader on demand follows with the scan itself; until then a scan is not available.</remarks>
        public Task<ReaderSnapshot> ScanAsync(CancellationToken ct = default) =>
            ct.IsCancellationRequested
                ? Task.FromCanceled<ReaderSnapshot>(ct)
                : Task.FromException<ReaderSnapshot>(new NotSupportedException("Scanning the reader is not available in this build yet."));

        /// <inheritdoc/>
        public event EventHandler? ReaderStateChanged;

        /// <summary>
        /// Called by the main application when reader or card state changes so that
        /// registered listeners receive <see cref="ReaderStateChanged"/>. Each subscriber runs in isolation: this is
        /// called from the main window's own property setters, so a throwing extension must not break them.
        /// </summary>
        internal void RaiseStateChanged()
        {
            var handlers = ReaderStateChanged;
            if (handlers == null)
            {
                return;
            }

            foreach (var handler in handlers.GetInvocationList().Cast<EventHandler>())
            {
                try
                {
                    handler(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    var error = new SanitizedException(ex);
                    Logger.Error(
                        "ReaderStateChanged subscriber threw; the exception was not propagated. {ExceptionType}: {ExceptionMessage} {ExceptionStackTrace}",
                        error.TypeName, error.Message, error.StackTrace);
                }
            }
        }
    }
}
