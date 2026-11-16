#nullable enable
using RFiDGear.Services.Sam;

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Elatec.NET.Interfaces;
using Elatec.NET.Sam;

using Xunit;

namespace RFiDGear.Tests.Sam
{
    /// <summary>
    /// The data field of SAM_ChangeKeyEntry for the entries that Elatec.NET cannot write. The layout is the one the library sends for an AES entry:
    /// that is checked here against the library itself, with the same session keys and counter, so that a new version of the library that changes
    /// its frame is noticed.
    /// </summary>
    public class SamKeyEntryFrameTests
    {
        private const string TestKeyHex = "0F1E2D3C4B5A69788796A5B4C3D2E1F0";

        [Fact]
        public void A_native_key_of_zeros_is_three_copies_of_it_and_the_settings_of_a_picc_key_without_keep_iv()
        {
            var data = SamKeyEntryFrame.NativeTdeaPiccKey(new byte[16], 0);

            // 48 bytes of key (A, B, C); DF_AID (3), DF_KeyNo, KeyNoCEK, KeyVCEK; RefNoKUC FF; SET 0000; versions A, B, C; ExtSET 01.
            Assert.Equal(new string('0', 96) + "000000000000" + "FF" + "0000" + "000000" + "01", Convert.ToHexString(data));
            Assert.Equal(SamKeyEntryFrame.Length, data.Length);
        }

        [Fact]
        public void The_key_and_its_version_are_in_all_three_slots()
        {
            var key = Convert.FromHexString(TestKeyHex);

            var data = SamKeyEntryFrame.NativeTdeaPiccKey(key, 5);

            Assert.Equal(TestKeyHex + TestKeyHex + TestKeyHex + "000000000000" + "FF" + "0000" + "050505" + "01", Convert.ToHexString(data));
        }

        [Fact]
        public void A_set_word_is_written_low_byte_first_and_nothing_but_the_given_settings_is_set()
        {
            var data = SamKeyEntryFrame.Build(new byte[16], 0, 0x0124, 0x11);

            Assert.Equal(0x24, data[55]);
            Assert.Equal(0x01, data[56]);
            Assert.Equal(0x11, data[60]);
            Assert.Equal(0xFF, data[54]);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(15)]
        [InlineData(17)]
        [InlineData(24)]
        public void A_key_of_another_length_is_refused(int length)
        {
            Assert.Throws<ArgumentException>(() => SamKeyEntryFrame.NativeTdeaPiccKey(new byte[length], 0));
        }

        [Fact]
        public async Task The_layout_is_the_one_that_the_library_sends_for_an_aes_entry()
        {
            var ke = Enumerable.Range(0x10, 16).Select(i => (byte)i).ToArray();
            var km = Enumerable.Range(0x30, 16).Select(i => (byte)i).ToArray();
            var key = Convert.FromHexString(TestKeyHex);
            var entry = SamAv3KeyEntry.CreateAes128PiccKey(key, 3);

            // What the library sends: its session, with the keys and the counter that the other side of this test uses too.
            var capture = new CaptureChannel();
            var session = new SamAv3HostSession(capture);
            typeof(SamAv3HostSession).GetField("_secureMessaging", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(session, NewSecureMessaging(ke, km));

            await Assert.ThrowsAsync<SamException>(() => session.ChangeKeyEntryAsync(2, entry, false, false));

            Assert.NotNull(capture.Last);

            // What the frame builder sends, protected by an identical session.
            var secureMessaging = NewSecureMessaging(ke, km);
            var protect = secureMessaging.GetType().GetMethod("Protect")!;
            var data = SamKeyEntryFrame.Build(key, 3, entry.Set, entry.ExtSet);
            var mine = (SamApdu)protect.Invoke(secureMessaging, new object?[] { (byte)0x80, (byte)0xC1, (byte)2, (byte)0xFF, data, (byte?)null })!;

            Assert.Equal(Convert.ToHexString(LlaSamApduChannel.Encode(capture.Last!)), Convert.ToHexString(LlaSamApduChannel.Encode(mine)));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(128)]
        [InlineData(255)]
        public async Task The_real_session_refuses_the_master_entry_and_numbers_outside_the_sam_before_it_sends_anything(int entry)
        {
            // ExecuteSecureAsync has none of the guards of ChangeKeyEntryAsync: they are in the session.
            var channel = new ScriptedSamChannel(_ => new SamApduResponse(Array.Empty<byte>(), 0x9000));
            using var session = new Av3HostSession(channel);

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => session.WriteNativeKeyEntryAsync((byte)entry, new byte[16], 0));

            Assert.Empty(channel.Sent);
        }

        [Fact]
        public async Task The_real_session_sends_no_native_key_without_a_host_authentication()
        {
            var channel = new ScriptedSamChannel(_ => new SamApduResponse(Array.Empty<byte>(), 0x9000));
            using var session = new Av3HostSession(channel);

            await Assert.ThrowsAnyAsync<Exception>(() => session.WriteNativeKeyEntryAsync(1, new byte[16], 0));

            Assert.Empty(channel.Sent);
        }

        [Fact]
        public async Task The_real_session_refuses_a_key_of_the_wrong_length_before_it_sends_anything()
        {
            var channel = new ScriptedSamChannel(_ => new SamApduResponse(Array.Empty<byte>(), 0x9000));
            using var session = new Av3HostSession(channel);

            await Assert.ThrowsAsync<ArgumentException>(() => session.WriteNativeKeyEntryAsync(1, new byte[15], 0));

            Assert.Empty(channel.Sent);
        }
        private static object NewSecureMessaging(byte[] ke, byte[] km)
        {
            var type = typeof(SamAv3HostSession).Assembly.GetType("Elatec.NET.SamAv3SecureMessaging")!;

            return Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new object[] { ke, km, 0u }, null)!;
        }

        /// <summary>A channel that keeps the last frame and answers with something that the library refuses, which is all that is needed here.</summary>
        private sealed class CaptureChannel : ISamApduChannel
        {
            internal SamApdu? Last { get; private set; }

            public Task<SamApduResponse> TransmitAsync(SamApdu command)
            {
                Last = command;

                return Task.FromResult(new SamApduResponse(new byte[8], 0x9000));
            }
        }
    }
}