using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.ExtensionLibraries.CardCheck;

namespace RFiDGear.Extensions.CardCheckAssistant.Services
{
    /// <summary>
    /// The jobs of the test mode: the made-up orders of the support library's in-memory store. No order database is
    /// involved, so nothing here can read or change a real order. The store is made when the jobs are first asked for,
    /// so a window that is not in test mode never makes one, and it lives as long as this source, that is as long as the
    /// window.
    /// </summary>
    internal sealed class StubJobSource : IJobSource
    {
        private readonly Lazy<IOrderStore> _store;

        /// <summary>Creates the source.</summary>
        /// <param name="log">Where the creation of the store is noted.</param>
        /// <exception cref="ArgumentNullException"><paramref name="log"/> is null.</exception>
        public StubJobSource(ICardCheckLog log)
        {
            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            _store = new Lazy<IOrderStore>(() => CardCheckServices.CreateStubOrderStore(log));
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<JobEntry>> ListAsync(CancellationToken cancellationToken)
        {
            try
            {
                var result = await _store.Value.ListAsync(cancellationToken).ConfigureAwait(false);

                return result.IsSuccess
                    ? result.Value
                        .Select(order => new JobEntry(order.Id, order.JobNumber, order.ChipNumber, order.CustomerName, order.StatusText))
                        .ToList()
                    : Array.Empty<JobEntry>();
            }
            catch (Exception)
            {
                // The store reports its failures as results; anything else is a reason to show no jobs, not to stop the page.
                return Array.Empty<JobEntry>();
            }
        }
    }
}
