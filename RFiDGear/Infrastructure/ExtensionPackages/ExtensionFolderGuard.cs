#nullable enable

using System;
using System.Collections.Generic;
using System.IO;

namespace RFiDGear.Infrastructure.ExtensionPackages
{
    /// <summary>
    /// Says whether this process is the one that may tidy the user's folder of installed extensions. The first RFiDGear opens a
    /// lock file in the folder and keeps it open until it exits: another RFiDGear that is started meanwhile (it composes the
    /// extensions before it finds out that it is a second one and exits) cannot open it, and so never deletes a version that the
    /// first one uses, or the folder of an installation that the first one is asking about. A file, not a named mutex: it does not
    /// belong to a thread, and the operating system lets go of it when the process ends, however it ends.
    /// </summary>
    internal sealed class ExtensionFolderGuard : IDisposable
    {
        /// <summary>The name of the lock file in the user's folder of installed extensions.</summary>
        internal const string LockFileName = ".lock";

        private static readonly Dictionary<string, ExtensionFolderGuard> Guards = new Dictionary<string, ExtensionFolderGuard>(StringComparer.OrdinalIgnoreCase);

        private readonly object _sync = new object();
        private readonly string _lockFile;
        private FileStream? _held;

        /// <summary>Creates a guard for a lock file.</summary>
        /// <param name="lockFile">The full path of the lock file; its folder has to exist.</param>
        internal ExtensionFolderGuard(string lockFile)
        {
            _lockFile = lockFile ?? throw new ArgumentNullException(nameof(lockFile));
        }

        /// <summary>Gets the guard of a user's folder of installed extensions: the same one for the same folder, for the whole process.</summary>
        /// <param name="userRoot">The user's folder of installed extensions.</param>
        /// <returns>The guard.</returns>
        internal static ExtensionFolderGuard For(string userRoot)
        {
            var lockFile = Path.Combine(userRoot, LockFileName);
            lock (Guards)
            {
                if (!Guards.TryGetValue(lockFile, out var guard))
                {
                    Guards[lockFile] = guard = new ExtensionFolderGuard(lockFile);
                }

                return guard;
            }
        }

        /// <summary>
        /// Tries to be the process that tidies. It is true again when this guard has the file open already, and false while another
        /// process has it, or when the file cannot be opened at all (then nothing is tidied).
        /// </summary>
        /// <returns><c>true</c> if this process may tidy.</returns>
        public bool TryTake()
        {
            lock (_sync)
            {
                if (_held != null)
                {
                    return true;
                }

                try
                {
                    // System.IO.FileAccess in full: inside RFiDGear.Infrastructure the name means the namespace of the host's file access.
                    _held = new FileStream(_lockFile, FileMode.OpenOrCreate, System.IO.FileAccess.ReadWrite, FileShare.None);
                    return true;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    return false;
                }
            }
        }

        /// <summary>Lets go of the lock file, if this guard has it open.</summary>
        public void Dispose()
        {
            lock (_sync)
            {
                _held?.Dispose();
                _held = null;
            }
        }
    }
}
