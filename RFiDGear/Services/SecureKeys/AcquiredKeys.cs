#nullable enable
using System;
using System.Collections.Generic;

namespace RFiDGear.Services.SecureKeys
{
    /// <summary>
    /// Why a command could not get a key. Holds no reference and no key, so it may be logged and shown.
    /// </summary>
    /// <param name="Slot">The slot that failed.</param>
    /// <param name="ProviderId">The provider of the slot; empty when the binding itself is wrong.</param>
    /// <param name="Cause">A short name for the cause: a provider's failure reason, a problem with the binding, or <c>NoKeyContext</c>.</param>
    internal sealed record KeyFailure(string Slot, string ProviderId, string Cause);

    /// <summary>
    /// The keys one command works with, all obtained before it touches the card: the embedded key of a slot that has no
    /// binding, the key a provider handed out for one that has. It lives on the stack of the command. A command holds its
    /// keys only as long as it runs; nothing here is stored in the task, a log or a result.
    /// <para>
    /// The keys are hexadecimal text, because that is what the readers take. A text cannot be erased, so it stays in memory
    /// until the garbage collector reclaims it, in this object and in whatever the reader library copies.
    /// </para>
    /// </summary>
    internal sealed class AcquiredKeys
    {
        private readonly Dictionary<string, string?> _hex;
        private readonly HashSet<string> _bound;

        private AcquiredKeys(Dictionary<string, string?> hex, HashSet<string> bound, KeyFailure? failure)
        {
            _hex = hex;
            _bound = bound;
            Failure = failure;
        }

        /// <summary>Gets why the keys could not be obtained, or <c>null</c> when they were.</summary>
        internal KeyFailure? Failure { get; }

        /// <summary>Gets whether the keys could not be obtained. No card call may be made then.</summary>
        internal bool Failed => Failure != null;

        /// <summary>Creates the result of a successful acquisition.</summary>
        /// <param name="hex">The key of each slot; an embedded key is passed on as it is, even when it is empty.</param>
        /// <param name="bound">The slots whose key came from a provider.</param>
        /// <returns>The keys.</returns>
        internal static AcquiredKeys Obtained(Dictionary<string, string?> hex, HashSet<string> bound) => new AcquiredKeys(hex, bound, null);

        /// <summary>Creates the result of a failed acquisition. It holds no key.</summary>
        /// <param name="failure">What went wrong.</param>
        /// <returns>The failure.</returns>
        internal static AcquiredKeys Unavailable(KeyFailure failure) =>
            new AcquiredKeys(new Dictionary<string, string?>(), new HashSet<string>(StringComparer.Ordinal), failure);

        /// <summary>Returns whether the key of a slot came from a provider.</summary>
        /// <param name="slot">The slot id.</param>
        /// <returns><c>true</c> when it did.</returns>
        internal bool IsBound(string slot) => _bound.Contains(slot);

        /// <summary>Returns the key of a slot as hexadecimal text.</summary>
        /// <param name="slot">The slot id; it must be one the command asked for.</param>
        /// <returns>The key. An embedded key is returned as it is, so it can be <c>null</c> or empty.</returns>
        /// <exception cref="InvalidOperationException">The acquisition failed, or the command did not ask for the slot.</exception>
        internal string? Hex(string slot)
        {
            if (Failed)
            {
                throw new InvalidOperationException("The keys could not be obtained.");
            }

            return _hex.TryGetValue(slot, out var hex)
                ? hex
                : throw new InvalidOperationException($"The command did not ask for the key of slot {slot}.");
        }

        /// <inheritdoc />
        public override string ToString() => Failed ? $"AcquiredKeys [failed: {Failure!.Cause}]" : "AcquiredKeys [redacted]";
    }
}
