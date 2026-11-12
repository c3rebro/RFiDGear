#nullable enable
using RFiDGear.Contracts;
using RFiDGear.Infrastructure;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Xunit;

namespace RFiDGear.HIL.Tests
{
    /// <summary>
    /// A slot of a DESFire task that is bound to the Windows Credential Manager, run against a real card: the key comes from
    /// the vault, a binding that cannot be fulfilled stops the task before the card is touched, and a typed key is never a
    /// way around a binding. They work on the reserved test application only (<see cref="HilConstants.TestAppId"/>), the way
    /// the other DESFire cases do, and they are the real-card counterpart of the key acquisition tests of
    /// <c>RFiDGear.Tests</c>, which run against a scripted reader. See SECURE-KEYS.md for the cases that need a person.
    /// <para>
    /// Each case puts test keys into the Windows Credential Manager of this user under <c>RFiDGear/key/hil/...</c> and
    /// removes them in the end, and changes the key of the test application; the application is deleted again with the
    /// PICC master key, which no case changes. They run only when <c>hil.settings.json</c> sets <c>secureKeys</c>.
    /// </para>
    /// <para>
    /// In every case the keys that are typed into a bound slot are decoys: they are the ones the card would accept where
    /// the vault key is refused, or the other way round, so that a task which used the typed key instead of the bound one
    /// fails the case.
    /// </para>
    /// </summary>
    [Collection("HIL")]
    public sealed class SecureKeyWorkflowTests
    {
        private const string OtherKey = "0102030405060708090A0B0C0D0E0F10";

        // 6 bytes: the length of a MIFARE Classic key. 24 bytes: a 3K3DES key. Neither is a key of a 16-byte application.
        private const string TooShort = "010203040506";
        private const string TooLong = "0102030405060708090A0B0C0D0E0F101112131415161718";

        private readonly HilFixture _fixture;

        /// <summary>Creates the cases with the fixture that holds the reader.</summary>
        /// <param name="fixture">The fixture that connects to the reader and checks for a DESFire card.</param>
        public SecureKeyWorkflowTests(HilFixture fixture)
        {
            _fixture = fixture;
        }

        /// <summary>Key type and the key a new application has.</summary>
        public static TheoryData<DESFireKeyType, string> KeyTypesWithTheirDefaultKey =>
            new TheoryData<DESFireKeyType, string>
            {
                { DESFireKeyType.DF_KEY_AES, HilConstants.DefaultKeyAes },
                { DESFireKeyType.DF_KEY_DES, HilConstants.DefaultKeyDes },
            };

        /// <summary>Key type, the key a new application has, and another key of the same type.</summary>
        public static TheoryData<DESFireKeyType, string, string> KeyTypes =>
            new TheoryData<DESFireKeyType, string, string>
            {
                { DESFireKeyType.DF_KEY_AES, HilConstants.DefaultKeyAes, OtherKey },
                { DESFireKeyType.DF_KEY_DES, HilConstants.DefaultKeyDes, OtherKey },
            };

