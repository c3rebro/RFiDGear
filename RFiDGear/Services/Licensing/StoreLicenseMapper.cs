using System;

namespace RFiDGear.Services.Licensing
{
    /// <summary>
    /// Turns what the Store says about the app's license into a <see cref="LicenseSnapshot"/>. It is the one place that holds
    /// these rules and takes plain values, so it is tested in the host: the Store assembly itself (which needs a Windows-versioned
    /// target framework) is not referenced by the test project.
    /// </summary>
    public static class StoreLicenseMapper
    {
        /// <summary>
        /// Maps the license of the app.
        /// </summary>
        /// <param name="installedFromStore">
        /// Whether the package was installed from the Store. A copy that was sideloaded (a test build, or one signed by somebody
        /// else) has no Store license at all, which would otherwise look like a trial that has ended.
        /// </param>
        /// <param name="isActive">Whether the Store reports a license that is in force.</param>
        /// <param name="isTrial">Whether that license is a trial.</param>
        /// <param name="trialEnds">The end of the license, which is the end of the trial when it is one.</param>
        /// <returns>
        /// <see cref="LicenseSnapshot.Unknown"/> for a copy that is not from the Store (it is not gated), the ended trial for an
        /// app without a license in force, the trial with its end, or the full license.
        /// </returns>
        public static LicenseSnapshot Map(bool installedFromStore, bool isActive, bool isTrial, DateTimeOffset trialEnds)
        {
            if (!installedFromStore)
            {
                return LicenseSnapshot.Unknown;
            }

            if (!isActive)
            {
                return LicenseSnapshot.Expired;
            }

            return isTrial ? LicenseSnapshot.Trial(trialEnds) : LicenseSnapshot.Full;
        }
    }
}
