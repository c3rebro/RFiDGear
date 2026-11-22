using System;

namespace RFiDGear.Services.Licensing
{
    /// <summary>
    /// Chooses the license service of this process.
    /// </summary>
    internal static class LicenseServiceFactory
    {
        /// <summary>The environment variable of the Debug switch, see <see cref="FakeLicenseSetting"/>.</summary>
        internal const string FakeLicenseVariable = "RFIDGEAR_FAKE_LICENSE";

        /// <summary>
        /// Makes the license service. A Debug build with the switch set gets a fake license, so the trial can be looked at
        /// without a Store listing; every other process gets the service of an edition that needs no license. (The service
        /// of the Store edition comes with the Store assembly, in a later step.)
        /// </summary>
        /// <returns>The service.</returns>
        public static ILicenseService Create()
        {
#if DEBUG
            var fake = FakeLicenseSetting.Parse(Environment.GetEnvironmentVariable(FakeLicenseVariable), DateTimeOffset.UtcNow);
            if (fake != null)
            {
                return new FakeLicenseService(fake);
            }
#endif
            return new NullLicenseService();
        }
    }
}
