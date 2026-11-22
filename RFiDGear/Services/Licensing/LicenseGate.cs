using System;
using System.Threading;
using System.Threading.Tasks;

using Serilog;

namespace RFiDGear.Services.Licensing
{
    /// <summary>
    /// Decides what the program does about the license of the Store edition: it reads the license, shows how many days of a
    /// trial are left, and when the trial has ended it refuses new runs, stops automatic mode and asks the person to buy or
    /// close the program.
    /// <para>
    /// Four rules shape it. It <b>fails open</b>: a license that was never read counts as licensed (the Store itself refuses to
    /// start an expired trial, so this only matters while the program is already running). But what was <b>learned is kept</b>:
    /// a read that fails, or an answer of "unknown", never replaces a known license, so a hiccup of the Store does not lift an
    /// expiry. It <b>never interrupts a run</b>: a card operation is one attempt, and the dialog waits until the run is over.
    /// And it <b>goes by the newest answer</b>: a read that was started before the Store pushed a license is out of date when it
    /// ends and is dropped. It <b>takes no window</b>: everything it does to the program goes through
    /// <see cref="ILicenseEnforcement"/>.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Not thread-safe. Every member is meant to be called on the thread of the main window, as are the callbacks of
    /// <see cref="ILicenseEnforcement"/> and the <see cref="ILicenseService.Changed"/> event.
    /// </remarks>
    public sealed class LicenseGate
    {
        /// <summary>How long a license that was read is trusted before it is read again.</summary>
        internal static readonly TimeSpan RereadInterval = TimeSpan.FromHours(6);

        /// <summary>When nothing was learned (the read failed, or the answer was "unknown"), how long to wait before the next try.</summary>
        internal static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(15);

        private readonly ILicenseService _service;
        private readonly ILicenseEnforcement _host;
        private readonly Func<DateTimeOffset> _clock;

        private LicenseSnapshot _snapshot = LicenseSnapshot.Unknown;
        private DateTimeOffset _nextRead = DateTimeOffset.MinValue;
        private string _statusKey = string.Empty;
        private bool _endedShown;
        private bool _attached;

        // Grows when a read starts and when the Store pushes a license. A read whose number is not the current one when it ends
        // was overtaken by something newer, and its answer is dropped.
        private int _generation;

        // The number of the read that is under way, 0 for none: the timer does not start a second one on top of it.
        private int _readInFlight;

        /// <summary>
        /// Initializes a new instance of the <see cref="LicenseGate"/> class.
        /// </summary>
        /// <param name="service">Where the license comes from.</param>
        /// <param name="host">What the gate does to the program.</param>
        /// <param name="clock">The time; the system clock when omitted. A test passes its own.</param>
        public LicenseGate(ILicenseService service, ILicenseEnforcement host, Func<DateTimeOffset> clock = null)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _clock = clock ?? (() => DateTimeOffset.UtcNow);
        }

        /// <summary>Raised when what the person sees changes: the days left, the end of the trial, a purchase.</summary>
        public event EventHandler StatusChanged;

        /// <summary>Gets the license as it was last learned.</summary>
        public LicenseSnapshot Current => _snapshot;

        /// <summary>Gets a value indicating whether the trial is over now.</summary>
        public bool IsExpired => _snapshot.IsExpiredAt(_clock());

        /// <summary>Gets a value indicating whether a trial is running (and not over).</summary>
        public bool IsTrialRunning => _snapshot.Kind == LicenseKind.Trial && !IsExpired;

        /// <summary>Gets the days that are left of a trial; 0 when none is running.</summary>
        public int DaysLeft => _snapshot.DaysLeftAt(_clock());

        /// <summary>
        /// Starts listening to the service and reads the license. It never throws: a license that cannot be read counts as
        /// licensed.
        /// </summary>
        /// <returns>A task that ends when the first read is done.</returns>
        public async Task StartAsync()
        {
            if (!_attached)
            {
                _service.Changed += (_, pushed) => OnLicensePushed(pushed);
                _attached = true;
            }

            await ReadAsync(force: false);
        }

        /// <summary>
        /// Reads the license again and applies it, unless a read is under way already. It never throws.
        /// </summary>
        /// <returns>A task that ends when the answer is applied.</returns>
        public Task RefreshAsync() => ReadAsync(force: false);

