using System;

using RFiDGear.Infrastructure.Editions;

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
        /// without a Store listing. A process that runs from a package gets the service of the Store assembly. Every other
        /// process (the MSI edition), and a package whose Store assembly is missing or broken, gets the service of an edition
        /// that needs no license.
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
            if (AppEdition.IsPackaged)
            {
                var store = StoreLicenseServiceLoader.TryLoad(AppContext.BaseDirectory);
                if (store != null)
                {
                    return store;
                }
            }

            return new NullLicenseService();
        }
    }
}
