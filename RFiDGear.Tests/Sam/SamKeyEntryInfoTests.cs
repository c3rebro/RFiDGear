#nullable enable
using RFiDGear.Services.Sam;

using System;

using Elatec.NET.Sam;

using Xunit;

namespace RFiDGear.Tests.Sam
{
    /// <summary>
    /// What SAM_GetKeyEntry says about an entry, and which entries can be used to authenticate a PICC. The answers are those of a real
    /// MIFARE SAM AV3 (read back on 2026-11-15, after the entry 1 had been written with the AES-128 PICC test key); the meaning of the
    /// bits is from NXP's AN12701, table 2: SET bit 9 disables the entry, SET bits 3 to 5 are the key type, the low bits of ExtSET the
    /// key class.
    /// </summary>
    public class SamKeyEntryInfoTests
    {
        // Entry 1, written by SAM_ChangeKeyEntry with SamAv3KeyEntry.CreateAes128PiccKey.
        private const string WrittenEntry = "000000000000000000FF200001";

        // Entry 2, which nobody wrote: a blank entry is disabled.
        private const string BlankEntry = "000000000000000000FF200200";

        // Entry 0, the master key entry.
        private const string MasterEntry = "000000000000000000FF200000";

        [Fact]
        public void The_entry_that_was_written_is_enabled_an_aes_128_key_of_the_picc_class()
        {
            var info = SamKeyEntryInfo.Parse(Convert.FromHexString(WrittenEntry));

            Assert.NotNull(info);
            Assert.Equal(0x0020, info!.Set);
            Assert.Equal(0x01, info.ExtSet);
            Assert.Equal(0xFF, info.RefNoKuc);
            Assert.Equal(0, info.VersionA);
            Assert.Equal(0, info.KeyNoCek);
            Assert.True(info.IsEnabled);
            Assert.True(info.IsAes128);
            Assert.True(info.IsPiccKey);
            Assert.Equal(1, info.KeyClass);
            Assert.Equal(SamKeyEntryCheck.Matches, info.Verdict);
        }

        [Fact]
        public void A_blank_entry_is_disabled_by_bit_9_of_the_set_word_and_that_wins_over_everything_else()
        {
            var info = SamKeyEntryInfo.Parse(Convert.FromHexString(BlankEntry));

            Assert.Equal(0x0220, info!.Set);
            Assert.False(info.IsEnabled);
            Assert.True(info.IsAes128);
            Assert.Equal(SamKeyEntryCheck.Disabled, info.Verdict);

            // A PICC key in an entry that stays disabled is no key that anybody can use.
            var disabledPicc = SamKeyEntryInfo.Parse(Convert.FromHexString("000000000000000000FF200201"));

            Assert.True(disabledPicc!.IsPiccKey);
            Assert.Equal(SamKeyEntryCheck.Disabled, disabledPicc.Verdict);
        }

        [Fact]
        public void The_master_entry_is_enabled_and_aes_128_but_it_is_a_host_key_and_not_a_picc_key()
        {
            var info = SamKeyEntryInfo.Parse(Convert.FromHexString(MasterEntry));

            Assert.True(info!.IsEnabled);
            Assert.True(info.IsAes128);
            Assert.False(info.IsPiccKey);
            Assert.Equal(0, info.KeyClass);

            // SET 0x0020 says AES-128 and nothing about what the key is for: that is the class in ExtSET.
            Assert.Equal(SamKeyEntryCheck.WrongKind, info.Verdict);
        }

        [Fact]
        public void The_master_entry_has_no_key_usage_counter_and_needs_no_lock_unlock_after_a_reset()
        {
            var info = SamKeyEntryInfo.Parse(Convert.FromHexString(MasterEntry));

            Assert.Equal(0xFF, info!.RefNoKuc);

            // SET bit 10 would demand SAM_LockUnlock after every reset (AN12701): the activation survived a removal of the SAM.
            Assert.Equal(0, info.Set & 0x0400);
        }

        [Theory]
        [InlineData(0x0020, 0x01, true)]
        [InlineData(0x0020, 0x02, false)]
        [InlineData(0x0020, 0x00, false)]
        [InlineData(0x0018, 0x01, false)]
        [InlineData(0x0000, 0x01, false)]
        public void Only_an_aes_128_entry_of_the_picc_class_matches(int set, int extSet, bool matches)
        {
            var answer = new byte[13];
            answer[9] = 0xFF;
            answer[10] = (byte)(set & 0xFF);
            answer[11] = (byte)(set >> 8);
            answer[12] = (byte)extSet;

            var info = SamKeyEntryInfo.Parse(answer);

            Assert.Equal(matches ? SamKeyEntryCheck.Matches : SamKeyEntryCheck.WrongKind, info!.Verdict);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(12)]
        [InlineData(14)]
        [InlineData(61)]
        public void An_answer_that_is_not_13_bytes_long_is_no_key_entry(int length)
        {
            Assert.Null(SamKeyEntryInfo.Parse(new byte[length]));
        }

        [Fact]
        public void What_the_application_writes_is_enabled_so_that_the_key_in_it_can_be_used()
        {
            // The regression: a blank entry has SET bit 9 set (disabled), and the write has to clear it. SamAv3KeyEntry writes SET as
            // a whole, so an entry that was built with the bit left in would be stored disabled, and the key in it would be unusable.
            var key = new byte[16];
            var entry = SamAv3KeyEntry.CreateAes128PiccKey(key, 0);

            Assert.Equal(0, entry.Set & 0x0200);
            Assert.Equal(SamAv3KeyEntry.SetKeyTypeAes128, entry.Set & SamAv3KeyEntry.SetKeyTypeMask);
            Assert.Equal(SamAv3KeyEntry.ExtSetKeyClassPicc, entry.ExtSet & 0x07);

            // And what a SAM answers for such an entry is read as the entry that was written.
            var answer = new byte[]
            {
                entry.VersionA, entry.VersionA, entry.VersionA, 0, 0, 0, entry.DfKeyNo, entry.KeyNoCek, entry.KeyVersionCek, entry.RefNoKuc,
                (byte)(entry.Set & 0xFF), (byte)(entry.Set >> 8), entry.ExtSet,
            };

            Assert.Equal(SamKeyEntryCheck.Matches, SamKeyEntryInfo.Parse(answer)!.Verdict);
        }

        [Fact]
        public void A_host_key_is_enabled_too_but_of_the_host_class()
        {
            var entry = SamAv3KeyEntry.CreateAes128HostKey(new byte[16], 0);

            Assert.Equal(0, entry.Set & 0x0200);
            Assert.Equal(SamAv3KeyEntry.ExtSetKeyClassHost, entry.ExtSet & 0x07);
        }
    }
}
