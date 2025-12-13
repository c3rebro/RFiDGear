using System.Collections.Generic;
using RFiDGear.Infrastructure.AccessControl;

namespace RFiDGear.Infrastructure.ReaderProviders
{
    /// <summary>
    /// Resolves the parameters required for DESFire key settings updates, providing warnings when
    /// live card information is unavailable and configured defaults are used instead.
    /// </summary>
    internal static class DesfireKeySettingsResolver
    {
        /// <summary>
        /// Resolves the key settings payload using live card values when available or falling back to
        /// configured defaults while surfacing informational warnings.
        /// </summary>
        /// <param name="requestedSettings">The key settings requested by the caller.</param>
        /// <param name="cardKeyCount">The key count read from the card, if available.</param>
        /// <param name="cardKeyType">The key type read from the card, if available.</param>
        /// <param name="configuredKeyCount">The configured fallback key count.</param>
        /// <param name="configuredKeyType">The configured fallback key type.</param>
        /// <returns>A resolution result containing the applied values and any warnings.</returns>
        internal static DesfireKeySettingsResolution Resolve(
            DESFireKeySettings requestedSettings,
            byte? cardKeyCount,
            DESFireKeyType? cardKeyType,
            byte configuredKeyCount,
            DESFireKeyType configuredKeyType)
        {
            var warnings = new List<string>();
            var resolvedKeyCount = cardKeyCount ?? configuredKeyCount;
            if (!cardKeyCount.HasValue)
            {
                warnings.Add("Key count unavailable after authentication; using configured value instead.");
            }

            var resolvedKeyType = cardKeyType ?? configuredKeyType;
            if (!cardKeyType.HasValue)
            {
                warnings.Add("Key type unavailable after authentication; using configured value instead.");
            }

            if (resolvedKeyCount == 0)
            {
                warnings.Add("Resolved DESFire key count is zero; reader defaults may apply.");
            }

            return new DesfireKeySettingsResolution(requestedSettings, resolvedKeyCount, resolvedKeyType, warnings);
        }
    }

    /// <summary>
    /// Container describing the resolved key settings payload and any associated warnings.
    /// </summary>
    internal sealed class DesfireKeySettingsResolution
    {
        internal DesfireKeySettingsResolution(
            DESFireKeySettings settings,
            byte keyCount,
            DESFireKeyType keyType,
            IReadOnlyList<string> warnings)
        {
            Settings = settings;
            KeyCount = keyCount;
            KeyType = keyType;
            Warnings = warnings;
        }

        /// <summary>
        /// The key settings byte to apply.
        /// </summary>
        internal DESFireKeySettings Settings { get; }

        /// <summary>
        /// The resolved key count used when issuing the change command.
        /// </summary>
        internal byte KeyCount { get; }

        /// <summary>
        /// The resolved key type used when issuing the change command.
        /// </summary>
        internal DESFireKeyType KeyType { get; }

        /// <summary>
        /// Any warnings generated while resolving settings values.
        /// </summary>
        internal IReadOnlyList<string> Warnings { get; }
    }
}
