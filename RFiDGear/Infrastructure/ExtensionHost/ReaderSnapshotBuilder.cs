#nullable enable
using RFiDGear.Contracts;
using RFiDGear.Models;

using System;
using System.Collections.Generic;

namespace RFiDGear.Infrastructure.ExtensionHost
{
    /// <summary>
    /// Turns the chip tree the reader providers fill (<see cref="GenericChipModel"/> and its <c>Childs</c>) into the
    /// immutable <see cref="ReaderSnapshot"/> extensions see. It copies what it needs and keeps no reference to the tree,
    /// and it takes no keys or card data because the tree holds none.
    /// <para>
    /// The provider's thread may still be filling the tree while it is read here, so every value is read once, the
    /// children are read by index and a list that gets shorter meanwhile gives what was read before, and a node reached
    /// twice is skipped. That makes the build safe, not exact: only a build right after the read, on the thread that made
    /// it, sees the tree as the read left it.
    /// </para>
    /// </summary>
    internal static class ReaderSnapshotBuilder
    {
        /// <summary>The name given to a card type that has no name in <see cref="CARD_TYPE"/>.</summary>
        internal const string UnknownTypeName = "Unknown";

        /// <summary>Builds the snapshot of a reader and the chip it last read.</summary>
        /// <param name="readerName">The reader's display name; <c>null</c> is taken as empty.</param>
        /// <param name="isConnected">Whether a reader is connected. If not, a chip left over from earlier is not a card.</param>
        /// <param name="chip">The chip the reader last read, or <c>null</c>.</param>
        /// <returns>A snapshot in the state <see cref="ReaderScanState.NoReader"/> or <see cref="ReaderScanState.Ready"/>.</returns>
        internal static ReaderSnapshot Build(string? readerName, bool isConnected, GenericChipModel? chip) =>
            isConnected
                ? new ReaderSnapshot(readerName ?? string.Empty, ReaderScanState.Ready, CredentialsOf(chip))
                : NoReader(readerName);

        /// <summary>
        /// The one place that says what the result of a read means. The providers never throw: an exception inside
        /// them comes back as <see cref="ERROR.TransportError"/>, and no card is not an error of its own.
        /// <list type="bullet">
        /// <item>No device: no reader.</item>
        /// <item>
        /// Elatec: a reader that is not connected after the read is no reader, whatever the result (a reader that is
        /// not attached also answers <see cref="ERROR.NoError"/> with an empty chip); a connected reader with
        /// <see cref="ERROR.NoError"/> was read (an empty chip then means no card); any other result is a failed read.
        /// </item>
        /// <item>
        /// PC/SC: <c>IsConnected</c> only means that a card session exists, so it says nothing about the reader and is
        /// not used. <see cref="ERROR.NoError"/> was read; <see cref="ERROR.TransportError"/> is what "no card" also
        /// gives (as does a missing reader), so it counts as no card, which is the documented best effort; any other
        /// result is a failed read.
        /// </item>
        /// </list>
        /// </summary>
        /// <param name="readerName">The reader's display name; <c>null</c> is taken as empty.</param>
        /// <param name="hasDevice">Whether the application had a reader device to read with.</param>
        /// <param name="isPcsc">Whether that device is a PC/SC reader.</param>
        /// <param name="isConnected">The device's <c>IsConnected</c> after the read.</param>
        /// <param name="result">What the read returned.</param>
        /// <param name="chip">The chip the read left, only looked at when the reader was read.</param>
        /// <returns>The snapshot; its state is <c>NoReader</c>, <c>Ready</c> or <c>ReadFailed</c>.</returns>
        internal static ReaderSnapshot FromRead(string? readerName, bool hasDevice, bool isPcsc, bool isConnected, ERROR result, GenericChipModel? chip)
        {
            if (!hasDevice)
            {
                return NoReader(readerName);
            }

            if (isPcsc)
            {
                return result switch
                {
                    ERROR.NoError => new ReaderSnapshot(readerName, ReaderScanState.Ready, CredentialsOf(chip)),
                    ERROR.TransportError => new ReaderSnapshot(readerName, ReaderScanState.Ready, Array.Empty<CredentialInfo>()),
                    _ => ReadFailed(readerName),
                };
            }

            if (!isConnected)
            {
                return NoReader(readerName);
            }

            return result == ERROR.NoError
                ? new ReaderSnapshot(readerName, ReaderScanState.Ready, CredentialsOf(chip))
                : ReadFailed(readerName);
        }

