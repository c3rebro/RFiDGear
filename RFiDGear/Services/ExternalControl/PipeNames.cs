#nullable enable
using System;
using System.Security.Principal;

using RFiDGear.Bridge;

namespace RFiDGear.Services.ExternalControl
{
    /// <summary>The name of the pipe the external control bridge listens on.</summary>
    internal static class PipeNames
    {
        /// <summary>
        /// Makes the pipe name of the Windows user who runs this process. The pipe namespace of Windows is machine-wide, so the
        /// user is part of the name: two users on one machine (a terminal server) do not collide, and a client finds the pipe of
        /// its own user from its own security identifier.
        /// </summary>
        /// <returns>The name, without the <c>\\.\pipe\</c> prefix.</returns>
        /// <exception cref="InvalidOperationException">The process has no user identity.</exception>
        internal static string ForCurrentUser()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var sid = identity.User?.Value ?? throw new InvalidOperationException("The current Windows identity has no security identifier.");

            return BridgeProtocol.PipeNameFor(sid);
        }
    }
}
