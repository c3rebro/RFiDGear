using System;
using System.Runtime.InteropServices;

namespace RFiDGear.Infrastructure
{
    /// <summary>
    /// Detects RDP session state and probes PC/SC subsystem accessibility.
    /// </summary>
    public static class RdpSessionDetector
    {
        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("winscard.dll", SetLastError = false)]
        private static extern uint SCardEstablishContext(uint dwScope, IntPtr pvReserved1, IntPtr pvReserved2, out IntPtr phContext);

        [DllImport("winscard.dll", SetLastError = false)]
        private static extern uint SCardReleaseContext(IntPtr hContext);

        private const uint SCARD_S_SUCCESS = 0x00000000u;
        private const uint SCARD_SCOPE_USER = 0u;

        /// <summary>
        /// Returns <see langword="true"/> when the process is hosted inside an RDP or
        /// Terminal Services session (i.e. <c>GetSystemMetrics(SM_REMOTESESSION)</c> is non-zero).
        /// </summary>
        public static bool IsRemoteDesktopSession => GetSystemMetrics(0x1000) != 0;

        /// <summary>
        /// Probes whether the PC/SC smart card subsystem is accessible in the current Windows session.
        /// Returns <see langword="false"/> when the per-session Smart Card Resource Manager is not
        /// reachable — typically an RDP session without smart card redirection active.
        /// </summary>
        public static bool CanEstablishPcscContext()
        {
            var rc = SCardEstablishContext(SCARD_SCOPE_USER, IntPtr.Zero, IntPtr.Zero, out var hCtx);
            if (rc == SCARD_S_SUCCESS)
            {
                SCardReleaseContext(hCtx);
                return true;
            }
            return false;
        }
    }
}
