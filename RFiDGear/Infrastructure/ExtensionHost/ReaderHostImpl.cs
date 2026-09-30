using RFiDGear.Contracts;
using RFiDGear.Infrastructure.ReaderProviders;

using System;

namespace RFiDGear.Infrastructure.ExtensionHost
{
    /// <summary>
    /// <see cref="IReaderHost"/> implementation that reads from <see cref="ReaderDevice.Instance"/>.
    /// </summary>
    internal sealed class ReaderHostImpl : IReaderHost
    {
        private readonly string _readerName;

        /// <param name="readerName">Display name of the configured reader (from settings).</param>
        public ReaderHostImpl(string readerName)
        {
            _readerName = readerName;
        }

        /// <inheritdoc/>
        public string? ReaderName => _readerName;

        /// <inheritdoc/>
        public string? CurrentUID => ReaderDevice.Instance?.GenericChip?.UID;

        /// <inheritdoc/>
        public string? ChipTypeName => ReaderDevice.Instance?.GenericChip?.CardType.ToString();

        /// <inheritdoc/>
        public bool IsConnected => ReaderDevice.Instance?.IsConnected ?? false;

        /// <inheritdoc/>
        public event EventHandler ReaderStateChanged;

        /// <summary>
        /// Called by the main application when reader or card state changes so that
        /// registered listeners receive <see cref="ReaderStateChanged"/>.
        /// </summary>
        internal void RaiseStateChanged() =>
            ReaderStateChanged?.Invoke(this, EventArgs.Empty);
    }
}
