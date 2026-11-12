using RFiDGear.HIL.Tests;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Services.SecureKeys;
using RFiDGear.Tests.KeySlots;
using RFiDGear.ViewModel.TaskSetupViewModels;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xunit;

namespace RFiDGear.Tests.SecureKeys
{
    /// <summary>
    /// The building blocks of the secure-key hardware cases (<c>RFiDGear.HIL.Tests/SecureKeyWorkflowTests</c>), run here
    /// against a scripted reader and a dictionary as the vault: the tasks they build, and the helper that puts test keys into
    /// the Credential Manager. A case that is wrong in its set-up (a slot id that is not one, a binding that is not in effect,
    /// a typed key that is used after all) would fail on a card in a way that looks like a defect of the application; this is
    /// what shows the difference before the cards are involved. The cases themselves need a reader.
    /// </summary>
    public sealed class HilSecureKeyScenarioSelfCheckTests : IDisposable
    {
        private const string Default = "00000000000000000000000000000000";
        private const string Other = "0102030405060708090A0B0C0D0E0F10";

        private readonly FakeCredentialStore _store = new FakeCredentialStore();

        public HilSecureKeyScenarioSelfCheckTests()
        {
            var registry = SecureKeyRegistry.Create(
                new[] { new SecureKeyProviderRegistration(WindowsCredentialKeyProvider.ProviderId, WindowsCredentialKeyProvider.DisplayName, new WindowsCredentialKeyProvider(_store)) },
                Array.Empty<Lazy<RFiDGear.Contracts.ISecureKeyProvider, RFiDGear.ExtensionSdk.ISecureKeyProviderMetadata>>());
            KeyResolutionScope.SetDefault(new KeyResolutionContext(registry, "run-1", "0", null, interactive: false));
        }

        public void Dispose()
        {
            KeyResolutionScope.SetDefault(null);
        }

        public static TheoryData<DESFireKeyType> KeyTypes => new TheoryData<DESFireKeyType> { DESFireKeyType.DF_KEY_AES, DESFireKeyType.DF_KEY_DES };

        // ── The tasks ──

        [Theory]
        [MemberData(nameof(KeyTypes))]
        public async Task ABoundAuthentication_ReachesTheCardWithTheKeyOfTheVault_AndNotWithTheTypedOne(DESFireKeyType keyType)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var vault = new HilVault(_store);
                var reference = vault.Store("app-key", Other);
                var task = HilSecureKeyTasks.Authenticate(keyType, Default, 0, HilSecureKeyTasks.Bind(HilSecureKeyTasks.AppCurrent, reference));

                var (reader, error) = await RunAsync(task);

