using System;
using System.Threading;
using System.Threading.Tasks;

namespace RFiDGear.Services.Licensing
{
    /// <summary>
    /// Where the program learns whether it may be used: the Store for the Store edition, nothing for the MSI edition. It
    /// reads and asks to buy; it never decides what the program does about the answer (that is <see cref="LicenseGate"/>).
    /// </summary>
    public interface ILicenseService
    {
        /// <summary>
        /// Raised when the license changes while the program runs (the trial ends, the program is bought). It is raised on
        /// the thread of the main window, so a handler may touch the window.
        /// </summary>
        event EventHandler<LicenseSnapshot> Changed;

        /// <summary>
        /// Gets a value indicating whether this service has anything to check. The MSI edition has none, and the program then
        /// starts no gate and no timer for it.
        /// </summary>
        bool RequiresLicenseCheck { get; }

        /// <summary>
        /// Reads the license.
        /// </summary>
        /// <param name="cancellationToken">Cancels the read.</param>
        /// <returns>The state of the license now. A service that cannot read it throws, and the caller treats that as unknown.</returns>
        Task<LicenseSnapshot> GetAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Shows the purchase of the program to the person, over a window.
        /// </summary>
        /// <param name="ownerWindow">The handle of the window that owns the Store's dialog.</param>
        /// <param name="cancellationToken">Cancels the wait.</param>
        /// <returns><see langword="true"/> when the Store reports that the purchase went through.</returns>
        Task<bool> PurchaseAsync(IntPtr ownerWindow, CancellationToken cancellationToken);
    }
}
