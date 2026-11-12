using RFiDGear.Models;
using RFiDGear.Services.SecureKeys;

using System;
using System.Linq;

using Xunit;

namespace RFiDGear.Tests.SecureKeys
{
    /// <summary>
    /// A binding that is not usable fails the task that has it. It must never be mistaken for "no binding", which would fall
    /// back to an embedded key, so every kind of mistake has to be found, and has to be named without the reference.
    /// </summary>
    public class KeyBindingValidatorTests
    {
        private static KeyBindingEntry Entry(string slot, string provider = "test.vault", string reference = "cards/main") =>
            new KeyBindingEntry { Slot = slot, ProviderId = provider, Reference = reference };

        [Fact]
        public void Valid_bindings_have_no_issues()
        {
            var issues = KeyBindingValidator.Check(
                new[] { Entry(SecureKeySlots.DesfireAppCurrent), Entry(SecureKeySlots.DesfireRead, "other-vault", "a.b:c/d_e-f") },
                SecureKeySlots.Desfire);

            Assert.Empty(issues);
        }

        [Fact]
        public void A_slot_the_task_does_not_have_is_unknown()
        {
            var issues = KeyBindingValidator.Check(new[] { Entry(SecureKeySlots.ClassicACurrent) }, SecureKeySlots.Desfire);

            Assert.Equal(new KeyBindingIssue(SecureKeySlots.ClassicACurrent, KeyBindingProblem.UnknownSlot), Assert.Single(issues));
        }

        [Theory]
        [InlineData("DESFIRE.APP.CURRENT")]
        [InlineData("desfire.app.current ")]
        [InlineData("")]
        public void Slot_ids_are_compared_exactly(string slot)
        {
            var issues = KeyBindingValidator.Check(new[] { Entry(slot) }, SecureKeySlots.Desfire);

            Assert.Equal(KeyBindingProblem.UnknownSlot, Assert.Single(issues).Problem);
        }

        [Fact]
        public void A_slot_with_two_entries_is_reported_for_both()
        {
            var issues = KeyBindingValidator.Check(
                new[] { Entry(SecureKeySlots.DesfireRead), Entry(SecureKeySlots.DesfireWrite), Entry(SecureKeySlots.DesfireRead, "other-vault") },
                SecureKeySlots.Desfire);

            Assert.Equal(
                new[] { new KeyBindingIssue(SecureKeySlots.DesfireRead, KeyBindingProblem.DuplicateSlot), new KeyBindingIssue(SecureKeySlots.DesfireRead, KeyBindingProblem.DuplicateSlot) },
                issues);
        }

        [Theory]
        [InlineData("")]
        [InlineData("Upper")]
        [InlineData("has space")]
        public void A_provider_id_of_the_wrong_shape_is_invalid(string provider)
        {
            var issues = KeyBindingValidator.Check(new[] { Entry(SecureKeySlots.DesfireRead, provider) }, SecureKeySlots.Desfire);

            Assert.Equal(KeyBindingProblem.InvalidProviderId, Assert.Single(issues).Problem);
        }

        [Theory]
        [InlineData("")]
        [InlineData("has space")]
        [InlineData("line\nbreak")]
        [InlineData("trailing\n")]
        [InlineData("<markup>")]
        [InlineData("ümlaut")]
        public void A_reference_of_the_wrong_shape_is_invalid(string reference)
        {
            var issues = KeyBindingValidator.Check(new[] { Entry(SecureKeySlots.DesfireRead, reference: reference) }, SecureKeySlots.Desfire);

            var issue = Assert.Single(issues);
            Assert.Equal(KeyBindingProblem.InvalidReference, issue.Problem);

            if (reference.Trim().Length > 0)
            {
                Assert.DoesNotContain(reference.Trim(), issue.ToString(), StringComparison.Ordinal);
            }
        }

        [Fact]
        public void The_first_problem_of_an_entry_is_the_one_reported()
        {
            var issues = KeyBindingValidator.Check(new[] { Entry("nowhere", "Bad Provider", "bad reference") }, SecureKeySlots.Desfire);

            Assert.Equal(KeyBindingProblem.UnknownSlot, Assert.Single(issues).Problem);
        }

        [Fact]
        public void The_reference_is_never_part_of_an_issue()
        {
            var issues = KeyBindingValidator.Check(new[] { Entry(SecureKeySlots.DesfireRead, "Bad Provider", "cards/secret-looking-name") }, SecureKeySlots.Desfire);

            Assert.DoesNotContain("secret-looking-name", Assert.Single(issues).ToString(), StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("a")]
        [InlineData("AuthenticationError")]
        [InlineData("Cards.Main_01:v2/slot-3")]
        public void A_reference_of_letters_digits_and_dot_underscore_colon_slash_dash_is_valid(string reference)
        {
            Assert.True(SecureKeySyntax.IsValidReference(reference));
        }

        [Fact]
        public void A_reference_has_at_most_128_characters()
        {
            Assert.True(SecureKeySyntax.IsValidReference(new string('r', 128)));
            Assert.False(SecureKeySyntax.IsValidReference(new string('r', 129)));
            Assert.False(SecureKeySyntax.IsValidReference(null));
        }

        // ── The list ──

        [Fact]
        public void Copying_gives_new_entries_and_drops_missing_ones()
        {
            var source = new[] { Entry(SecureKeySlots.DesfireRead), null! };

            var copy = KeyBindingList.Copy(source);
            copy[0].Reference = "changed";

            Assert.Single(copy);
            Assert.Equal("cards/main", source[0].Reference);
            Assert.Empty(KeyBindingList.Copy(null));
        }

        [Fact]
        public void A_slot_is_found_by_its_exact_id_and_the_first_entry_wins()
        {
            var entries = new[] { Entry(SecureKeySlots.DesfireRead, "first-vault"), Entry(SecureKeySlots.DesfireRead, "second-vault") };

            Assert.Equal("first-vault", KeyBindingList.Find(entries, SecureKeySlots.DesfireRead)!.ProviderId);
            Assert.True(KeyBindingList.IsBound(entries, SecureKeySlots.DesfireRead));
            Assert.False(KeyBindingList.IsBound(entries, "DESFIRE.READ"));
            Assert.False(KeyBindingList.IsBound(entries, SecureKeySlots.DesfireWrite));
        }

        [Fact]
        public void An_unusable_binding_still_counts_as_bound()
        {
            var entries = new[] { Entry(SecureKeySlots.DesfireRead, string.Empty, string.Empty) };

            Assert.True(KeyBindingList.IsBound(entries, SecureKeySlots.DesfireRead));
        }

        [Fact]
        public void Every_slot_id_is_listed_once_and_follows_the_naming()
        {
            var all = SecureKeySlots.Desfire.Concat(SecureKeySlots.Classic).ToList();

            Assert.Equal(15, all.Count);
            Assert.Equal(all.Count, all.Distinct(StringComparer.Ordinal).Count());
            Assert.All(all, slot => Assert.Matches("^(desfire|classic)(\\.[a-z]+)+$", slot));
        }
    }
}
