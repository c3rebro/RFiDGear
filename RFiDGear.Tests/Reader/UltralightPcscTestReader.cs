using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LibLogicalAccess;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Infrastructure.Ultralight;

namespace RFiDGear.Tests.Reader
{
    /// <summary>
    /// A PC/SC reader with a chip on it, in memory, behind the four calls that reach LibLogicalAccess. It names the chip the way
    /// LibLogicalAccess names it ("MifareUltralight", "MifareUltralightC", and for an Ultralight EV1 or an NTAG, which it does not
    /// know, "__UNSUPPORTED__UNKNOWN", as it was seen to do for an NTAG215), reads and writes one page at a time, and says no with
    /// the managed refusal that the page operations throw in place of the native card exception. Every call is recorded, with the
    /// key array that it received, so tests can see what was asked and that the array was cleared.
    /// </summary>
    internal sealed class UltralightPcscTestReader : LibLogicalAccessProvider
    {
        public UltralightPcscTestReader(UltralightVariant variant)
        {
            Variant = variant;
            CardType = variant == UltralightVariant.Plain ? "MifareUltralight"
                : variant == UltralightVariant.C ? "MifareUltralightC"
                : "__UNSUPPORTED__UNKNOWN";
            PageCount = UltralightMemoryMap.For(variant).PageCount;
            Memory = new byte[PageCount * 4];

            for (var page = 0; page < PageCount; page++)
            {
                Array.Copy(UltralightTestReader.PageOf(page), 0, Memory, page * 4, 4);
            }
        }

        public UltralightVariant Variant { get; }

        public string CardType { get; set; }

        public int PageCount { get; }

        public byte[] Memory { get; }

        public bool ChipOnReader { get; set; } = true;

        public Exception SelectThrows { get; set; }

        public HashSet<int> RefusedReads { get; } = new HashSet<int>();

        public HashSet<int> RefusedWrites { get; } = new HashSet<int>();

        public HashSet<int> IgnoredWrites { get; } = new HashSet<int>();

        public HashSet<int> EmptyReads { get; } = new HashSet<int>();

        public byte[] AcceptedKey { get; set; }

        /// <summary>The error code that a key the chip does not take is answered with.</summary>
        public CardException.ErrorType WrongKeyCode { get; set; } = CardException.ErrorType.AUTHENTICATION_FAILURE;

        public List<string> Calls { get; } = new List<string>();

        public List<byte[]> SecretsReceived { get; } = new List<byte[]>();

        /// <summary>The arrays that the writes received, kept as they were handed over (not copies) so a test can see that they were cleared afterwards.</summary>
        public List<byte[]> ChunksReceived { get; } = new List<byte[]>();

        public byte[] PagesOf(int startPage, int count)
        {
            var bytes = new byte[count * 4];
            Array.Copy(Memory, startPage * 4, bytes, 0, bytes.Length);

            return bytes;
        }

        public int CountStartingWith(string prefix) => Calls.Count(c => c.StartsWith(prefix, StringComparison.Ordinal));

        protected override Task<PcscUltralightSighting> SelectPcscUltralightChipAsync()
        {
            Calls.Add("select");

            if (SelectThrows != null)
            {
                throw SelectThrows;
            }

            return Task.FromResult(ChipOnReader ? new PcscUltralightSighting(true, CardType) : new PcscUltralightSighting(false, null));
        }

        /// <summary>When set, a read waits for it before it answers: a call that is held inside the reader, so a test can start another one.</summary>
        public TaskCompletionSource<bool> HoldReads { get; set; }

        protected override async Task<byte[]> ReadPcscUltralightPageAsync(int page)
        {
            Calls.Add("read " + page);

            if (HoldReads != null)
            {
                await HoldReads.Task;
            }

            if (RefusedReads.Contains(page) || page >= PageCount)
            {
                throw new PcscCardRefusal(CardException.ErrorType.SECURITY_STATUS);
            }

            if (EmptyReads.Contains(page))
            {
                return new byte[0];
            }

            return PagesOf(page, 1);
        }

        protected override Task WritePcscUltralightPageAsync(int page, byte[] data)
        {
            Calls.Add("write " + page);
            ChunksReceived.Add(data);

            if (RefusedWrites.Contains(page) || page >= PageCount)
            {
                throw new PcscCardRefusal(CardException.ErrorType.SECURITY_STATUS);
            }

            if (!IgnoredWrites.Contains(page))
            {
                Array.Copy(data, 0, Memory, page * 4, 4);
            }

            return Task.CompletedTask;
        }

        protected override Task AuthenticatePcscUltralightCAsync(byte[] key)
        {
            Calls.Add("authC");
            SecretsReceived.Add(key);

            if (AcceptedKey == null || !key.SequenceEqual(AcceptedKey))
            {
                throw new PcscCardRefusal(WrongKeyCode);
            }

            return Task.CompletedTask;
        }
    }
}
