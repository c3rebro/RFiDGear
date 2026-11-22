using System;
using System.Threading;
using System.Threading.Tasks;

namespace RFiDGear.Services.Licensing
{
    /// <summary>
    /// The license service of an edition that needs none (the MSI edition): the program is always licensed and nothing can be
    /// bought.
    /// </summary>
    public sealed class NullLicenseService : ILicenseService
    {
        private EventHandler<LicenseSnapshot> _changed;

        /// <inheritdoc />
        public event EventHandler<LicenseSnapshot> Changed
        {
            add { _changed += value; }
            remove { _changed -= value; }
        }

        /// <inheritdoc />
        public bool RequiresLicenseCheck => false;

        /// <inheritdoc />
        public Task<LicenseSnapshot> GetAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(LicenseSnapshot.Full);
        }

        /// <inheritdoc />
        public Task<bool> PurchaseAsync(IntPtr ownerWindow, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(false);
        }
    }
}
