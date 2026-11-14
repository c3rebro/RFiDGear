using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Elatec.NET;
using Elatec.NET.Cards.Mifare;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Infrastructure.Ultralight;

namespace RFiDGear.Tests.Reader
{
    /// <summary>
    /// An Elatec reader with a chip of the Ultralight family on it, in memory. It answers the way the TWN4 was seen to answer a real
    /// NTAG215: a READ returns 16 bytes (four pages, and from page 0 again past the last page), a refused READ returns <c>null</c>, a
    /// refused WRITE and a refused authentication throw <c>ReaderException</c> with the text "error AccessDenied" and an
    /// <c>ErrorNumber</c> of <c>None</c>, and after any refusal the chip is idle: every call fails until the tag is selected again.
    /// Every call is recorded, with the key arrays that it received, so tests can see what was asked and that the arrays were cleared.
    /// </summary>
    internal sealed class UltralightTestReader : ElatecNetProvider
    {
        private bool _idle;

        public UltralightTestReader(UltralightVariant variant)
        {
            Variant = variant;
            PageCount = UltralightMemoryMap.For(variant).PageCount;
            Memory = new byte[PageCount * 4];

            for (var page = 0; page < PageCount; page++)
            {
                Array.Copy(PageOf(page), 0, Memory, page * 4, 4);
            }

            switch (variant)
            {
                case UltralightVariant.C:
                    SubType = MifareChipSubType.MifareUltralightC;
                    break;

                case UltralightVariant.Plain:
                    VersionAnswer = null;
                    break;

                case UltralightVariant.Ev1Mf0ul11:
                    VersionAnswer = Convert.FromHexString("0004030101000B03");
                    break;

                case UltralightVariant.Ev1Mf0ul21:
                    VersionAnswer = Convert.FromHexString("0004030101000E03");
                    break;

                case UltralightVariant.Ntag213:
                    VersionAnswer = Convert.FromHexString("0004040201000F03");
                    break;

                case UltralightVariant.Ntag215:
                    VersionAnswer = Convert.FromHexString("0004040201001103");
                    break;

                default:
                    VersionAnswer = Convert.FromHexString("0004040201001303");
                    break;
            }
        }

        public UltralightVariant Variant { get; }

        public int PageCount { get; }

        public byte[] Memory { get; }

        public bool Connected { get; set; } = true;

        public bool TagOnReader { get; set; } = true;

        public MifareChipSubType? SubType { get; set; } = MifareChipSubType.MifareUltralight;

        public byte[] VersionAnswer { get; set; }

        public HashSet<int> RefusedReads { get; } = new HashSet<int>();

        public HashSet<int> RefusedWrites { get; } = new HashSet<int>();

        public HashSet<int> IgnoredWrites { get; } = new HashSet<int>();

        public bool RemoveTagWhenAReadIsRefused { get; set; }

        public Exception SelectThrows { get; set; }

        public byte[] AcceptedKey { get; set; }

        public List<string> Calls { get; } = new List<string>();

        public List<byte[]> SecretsReceived { get; } = new List<byte[]>();

        public override bool IsConnected => Connected;

        protected override TimeSpan UltralightSelectRetryDelay => TimeSpan.Zero;

        /// <summary>What a page holds when the chip is new: a pattern that differs per page.</summary>
        public static byte[] PageOf(int page) => new[] { (byte)(page * 7 + 1), (byte)(page * 7 + 2), (byte)(page * 7 + 3), (byte)(page * 7 + 4) };

        public byte[] PagesOf(int startPage, int count)
        {
            var bytes = new byte[count * 4];
            Array.Copy(Memory, startPage * 4, bytes, 0, bytes.Length);

            return bytes;
        }

        public int CountOf(string call) => Calls.Count(c => c == call);

        public int CountStartingWith(string prefix) => Calls.Count(c => c.StartsWith(prefix, StringComparison.Ordinal));

        private static ReaderException AccessDenied() => new ReaderException("Call was not successfull, error AccessDenied", null);

        protected override Task<UltralightSighting> SelectUltralightChipAsync()
        {
            Calls.Add("select");
            _idle = false;

            if (SelectThrows != null)
            {
                throw SelectThrows;
            }

            return Task.FromResult(TagOnReader ? new UltralightSighting(true, SubType) : new UltralightSighting(false, null));
        }

        protected override Task<byte[]> GetUltralightVersionAsync()
        {
            Calls.Add("version");

            if (_idle || VersionAnswer == null)
            {
                _idle = true;

                return Task.FromResult<byte[]>(null);
            }

            return Task.FromResult((byte[])VersionAnswer.Clone());
        }

        protected override Task<byte[]> ReadUltralightPageAsync(byte page)
        {
            Calls.Add("read " + page);

            if (_idle || page >= PageCount || RefusedReads.Contains(page))
            {
                _idle = true;
                TagOnReader = !RemoveTagWhenAReadIsRefused && TagOnReader;

                return Task.FromResult<byte[]>(null);
            }

            var block = new byte[16];

            for (var i = 0; i < 4; i++)
            {
                Array.Copy(Memory, ((page + i) % PageCount) * 4, block, i * 4, 4);
            }

            return Task.FromResult(block);
        }

        protected override Task WriteUltralightPageAsync(byte page, byte[] data)
        {
            Calls.Add("write " + page);

            if (_idle || page >= PageCount || RefusedWrites.Contains(page))
            {
                _idle = true;

                throw AccessDenied();
            }

            if (!IgnoredWrites.Contains(page))
            {
                Array.Copy(data, 0, Memory, page * 4, 4);
            }

            return Task.CompletedTask;
        }

        protected override Task AuthUltralightCAsync(byte[] key)
        {
            Calls.Add("authC");
            SecretsReceived.Add(key);

            return Authenticate(key);
        }

        protected override Task PwdAuthUltralightAsync(byte[] password, byte[] pwdAck)
        {
            Calls.Add("pwd");
            SecretsReceived.Add(password);
            SecretsReceived.Add(pwdAck);

            return Authenticate(password);
        }

        protected override Task<bool> IsUltralightTagPresentAsync()
        {
            Calls.Add("present");
            _idle = false;

            return Task.FromResult(TagOnReader);
        }

        private Task Authenticate(byte[] key)
        {
            if (_idle || AcceptedKey == null || !key.SequenceEqual(AcceptedKey))
            {
                _idle = true;

                throw AccessDenied();
            }

            return Task.CompletedTask;
        }
    }
}
