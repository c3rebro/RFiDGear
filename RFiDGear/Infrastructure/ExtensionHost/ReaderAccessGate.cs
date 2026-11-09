#nullable enable
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace RFiDGear.Infrastructure.ExtensionHost
{
    /// <summary>
    /// Lets one party at a time read a card with the reader. A reader scan of an extension
    /// (<c>IReaderHost.ScanAsync</c>) takes it, and so should every read the application makes itself. A read resets
    /// and refills the chip that all of them share, and with a PC/SC reader it ends the card session first, so two reads
    /// at once would corrupt each other's result. The gate is not re-entrant: a party that holds it must not enter it
    /// again.
    /// </summary>
    [SuppressMessage("Major Bug", "S2931:Classes with IDisposable members should implement IDisposable", Justification = "The semaphore is never used through its wait handle, so it holds no operating system resource that would have to be released.")]
    public sealed class ReaderAccessGate
    {
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

        /// <summary><c>true</c> while somebody holds the gate. A hint for callers that only want to look.</summary>
        public bool IsHeld => _gate.CurrentCount == 0;

        /// <summary>Takes the gate if it is free, without waiting.</summary>
        /// <returns><c>true</c> if the caller now holds the gate and must call <see cref="Exit"/>.</returns>
        public bool TryEnter() => _gate.Wait(0);

        /// <summary>Waits until the gate is free and takes it. The caller must call <see cref="Exit"/>.</summary>
        /// <param name="cancellationToken">Stops the waiting; the gate is then not held.</param>
        /// <returns>A task that completes when the caller holds the gate.</returns>
        public Task EnterAsync(CancellationToken cancellationToken = default) => _gate.WaitAsync(cancellationToken);

        /// <summary>Gives the gate back. Call it in a <c>finally</c> block after a successful enter.</summary>
        public void Exit() => _gate.Release();
    }
}
