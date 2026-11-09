using System;
using System.Collections.Generic;
using System.Linq;

namespace RFiDGear.Contracts
{
    /// <summary>
    /// An immutable picture of the reader and the card on it at one moment, as one consistent set of values. It carries
    /// no keys, no card data and no reference to RFiDGear's own models, so it is safe to keep, to pass between threads
    /// and to compare: two snapshots are equal when they hold the same values.
    /// </summary>
    public sealed class ReaderSnapshot : IEquatable<ReaderSnapshot>
    {
        /// <summary>Creates a snapshot. The credentials are copied, so later changes to the argument do not reach it.</summary>
        /// <param name="readerName">The reader's display name; <c>null</c> is taken as empty.</param>
        /// <param name="state">What the look at the reader found.</param>
        /// <param name="credentials">What is on the reader, the main credential first. Must be empty unless <paramref name="state"/> is <see cref="ReaderScanState.Ready"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="credentials"/> is null or holds a null entry.</exception>
        /// <exception cref="ArgumentException">Credentials are given although <paramref name="state"/> is not <see cref="ReaderScanState.Ready"/>.</exception>
        public ReaderSnapshot(string? readerName, ReaderScanState state, IEnumerable<CredentialInfo> credentials)
        {
            if (credentials == null)
            {
                throw new ArgumentNullException(nameof(credentials));
            }

            var copy = credentials.ToArray();
            if (copy.Any(credential => credential == null))
            {
                throw new ArgumentNullException(nameof(credentials), "A credential is null.");
            }

            if (state != ReaderScanState.Ready && copy.Length > 0)
            {
                throw new ArgumentException("Only a snapshot in the state Ready can list credentials.", nameof(credentials));
            }

            ReaderName = readerName ?? string.Empty;
            State = state;
            Credentials = Array.AsReadOnly(copy);
        }

        /// <summary>The reader's display name, as <see cref="IReaderHost.ReaderName"/> gives it; empty if unknown.</summary>
        public string ReaderName { get; }

        /// <summary>What the look at the reader found.</summary>
        public ReaderScanState State { get; }

        /// <summary>
        /// What is on the reader, the main credential first (its <see cref="CredentialInfo.IsPrimary"/> is <c>true</c>).
        /// Empty unless <see cref="State"/> is <see cref="ReaderScanState.Ready"/>, and also empty when the reader was read
        /// and there is no card on it (with a PC/SC reader it can also mean a card that could not be read).
        /// </summary>
        public IReadOnlyList<CredentialInfo> Credentials { get; }

        /// <summary>
        /// <c>true</c> when the reader was read and a card is on it. <c>false</c> means "no card" only when
        /// <see cref="State"/> is <see cref="ReaderScanState.Ready"/>, and with a PC/SC reader not even then (see
        /// <see cref="IReaderHost.ScanAsync"/>).
        /// </summary>
        public bool HasCard => Credentials.Count > 0;

        /// <inheritdoc/>
        public bool Equals(ReaderSnapshot? other) =>
            other != null
            && State == other.State
            && string.Equals(ReaderName, other.ReaderName, StringComparison.Ordinal)
            && Credentials.SequenceEqual(other.Credentials);

        /// <inheritdoc/>
        public override bool Equals(object? obj) => Equals(obj as ReaderSnapshot);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(State);
            hash.Add(ReaderName, StringComparer.Ordinal);
            foreach (var credential in Credentials)
            {
                hash.Add(credential);
            }

            return hash.ToHashCode();
        }

        /// <summary>A short description for logs: the state and how many credentials; no identifiers.</summary>
        /// <returns>The description.</returns>
        public override string ToString() => $"{State}, {Credentials.Count} credential(s)";
    }
}
