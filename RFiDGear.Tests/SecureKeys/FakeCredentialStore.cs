using RFiDGear.Services.SecureKeys;

using System;
using System.Collections.Generic;
using System.Linq;

namespace RFiDGear.Tests.SecureKeys
{
    /// <summary>A vault that is a dictionary, and notes what it was asked.</summary>
    internal sealed class FakeCredentialStore : ICredentialStore
    {
        public Dictionary<string, byte[]> Blobs { get; } = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        public List<string> Reads { get; } = new List<string>();

        public List<string> Writes { get; } = new List<string>();

        public List<string> Deletes { get; } = new List<string>();

        /// <summary>Gets the copies of secrets that were handed out, to see whether they were erased.</summary>
        public List<byte[]> HandedOut { get; } = new List<byte[]>();

        /// <summary>Gets the blobs that were written, to see whether they were erased.</summary>
        public List<byte[]> Written { get; } = new List<byte[]>();

        /// <summary>Gets the prefixes that a listing was asked for.</summary>
        public List<string> Enumerated { get; } = new List<string>();

        public int? ReadError { get; set; }

        public int WriteError { get; set; }

        public int EnumerateError { get; set; }

        /// <summary>Gets or sets the error that a delete answers with, without deleting anything.</summary>
        public int DeleteError { get; set; }

        /// <summary>Gets or sets whether a listing hands out every name, as a vault that does not apply its filter would.</summary>
        public bool EnumerateIgnoresPrefix { get; set; }

        public string? ThrowText { get; set; }

        public CredentialRead Read(string targetName)
        {
            Reads.Add(targetName);

            if (ThrowText != null)
            {
                throw new InvalidOperationException(ThrowText);
            }

            if (ReadError != null)
            {
                return new CredentialRead(ReadError.Value, null);
            }

            if (!Blobs.TryGetValue(targetName, out var blob))
            {
                return new CredentialRead(CredentialErrors.NotFound, null);
            }

            var copy = (byte[])blob.Clone();
            HandedOut.Add(copy);

            return new CredentialRead(0, copy);
        }

        public int Write(string targetName, byte[] blob)
        {
            Writes.Add(targetName);

            if (ThrowText != null)
            {
                throw new InvalidOperationException(ThrowText);
            }

            // The array is the caller's and erased after the call: what is kept is a copy, what is noted is the array.
            Written.Add(blob);

            if (WriteError == 0)
            {
                Blobs[targetName] = (byte[])blob.Clone();
            }

            return WriteError;
        }

        public int Delete(string targetName)
        {
            Deletes.Add(targetName);

            if (ThrowText != null)
            {
                throw new InvalidOperationException(ThrowText);
            }

            if (DeleteError != 0)
            {
                return DeleteError;
            }

            return Blobs.Remove(targetName) ? 0 : CredentialErrors.NotFound;
        }

        public CredentialList Enumerate(string targetPrefix)
        {
            Enumerated.Add(targetPrefix);

            if (ThrowText != null)
            {
                throw new InvalidOperationException(ThrowText);
            }

            if (EnumerateError != 0)
            {
                return new CredentialList(EnumerateError, Array.Empty<string>());
            }

            // Only names are handed out, never a blob: the secret of a credential is not part of a listing.
            return new CredentialList(
                0,
                Blobs.Keys.Where(name => EnumerateIgnoresPrefix || name.StartsWith(targetPrefix, StringComparison.OrdinalIgnoreCase)).ToList());
        }
    }
}
