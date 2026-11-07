using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>
    /// Holds a named system mutex for as long as it lives, so that the steps of one settings file (read, change, write)
    /// are not interleaved with those of another store for the same file: another window of the extension, or another
    /// program. The name is made from the path, so every store for the same file asks for the same mutex. The mutex is
    /// not a static of this library; it is made for each operation and let go at its end.
    /// </summary>
    internal sealed class FileGuard : IDisposable
    {
        private readonly Mutex? _mutex;

        private FileGuard(Mutex? mutex)
        {
            _mutex = mutex;
        }

        /// <summary>Waits for the guard of a file.</summary>
        /// <param name="path">The settings file.</param>
        /// <param name="wait">How long to wait for another holder.</param>
        /// <returns>
        /// The guard, to be disposed when the operation is over; <c>null</c> if another holder did not let go in time. If
        /// the system will not make a mutex at all (a restricted environment), the guard is an empty one and the
        /// in-process lock of the store is all there is, as before.
        /// </returns>
        internal static FileGuard? Acquire(string path, TimeSpan wait)
        {
            Mutex? mutex = null;

            try
            {
                var hash = SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToUpperInvariant()));
                mutex = new Mutex(initiallyOwned: false, "Local\\RFiDGear.CardCheck.Settings." + Convert.ToHexString(hash, 0, 8));

                bool owned;
                try
                {
                    owned = mutex.WaitOne(wait);
                }
                catch (AbandonedMutexException)
                {
                    // The last holder ended without letting go. The mutex is ours now, and the file is whole: it is only
                    // ever replaced in one step.
                    owned = true;
                }

                if (!owned)
                {
                    mutex.Dispose();

                    return null;
                }

                return new FileGuard(mutex);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException || ex is WaitHandleCannotBeOpenedException || ex is ArgumentException || ex is NotSupportedException)
            {
                mutex?.Dispose();

                return new FileGuard(null);
            }
        }

        /// <summary>Lets go of the mutex.</summary>
        public void Dispose()
        {
            if (_mutex == null)
            {
                return;
            }

            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Not held by this thread any more; there is nothing left to let go of.
            }

            _mutex.Dispose();
        }
    }
}
