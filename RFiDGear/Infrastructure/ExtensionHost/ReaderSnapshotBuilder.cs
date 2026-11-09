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

        /// <summary>The snapshot of a host that has no reader to look at.</summary>
        /// <param name="readerName">The reader's display name; <c>null</c> is taken as empty.</param>
        /// <returns>A snapshot in the state <see cref="ReaderScanState.NoReader"/>.</returns>
        internal static ReaderSnapshot NoReader(string? readerName) =>
            new ReaderSnapshot(readerName ?? string.Empty, ReaderScanState.NoReader, Array.Empty<CredentialInfo>());

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
