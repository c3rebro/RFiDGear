using RFiDGear.Models;
using RFiDGear.Services.SecureKeys;

using System;
using System.Linq;
using System.Security;

using Xunit;

namespace RFiDGear.Tests.SecureKeys
{
    /// <summary>Turning what is typed into a password box into a key, and the edits of a list of key bindings.</summary>
    public class KeyEntryTests
    {
        [Theory]
        [InlineData("A1B2C3D4E5F6", "A1B2C3D4E5F6")]
        [InlineData("a1b2c3d4e5f6", "A1B2C3D4E5F6")]
        [InlineData("000102030405060708090A0B0C0D0E0F", "000102030405060708090A0B0C0D0E0F")]
        public void Exactly_the_digits_of_a_key_are_a_key(string entered, string expectedHex)
        {
            Span<byte> key = stackalloc byte[32];

            var length = KeyEntry.Read(Secure(entered), entered.Length, key);

            Assert.Equal(expectedHex.Length / 2, length);
            Assert.Equal(expectedHex, Convert.ToHexString(key.Slice(0, length)));
        }

        [Theory]
        [InlineData("A1B2C3D4E5F")]
        [InlineData("A1B2C3D4E5F6A")]
        [InlineData("A1B2C3D4E5F6A1")]
        [InlineData("A1B2C3D4E5GG")]
        [InlineData("A1B2C3D4E5 6")]
        [InlineData("A1-B2-C3-D4-E5")]
        [InlineData("")]
        public void Anything_else_is_not_a_key_and_leaves_nothing_behind(string entered)
        {
            Span<byte> key = stackalloc byte[32];
            key.Fill(0xFF);

            var length = KeyEntry.Read(Secure(entered), 12, key);

            Assert.Equal(0, length);

            // A refused entry leaves no part of itself in the buffer; digits that were good up to the first bad one are erased.
            if (entered.Length == 12)
            {
                Assert.True(key.Slice(0, 6).ToArray().All(b => b == 0));
            }
        }

        [Fact]
        public void No_entry_at_all_is_not_a_key()
        {
            Span<byte> key = stackalloc byte[32];

            Assert.Equal(0, KeyEntry.Read(null, 12, key));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-2)]
        [InlineData(11)]
        public void A_length_that_cannot_be_the_digits_of_a_key_gives_no_key(int hexLength)
        {
            Span<byte> key = stackalloc byte[32];

            Assert.Equal(0, KeyEntry.Read(Secure("A1B2C3D4E5F6"), hexLength, key));
        }

        [Fact]
        public void Room_that_is_too_small_gives_no_key()
        {
            Span<byte> key = stackalloc byte[5];

            Assert.Equal(0, KeyEntry.Read(Secure("A1B2C3D4E5F6"), 12, key));
        }

        // ── The edits of a list of bindings ──

        [Fact]
        public void A_binding_for_a_slot_without_one_is_added_at_the_end()
        {
            var list = new[] { Entry("a", "p", "1"), Entry("b", "p", "2") };

            var result = KeyBindingList.With(list, Entry("c", "p", "3"));

            Assert.Equal(new[] { "a", "b", "c" }, result.Select(entry => entry.Slot));
        }

        [Fact]
        public void A_binding_for_a_slot_with_one_replaces_it_in_its_place()
        {
            var list = new[] { Entry("a", "p", "1"), Entry("b", "p", "2"), Entry("c", "p", "3") };

            var result = KeyBindingList.With(list, Entry("b", "q", "new"));

            Assert.Equal(new[] { "a", "b", "c" }, result.Select(entry => entry.Slot));
            Assert.Equal("q", result[1].ProviderId);
            Assert.Equal("new", result[1].Reference);
        }

        [Fact]
        public void A_slot_that_a_damaged_project_lists_twice_has_one_entry_afterwards()
        {
            var list = new[] { Entry("a", "p", "1"), Entry("b", "p", "2"), Entry("a", "p", "3") };

            var result = KeyBindingList.With(list, Entry("a", "q", "new"));

            Assert.Equal(new[] { "a", "b" }, result.Select(entry => entry.Slot));
            Assert.Equal("new", result[0].Reference);
        }

        [Fact]
        public void The_result_of_an_edit_shares_nothing_with_the_list_or_the_entry_it_was_made_from()
        {
            var list = new[] { Entry("a", "p", "1") };
            var added = Entry("b", "p", "2");

            var result = KeyBindingList.With(list, added);
            result[0].Reference = "changed";
            result[1].Reference = "changed";

            Assert.Equal("1", list[0].Reference);
            Assert.Equal("2", added.Reference);
        }

        [Fact]
        public void Without_drops_every_entry_of_the_slot_and_copies_the_rest()
        {
            var list = new[] { Entry("a", "p", "1"), Entry("b", "p", "2"), Entry("a", "p", "3") };

            var result = KeyBindingList.Without(list, "a");

            var remaining = Assert.Single(result);
            Assert.Equal("b", remaining.Slot);
            Assert.NotSame(list[1], remaining);
        }

        private static KeyBindingEntry Entry(string slot, string provider, string reference) =>
            new KeyBindingEntry { Slot = slot, ProviderId = provider, Reference = reference };

        private static SecureString Secure(string text)
        {
            var secure = new SecureString();

            foreach (var c in text)
            {
                secure.AppendChar(c);
            }

            return secure;
        }
    }
}
