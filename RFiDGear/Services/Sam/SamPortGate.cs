#nullable enable
using System.Threading;

namespace RFiDGear.Services.Sam
{
    /// <summary>
    /// The SAM, held by one user at a time in the whole process: the setup window (a host session lives on its connection) or a command that
    /// uses a key of the SAM. Two connections to the same SAM would share its state, and a PICC authentication that the one leaves half done
    /// would break the other.
    /// </summary>
    internal static class SamPortGate
    {
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);

        /// <summary>Gets whether somebody holds the SAM.</summary>
        internal static bool IsHeld => Gate.CurrentCount == 0;

        /// <summary>Takes the SAM if it is free.</summary>
        /// <returns><see langword="true"/> when it is held now; <see cref="Exit"/> gives it back.</returns>
        internal static bool TryEnter() => Gate.Wait(0);

        /// <summary>Gives the SAM back. Only the one that took it calls this, once.</summary>
        internal static void Exit() => Gate.Release();
    }
}