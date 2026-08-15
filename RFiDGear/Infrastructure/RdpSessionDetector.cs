using System.Runtime.InteropServices;

namespace RFiDGear.Infrastructure
{
    /// <summary>
    /// Detects whether the current process is running inside a Remote Desktop (RDP/Terminal Services) session.
    /// </summary>
    public static class RdpSessionDetector
    {
        // SM_REMOTESESSION = 0x1000
        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        /// <summary>
        /// Returns <see langword="true"/> when the process is hosted inside an RDP or
        /// Terminal Services session (i.e. <c>GetSystemMetrics(SM_REMOTESESSION)</c> is non-zero).
        /// </summary>
        public static bool IsRemoteDesktopSession => GetSystemMetrics(0x1000) != 0;
    }
}
