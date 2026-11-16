#nullable enable
using RFiDGear.Contracts;
using RFiDGear.Models;
using RFiDGear.Services.Sam;
using RFiDGear.Services.SecureKeys;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace RFiDGear.Tests.SecureKeys
{
    /// <summary>
    /// A key of the SAM as the source of a key slot: what its reference looks like, which binding is wrong before a command looks at it, and
    /// that the provider of that name hands out nothing. The commands that can use one come later, so none is offered for any slot yet.
    /// </summary>
    public class SamKeySourceTests
    {
        private static KeyBindingEntry Entry(string slot, string provider, string reference) =>
            new KeyBindingEntry { Slot = slot, ProviderId = provider, Reference = reference };

        [Theory]
        [InlineData("sam", "1:0", true)]
        [InlineData("sam", "127:255", true)]
        [InlineData("sam", "0:0", false)]
        [InlineData("sam", "128:0", false)]
        [InlineData("sam", "1:256", false)]
        [InlineData("sam", "1", false)]
        [InlineData("sam", "01:0", false)]
        [InlineData("sam", "1:00", false)]
        [InlineData("sam", "a/b", false)]
        [InlineData("sam", "", false)]
        [InlineData("sam", null, false)]
        [InlineData("test.vault", "cards/main", true)]
        [InlineData("test.vault", "0:0", true)]
        [InlineData("test.vault", "", false)]
        [InlineData("windows-credential-manager", "0:0", true)]
        public void A_reference_of_the_sam_is_a_key_entry_and_a_version_and_the_others_are_left_alone(string provider, string? reference, bool valid)
        {
            Assert.Equal(valid, SecureKeySyntax.IsValidReferenceFor(provider, reference));
        }

        [Fact]
        public void The_provider_id_is_the_one_a_project_names()
        {
            Assert.Equal("sam", SamKeyProvider.ProviderId);
            Assert.Equal(SamKeyReference.ProviderId, SamKeyProvider.ProviderId);
        }

        [Fact]
        public void A_binding_to_a_key_of_the_sam_with_a_reference_that_is_none_is_wrong_before_any_command_looks_at_it()
        {
            var issues = KeyBindingValidator.Check(new[] { Entry(SecureKeySlots.DesfireRead, "sam", "0:0") }, SecureKeySlots.Desfire);

            Assert.Equal(new KeyBindingIssue(SecureKeySlots.DesfireRead, KeyBindingProblem.InvalidReference), Assert.Single(issues));
            Assert.Empty(KeyBindingValidator.Check(new[] { Entry(SecureKeySlots.DesfireRead, "sam", "3:1") }, SecureKeySlots.Desfire));
        }

        [Fact]
        public async Task The_provider_of_the_sam_hands_out_no_key_whatever_it_is_asked()
        {
            var provider = new SamKeyProvider();

            foreach (var algorithm in new[] { SecureKeyAlgorithm.Des, SecureKeyAlgorithm.Aes128, SecureKeyAlgorithm.TripleDes2K })
            {
                var request = new SecureKeyRequest { SlotId = SecureKeySlots.DesfirePiccCurrent, Reference = "1:0", Algorithm = algorithm, Usage = SecureKeyUsage.Authenticate };

                using var answer = await provider.ResolveAsync(request, CancellationToken.None);

                Assert.False(answer.IsResolved);
                Assert.Equal(SecureKeyFailureReason.Unsupported, answer.FailureReason);
            }
        }

        [Fact]
        public async Task The_provider_of_the_sam_stops_when_the_run_was_cancelled()
        {
            using var cancel = new CancellationTokenSource();
            cancel.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await new SamKeyProvider().ResolveAsync(new SecureKeyRequest { SlotId = SecureKeySlots.DesfirePiccCurrent, Reference = "1:0" }, cancel.Token));
        }

        [Fact]
        public void The_registry_of_the_application_lists_the_sam_and_no_extension_can_take_its_id()
        {
            var registry = SecureKeyRegistry.FromContainer(null, SecureKeyRegistry.BuiltInProviders());

            var info = Assert.Single(registry.Providers, provider => provider.Id == SamKeyProvider.ProviderId);

            Assert.Equal(SamKeyProvider.DisplayName, info.DisplayName);
        }

        [Fact]
        public void The_sam_is_offered_for_no_slot_yet()
        {
            Assert.Empty(SamKeySlots.Offered);
            Assert.All(SecureKeySlots.Desfire.Concat(SecureKeySlots.Classic).Concat(SecureKeySlots.Ultralight), slot => Assert.False(SamKeySlots.Offers(slot)));
        }

        [Fact]
        public void The_sam_is_listed_as_a_provider_of_the_slots_that_offer_it_and_of_no_other()
        {
            var providers = new[]
            {
                new SecureKeyProviderInfo("bridge-client", "Bridge"),
                new SecureKeyProviderInfo(SamKeyProvider.ProviderId, SamKeyProvider.DisplayName),
                new SecureKeyProviderInfo("windows-credential-manager", "Credential Manager"),
            };
            var offered = new[] { SecureKeySlots.DesfirePiccCurrent };

            Assert.Equal(
                new[] { "bridge-client", SamKeyProvider.ProviderId, "windows-credential-manager" },
                SamKeySlots.ProvidersFor(SecureKeySlots.DesfirePiccCurrent, providers, offered).Select(provider => provider.Id));
            Assert.Equal(
                new[] { "bridge-client", "windows-credential-manager" },
                SamKeySlots.ProvidersFor(SecureKeySlots.DesfireRead, providers, offered).Select(provider => provider.Id));
            Assert.Equal(
                new[] { "bridge-client", "windows-credential-manager" },
                SamKeySlots.ProvidersFor(SecureKeySlots.DesfirePiccCurrent, providers, Array.Empty<string>()).Select(provider => provider.Id));
        }
    }

    /// <summary>
    /// What a command gets when a slot names a key of the SAM: the reference and no bytes, only for a command that says it can use one,
    /// and only when all the keys it needs are keys of the SAM.
    /// </summary>
    public class SamKeyAcquisitionTests
    {
        private const string Provider = "test.vault";

        private static readonly KeyNeed Current = new KeyNeed(SecureKeySlots.DesfirePiccCurrent, SecureKeyAlgorithm.Aes128, SecureKeyUsage.Authenticate, 0, 0);
        private static readonly KeyNeed New = new KeyNeed(SecureKeySlots.DesfirePiccNew, SecureKeyAlgorithm.Des, SecureKeyUsage.ChangeKeyNew, 0, 0);

        private static KeyBindingEntry Sam(string slot, string reference = "1:0") =>
            new KeyBindingEntry { Slot = slot, ProviderId = SamKeyProvider.ProviderId, Reference = reference };

        private static KeyBindingEntry Vault(string slot) =>
            new KeyBindingEntry { Slot = slot, ProviderId = Provider, Reference = "ref/" + slot };

        private static Task<AcquiredKeys> AcquireAsync(KeyNeed[] needs, KeyBindingEntry[] bindings, SamKeyPolicy policy, SlotVault? vault = null) =>
            KeyAcquisition.AcquireAsync(
                needs,
                bindings,
                SecureKeySlots.Desfire,
                slot => "EMBEDDED-" + slot,
                vault == null ? null : new KeyResolutionContext(vault.Registry(), "run-1", "0", null, interactive: false),
                policy);

        private static SlotVault NewVault() => new SlotVault(Provider, _ => "00112233445566778899AABBCCDDEEFF", 16);

        [Fact]
        public async Task A_command_that_does_not_know_the_sam_refuses_a_slot_that_names_one_and_asks_nobody()
        {
            var vault = NewVault();

            // The default is Refuse, and the slot that is not a key of the SAM is not even asked for: the first refusal ends it.
            var keys = await KeyAcquisition.AcquireAsync(
                new[] { Current, New },
                new[] { Sam(Current.Slot), Vault(New.Slot) },
                SecureKeySlots.Desfire,
                slot => "EMBEDDED-" + slot,
                new KeyResolutionContext(vault.Registry(), "run-1", "0", null, interactive: false));

            Assert.True(keys.Failed);
            Assert.Equal(new KeyFailure(Current.Slot, SamKeyProvider.ProviderId, KeyAcquisition.SamNotSupported), keys.Failure);
            Assert.Empty(vault.Requests);
        }

        [Fact]
        public async Task A_command_that_can_use_the_sam_gets_the_reference_of_each_slot_and_no_key()
        {
            var keys = await AcquireAsync(new[] { Current, New }, new[] { Sam(Current.Slot, "2:7"), Sam(New.Slot, "1:0") }, SamKeyPolicy.AllOrNothing);

            Assert.False(keys.Failed);
            Assert.True(keys.IsSam(Current.Slot));
            Assert.True(keys.IsBound(Current.Slot));
            Assert.Equal(2, keys.SamKey(Current.Slot).Entry);
            Assert.Equal(7, keys.SamKey(Current.Slot).Version);
            Assert.Equal(1, keys.SamKey(New.Slot).Entry);
            Assert.Equal(0, keys.SamKey(New.Slot).Version);
        }

        [Fact]
        public async Task A_key_of_the_sam_needs_no_context_because_nobody_is_asked()
        {
            var keys = await AcquireAsync(new[] { Current }, new[] { Sam(Current.Slot) }, SamKeyPolicy.AllOrNothing, vault: null);

            Assert.False(keys.Failed);
            Assert.True(keys.IsSam(Current.Slot));
        }

        [Fact]
        public async Task A_key_of_the_sam_has_no_text_and_asking_for_it_is_a_mistake_of_the_command()
        {
            var keys = await AcquireAsync(new[] { Current }, new[] { Sam(Current.Slot) }, SamKeyPolicy.AllOrNothing);

            var error = Assert.Throws<InvalidOperationException>(() => keys.Hex(Current.Slot));

            // Not "the command did not ask for it": the slot was asked for, and it is a key that stays in the SAM.
            Assert.Contains("stays in the SAM", error.Message, StringComparison.Ordinal);
            Assert.Contains(Current.Slot, error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("1:0", error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("EMBEDDED", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task The_reference_of_a_slot_that_is_not_a_key_of_the_sam_is_not_given()
        {
            var vault = NewVault();
            var keys = await AcquireAsync(new[] { Current }, new[] { Vault(Current.Slot) }, SamKeyPolicy.AllOrNothing, vault);

            Assert.False(keys.IsSam(Current.Slot));
            Assert.Throws<InvalidOperationException>(() => keys.SamKey(Current.Slot));
            Assert.Equal("00112233445566778899AABBCCDDEEFF", keys.Hex(Current.Slot));
        }

        [Fact]
        public async Task A_command_that_mixes_the_sam_with_other_keys_is_refused_because_the_sam_needs_both_keys_of_a_change()
        {
            var vault = NewVault();

            var withProvider = await AcquireAsync(new[] { Current, New }, new[] { Sam(Current.Slot), Vault(New.Slot) }, SamKeyPolicy.AllOrNothing, vault);
            var withEmbedded = await AcquireAsync(new[] { Current, New }, new[] { Sam(Current.Slot) }, SamKeyPolicy.AllOrNothing);

            Assert.Equal(KeyAcquisition.SamMixedKeys, withProvider.Failure!.Cause);
            Assert.Equal(KeyAcquisition.SamMixedKeys, withEmbedded.Failure!.Cause);
            Assert.Equal(Current.Slot, withEmbedded.Failure!.Slot);
        }

        [Fact]
        public async Task A_key_the_sam_cannot_hold_is_refused_for_what_it_is()
        {
            var triple = new KeyNeed(SecureKeySlots.DesfirePiccCurrent, SecureKeyAlgorithm.TripleDes3K, SecureKeyUsage.Authenticate, 0, 0);

            var keys = await AcquireAsync(new[] { triple }, new[] { Sam(triple.Slot) }, SamKeyPolicy.AllOrNothing);

            Assert.True(keys.Failed);
            Assert.Equal(SecureKeyFailureReason.Unsupported.ToString(), keys.Failure!.Cause);
        }

        [Theory]
        [InlineData("0:0")]
        [InlineData("128:0")]
        [InlineData("1:256")]
        [InlineData("1")]
        [InlineData("x")]
        public async Task A_reference_that_is_no_key_entry_is_refused_by_a_command_that_could_have_used_it(string reference)
        {
            // The binding is checked first, so the reason is the one of the binding; the command never sees the text.
            var keys = await AcquireAsync(new[] { Current }, new[] { Sam(Current.Slot, reference) }, SamKeyPolicy.AllOrNothing);

            Assert.True(keys.Failed);
            Assert.Equal(KeyBindingProblem.InvalidReference.ToString(), keys.Failure!.Cause);
            Assert.DoesNotContain(reference, keys.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public async Task A_task_without_a_key_of_the_sam_is_acquired_as_before_under_either_policy()
        {
            var vault = NewVault();

            foreach (var policy in new[] { SamKeyPolicy.Refuse, SamKeyPolicy.AllOrNothing })
            {
                var keys = await AcquireAsync(new[] { Current, New }, new[] { Vault(Current.Slot) }, policy, vault);

                Assert.False(keys.Failed);
                Assert.False(keys.IsSam(Current.Slot));
                Assert.Equal("00112233445566778899AABBCCDDEEFF", keys.Hex(Current.Slot));
                Assert.Equal("EMBEDDED-" + New.Slot, keys.Hex(New.Slot));
            }
        }

        [Fact]
        public async Task Nothing_of_a_refusal_shows_the_reference()
        {
            var keys = await AcquireAsync(new[] { Current }, new[] { Sam(Current.Slot, "99:42") }, SamKeyPolicy.Refuse);

            Assert.True(keys.Failed);
            Assert.DoesNotContain("99:42", keys.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("99:42", string.Join(" ", keys.Failure!.Slot, keys.Failure.ProviderId, keys.Failure.Cause), StringComparison.Ordinal);
        }
    }
}
