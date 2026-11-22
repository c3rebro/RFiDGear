using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace RFiDGear.Services.Licensing
{
    /// <summary>
    /// A license service with a license of its own, for the tests of the gate and for looking at the trial in a Debug build
    /// (see <see cref="LicenseServiceFactory"/>). Buying makes it a full license, and nothing is ever sent anywhere.
    /// </summary>
    internal sealed class FakeLicenseService : ILicenseService
    {
        private readonly Queue<TaskCompletionSource<LicenseSnapshot>> _held = new Queue<TaskCompletionSource<LicenseSnapshot>>();
        private EventHandler<LicenseSnapshot> _changed;
        private LicenseSnapshot _snapshot;

        /// <summary>
        /// Initializes a new instance of the <see cref="FakeLicenseService"/> class.
        /// </summary>
        /// <param name="snapshot">The license it starts with.</param>
        public FakeLicenseService(LicenseSnapshot snapshot)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        /// <inheritdoc />
        public event EventHandler<LicenseSnapshot> Changed
        {
            add { _changed += value; }
            remove { _changed -= value; }
        }

        /// <inheritdoc />
        public bool RequiresLicenseCheck => true;

        /// <summary>Gets the number of times the license was read.</summary>
        public int Reads { get; private set; }

        /// <summary>Gets the number of times a purchase was asked for.</summary>
        public int Purchases { get; private set; }

        /// <summary>Gets or sets what a read does instead of answering: a service that cannot be reached.</summary>
        public Exception FailWith { get; set; }

        /// <summary>Gets or sets what a purchase does instead of going through: the person closed the Store.</summary>
        public Exception PurchaseFailWith { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a read stays open until <see cref="CompleteHeldRead"/> answers it, as a slow
        /// Store does.
        /// </summary>
        public bool HoldReads { get; set; }

        /// <summary>
        /// Answers the oldest read that is held open.
        /// </summary>
        /// <param name="snapshot">The answer.</param>
        public void CompleteHeldRead(LicenseSnapshot snapshot)
        {
            _held.Dequeue().SetResult(snapshot);
        }

        /// <summary>
        /// Gives the service another license, as the Store does when the trial ends, and tells the listeners.
        /// </summary>
        /// <param name="snapshot">The new license.</param>
        public void Change(LicenseSnapshot snapshot)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _changed?.Invoke(this, _snapshot);
        }

        /// <inheritdoc />
        public Task<LicenseSnapshot> GetAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Reads++;

            if (FailWith != null)
            {
                return Task.FromException<LicenseSnapshot>(FailWith);
            }

            if (HoldReads)
            {
                var held = new TaskCompletionSource<LicenseSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
                _held.Enqueue(held);
                return held.Task;
            }

            return Task.FromResult(_snapshot);
        }

        /// <inheritdoc />
        public Task<bool> PurchaseAsync(IntPtr ownerWindow, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Purchases++;

            if (PurchaseFailWith != null)
            {
                return Task.FromException<bool>(PurchaseFailWith);
            }

            Change(LicenseSnapshot.Full);
            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// Reads the text of the Debug switch <c>RFIDGEAR_FAKE_LICENSE</c>: <c>full</c>, <c>expired</c> or <c>trial:N</c> (N days left).
    /// </summary>
    internal static class FakeLicenseSetting
    {
        private const string TrialPrefix = "trial:";
        private const int MostDays = 3650;

        /// <summary>
        /// Turns the text of the switch into a license.
        /// </summary>
        /// <param name="text">The value of the environment variable; may be empty or <see langword="null"/>.</param>
        /// <param name="now">The moment a trial is counted from.</param>
        /// <returns>The license; <see langword="null"/> when the text names none (the switch is off).</returns>
        public static LicenseSnapshot Parse(string text, DateTimeOffset now)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var value = text.Trim();

            if (value.Equals("full", StringComparison.OrdinalIgnoreCase))
            {
                return LicenseSnapshot.Full;
            }

            if (value.Equals("expired", StringComparison.OrdinalIgnoreCase))
            {
                return LicenseSnapshot.Expired;
            }

            if (value.StartsWith(TrialPrefix, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(value.Substring(TrialPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var days)
                && days <= MostDays)
            {
                return LicenseSnapshot.Trial(now.AddDays(days));
            }

            return null;
        }
    }
}
