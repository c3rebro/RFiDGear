#nullable enable
using RFiDGear.Contracts;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Models;

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
        private readonly ExtensionOperationCoordinator? _coordinator;
        private readonly ReaderAccessGate? _gate;
        private readonly object _publishLock = new object();

        // What the last scan found, or null while nothing is known: at the start, and after the reader changed.
        private ReaderSnapshot? _published;

        // Counts the reader changes. A scan remembers the number it started under and its result is dropped if the
        // reader changed meanwhile, so the card of the old reader is never shown under the name of the new one.
        private int _generation;

        /// <param name="readerName">Display name of the configured reader (from settings).</param>
        /// <param name="coordinator">Keeps a reader scan apart from loads, runs and project edits; none when omitted.</param>
        /// <param name="gate">Keeps a reader scan apart from the reads the main window makes; none when omitted.</param>
        public ReaderHostImpl(string readerName, ExtensionOperationCoordinator? coordinator = null, ReaderAccessGate? gate = null)
            : this(() => readerName, coordinator, gate)
        {
        }

        /// <param name="getReaderName">
        /// Returns the display name of the configured reader. Called each time <see cref="ReaderName"/> is read, so the
        /// name follows the reader the user selects while the application runs.
        /// </param>
        /// <param name="coordinator">Keeps a reader scan apart from loads, runs and project edits; none when omitted.</param>
        /// <param name="gate">Keeps a reader scan apart from the reads the main window makes; none when omitted.</param>
        public ReaderHostImpl(Func<string?> getReaderName, ExtensionOperationCoordinator? coordinator = null, ReaderAccessGate? gate = null)
        {
            _getReaderName = getReaderName ?? throw new ArgumentNullException(nameof(getReaderName));
            _coordinator = coordinator;
            _gate = gate;
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
                // The card is what the last scan found; the name is the one the reader has now. If the reader changes
                // between taking the one and reading the other, the pair is wrong (the card of the old reader under the
                // name of the new one), so it is taken again.
                for (var attempt = 0; attempt < 3; attempt++)
                {
                    ReaderSnapshot? published;
                    int generation;
                    lock (_publishLock)
                    {
                        published = _published;
                        generation = _generation;
                    }

                    if (published == null)
                    {
                        break;
                    }

                    var name = SafeReaderName();
                    if (Volatile.Read(ref _generation) == generation)
                    {
                        return new ReaderSnapshot(name, published.State, published.Credentials);
                    }
                }

                return LiveSnapshot();
            }
        }

        /// <inheritdoc/>
        public async Task<ReaderSnapshot> ScanAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var name = SafeReaderName();

            var slotTaken = false;
            var gateTaken = false;
            var changed = false;
            var generation = 0;
            ReaderSnapshot snapshot;
            try
            {
                // The gate first: a poll that finds the reader taken then never touches the scan slot, and so never
                // makes a load, a run or an edit that arrives at that moment look busy.
                if (_gate != null)
                {
                    if (!_gate.TryEnter())
                    {
                        return ReaderSnapshotBuilder.Busy(name);
                    }

                    gateTaken = true;
                }

                if (_coordinator != null)
                {
                    if (!_coordinator.TryBeginScan())
                    {
                        return ReaderSnapshotBuilder.Busy(name);
                    }

                    slotTaken = true;
                }

                // Cancelling works until the read has started (Task.Run does not start a cancelled task); a read that has
                // started is never interrupted, because the token is not given to it.
                generation = Volatile.Read(ref _generation);
                snapshot = await Task.Run(ReadOnceAsync, ct).ConfigureAwait(false);

                // The state is updated while the slot is still held, so scans are published in the order they were made.
                changed = Publish(snapshot, generation);
            }
            finally
            {
                if (gateTaken)
                {
                    _gate!.Exit();
                }

                if (slotTaken)
                {
                    _coordinator!.EndScan();
                }
            }

            // After the release, so that a subscriber may scan again or start a run from the handler.
            BeforeAnnounce?.Invoke();
            AnnounceIfCurrent(changed, generation);

            return snapshot;
        }

        /// <inheritdoc/>
        public event EventHandler? ReaderStateChanged;

        /// <summary>
        /// A seam for tests: called after a scan has stored its picture and given everything back, just before it
        /// announces the change. It lets a test make the reader change at exactly that moment.
        /// </summary>
        internal Action? BeforeAnnounce { get; set; }

        /// <summary>
        /// Raises <see cref="ReaderStateChanged"/> for a change that was stored, unless the reader has changed since:
        /// the change of the reader announced itself, and the picture it replaced is obsolete.
        /// </summary>
        /// <param name="changed">Whether storing the picture reported a change.</param>
        /// <param name="generation">The number of reader changes from before the read.</param>
        internal void AnnounceIfCurrent(bool changed, int generation)
        {
            if (changed && Volatile.Read(ref _generation) == generation)
            {
                RaiseStateChanged();
            }
        }

        /// <summary>
        /// Called by the main application when the reader the user selected changes. What the old reader showed says
        /// nothing about the new one, so the published picture is forgotten (a scan that is on its way is dropped),
        /// and the subscribers are told, exactly once, as before.
        /// </summary>
        internal void NotifyReaderNameChanged()
        {
            lock (_publishLock)
            {
                _published = null;
                _generation++;
            }

            RaiseStateChanged();
        }

        /// <summary>
        /// Raises <see cref="ReaderStateChanged"/>. Each subscriber runs in isolation: this is
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

        // Runs on a pool thread, under the scan slot and the gate. Takes the device once, because the application can
        // swap it. The providers do not throw, but a failure in here must not escape as anything but a failed read.
        private async Task<ReaderSnapshot> ReadOnceAsync()
        {
            var name = SafeReaderName();
            GenericChipModel? previous = null;
            ReaderDevice? device = null;
            try
            {
                device = ReaderDevice.Instance;
                if (device == null)
                {
                    return ReaderSnapshotBuilder.NoReader(name);
                }

                // A stale chip (old children, a card that is gone) must not be mistaken for what is on the reader now.
                previous = device.GenericChip;
                device.GenericChip = new GenericChipModel();
                var result = await device.ReadChipPublic().ConfigureAwait(false);
                var snapshot = ReaderSnapshotBuilder.FromRead(name, true, device.IsPcscReader, device.IsConnected, result, device.GenericChip);

                // A read that failed knows nothing: the chip of before is the better guess than an empty one.
                if (snapshot.State == ReaderScanState.ReadFailed)
                {
                    device.GenericChip = previous;
                }

                return snapshot;
            }
            catch (Exception ex)
            {
                var error = new SanitizedException(ex);
                Logger.Warning("Reading the reader failed. {ExceptionType}: {ExceptionMessage}", error.TypeName, error.Message);
                if (device != null && previous != null)
                {
                    device.GenericChip = previous;
                }

                return ReaderSnapshotBuilder.ReadFailed(name);
            }
        }

        // Replaces the published picture by this one if it is one that can be published (the reader was looked at: Ready
        // or NoReader, never Busy or ReadFailed) and the reader has not changed since the scan began. The first picture
        // always counts as a change: nothing was known before, so a subscriber may have seen anything.
        private bool Publish(ReaderSnapshot snapshot, int generation)
        {
            if (snapshot.State != ReaderScanState.Ready && snapshot.State != ReaderScanState.NoReader)
            {
                return false;
            }

            lock (_publishLock)
            {
                if (generation != _generation)
                {
                    return false;
                }

                var changed = _published == null || !SameCard(_published, snapshot);
                _published = snapshot;

                return changed;
            }
        }

        // The reader name is not part of the card: a name change has its own event.
        private static bool SameCard(ReaderSnapshot a, ReaderSnapshot b) =>
            a.State == b.State && a.Credentials.SequenceEqual(b.Credentials);

        // Until something was published the picture is the host's guess from the state of the reader, as it was in 4.1.
        private ReaderSnapshot LiveSnapshot()
        {
            var name = SafeReaderName();
            try
            {
                var device = ReaderDevice.Instance;

                return ReaderSnapshotBuilder.Build(name, device?.IsConnected ?? false, device?.GenericChip);
            }
            catch (Exception ex)
            {
                var error = new SanitizedException(ex);
                Logger.Warning("Looking at the reader failed. {ExceptionType}: {ExceptionMessage}", error.TypeName, error.Message);

                return ReaderSnapshotBuilder.NoReader(name);
            }
        }

        private string? SafeReaderName()
        {
            try
            {
                return ReaderName;
            }
            catch (Exception)
            {
                // The name is only a label of the answer; a name that cannot be read is an empty one.
                return null;
            }
        }
    }
}
