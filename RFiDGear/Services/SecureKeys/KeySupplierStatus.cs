#nullable enable
using System;

namespace RFiDGear.Services.SecureKeys
{
    /// <summary>Whether a program can supply card keys through external control right now.</summary>
    internal enum KeySupplierAvailability
    {
        /// <summary>External control does not run, so no program can connect.</summary>
        Off,

        /// <summary>External control runs, but no connected program has registered as the supplier of keys.</summary>
        NoSupplier,

        /// <summary>A connected program has registered as the supplier of keys.</summary>
        Connected,
    }

    /// <summary>
    /// What a window needs to show whether a key supplier is connected, without the bridge behind it. A window subscribes while it
    /// is open and unsubscribes when it closes; the source lives as long as the application.
    /// </summary>
    internal interface IKeySupplierStatus
    {
        /// <summary>Gets whether a program can supply keys now.</summary>
        KeySupplierAvailability Current { get; }

        /// <summary>
        /// Raised when <see cref="Current"/> may have changed, on whatever thread caused it. A client that registers as supplier
        /// raises nothing, so a window that must show that asks <see cref="Current"/> again now and then.
        /// </summary>
        event EventHandler? Changed;
    }

    /// <summary>The status of the key supplier of external control, read from the built-in provider that stands for it.</summary>
    internal sealed class BridgeKeySupplierStatus : IKeySupplierStatus
    {
        private readonly BridgeKeyProvider _provider;

        /// <summary>Creates the status of the provider of this process.</summary>
        internal BridgeKeySupplierStatus()
            : this(BridgeKeyProvider.Shared)
        {
        }

        /// <summary>Creates the status of a provider.</summary>
        /// <param name="provider">The provider, which tells whether it is bound and whether the client supplies keys.</param>
        internal BridgeKeySupplierStatus(BridgeKeyProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <inheritdoc />
        public KeySupplierAvailability Current => _provider.Availability;

        /// <inheritdoc />
        public event EventHandler? Changed
        {
            add => _provider.AvailabilityChanged += value;
            remove => _provider.AvailabilityChanged -= value;
        }
    }
}
