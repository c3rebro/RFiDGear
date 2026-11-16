#nullable enable
using RFiDGear.Services.Sam;

namespace RFiDGear.Infrastructure.ReaderProviders
{
    /// <summary>Which key of a DESFire card is authenticated with a key entry of a MIFARE SAM AV3. It holds no key: the key stays in the SAM.</summary>
    /// <param name="AppId">The application, 0 for the card itself.</param>
    /// <param name="KeyNumber">The number of the key on the card, 0 to 13.</param>
    /// <param name="KeyType">The kind of the key on the card: DES (with 2K3DES) or AES, which is what a key entry of the SAM can be.</param>
    /// <param name="Key">The key entry of the SAM and the version of the key in it.</param>
    public readonly record struct DesfireSamAccess(uint AppId, byte KeyNumber, DESFireKeyType KeyType, SamKeyReference Key);

    /// <summary>The change of a key of a DESFire card with keys of a SAM: which key authenticates, which key is replaced, and by which key.</summary>
    /// <param name="AppId">The application, 0 for the card itself.</param>
    /// <param name="AuthKeyNumber">The number of the key on the card that authenticates.</param>
    /// <param name="AuthKeyType">The kind of the key that authenticates.</param>
    /// <param name="AuthKey">The key of the SAM that authenticates.</param>
    /// <param name="TargetKeyNumber">The number of the key on the card that is replaced.</param>
    /// <param name="NewKeyType">The kind of the new key (for the card master key, the kind that the card gets).</param>
    /// <param name="CurrentKey">The key of the SAM that the card holds now at <paramref name="TargetKeyNumber"/>.</param>
    /// <param name="NewKey">The key of the SAM that the card gets.</param>
    public readonly record struct DesfireSamKeyChange(
        uint AppId,
        byte AuthKeyNumber,
        DESFireKeyType AuthKeyType,
        SamKeyReference AuthKey,
        byte TargetKeyNumber,
        DESFireKeyType NewKeyType,
        SamKeyReference CurrentKey,
        SamKeyReference NewKey);

    /// <summary>What a reader needs to know about a request before it opens a SAM or touches a card.</summary>
    internal static class DesfireSamRequests
    {
        /// <summary>Maps the key type of a task to the kind the SAM works with.</summary>
        /// <param name="type">The key type of the task.</param>
        /// <param name="kind">The kind for the SAM.</param>
        /// <returns><see langword="false"/> for a key that a key entry of the SAM cannot be (3K3DES).</returns>
        internal static bool TryKind(DESFireKeyType type, out SamPiccKeyType kind)
        {
            switch (type)
            {
                case DESFireKeyType.DF_KEY_DES:
                    kind = SamPiccKeyType.NativeTdea;

                    return true;

                case DESFireKeyType.DF_KEY_AES:
                    kind = SamPiccKeyType.Aes128;

                    return true;

                default:
                    kind = default;

                    return false;
            }
        }

        /// <summary>Tells whether LibLogicalAccess names a card a DESFire card: it says "DESFire", and "DESFireEV1", "DESFireEV2" or "DESFireEV3" once the card is connected.</summary>
        /// <param name="cardType">The name that the library gives the card.</param>
        /// <returns><see langword="true"/> for a DESFire card.</returns>
        internal static bool IsDesfireCardType(string? cardType) =>
            cardType != null && cardType.StartsWith("DESFire", System.StringComparison.Ordinal);

        /// <summary>Turns a request to authenticate into what the session takes.</summary>
        /// <param name="access">The request.</param>
        /// <param name="kind">The kind of the key.</param>
        /// <returns><see langword="false"/> when the request cannot be made at all.</returns>
        internal static bool TryAuthenticate(DesfireSamAccess access, out SamPiccKeyType kind) => TryKind(access.KeyType, out kind);

        /// <summary>Turns a request to change a key into what the session takes; the key is always checked afterwards.</summary>
        /// <param name="change">The request.</param>
        /// <param name="result">What the session takes.</param>
        /// <returns><see langword="false"/> when the request cannot be made at all (a key that is no DES or AES key).</returns>
        internal static bool TryChange(DesfireSamKeyChange change, out SamPiccKeyChange result)
        {
            result = default;

            if (!TryKind(change.AuthKeyType, out var authKind) || !TryKind(change.NewKeyType, out var newKind))
            {
                return false;
            }

            result = new SamPiccKeyChange(
                change.AppId,
                change.AuthKeyNumber,
                authKind,
                change.AuthKey,
                change.TargetKeyNumber,
                newKind,
                change.CurrentKey,
                change.NewKey,
                Verify: true);

            return true;
        }
    }
}