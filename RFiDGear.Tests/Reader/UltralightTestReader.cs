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

        /// <summary>A chip that takes the first write and refuses every later one (as one that was pulled away, or locked meanwhile, does).</summary>
        public bool RefusedWritesAfterTheFirst { get; set; }

        private int _writesTaken;

        /// <summary>
        /// A write or an authentication that is refused also takes the tag away: Elatec.NET throws "AccessDenied" whenever the reader
        /// says no, so a tag that was pulled away looks the same as a refusal until the antenna is looked at.
        /// </summary>
        public bool RemoveTagWhenAWriteOrAuthenticationIsRefused { get; set; }

        /// <summary>The sub type the second and every later selection reports (a tag that was swapped), or <c>null</c> for the same as the first.</summary>
        public MifareChipSubType? SubTypeOfLaterSelections { get; set; }

        /// <summary>The UID the tag reports (made up for the tests, 7 bytes).</summary>
        public byte[] Uid { get; set; } = { 0x04, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 };

        /// <summary>The UID the second and every later selection reports (a tag of the same kind that was swapped), or <c>null</c> for the same as the first.</summary>
        public byte[] UidOfLaterSelections { get; set; }

        private int _selections;

        public Exception SelectThrows { get; set; }

        public byte[] AcceptedKey { get; set; }

        /// <summary>
        /// AUTH0 of a chip with the password protection on for reads (PROT = 1): until the password was sent, a READ that starts at
        /// this page or after it is refused, and one that starts before it gets the pages from here on replaced by what the chip
        /// wraps around to (page 0, 1, ...). <c>null</c>: nothing is protected.
        /// </summary>
        public int? ReadProtectedFromPage { get; set; }

        /// <summary>Gets a value that says whether the chip took the key or the password since it was selected.</summary>
        public bool Authenticated { get; private set; }

        // What the data sheets say of the pages that a READ cannot give: the key of an Ultralight C (pages 44-47) is write-only, and a
        // READ goes on at page 0 where page 44 would be; the password and the PACK of an Ultralight EV1 and an NTAG read as zeros.
        private int ReadablePages => Variant == UltralightVariant.C ? 44 : PageCount;

        private bool IsHidden(int page) => (Variant != UltralightVariant.Plain && Variant != UltralightVariant.C) && page >= PageCount - 2;

        public List<string> Calls { get; } = new List<string>();

        public List<byte[]> SecretsReceived { get; } = new List<byte[]>();

        /// <summary>The arrays that the writes received, kept as they were handed over (not copies) so a test can see that they were cleared afterwards.</summary>
        public List<byte[]> ChunksReceived { get; } = new List<byte[]>();

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
            Authenticated = false;

            if (SelectThrows != null)
            {
                throw SelectThrows;
            }

            var later = _selections++ > 0;
            var subType = later && SubTypeOfLaterSelections.HasValue ? SubTypeOfLaterSelections : SubType;
            var uid = later && UidOfLaterSelections != null ? UidOfLaterSelections : Uid;

            return Task.FromResult(TagOnReader ? new UltralightSighting(true, subType, uid) : new UltralightSighting(false, null));
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

            var protectedFrom = Authenticated ? null : ReadProtectedFromPage;

            if (_idle || page >= PageCount || RefusedReads.Contains(page) || (protectedFrom.HasValue && page >= protectedFrom.Value))
            {
                _idle = true;
                TagOnReader = !RemoveTagWhenAReadIsRefused && TagOnReader;

                return Task.FromResult<byte[]>(null);
            }

            var block = new byte[16];

            for (var i = 0; i < 4; i++)
            {
                var wanted = page + i;
                var seen = wanted;

                if (protectedFrom.HasValue && wanted >= protectedFrom.Value)
                {
                    seen = wanted - protectedFrom.Value;
                }
                else if (wanted >= ReadablePages)
                {
                    seen = wanted - ReadablePages;
                }

                if (!IsHidden(seen))
                {
                    Array.Copy(Memory, seen * 4, block, i * 4, 4);
                }
            }

            return Task.FromResult(block);
        }

        protected override Task WriteUltralightPageAsync(byte page, byte[] data)
        {
            Calls.Add("write " + page);
            ChunksReceived.Add(data);

            if (_idle || page >= PageCount || RefusedWrites.Contains(page) || (RefusedWritesAfterTheFirst && _writesTaken > 0))
            {
                _idle = true;
                TagOnReader = !RemoveTagWhenAWriteOrAuthenticationIsRefused && TagOnReader;

                throw AccessDenied();
            }

            _writesTaken++;

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
            Authenticated = false;

            return Task.FromResult(TagOnReader);
        }

        private Task Authenticate(byte[] key)
        {
            if (_idle || AcceptedKey == null || !key.SequenceEqual(AcceptedKey))
            {
                _idle = true;
                TagOnReader = !RemoveTagWhenAWriteOrAuthenticationIsRefused && TagOnReader;

                throw AccessDenied();
            }

            Authenticated = true;

            return Task.CompletedTask;
        }
    }
}
