using RFiDGear.Services.SecureKeys;

using System;
using System.Collections.Generic;

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

        public int? ReadError { get; set; }

        public int WriteError { get; set; }

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

            return Blobs.Remove(targetName) ? 0 : CredentialErrors.NotFound;
        }
    }
}
