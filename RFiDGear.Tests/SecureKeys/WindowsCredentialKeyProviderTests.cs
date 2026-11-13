using RFiDGear.Contracts;
using RFiDGear.Services.SecureKeys;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace RFiDGear.Tests.SecureKeys
{
    /// <summary>
    /// The provider that reads keys from the Windows Credential Manager, tried against a store that is a dictionary: the
    /// tests never touch the vault of the person who runs them. One test at the end does, with a name of its own that it
    /// removes again.
    /// </summary>
    public class WindowsCredentialKeyProviderTests
    {
        private const string Prefix = "RFiDGear/key/";

        // ── What is read ──

        [Theory]
        [InlineData("A1B2C3D4E5F6", false)]
        [InlineData("a1b2c3d4e5f6", false)]
        [InlineData("000102030405060708090A0B0C0D0E0F", false)]
        [InlineData("000102030405060708090a0b0c0d0e0f1011121314151617", true)]
        public async Task A_key_kept_as_utf16_hex_digits_is_handed_out_as_bytes(string hex, bool lower)
        {
            var store = new FakeCredentialStore();
            store.Blobs[Prefix + "cards/main"] = Encoding.Unicode.GetBytes(lower ? hex.ToLowerInvariant() : hex);
            var provider = new WindowsCredentialKeyProvider(store);

            using var answer = await provider.ResolveAsync(RequestFor("cards/main"), CancellationToken.None);

            Assert.True(answer.IsResolved);
            var lease = Assert.IsType<SecureKeyLease>(answer.Key);
            Assert.Equal(hex.ToUpperInvariant(), Convert.ToHexString(lease.Key));
        }

        [Fact]
        public async Task The_vault_is_asked_for_the_reference_below_the_fixed_prefix_and_nothing_else()
        {
            var store = new FakeCredentialStore();
            var provider = new WindowsCredentialKeyProvider(store);

            using var answer = await provider.ResolveAsync(RequestFor("projects/a-1/key.v2"), CancellationToken.None);

            Assert.Equal(SecureKeyFailureReason.NotFound, answer.FailureReason);
            Assert.Equal(Prefix + "projects/a-1/key.v2", Assert.Single(store.Reads));
        }

        [Theory]
        [InlineData("git:https://example.com")]
        [InlineData("../../Microsoft_OC1")]
        [InlineData("LegacyGeneric:target:x")]
        [InlineData("WindowsLive:virtualapp/didlogical")]
        public async Task A_reference_cannot_reach_a_credential_of_another_application(string reference)
        {
            var store = new FakeCredentialStore();
            var provider = new WindowsCredentialKeyProvider(store);

            using var answer = await provider.ResolveAsync(RequestFor(reference), CancellationToken.None);

            // Whatever the reference is, the name that reaches the vault starts with the prefix.
            Assert.StartsWith(Prefix, Assert.Single(store.Reads), StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("a b")]
        [InlineData("a\\b")]
        [InlineData("a\nb")]
        [InlineData("keyä")]
        [InlineData("*")]
        public async Task A_reference_with_a_character_outside_the_allowed_alphabet_never_reaches_the_vault(string reference)
        {
            var store = new FakeCredentialStore();
            var provider = new WindowsCredentialKeyProvider(store);

            using var answer = await provider.ResolveAsync(RequestFor(reference), CancellationToken.None);

            Assert.Equal(SecureKeyFailureReason.NotFound, answer.FailureReason);
            Assert.Empty(store.Reads);
        }

        [Fact]
        public async Task A_reference_that_is_too_long_never_reaches_the_vault()
        {
            var store = new FakeCredentialStore();
            var provider = new WindowsCredentialKeyProvider(store);

            using var answer = await provider.ResolveAsync(RequestFor(new string('a', SecureKeySyntax.MaxReferenceLength + 1)), CancellationToken.None);

            Assert.Equal(SecureKeyFailureReason.NotFound, answer.FailureReason);
            Assert.Empty(store.Reads);
        }

        [Fact]
        public async Task A_cancelled_request_does_not_ask_the_vault()
        {
            var store = new FakeCredentialStore();
            var provider = new WindowsCredentialKeyProvider(store);
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();

            using var answer = await provider.ResolveAsync(RequestFor("cards/main"), cancelled.Token);

            Assert.Equal(SecureKeyFailureReason.Cancelled, answer.FailureReason);
            Assert.Empty(store.Reads);
        }

        // ── What goes wrong ──

        [Theory]
        [InlineData(CredentialErrors.NotFound, SecureKeyFailureReason.NotFound)]
        [InlineData(CredentialErrors.AccessDenied, SecureKeyFailureReason.NotAuthorized)]
        [InlineData(1312, SecureKeyFailureReason.ProviderUnavailable)]
        [InlineData(1722, SecureKeyFailureReason.ProviderUnavailable)]
        public async Task An_error_of_the_vault_becomes_the_failure_it_stands_for(int error, SecureKeyFailureReason expected)
        {
            var store = new FakeCredentialStore { ReadError = error };
            var provider = new WindowsCredentialKeyProvider(store);

            using var answer = await provider.ResolveAsync(RequestFor("cards/main"), CancellationToken.None);

            Assert.False(answer.IsResolved);
            Assert.Equal(expected, answer.FailureReason);
        }

        [Theory]
        [InlineData("empty")]
        [InlineData("one byte per character")]
        [InlineData("odd number of digits")]
        [InlineData("not hexadecimal")]
        [InlineData("a space inside")]
        [InlineData("a character that is not ascii")]
        [InlineData("more than 32 bytes")]
        public async Task A_secret_that_is_not_hexadecimal_utf16_is_not_a_key(string kind)
        {
            var store = new FakeCredentialStore();
            store.Blobs[Prefix + "cards/main"] = kind switch
            {
                "empty" => Array.Empty<byte>(),
                "one byte per character" => Encoding.ASCII.GetBytes("A1B2C3D4E5F6"),
                "odd number of digits" => Encoding.Unicode.GetBytes("A1B2C3D4E5F"),
                "not hexadecimal" => Encoding.Unicode.GetBytes("A1B2C3D4E5GG"),
                "a space inside" => Encoding.Unicode.GetBytes("A1B2C3 D4E5F6"),
                "a character that is not ascii" => Encoding.Unicode.GetBytes("A1B2C3D4E5F١"),
                _ => Encoding.Unicode.GetBytes(new string('A', 66)),
            };
            var provider = new WindowsCredentialKeyProvider(store);

            using var answer = await provider.ResolveAsync(RequestFor("cards/main"), CancellationToken.None);

            Assert.False(answer.IsResolved);
            Assert.Equal(SecureKeyFailureReason.InvalidKey, answer.FailureReason);
        }

        [Fact]
        public async Task A_vault_that_throws_is_unavailable()
        {
            var provider = new WindowsCredentialKeyProvider(new FakeCredentialStore { ThrowText = "vault failed" });

            using var answer = await provider.ResolveAsync(RequestFor("cards/main"), CancellationToken.None);

            Assert.Equal(SecureKeyFailureReason.ProviderUnavailable, answer.FailureReason);
        }

        // ── Nothing is left behind ──

        [Fact]
        public async Task The_copy_of_the_secret_that_the_vault_handed_out_is_erased()
        {
            var store = new FakeCredentialStore();
            store.Blobs[Prefix + "cards/main"] = Encoding.Unicode.GetBytes("A1B2C3D4E5F6");
            var provider = new WindowsCredentialKeyProvider(store);

            using var answer = await provider.ResolveAsync(RequestFor("cards/main"), CancellationToken.None);

            Assert.True(answer.IsResolved);
            Assert.All(Assert.Single(store.HandedOut), b => Assert.Equal(0, b));
        }

        [Fact]
        public async Task The_copy_of_a_secret_that_is_not_a_key_is_erased_too()
        {
            var store = new FakeCredentialStore();
            store.Blobs[Prefix + "cards/main"] = Encoding.Unicode.GetBytes("A1B2C3D4E5GG");
            var provider = new WindowsCredentialKeyProvider(store);

            using var answer = await provider.ResolveAsync(RequestFor("cards/main"), CancellationToken.None);

            Assert.False(answer.IsResolved);
            Assert.All(Assert.Single(store.HandedOut), b => Assert.Equal(0, b));
        }

        [Fact]
        public void The_scratch_space_a_key_is_put_together_in_is_erased_when_it_is_a_key()
        {
            var scratch = new byte[WindowsCredentialKeyProvider.MaxKeyBytes];
            Array.Fill(scratch, (byte)0xFF);

            using var lease = CredentialBlob.ToLease(Encoding.Unicode.GetBytes("A1B2C3D4E5F6"), scratch);

            Assert.NotNull(lease);
            Assert.Equal("A1B2C3D4E5F6", Convert.ToHexString(lease!.Key));
            Assert.All(scratch.Take(6), b => Assert.Equal(0, b));
        }

        [Fact]
        public void The_scratch_space_is_erased_when_the_blob_turns_out_not_to_be_a_key_after_some_digits()
        {
            var scratch = new byte[WindowsCredentialKeyProvider.MaxKeyBytes];
            Array.Fill(scratch, (byte)0xFF);

            // The first two bytes of the key are good; the third is not hexadecimal.
            var lease = CredentialBlob.ToLease(Encoding.Unicode.GetBytes("A1B2GG"), scratch);

            Assert.Null(lease);
            Assert.All(scratch.Take(3), b => Assert.Equal(0, b));
        }

        [Fact]
        public async Task The_log_never_shows_a_reference_a_key_or_what_the_vault_said()
        {
            using var logs = CapturedLogs.Install();
            var store = new FakeCredentialStore();
            store.Blobs[Prefix + "secret-reference/one"] = Encoding.Unicode.GetBytes("A1B2C3D4E5F6");
            store.Blobs[Prefix + "secret-reference/bad"] = Encoding.Unicode.GetBytes("A1B2C3D4E5GG");
            var provider = new WindowsCredentialKeyProvider(store);

            (await provider.ResolveAsync(RequestFor("secret-reference/one"), CancellationToken.None)).Dispose();
            (await provider.ResolveAsync(RequestFor("secret-reference/bad"), CancellationToken.None)).Dispose();
            (await provider.ResolveAsync(RequestFor("secret-reference/missing"), CancellationToken.None)).Dispose();
            store.ReadError = CredentialErrors.AccessDenied;
            (await provider.ResolveAsync(RequestFor("secret-reference/one"), CancellationToken.None)).Dispose();
            store.ReadError = null;
            store.ThrowText = "the vault failed for secret-reference/one " + SecureKeyTestData.Secret;
            (await provider.ResolveAsync(RequestFor("secret-reference/one"), CancellationToken.None)).Dispose();
            provider.Store("secret-reference/one", new byte[] { 0xA1, 0xB2, 0xC3, 0xD4, 0xE5, 0xF6 });
            provider.Remove("secret-reference/one");

            Assert.NotEmpty(logs.All());

            foreach (var text in logs.All().Select(CapturedLogs.EverythingWrittenFor))
            {
                Assert.DoesNotContain("secret-reference", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(SecureKeyTestData.Secret, text, StringComparison.Ordinal);
                Assert.DoesNotContain("A1B2C3D4E5F6", text, StringComparison.OrdinalIgnoreCase);
            }
        }

        // ── Storing a key ──

        [Fact]
        public async Task A_stored_key_is_kept_as_upper_case_utf16_digits_under_the_prefix_and_is_read_back()
        {
            var store = new FakeCredentialStore();
            var provider = new WindowsCredentialKeyProvider(store);
            var key = new byte[] { 0xA1, 0xB2, 0xC3, 0xD4, 0xE5, 0xF6 };

            Assert.Equal(0, provider.Store("cards/main", key));

            Assert.Equal(Encoding.Unicode.GetBytes("A1B2C3D4E5F6"), store.Blobs[Prefix + "cards/main"]);
            using var answer = await provider.ResolveAsync(RequestFor("cards/main"), CancellationToken.None);
            Assert.Equal(key, Assert.IsType<SecureKeyLease>(answer.Key).Key.ToArray());
        }

        [Fact]
        public void The_blob_made_for_a_stored_key_is_on_the_pinned_heap_so_that_erasing_it_reaches_the_only_copy()
        {
            var blob = CredentialBlob.FromKey(new byte[] { 0xA1, 0xB2, 0xC3, 0xD4, 0xE5, 0xF6 });

            // An array on the pinned object heap is generation 2 from the first moment; one on the ordinary heap is generation 0.
            Assert.Equal(GC.MaxGeneration, GC.GetGeneration(blob));
        }

        [Fact]
        public void The_blob_handed_to_the_vault_for_a_stored_key_is_erased_afterwards()
        {
            var store = new FakeCredentialStore();
            var provider = new WindowsCredentialKeyProvider(store);

            provider.Store("cards/main", new byte[] { 0xA1, 0xB2, 0xC3, 0xD4, 0xE5, 0xF6 });

            Assert.All(Assert.Single(store.Written), b => Assert.Equal(0, b));
        }

        [Theory]
        [InlineData("")]
        [InlineData("a b")]
        [InlineData("git:https://x y")]
        public void A_key_is_not_stored_under_a_reference_that_would_not_be_read_back(string reference)
        {
            var store = new FakeCredentialStore();
            var provider = new WindowsCredentialKeyProvider(store);

            Assert.Equal(CredentialErrors.InvalidParameter, provider.Store(reference, new byte[6]));
            Assert.Empty(store.Writes);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(33)]
        public void A_key_of_a_length_that_would_not_be_read_back_is_not_stored(int length)
        {
            var store = new FakeCredentialStore();
            var provider = new WindowsCredentialKeyProvider(store);

            Assert.Equal(CredentialErrors.InvalidParameter, provider.Store("cards/main", new byte[length]));
            Assert.Empty(store.Writes);
        }

        [Fact]
        public void A_refusal_of_the_vault_is_reported_and_a_vault_that_throws_does_not_escape()
        {
            var store = new FakeCredentialStore { WriteError = CredentialErrors.AccessDenied };
            var provider = new WindowsCredentialKeyProvider(store);

            Assert.Equal(CredentialErrors.AccessDenied, provider.Store("cards/main", new byte[6]));

            store.WriteError = 0;
            store.ThrowText = "boom";
            Assert.Equal(CredentialErrors.Failed, provider.Store("cards/main", new byte[6]));
            Assert.Equal(CredentialErrors.Failed, provider.Remove("cards/main"));
        }

        [Fact]
        public void A_removed_key_is_removed_below_the_prefix_only()
        {
            var store = new FakeCredentialStore();
            store.Blobs[Prefix + "cards/main"] = Encoding.Unicode.GetBytes("A1B2C3D4E5F6");
            var provider = new WindowsCredentialKeyProvider(store);

            Assert.Equal(0, provider.Remove("cards/main"));
            Assert.Equal(CredentialErrors.InvalidParameter, provider.Remove("a b"));

            Assert.Equal(Prefix + "cards/main", Assert.Single(store.Deletes));
            Assert.Empty(store.Blobs);
        }

        // ── Listing the references ──

        [Fact]
        public void The_references_that_are_stored_are_listed_without_the_prefix_in_order_and_no_key_is_read()
        {
            var store = new FakeCredentialStore();
            store.Blobs[Prefix + "cards/b"] = Encoding.Unicode.GetBytes("A1B2C3D4E5F6");
            store.Blobs[Prefix + "Cards/a"] = Encoding.Unicode.GetBytes("A1B2C3D4E5F6");
            store.Blobs[Prefix + "migrated/0123456789abcdef"] = Encoding.Unicode.GetBytes("A1B2C3D4E5F6");
            var provider = new WindowsCredentialKeyProvider(store);

            var list = provider.List();

            Assert.Equal(0, list.Error);
            Assert.Equal(new[] { "Cards/a", "cards/b", "migrated/0123456789abcdef" }, list.References);

            // Only the names are asked for: the secret of a credential is never read to show a list.
            Assert.Empty(store.Reads);
            Assert.Equal(Prefix, Assert.Single(store.Enumerated));
        }

        [Fact]
        public void A_credential_of_another_application_or_with_a_name_that_is_not_a_reference_is_not_listed_even_when_the_vault_hands_it_out()
        {
            var store = new FakeCredentialStore { EnumerateIgnoresPrefix = true };
            store.Blobs[Prefix + "cards/main"] = Encoding.Unicode.GetBytes("A1B2C3D4E5F6");
            store.Blobs["OtherApp/key/cards"] = Encoding.Unicode.GetBytes("A1B2C3D4E5F6");
            store.Blobs[Prefix + "has space"] = Encoding.Unicode.GetBytes("A1B2C3D4E5F6");
            store.Blobs[Prefix + "ümlaut"] = Encoding.Unicode.GetBytes("A1B2C3D4E5F6");
            store.Blobs[Prefix] = Encoding.Unicode.GetBytes("A1B2C3D4E5F6");

            var list = new WindowsCredentialKeyProvider(store).List();

            Assert.Equal(new[] { "cards/main" }, list.References);
        }

        [Fact]
        public void An_empty_vault_has_an_empty_list_and_no_error()
        {
            var list = new WindowsCredentialKeyProvider(new FakeCredentialStore()).List();

            Assert.Equal(0, list.Error);
            Assert.Empty(list.References);
        }

        [Fact]
        public void A_vault_that_refuses_gives_the_error_and_an_empty_list()
        {
            var store = new FakeCredentialStore { EnumerateError = CredentialErrors.AccessDenied };
            store.Blobs[Prefix + "cards/main"] = Encoding.Unicode.GetBytes("A1B2C3D4E5F6");

            var list = new WindowsCredentialKeyProvider(store).List();

            Assert.Equal(CredentialErrors.AccessDenied, list.Error);
            Assert.Empty(list.References);
        }

        [Fact]
        public void A_vault_that_throws_gives_the_failed_code_and_the_log_shows_the_type_only()
        {
            using var logs = CapturedLogs.Install();
            var store = new FakeCredentialStore { ThrowText = SecureKeyTestData.Secret };

            var list = new WindowsCredentialKeyProvider(store).List();

            Assert.Equal(CredentialErrors.Failed, list.Error);
            Assert.Empty(list.References);
            Assert.NotEmpty(logs.All());
            Assert.All(logs.All().Select(CapturedLogs.EverythingWrittenFor), text => Assert.DoesNotContain(SecureKeyTestData.Secret, text, StringComparison.Ordinal));
        }

        // ── Part of the application ──

        [Fact]
        public void The_id_and_the_name_of_the_provider_are_valid()
        {
            Assert.True(SecureKeySyntax.IsValidProviderId(WindowsCredentialKeyProvider.ProviderId));
            Assert.False(string.IsNullOrWhiteSpace(WindowsCredentialKeyProvider.DisplayName));
        }

        [Fact]
        public void The_registry_of_the_application_holds_the_provider_even_when_the_extensions_cannot_be_listed()
        {
            var registry = SecureKeyRegistry.FromContainer(null, SecureKeyRegistry.BuiltInProviders());

            // The built-in ones: the Credential Manager, and the program that is connected to the external control bridge.
            Assert.Equal(new[] { BridgeKeyProvider.ProviderId, WindowsCredentialKeyProvider.ProviderId }, registry.Providers.Select(p => p.Id));
            var info = Assert.Single(registry.Providers, p => p.Id == WindowsCredentialKeyProvider.ProviderId);
            Assert.Equal(WindowsCredentialKeyProvider.DisplayName, info.DisplayName);
        }

        [Fact]
        public void When_the_extensions_cannot_be_composed_the_registry_still_holds_the_provider_and_the_log_shows_the_type_only()
        {
            using var logs = CapturedLogs.Install();

            var registry = SecureKeyRegistry.FromComposition(() => throw new InvalidOperationException(SecureKeyTestData.Secret));

            Assert.Contains(registry.Providers, info => info.Id == WindowsCredentialKeyProvider.ProviderId);
            Assert.NotEmpty(logs.All());
            Assert.All(logs.All().Select(CapturedLogs.EverythingWrittenFor), text => Assert.DoesNotContain(SecureKeyTestData.Secret, text, StringComparison.Ordinal));
        }

        [Fact]
        public void The_registry_the_application_builds_holds_the_provider()
        {
            var registry = SecureKeyRegistry.FromApplicationExtensions();

            Assert.Contains(registry.Providers, info => info.Id == WindowsCredentialKeyProvider.ProviderId);
        }

        [Fact]
        public async Task The_registry_asks_the_provider_for_a_key_and_gets_it()
        {
            var store = new FakeCredentialStore();
            store.Blobs[Prefix + "cards/main"] = Encoding.Unicode.GetBytes("A1B2C3D4E5F6");
            var registry = SecureKeyRegistry.Create(
                new[] { new SecureKeyProviderRegistration(WindowsCredentialKeyProvider.ProviderId, WindowsCredentialKeyProvider.DisplayName, new WindowsCredentialKeyProvider(store)) },
                Array.Empty<Lazy<ISecureKeyProvider, RFiDGear.ExtensionSdk.ISecureKeyProviderMetadata>>());

            using var answer = await registry.ResolveAsync(WindowsCredentialKeyProvider.ProviderId, RequestFor("cards/main"), CancellationToken.None);

            Assert.Equal("A1B2C3D4E5F6", Convert.ToHexString(Assert.IsType<SecureKeyLease>(answer.Key).Key));
        }

        // ── The real vault ──

        [Fact]
        public async Task A_key_stored_in_the_real_credential_manager_is_read_back_and_removed()
        {
            // Touches the vault of the person who runs the test: a name of its own, removed in any case.
            var provider = new WindowsCredentialKeyProvider(new NativeCredentialStore());
            var reference = "tests/" + Guid.NewGuid().ToString("N");
            var key = Enumerable.Range(0, 16).Select(i => (byte)(0x30 + i)).ToArray();

            try
            {
                Assert.Equal(0, provider.Store(reference, key));

                using var answer = await provider.ResolveAsync(RequestFor(reference), CancellationToken.None);
                Assert.True(answer.IsResolved, $"the real vault answered {answer.FailureReason}");
                Assert.Equal(key, Assert.IsType<SecureKeyLease>(answer.Key).Key.ToArray());

                // The copy of the secret that the store hands out is on the pinned heap (generation 2 from the start).
                var read = new NativeCredentialStore().Read(Prefix + reference);

                try
                {
                    Assert.Equal(0, read.Error);
                    Assert.Equal(GC.MaxGeneration, GC.GetGeneration(read.Blob!));
                }
                finally
                {
                    if (read.Blob != null)
                    {
                        CryptographicOperations.ZeroMemory(read.Blob);
                    }
                }
            }
            finally
            {
                provider.Remove(reference);
            }

            using var gone = await provider.ResolveAsync(RequestFor(reference), CancellationToken.None);
            Assert.Equal(SecureKeyFailureReason.NotFound, gone.FailureReason);
        }

        [Fact]
        public void A_key_stored_in_the_real_credential_manager_is_listed_by_its_reference_until_it_is_removed()
        {
            // The vault of the person who runs the test may hold other keys of ours: the list is only asked for the one of this test.
            var provider = new WindowsCredentialKeyProvider(new NativeCredentialStore());
            var reference = "tests/" + Guid.NewGuid().ToString("N");

            try
            {
                Assert.Equal(0, provider.Store(reference, Enumerable.Range(0, 6).Select(i => (byte)(0x10 + i)).ToArray()));

                var list = provider.List();
                Assert.Equal(0, list.Error);
                Assert.Contains(reference, list.References);
            }
            finally
            {
                provider.Remove(reference);
            }

            Assert.DoesNotContain(reference, provider.List().References);
        }

        [Fact]
        public async Task A_key_stored_with_the_cmdkey_tool_as_documented_is_read()
        {
            // The way a person without a tool of ours stores a key: the format of the blob is the one cmdkey writes.
            var provider = new WindowsCredentialKeyProvider(new NativeCredentialStore());
            var reference = "tests/" + Guid.NewGuid().ToString("N");
            var target = Prefix + reference;

            try
            {
                // A dummy value on the command line, only to pin the format that the tool stores. A real key does not belong
                // there (the documentation says to use the prompt of the tool), but the prompt reads from the console and
                // cannot be driven by a test: with redirected input it stores an empty password.
                Assert.Equal(0, RunCmdkey($"/generic:{target} /user:RFiDGear /pass:A1B2C3D4E5F6"));

                using var answer = await provider.ResolveAsync(RequestFor(reference), CancellationToken.None);
                Assert.True(answer.IsResolved, $"the real vault answered {answer.FailureReason}");
                Assert.Equal("A1B2C3D4E5F6", Convert.ToHexString(Assert.IsType<SecureKeyLease>(answer.Key).Key));
            }
            finally
            {
                RunCmdkey($"/delete:{target}");
            }
        }

        private static int RunCmdkey(string arguments)
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmdkey.exe", arguments)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            })!;

            // Nothing to answer: closing the input keeps the tool from waiting for it.
            process.StandardInput.Close();
            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();

            return process.ExitCode;
        }

        private static SecureKeyRequest RequestFor(string reference) => new SecureKeyRequest
        {
            SlotId = "desfire.app.current",
            Reference = reference,
            Algorithm = SecureKeyAlgorithm.Aes128,
            Usage = SecureKeyUsage.Authenticate,
            Scope = SecureKeyScope.None,
            RunId = "run-1",
            TaskIndex = "0",
            CardUid = null,
            Interactive = false,
        };
    }
}
