#nullable enable
using RFiDGear.Contracts;
using RFiDGear.Infrastructure;

namespace RFiDGear.Services.SecureKeys
{
    /// <summary>
    /// One key a command needs before it touches the card: which slot, which kind of key it is, and what the key is for. A
    /// command lists everything it needs up front, so a missing key stops it before the first card call.
    /// </summary>
    /// <param name="Slot">The slot id; see <see cref="SecureKeySlots"/>.</param>
    /// <param name="KeyType">The type of the key. It fixes how long the key must be.</param>
    /// <param name="Usage">What the key does in the operation.</param>
    /// <param name="ApplicationId">The DESFire application the key belongs to (0 for the card itself), or <c>null</c>.</param>
    /// <param name="KeyNumber">The number of the key within its application, or <c>null</c>.</param>
    internal readonly record struct KeyNeed(string Slot, DESFireKeyType KeyType, SecureKeyUsage Usage, uint? ApplicationId, int? KeyNumber);
}
