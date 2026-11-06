#nullable enable
using RFiDGear.Contracts;
using RFiDGear.Infrastructure.ReaderProviders;

using Serilog;

using System;
using System.Linq;

namespace RFiDGear.Infrastructure.ExtensionHost
{
    /// <summary>
    /// <see cref="IReaderHost"/> implementation that reads from <see cref="ReaderDevice.Instance"/>.
    /// </summary>
    internal sealed class ReaderHostImpl : IReaderHost
    {
        private static ILogger Logger => Log.ForContext<ReaderHostImpl>();

        private readonly Func<string?> _getReaderName;

        /// <param name="readerName">Display name of the configured reader (from settings).</param>
        public ReaderHostImpl(string readerName)
            : this(() => readerName)
        {
        }

        /// <param name="getReaderName">
        /// Returns the display name of the configured reader. Called each time <see cref="ReaderName"/> is read, so the
        /// name follows the reader the user selects while the application runs.
        /// </param>
        public ReaderHostImpl(Func<string?> getReaderName)
        {
            _getReaderName = getReaderName ?? throw new ArgumentNullException(nameof(getReaderName));
        }

        /// <inheritdoc/>
        public string? ReaderName => _getReaderName();

        /// <inheritdoc/>
        public string? CurrentUID => ReaderDevice.Instance?.GenericChip?.UID;

        /// <inheritdoc/>
        public string? ChipTypeName => ReaderDevice.Instance?.GenericChip?.CardType.ToString();

        /// <inheritdoc/>
        public bool IsConnected => ReaderDevice.Instance?.IsConnected ?? false;

        /// <inheritdoc/>
        public event EventHandler? ReaderStateChanged;

        /// <summary>
        /// Called by the main application when reader or card state changes so that
        /// registered listeners receive <see cref="ReaderStateChanged"/>. Each subscriber runs in isolation: this is
        /// called from the main window's own property setters, so a throwing extension must not break them.
        /// </summary>
        internal void RaiseStateChanged()
        {
            var handlers = ReaderStateChanged;
            if (handlers == null)
            {
                return;
            }

            foreach (var handler in handlers.GetInvocationList().Cast<EventHandler>())
            {
                try
                {
                    handler(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    var error = new SanitizedException(ex);
                    Logger.Error(
                        "ReaderStateChanged subscriber threw; the exception was not propagated. {ExceptionType}: {ExceptionMessage} {ExceptionStackTrace}",
                        error.TypeName, error.Message, error.StackTrace);
                }
            }
        }
    }
}