        /// <summary>
        /// The application key is changed with typed keys to one that only the Credential Manager knows. A task whose slot is
        /// bound to that credential opens the application; the typed key in the task is the old one, which the card now
        /// refuses.
        /// </summary>
        /// <param name="keyType">Key type of the application.</param>
        /// <param name="defaultKey">The key of a new application.</param>
        /// <param name="otherKey">Another key of the same type.</param>
        /// <returns>A task that ends with the case.</returns>
        [HilSecureKeyTheory]
        [MemberData(nameof(KeyTypes))]
        public async Task ABoundSlot_OpensTheApplicationWithTheKeyFromTheCredentialManager(DESFireKeyType keyType, string defaultKey, string otherKey)
        {
            _fixture.SkipIfNoHardware();

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var vault = new HilVault();

                try
                {
                    await ArrangeApplicationAsync(keyType);
                    await SucceedsAsync("Changing the application key with typed keys", keyType,
                        HilSecureKeyTasks.ChangeAppKey(keyType, defaultKey, otherKey, 0));
                    var reference = vault.Store("app-key", otherKey);

                    await SucceedsAsync("Authenticating with the key from the Credential Manager", keyType,
                        HilSecureKeyTasks.Authenticate(keyType, defaultKey, 0, HilSecureKeyTasks.Bind(HilSecureKeyTasks.AppCurrent, reference)));
                }
                finally
                {
                    await _fixture.CleanupTestAidAsync();
                }
            });
        }

        /// <summary>
        /// A key change with both keys from the Credential Manager, the key that opens the application and the new key, and
        /// then back the other way round. After each change the card is asked with typed keys: the new key works, the old
        /// one is gone. The typed keys of the bound change are swapped on purpose.
        /// </summary>
        /// <param name="keyType">Key type of the application.</param>
        /// <param name="defaultKey">The key of a new application.</param>
        /// <param name="otherKey">Another key of the same type.</param>
        /// <returns>A task that ends with the case.</returns>
        [HilSecureKeyTheory]
        [MemberData(nameof(KeyTypes))]
        public async Task ABoundKeyChange_UsesTheBoundAuthenticationKeyAndTheBoundNewKey_AndBackAgain(DESFireKeyType keyType, string defaultKey, string otherKey)
        {
            _fixture.SkipIfNoHardware();

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var vault = new HilVault();

                try
                {
                    await ArrangeApplicationAsync(keyType);
                    var current = vault.Store("current-key", defaultKey);
                    var next = vault.Store("next-key", otherKey);

                    await SucceedsAsync("Changing the key with both keys from the Credential Manager", keyType,
                        HilSecureKeyTasks.ChangeAppKey(
                            keyType, otherKey, defaultKey, 0,
                            HilSecureKeyTasks.Bind(HilSecureKeyTasks.AppCurrent, current),
                            HilSecureKeyTasks.Bind(HilSecureKeyTasks.AppNew, next)));
                    await SucceedsAsync("Authenticating with the new key, typed", keyType,
                        HilSecureKeyTasks.Authenticate(keyType, otherKey, 0));

                    await SucceedsAsync("Changing the key back with both keys from the Credential Manager", keyType,
                        HilSecureKeyTasks.ChangeAppKey(
                            keyType, defaultKey, otherKey, 0,
                            HilSecureKeyTasks.Bind(HilSecureKeyTasks.AppCurrent, next),
                            HilSecureKeyTasks.Bind(HilSecureKeyTasks.AppNew, current)));
                    await SucceedsAsync("Authenticating with the first key again, typed", keyType,
                        HilSecureKeyTasks.Authenticate(keyType, defaultKey, 0));
                }
                finally
                {
                    await _fixture.CleanupTestAidAsync();
                }
            });
        }

        /// <summary>
        /// A reference that gives no key stops the task before the card is touched, with the outcome KeyUnavailable, for the
        /// new key and for the key that opens the application. The key typed into the task is the right one in both: it is
        /// not used as a way around the binding. Afterwards the card still has its old key.
        /// </summary>
        /// <param name="keyType">Key type of the application.</param>
        /// <param name="defaultKey">The key of a new application.</param>
        /// <param name="otherKey">Another key of the same type.</param>
        /// <returns>A task that ends with the case.</returns>
        [HilSecureKeyTheory]
        [MemberData(nameof(KeyTypes))]
        public async Task AReferenceThatGivesNoKey_StopsBeforeTheCard_AndTheOldKeyStillWorks(DESFireKeyType keyType, string defaultKey, string otherKey)
        {
            _fixture.SkipIfNoHardware();

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var vault = new HilVault();

                try
                {
                    await ArrangeApplicationAsync(keyType);
                    var current = vault.Store("current-key", defaultKey);
                    var next = vault.Store("next-key", otherKey);
                    var missing = vault.NeverStored("missing-key");

                    await KeyIsUnavailableAsync("A new key whose credential does not exist", keyType,
                        HilSecureKeyTasks.ChangeAppKey(
                            keyType, defaultKey, otherKey, 0,
                            HilSecureKeyTasks.Bind(HilSecureKeyTasks.AppCurrent, current),
                            HilSecureKeyTasks.Bind(HilSecureKeyTasks.AppNew, missing)));
                    await SucceedsAsync("Authenticating with the old key after the first refusal", keyType,
                        HilSecureKeyTasks.Authenticate(keyType, defaultKey, 0));

                    await KeyIsUnavailableAsync("A key for the authentication whose credential does not exist", keyType,
                        HilSecureKeyTasks.ChangeAppKey(
                            keyType, defaultKey, otherKey, 0,
                            HilSecureKeyTasks.Bind(HilSecureKeyTasks.AppCurrent, missing),
                            HilSecureKeyTasks.Bind(HilSecureKeyTasks.AppNew, next)));
                    await SucceedsAsync("Authenticating with the old key after the second refusal", keyType,
                        HilSecureKeyTasks.Authenticate(keyType, defaultKey, 0));
                }
                finally
                {
                    await _fixture.CleanupTestAidAsync();
                }
            });
        }

        /// <summary>
        /// Two tasks in one run: the first opens the application with a key from the Credential Manager, the second names a
        /// credential that does not exist, and the card would accept the key of the first. The second must fail with
        /// KeyUnavailable and must not use the key of the first task, nor its own typed key.
        /// </summary>
        /// <param name="keyType">Key type of the application.</param>
        /// <param name="defaultKey">The key of a new application.</param>
        /// <returns>A task that ends with the case.</returns>
        [HilSecureKeyTheory]
        [MemberData(nameof(KeyTypesWithTheirDefaultKey))]
        public async Task TheSecondTasksMissingCredential_DoesNotUseTheKeyOfTheFirstTask(DESFireKeyType keyType, string defaultKey)
        {
            _fixture.SkipIfNoHardware();

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var vault = new HilVault();

                try
                {
                    await ArrangeApplicationAsync(keyType);
                    var present = vault.Store("current-key", defaultKey);
                    var missing = vault.NeverStored("missing-key");

                    var (result, diagnostics) = await RunAsync(new object[]
                    {
                        HilSecureKeyTasks.Authenticate(keyType, defaultKey, 0, HilSecureKeyTasks.Bind(HilSecureKeyTasks.AppCurrent, present)),
                        HilSecureKeyTasks.Authenticate(keyType, defaultKey, 1, HilSecureKeyTasks.Bind(HilSecureKeyTasks.AppCurrent, missing)),
                    });

                    Assert.Equal(2, result.TaskOutcomes.Count);
                    Assert.True(result.TaskOutcomes[0].IsSuccess == true, $"The first task did not open the application — keyType={keyType}\n{diagnostics}");
                    Assert.True(
                        result.TaskOutcomes[1].IsSuccess != true && result.TaskOutcomes[1].NormalizedErrorCode == nameof(ERROR.KeyUnavailable),
                        $"The second task should end with KeyUnavailable, it ended with {result.TaskOutcomes[1].NormalizedErrorCode} — keyType={keyType}\n{diagnostics}");
                }
                finally
                {
                    await _fixture.CleanupTestAidAsync();
                }
            });
        }

        /// <summary>
        /// A credential whose key has the wrong length for the slot (6 bytes, the length of a MIFARE Classic key, and 24
        /// bytes, a 3K3DES key; the application has 16-byte keys) is refused before the card is touched: for the key that
        /// opens the application and for the new key. Afterwards the old key still works.
        /// </summary>
        /// <param name="keyType">Key type of the application.</param>
        /// <param name="defaultKey">The key of a new application.</param>
        /// <param name="otherKey">Another key of the same type.</param>
        /// <returns>A task that ends with the case.</returns>
        [HilSecureKeyTheory]
        [MemberData(nameof(KeyTypes))]
        public async Task AKeyOfTheWrongLength_IsRefusedBeforeTheCard_AndTheOldKeyStillWorks(DESFireKeyType keyType, string defaultKey, string otherKey)
        {
            _fixture.SkipIfNoHardware();

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var vault = new HilVault();

                try
                {
                    await ArrangeApplicationAsync(keyType);
                    var current = vault.Store("current-key", defaultKey);
                    var tooShort = vault.Store("too-short", TooShort);
                    var tooLong = vault.Store("too-long", TooLong);

                    foreach (var (name, reference) in new[] { ("6 bytes", tooShort), ("24 bytes", tooLong) })
                    {
                        await KeyIsUnavailableAsync($"A key of {name} for the authentication", keyType,
                            HilSecureKeyTasks.Authenticate(keyType, defaultKey, 0, HilSecureKeyTasks.Bind(HilSecureKeyTasks.AppCurrent, reference)));

                        await KeyIsUnavailableAsync($"A new key of {name}", keyType,
                            HilSecureKeyTasks.ChangeAppKey(
                                keyType, defaultKey, otherKey, 0,
                                HilSecureKeyTasks.Bind(HilSecureKeyTasks.AppCurrent, current),
                                HilSecureKeyTasks.Bind(HilSecureKeyTasks.AppNew, reference)));
                    }

                    await SucceedsAsync("Authenticating with the old key after the refusals", keyType,
                        HilSecureKeyTasks.Authenticate(keyType, defaultKey, 0));
                }
                finally
                {
                    await _fixture.CleanupTestAidAsync();
                }
            });
        }

        // Starts from a clean card: no test application left by an interrupted run, then a new one with the default key.
        private async Task ArrangeApplicationAsync(DESFireKeyType keyType)
        {
            await _fixture.CleanupTestAidAsync();
            await RunAndExpectSuccessAsync("Creating the test application", keyType, HilTaskBuilder.BuildCreateApplication(HilConstants.TestAppId, keyType).ToArray());
        }

        private Task SucceedsAsync(string what, DESFireKeyType keyType, params object[] tasks) => RunAndExpectSuccessAsync(what, keyType, tasks);

        private async Task RunAndExpectSuccessAsync(string what, DESFireKeyType keyType, IReadOnlyCollection<object> tasks)
        {
            var (result, diagnostics) = await RunAsync(tasks);

            Assert.True(result.Outcome == ExecutionOutcome.Success, $"{what} failed — keyType={keyType}, outcome {result.Outcome}\n{diagnostics}");
        }

        // The task must end with KeyUnavailable: the key could not be had, so the task made no card call.
        private async Task KeyIsUnavailableAsync(string what, DESFireKeyType keyType, params object[] tasks)
        {
            var (result, diagnostics) = await RunAsync(tasks);

            var outcome = Assert.Single(result.TaskOutcomes);
            Assert.True(
                outcome.IsSuccess != true && outcome.NormalizedErrorCode == nameof(ERROR.KeyUnavailable),
                $"{what}: the task should end with KeyUnavailable, it ended with {outcome.NormalizedErrorCode} (success: {outcome.IsSuccess}) — keyType={keyType}\n{diagnostics}");
        }

        private async Task<(ExecResult Result, string Diagnostics)> RunAsync(IEnumerable<object> tasks)
        {
            _fixture.Host!.Project.ReplaceTaskCollection(tasks);

            try
            {
                var result = await _fixture.Host.Execution.ExecuteAllAsync(ExecutionRequest.Empty);

                return (result, _fixture.Host.GetTaskErrorSummary());
            }
            finally
            {
                _fixture.Host.Execution.ResetStatus();
            }
        }
    }
}