        /// <summary>The answer when the reader was not read because the host is busy.</summary>
        /// <param name="readerName">The reader's display name; <c>null</c> is taken as empty.</param>
        /// <returns>A snapshot in the state <see cref="ReaderScanState.Busy"/>.</returns>
        internal static ReaderSnapshot Busy(string? readerName) =>
            new ReaderSnapshot(readerName, ReaderScanState.Busy, Array.Empty<CredentialInfo>());

        /// <summary>The answer when reading the reader failed.</summary>
        /// <param name="readerName">The reader's display name; <c>null</c> is taken as empty.</param>
        /// <returns>A snapshot in the state <see cref="ReaderScanState.ReadFailed"/>.</returns>
        internal static ReaderSnapshot ReadFailed(string? readerName) =>
            new ReaderSnapshot(readerName, ReaderScanState.ReadFailed, Array.Empty<CredentialInfo>());

        /// <summary>The snapshot of a host that has no reader to look at.</summary>
        /// <param name="readerName">The reader's display name; <c>null</c> is taken as empty.</param>
        /// <returns>A snapshot in the state <see cref="ReaderScanState.NoReader"/>.</returns>
        internal static ReaderSnapshot NoReader(string? readerName) =>
            new ReaderSnapshot(readerName ?? string.Empty, ReaderScanState.NoReader, Array.Empty<CredentialInfo>());

        /// <summary>
        /// The UID of the primary credential of a chip tree, written exactly as <see cref="CredentialInfo.Uid"/> and
        /// <see cref="ReaderSnapshot"/> report it (trimmed, upper case). It is the one place that says which UID stands
        /// for a card, so the polls of automatic mode, the card it remembers and <see cref="ExecResult.CardUid"/> agree.
        /// </summary>
        /// <param name="chip">The root of the tree, or <c>null</c>.</param>
        /// <returns>The UID; <c>null</c> when the chip is <c>null</c> or holds no card.</returns>
        internal static string? PrimaryUidOf(GenericChipModel? chip)
        {
            var credentials = CredentialsOf(chip);

            return credentials.Count > 0 ? credentials[0].Uid : null;
        }

        /// <summary>
        /// Lists the credentials of a chip tree, depth first, the chip itself before its children. A node without a UID
        /// is not a credential, but its children still are. The first credential is the primary one.
        /// </summary>
        /// <param name="chip">The root of the tree, or <c>null</c>.</param>
        /// <returns>The credentials; empty if there is none.</returns>
        internal static IReadOnlyList<CredentialInfo> CredentialsOf(GenericChipModel? chip)
        {
            var credentials = new List<CredentialInfo>();
            if (chip == null)
            {
                return credentials;
            }

            var seen = new HashSet<GenericChipModel>();
            var pending = new Stack<GenericChipModel>();
            pending.Push(chip);
            while (pending.Count > 0)
            {
                var node = pending.Pop();
                if (!seen.Add(node))
                {
                    continue;
                }

                var uid = node.UID?.Trim();
                var type = node.CardType;
                if (!string.IsNullOrEmpty(uid))
                {
                    credentials.Add(new CredentialInfo(uid.ToUpperInvariant(), (int)type, NameOf(type), credentials.Count == 0));
                }

                var children = ChildrenOf(node.Childs);
                for (var i = children.Count - 1; i >= 0; i--)
                {
                    pending.Push(children[i]);
                }
            }

            return credentials;
        }

        // Enum.GetName gives one of the names when several share a value (DESFire and DESFireEV0, for instance), and none
        // for a number that is no member, which a [Flags] enum can hold. The code is what identifies the type.
        private static string NameOf(CARD_TYPE type) => Enum.GetName(type) ?? UnknownTypeName;

        /// <summary>
        /// Copies the children of a node, one by one by index. The list may be changed by another thread while it is read:
        /// one that grows gives what was there when the count was read, and one that gets shorter makes the indexer throw,
        /// which ends the copy with what was read before. (A foreach over it, or <c>ToArray</c>, would throw at other
        /// moments of the same race.)
        /// </summary>
        /// <param name="list">The children, or <c>null</c>.</param>
        /// <returns>The children that could be read, without null entries.</returns>
        internal static List<GenericChipModel> ChildrenOf(IReadOnlyList<GenericChipModel>? list)
        {
            var children = new List<GenericChipModel>();
            if (list == null)
            {
                return children;
            }

            try
            {
                var count = list.Count;
                for (var i = 0; i < count; i++)
                {
                    var child = list[i];
                    if (child != null)
                    {
                        children.Add(child);
                    }
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                // The list got shorter while it was read: what was read before is what there is.
            }

            return children;
        }
    }
}
