#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using RFiDGear.Infrastructure;

namespace RFiDGear.Services.SecureKeys
{
    /// <summary>A key store as the person sees it: its name and a tooltip, in the language of the application.</summary>
    /// <param name="Id">The provider id stored in projects.</param>
    /// <param name="DisplayName">The name shown to the person.</param>
    /// <param name="ToolTip">What the key store is, in a sentence or two.</param>
    /// <param name="IsBuiltIn">Whether RFiDGear brings the key store itself (an extension does not).</param>
    internal sealed record KeySlotProviderItem(string Id, string DisplayName, string ToolTip, bool IsBuiltIn);

    /// <summary>
    /// Turns the providers of the registry into what the key manager lists. The two built-in providers get a name and a tooltip
    /// in the language of the application; an extension keeps the name it exports (the SDK carries no text to translate) and
    /// gets a generic tooltip that names its id. Nothing here touches the registry or the SDK.
    /// </summary>
    internal sealed class KeySlotProviderCatalog
    {
        private readonly Func<string, string> _text;

        /// <summary>Creates the catalog.</summary>
        /// <param name="text">Gives the text of a resource key; <c>null</c> for the application's resources.</param>
        internal KeySlotProviderCatalog(Func<string, string>? text = null)
        {
            _text = text ?? ResourceLoader.GetResource;
        }

        /// <summary>
        /// Describes the providers for the person: the Windows Credential Manager first (it is the default), then the connected
        /// external application, then the extensions by name.
        /// </summary>
        /// <param name="providers">The providers of the registry.</param>
        /// <returns>One item per provider, in the order to list them.</returns>
        internal IReadOnlyList<KeySlotProviderItem> Describe(IEnumerable<SecureKeyProviderInfo> providers)
        {
            ArgumentNullException.ThrowIfNull(providers);

            return providers
                .Select(Describe)
                .OrderBy(item => Rank(item.Id))
                .ThenBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        /// <summary>Gives the text that says whether a program can supply keys.</summary>
        /// <param name="availability">What the bridge says.</param>
        /// <returns>The sentence, in the language of the application.</returns>
        internal string StatusText(KeySupplierAvailability availability) => availability switch
        {
            KeySupplierAvailability.Connected => _text("keySlotSupplierStatusConnected"),
            KeySupplierAvailability.NoSupplier => _text("keySlotSupplierStatusNoSupplier"),
            _ => _text("keySlotSupplierStatusOff"),
        };

        private KeySlotProviderItem Describe(SecureKeyProviderInfo info)
        {
            switch (info.Id)
            {
                case WindowsCredentialKeyProvider.ProviderId:
                    return new KeySlotProviderItem(
                        info.Id,
                        _text("keySlotProviderNameCredentialManager"),
                        _text("keySlotProviderToolTipCredentialManager"),
                        true);

                case BridgeKeyProvider.ProviderId:
                    return new KeySlotProviderItem(
                        info.Id,
                        _text("keySlotProviderNameBridgeClient"),
                        _text("keySlotProviderToolTipBridgeClient"),
                        true);

                default:
                    return new KeySlotProviderItem(
                        info.Id,
                        string.IsNullOrWhiteSpace(info.DisplayName) ? info.Id : info.DisplayName,
                        string.Format(CultureInfo.CurrentCulture, _text("keySlotProviderToolTipExtension"), info.Id),
                        false);
            }
        }

        private static int Rank(string id) => id switch
        {
            WindowsCredentialKeyProvider.ProviderId => 0,
            BridgeKeyProvider.ProviderId => 1,
            _ => 2,
        };
    }
}