                var call = Assert.Single(reader.Calls);
                Assert.Equal("AuthToMifareDesfireApplication", call.Method);
                Assert.Equal(new[] { Other }, call.Keys);
                Assert.Equal(ERROR.NoError, error);
            });
        }

        [Theory]
        [MemberData(nameof(KeyTypes))]
        public async Task ATypedAuthentication_ReachesTheCardWithTheTypedKey_AndAsksNoProvider(DESFireKeyType keyType)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var task = HilSecureKeyTasks.Authenticate(keyType, Other, 0);

                var (reader, error) = await RunAsync(task);

                Assert.Equal(new[] { Other }, Assert.Single(reader.Calls).Keys);
                Assert.Equal(ERROR.NoError, error);
                Assert.Empty(_store.Reads);
            });
        }

        [Theory]
        [MemberData(nameof(KeyTypes))]
        public async Task ABoundKeyChange_ReachesTheCardWithBothKeysOfTheVault_WhateverIsTyped(DESFireKeyType keyType)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var vault = new HilVault(_store);
                var current = vault.Store("current-key", Default);
                var next = vault.Store("next-key", Other);

                // The typed keys are swapped on purpose, as in the case on the card.
                var task = HilSecureKeyTasks.ChangeAppKey(
                    keyType, Other, Default, 0,
                    HilSecureKeyTasks.Bind(HilSecureKeyTasks.AppCurrent, current),
                    HilSecureKeyTasks.Bind(HilSecureKeyTasks.AppNew, next));

                var (reader, error) = await RunAsync(task);

                var change = Assert.Single(reader.CallsOf("ChangeMifareDesfireKeyAsync"));
                Assert.Equal(Other, change.Keys[1]);
                Assert.Equal(Default, change.Keys[2]);
                Assert.Equal(ERROR.NoError, error);
            });
        }

        [Theory]
        [MemberData(nameof(KeyTypes))]
        public async Task ABoundKeyChange_BackAgain_UsesTheCredentialsTheOtherWayRound(DESFireKeyType keyType)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var vault = new HilVault(_store);
                var current = vault.Store("current-key", Default);
                var next = vault.Store("next-key", Other);

                var task = HilSecureKeyTasks.ChangeAppKey(
                    keyType, Default, Other, 0,
                    HilSecureKeyTasks.Bind(HilSecureKeyTasks.AppCurrent, next),
                    HilSecureKeyTasks.Bind(HilSecureKeyTasks.AppNew, current));

                var (reader, _) = await RunAsync(task);

                var change = Assert.Single(reader.CallsOf("ChangeMifareDesfireKeyAsync"));
                Assert.Equal(Default, change.Keys[1]);
                Assert.Equal(Other, change.Keys[2]);
            });
        }

        // ── A credential that cannot give the key ──

        [Theory]
        [InlineData(DESFireKeyType.DF_KEY_AES, true)]
        [InlineData(DESFireKeyType.DF_KEY_AES, false)]
        [InlineData(DESFireKeyType.DF_KEY_DES, true)]
        [InlineData(DESFireKeyType.DF_KEY_DES, false)]
        public async Task AReferenceThatGivesNoKey_StopsTheChangeBeforeTheCard_WhetherItIsTheNewKeyOrTheOneThatOpens(DESFireKeyType keyType, bool newKeyIsMissing)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var vault = new HilVault(_store);
                var present = vault.Store("present-key", Default);
                var missing = vault.NeverStored("missing-key");

                // The key typed into the task is the right one: it must not be a way around the binding.
                var task = HilSecureKeyTasks.ChangeAppKey(
                    keyType, Default, Other, 0,
                    HilSecureKeyTasks.Bind(HilSecureKeyTasks.AppCurrent, newKeyIsMissing ? present : missing),
                    HilSecureKeyTasks.Bind(HilSecureKeyTasks.AppNew, newKeyIsMissing ? missing : present));

                var (reader, error) = await RunAsync(task);

                Assert.Equal(ERROR.KeyUnavailable, error);
                Assert.Empty(reader.Calls);
            });
        }

        [Theory]
        [InlineData(DESFireKeyType.DF_KEY_AES, "010203040506")]
        [InlineData(DESFireKeyType.DF_KEY_AES, "0102030405060708090A0B0C0D0E0F101112131415161718")]
        [InlineData(DESFireKeyType.DF_KEY_DES, "010203040506")]
        [InlineData(DESFireKeyType.DF_KEY_DES, "0102030405060708090A0B0C0D0E0F101112131415161718")]
        public async Task AKeyOfTheWrongLength_StopsTheAuthenticationAndTheChangeBeforeTheCard(DESFireKeyType keyType, string wrongKey)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var vault = new HilVault(_store);
                var current = vault.Store("current-key", Default);
                var wrong = vault.Store("wrong-key", wrongKey);

                var authenticate = HilSecureKeyTasks.Authenticate(keyType, Default, 0, HilSecureKeyTasks.Bind(HilSecureKeyTasks.AppCurrent, wrong));
                var change = HilSecureKeyTasks.ChangeAppKey(
                    keyType, Default, Other, 0,
                    HilSecureKeyTasks.Bind(HilSecureKeyTasks.AppCurrent, current),
                    HilSecureKeyTasks.Bind(HilSecureKeyTasks.AppNew, wrong));

                var (authenticateReader, authenticateError) = await RunAsync(authenticate);
                var (changeReader, changeError) = await RunAsync(change);

                Assert.Equal(ERROR.KeyUnavailable, authenticateError);
                Assert.Empty(authenticateReader.Calls);
                Assert.Equal(ERROR.KeyUnavailable, changeError);
                Assert.Empty(changeReader.Calls);
            });
        }

        // ── The vault of the cases ──

        [Fact]
        public void TheVault_StoresUnderAReferenceOfItsOwn_AndRemovesWhatItStoredButNothingElse()
        {
            var foreign = WindowsCredentialKeyProvider.TargetPrefix + "somebody-elses-key";
            _store.Blobs[foreign] = Encoding.Unicode.GetBytes(Default);
            string reference;

            using (var vault = new HilVault(_store))
            {
                reference = vault.Store("app-key", Other);

                Assert.StartsWith("hil/", reference, StringComparison.Ordinal);
                Assert.True(SecureKeySyntax.IsValidReference(reference));
                Assert.Equal(Other, Encoding.Unicode.GetString(_store.Blobs[WindowsCredentialKeyProvider.TargetPrefix + reference]), ignoreCase: true);
            }

            Assert.DoesNotContain(WindowsCredentialKeyProvider.TargetPrefix + reference, _store.Blobs.Keys);
            Assert.Contains(foreign, _store.Blobs.Keys);
        }

        [Fact]
        public void TheVault_NeverStoresUnderAReferenceItGaveForNothing_AndEveryVaultHasItsOwnReferences()
        {
            using var first = new HilVault(_store);
            using var second = new HilVault(_store);

            var missing = first.NeverStored("missing-key");

            Assert.DoesNotContain(WindowsCredentialKeyProvider.TargetPrefix + missing, _store.Blobs.Keys);
            Assert.NotEqual(missing, second.NeverStored("missing-key"));
            Assert.NotEqual(first.Store("app-key", Other), second.Store("app-key", Other));
        }

        [Fact]
        public void TheVault_SaysSoWhenTheCredentialManagerRefusesAKey_AndHasNothingToRemoveAfterwards()
        {
            _store.WriteError = CredentialErrors.AccessDenied;

            using (var vault = new HilVault(_store))
            {
                var failure = Assert.Throws<InvalidOperationException>(() => vault.Store("app-key", Other));
                Assert.Contains(CredentialErrors.AccessDenied.ToString(), failure.Message, StringComparison.Ordinal);
            }

            Assert.Empty(_store.Blobs);
            Assert.Empty(_store.Deletes);
        }

        // Runs the task as its dialog does, against a reader that records the keys it receives and answers success.
        private static async Task<(KeyRecordingReader Reader, ERROR Error)> RunAsync(MifareDesfireSetupViewModel task)
        {
            var reader = new KeyRecordingReader();

            using (ReaderDeviceScope.Use(reader))
            {
                await task.CommandDelegator.ExecuteAsync(task.SelectedTaskType);
            }

            return (reader, task.CurrentTaskErrorLevel);
        }
    }
}
