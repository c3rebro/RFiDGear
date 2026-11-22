using System;

namespace RFiDGear.Services.Licensing
{
    /// <summary>
    /// The state of the license at one moment, as the Store told it. It is a value: a new answer is a new snapshot.
    /// </summary>
    public sealed class LicenseSnapshot
    {
        private LicenseSnapshot(LicenseKind kind, DateTimeOffset? trialEnds)
        {
            Kind = kind;
            TrialEnds = trialEnds;
        }

        /// <summary>Gets the snapshot of a license that could not be read.</summary>
        public static LicenseSnapshot Unknown { get; } = new LicenseSnapshot(LicenseKind.Unknown, null);

        /// <summary>Gets the snapshot of a bought license.</summary>
        public static LicenseSnapshot Full { get; } = new LicenseSnapshot(LicenseKind.Full, null);

        /// <summary>Gets the snapshot of a trial that is over.</summary>
        public static LicenseSnapshot Expired { get; } = new LicenseSnapshot(LicenseKind.Expired, null);

        /// <summary>Gets what kind of license this is.</summary>
        public LicenseKind Kind { get; }

        /// <summary>Gets the moment the trial ends; <see langword="null"/> unless this is a trial.</summary>
        public DateTimeOffset? TrialEnds { get; }

        /// <summary>
        /// Makes the snapshot of a trial.
        /// </summary>
        /// <param name="trialEnds">The moment the trial ends.</param>
        /// <returns>The snapshot.</returns>
        public static LicenseSnapshot Trial(DateTimeOffset trialEnds)
        {
            return new LicenseSnapshot(LicenseKind.Trial, trialEnds);
        }

        /// <summary>
        /// Tells whether the right to use the program has ended at a moment: the trial is over, or it was already reported as over.
        /// </summary>
        /// <param name="now">The moment to look at.</param>
        /// <returns><see langword="true"/> when the program may not start another run.</returns>
        public bool IsExpiredAt(DateTimeOffset now)
        {
            return Kind == LicenseKind.Expired
                || (Kind == LicenseKind.Trial && TrialEnds.HasValue && TrialEnds.Value <= now);
        }

        /// <summary>
        /// Counts the days that are left of a trial, a started day counting as a day.
        /// </summary>
        /// <param name="now">The moment to look at.</param>
        /// <returns>The days left; 0 when this is not a trial or it is over.</returns>
        public int DaysLeftAt(DateTimeOffset now)
        {
            if (Kind != LicenseKind.Trial || !TrialEnds.HasValue)
            {
                return 0;
            }

            var left = TrialEnds.Value - now;
            return left <= TimeSpan.Zero ? 0 : (int)Math.Ceiling(left.TotalDays);
        }
    }
}
