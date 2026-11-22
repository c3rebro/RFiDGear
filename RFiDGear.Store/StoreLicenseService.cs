using System;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Services.Licensing;

using Windows.ApplicationModel;
using Windows.Services.Store;

using WinRT.Interop;

namespace RFiDGear.Store
{
    /// <summary>
    /// The license of the Store edition, read from the Microsoft Store. It must be made on the thread of the main window: the
    /// event <see cref="Changed"/> is raised there.
    /// </summary>
    /// <remarks>
    /// <c>Windows.Services.Store</c> has no simulator: it answers only for a package that was installed from the Store. The rules
    /// that turn its answer into a license are in <see cref="StoreLicenseMapper"/> (in the program, where they are tested).
    /// </remarks>
    public sealed class StoreLicenseService : ILicenseService
    {
        private readonly StoreContext _context;
        private readonly SynchronizationContext? _window;
        private EventHandler<LicenseSnapshot>? _changed;

        /// <summary>
        /// Initializes a new instance of the <see cref="StoreLicenseService"/> class.
        /// </summary>
        public StoreLicenseService()
        {
            _context = StoreContext.GetDefault();
            _window = SynchronizationContext.Current;
            _context.OfflineLicensesChanged += OnOfflineLicensesChanged;
        }

        /// <inheritdoc />
        public event EventHandler<LicenseSnapshot> Changed
        {
            add { _changed += value; }
            remove { _changed -= value; }
        }

        /// <inheritdoc />
        public bool RequiresLicenseCheck => true;

        /// <inheritdoc />
        public async Task<LicenseSnapshot> GetAsync(CancellationToken cancellationToken)
        {
            // A copy that did not come from the Store (a test build that was sideloaded) has no license to ask for.
            if (Package.Current.SignatureKind != PackageSignatureKind.Store)
            {
                return StoreLicenseMapper.Map(installedFromStore: false, isActive: false, isTrial: false, trialEnds: default);
            }

            var license = await _context.GetAppLicenseAsync().AsTask(cancellationToken);

            return StoreLicenseMapper.Map(installedFromStore: true, license.IsActive, license.IsTrial, license.ExpirationDate);
        }

        /// <inheritdoc />
        public async Task<bool> PurchaseAsync(IntPtr ownerWindow, CancellationToken cancellationToken)
        {
            // The Store's dialog needs to know which window it belongs to (see the documentation of StoreContext for desktop apps).
            if (ownerWindow != IntPtr.Zero)
            {
                InitializeWithWindow.Initialize(_context, ownerWindow);
            }

            var found = await _context.GetStoreProductForCurrentAppAsync().AsTask(cancellationToken);

            if (found.ExtendedError != null || found.Product == null)
            {
                return false;
            }

            var purchase = await found.Product.RequestPurchaseAsync().AsTask(cancellationToken);

            return purchase.Status == StorePurchaseStatus.Succeeded || purchase.Status == StorePurchaseStatus.AlreadyPurchased;
        }

        // The Store raises this on a thread of its own, when the trial ends or the program is bought. The answer is read again and
        // handed to the window's thread.
        private async void OnOfflineLicensesChanged(StoreContext sender, object args)
        {
            try
            {
                var snapshot = await GetAsync(CancellationToken.None);
                Raise(snapshot);
            }
            catch (Exception)
            {
                // Nothing to tell: the gate reads the license again at its next look.
            }
        }

        private void Raise(LicenseSnapshot snapshot)
        {
            if (_window != null)
            {
                _window.Post(_ => _changed?.Invoke(this, snapshot), null);
            }
            else
            {
                _changed?.Invoke(this, snapshot);
            }
        }
    }
}