        /// <summary>
        /// Looks at the clock: the trial may have ended since the last look, a run may have ended that held the dialog back,
        /// and the license is read again when it is time. Called once a minute by the window. It never throws.
        /// </summary>
        /// <returns>A task that ends when the look is done.</returns>
        public async Task TickAsync()
        {
            // What is known is enforced first: a trial with a known end ends on time even while a read of the Store is slow.
            ApplySafely(_snapshot);

            if (_clock() >= _nextRead)
            {
                await ReadAsync(force: false);
            }
        }

        /// <summary>
        /// Asks the Store to sell the program and reads the license again.
        /// </summary>
        /// <param name="ownerWindow">The handle of the window that owns the Store's dialog.</param>
        /// <returns><see langword="true"/> when the license is a full one afterwards.</returns>
        public async Task<bool> PurchaseAsync(IntPtr ownerWindow)
        {
            try
            {
                await _service.PurchaseAsync(ownerWindow, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Log.ForContext<LicenseGate>().Warning("The purchase failed ({ExceptionType})", ex.GetType().Name);
                return false;
            }

            // A read that is under way started before the purchase: this one replaces it.
            await ReadAsync(force: true);
            return _snapshot.Kind == LicenseKind.Full;
        }

        private async Task ReadAsync(bool force)
        {
            if (_readInFlight != 0 && !force)
            {
                return;
            }

            var generation = ++_generation;
            _readInFlight = generation;

            LicenseSnapshot read = null;
            var failed = false;

            try
            {
                read = await _service.GetAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                // Only the type is logged: the text of an exception from the Store may hold names or ids.
                Log.ForContext<LicenseGate>().Warning("The license could not be read ({ExceptionType}); the last answer stays in force", ex.GetType().Name);
                failed = true;
            }

            if (_readInFlight == generation)
            {
                _readInFlight = 0;
            }

            if (generation != _generation)
            {
                // Overtaken: the Store pushed a license, or a purchase started a newer read, while this one was under way.
                return;
            }

            if (failed || !LearnsSomething(read))
            {
                _nextRead = _clock() + RetryInterval;
                ApplySafely(_snapshot);
                return;
            }

            _nextRead = _clock() + RereadInterval;
            ApplySafely(read);
        }

        private void OnLicensePushed(LicenseSnapshot pushed)
        {
            if (!LearnsSomething(pushed))
            {
                return;
            }

            // A read that is under way now carries an older answer; it is dropped when it ends.
            _generation++;
            _readInFlight = 0;
            _nextRead = _clock() + RereadInterval;
            ApplySafely(pushed);
        }

        // "Unknown" says that the Store could not tell: it never replaces a license that is known, and a null is no answer.
        private bool LearnsSomething(LicenseSnapshot answer)
        {
            return answer != null && (answer.Kind != LicenseKind.Unknown || _snapshot.Kind == LicenseKind.Unknown);
        }

        private void ApplySafely(LicenseSnapshot snapshot)
        {
            try
            {
                Apply(snapshot);
            }
            catch (Exception ex)
            {
                // The window may be closing: the gate must not take a timer or an event handler down with it.
                Log.ForContext<LicenseGate>().Error("The license could not be applied ({ExceptionType})", ex.GetType().Name);
            }
        }

        private void Apply(LicenseSnapshot snapshot)
        {
            _snapshot = snapshot;

            var now = _clock();
            var expired = snapshot.IsExpiredAt(now);

            _host.SetNewRunsBlocked(expired);

            if (expired)
            {
                _host.StopAutomaticMode();
            }
            else
            {
                // A later end of a trial (or a license that was bought, then lost) must be told again.
                _endedShown = false;
            }

            var key = expired
                ? "expired"
                : snapshot.Kind == LicenseKind.Trial ? "trial:" + snapshot.DaysLeftAt(now) : "none";

            if (!string.Equals(key, _statusKey, StringComparison.Ordinal))
            {
                _statusKey = key;
                StatusChanged?.Invoke(this, EventArgs.Empty);
            }

            if (expired && !_endedShown && !_host.IsRunInProgress)
            {
                _endedShown = true;
                _host.ShowTrialEnded();
            }
        }
    }
}
