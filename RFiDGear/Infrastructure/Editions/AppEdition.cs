using System;
using System.Runtime.InteropServices;

namespace RFiDGear.Infrastructure.Editions
{
    /// <summary>
    /// Tells whether the process runs from an MSIX package, which is how the Microsoft Store edition is delivered. The same
    /// binaries run unpackaged in the MSI edition, so nothing is decided at build time: the edition is what the process finds
    /// itself in.
    /// </summary>
    public static class AppEdition
    {
        /// <summary>The answer of <c>GetCurrentPackageFullName</c> when it is given no buffer: the process has a package.</summary>
        internal const int ErrorInsufficientBuffer = 122;

        /// <summary>The answer of <c>GetCurrentPackageFullName</c> for a process without a package identity.</summary>
        internal const int AppModelErrorNoPackage = 15700;

        private static readonly Lazy<bool> Packaged = new Lazy<bool>(DetectPackaged);

        [DllImport("kernel32.dll", SetLastError = false)]
        private static extern int GetCurrentPackageFullName(ref uint packageFullNameLength, IntPtr packageFullName);

        /// <summary>
        /// Gets a value indicating whether this process has a package identity, that is, runs as the Store edition. It is decided
        /// once per process.
        /// </summary>
        public static bool IsPackaged => Packaged.Value;

        /// <summary>
        /// Reads the answer of <c>GetCurrentPackageFullName</c> asked without a buffer.
        /// </summary>
        /// <param name="result">The return value of the call.</param>
        /// <returns>
        /// <see langword="true"/> when the process has a package (no buffer was given, so the call says the buffer is too small);
        /// <see langword="false"/> for no package and for any answer that is not known, which keeps the behaviour of the MSI edition.
        /// </returns>
        internal static bool InterpretPackageResult(int result)
        {
            return result == 0 || result == ErrorInsufficientBuffer;
        }

        private static bool DetectPackaged()
        {
            try
            {
                uint length = 0;
                return InterpretPackageResult(GetCurrentPackageFullName(ref length, IntPtr.Zero));
            }
            catch (EntryPointNotFoundException)
            {
                // A Windows without the package API cannot run an MSIX either.
                return false;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
        }
    }
}
