using System;
using System.Threading;
using System.Threading.Tasks;

namespace RFiDGear.Infrastructure
{
    /// <summary>
    /// Provides an async-compatible mutual exclusion lock for serialized operations.
    /// </summary>
    internal sealed class AsyncOperationLock
    {
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Acquires the lock and returns a disposable that releases it.
        /// </summary>
        /// <param name="cancellationToken">Token that cancels the wait for the lock.</param>
        /// <returns>A disposable that releases the lock when disposed.</returns>
        public async Task<IDisposable> AcquireAsync(CancellationToken cancellationToken = default)
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new Releaser(semaphore);
        }

        private sealed class Releaser : IDisposable
        {
            private readonly SemaphoreSlim semaphoreSlim;
            private bool isDisposed;

            public Releaser(SemaphoreSlim semaphoreSlim)
            {
                this.semaphoreSlim = semaphoreSlim;
            }

            public void Dispose()
            {
                if (isDisposed)
                {
                    return;
                }

                isDisposed = true;
                semaphoreSlim.Release();
            }
        }
    }
}
