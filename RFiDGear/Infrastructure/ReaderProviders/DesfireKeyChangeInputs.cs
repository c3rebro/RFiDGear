using System;
using System.Collections.Generic;

namespace RFiDGear.Infrastructure.ReaderProviders; 

internal static class DesfireKeyChangeInputs
{
    /// <summary>
    /// Resolved, provider-ready values for a DESFire key change.
    /// </summary>
    internal sealed record Resolved(
        uint AppId,
        byte TargetKeyNo,
        DESFireKeyType TargetKeyType,
        string OldTargetKeyHex,
        string NewTargetKeyHex,
        byte NewTargetKeyVersion,
        byte AuthKeyNo,
        DESFireKeyType AuthKeyType,
        string AuthKeyHex,
        byte KeySettingsByteOnWire);

    /// <summary>
    /// Resolves authentication parameters and normalizes key material for a DESFire key change.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method applies your in-app policy for selecting the authentication key number from
    /// <paramref name="keySettings"/>:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Authenticate with key 0 (master) unless "change-with-targeted-key" policy is active.</description></item>
    ///   <item><description>If "change-with-targeted-key" is active, authenticate with <paramref name="targetKeyNo"/> using the current target key.</description></item>
    /// </list>
    /// <para>
    /// It also normalizes hex strings to a spaced format (<c>"00 11 22"</c>) for downstream libraries that expect it.
    /// </para>
    /// <para>
    /// When changing the PICC master key (AppID 0, KeyNo 0), a missing <paramref name="currentTargetKeyHex"/> is
    /// treated as the current master key value provided in <paramref name="masterKeyHex"/>.
    /// </para>
    /// </remarks>
    /// <param name="appId">Application identifier (0 = PICC, &gt;0 = application).</param>
    /// <param name="targetKeyNo">Target key number to change.</param>
    /// <param name="targetKeyType">Type of the target key.</param>
    /// <param name="currentTargetKeyHex">Current (old) key value for the target slot.</param>
    /// <param name="newTargetKeyHex">New key value to set.</param>
    /// <param name="newTargetKeyVersion">Version byte for the new key.</param>
    /// <param name="masterKeyHex">Master key value (key 0) for the selected scope.</param>
    /// <param name="masterKeyType">Type of the master key.</param>
    /// <param name="keySettings">
    /// Current key settings for the selected scope; used to derive the authentication key number.
    /// </param>
    /// <returns>A resolved structure describing authentication and payload inputs.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when required key materials are missing or hex strings are malformed.
    /// </exception>
    internal static Resolved Resolve(
        uint appId,
        byte targetKeyNo,
        DESFireKeyType targetKeyType,
        string currentTargetKeyHex,
        string newTargetKeyHex,
        byte newTargetKeyVersion,
        string masterKeyHex,
        DESFireKeyType masterKeyType,
        AccessControl.DESFireKeySettings keySettings)
    {
        if (string.IsNullOrWhiteSpace(masterKeyHex))
            throw new ArgumentException("Master key must be provided.", nameof(masterKeyHex));
        if (string.IsNullOrWhiteSpace(currentTargetKeyHex) && appId == 0 && targetKeyNo == 0)
            currentTargetKeyHex = masterKeyHex;
        if (string.IsNullOrWhiteSpace(currentTargetKeyHex))
            throw new ArgumentException("Current target key must be provided (old key value).", nameof(currentTargetKeyHex));
        if (string.IsNullOrWhiteSpace(newTargetKeyHex))
            throw new ArgumentException("New target key must be provided.", nameof(newTargetKeyHex));

        var changeKeyMode = keySettings & AccessControl.DESFireKeySettings.ChangeKeyFrozen;
        var authKeyNo = changeKeyMode == AccessControl.DESFireKeySettings.ChangeKeyWithTargetedKeyNumber
            ? targetKeyNo
            : (byte)0;

        // Authenticate with master key (key 0) OR with the target key (self-key mode).
        var authKeyHex = authKeyNo == 0 ? masterKeyHex : currentTargetKeyHex;
        var authKeyType = authKeyNo == 0 ? masterKeyType : targetKeyType;

        // Preserve your existing on-wire masking behavior at PICC level.
        var keySettingsByte = appId == 0
            ? (byte)((byte)keySettings & 0x0F)
            : (byte)keySettings;

        return new Resolved(
            appId,
            targetKeyNo,
            targetKeyType,
            NormalizeKeyHex(currentTargetKeyHex),
            NormalizeKeyHex(newTargetKeyHex),
            newTargetKeyVersion,
            authKeyNo,
            authKeyType,
            NormalizeKeyHex(authKeyHex),
            keySettingsByte);
    }

    /// <summary>
    /// Normalizes a hex string to a spaced, uppercase representation: <c>"001122"</c> or <c>"00 11 22"</c> becomes <c>"00 11 22"</c>.
    /// </summary>
    /// <param name="hex">Hex-encoded bytes, with or without spaces.</param>
    /// <returns>Normalized hex bytes separated by spaces.</returns>
    /// <exception cref="ArgumentException">Thrown when the input has an odd number of hex characters.</exception>
    internal static string NormalizeKeyHex(string hex)
    {
        var s = hex.Replace(" ", "").Trim();
        if (s.Length % 2 != 0)
            throw new ArgumentException("Hex string must have an even number of characters.", nameof(hex));

        var parts = new List<string>(s.Length / 2);
        for (int i = 0; i < s.Length; i += 2)
            parts.Add(s.Substring(i, 2));

        return string.Join(" ", parts).ToUpperInvariant();
    }
}
