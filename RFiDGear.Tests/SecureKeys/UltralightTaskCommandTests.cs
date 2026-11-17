using RFiDGear.Contracts;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Infrastructure.Ultralight;
using RFiDGear.Models;
using RFiDGear.Services.SecureKeys;
using RFiDGear.Services.TaskExecution;
using RFiDGear.Tests.KeySlots;
using RFiDGear.ViewModel.TaskSetupViewModels;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Xunit;

namespace RFiDGear.Tests.SecureKeys
{
    /// <summary>
    /// What an Ultralight task does when it runs. It checks what it can without the card (the data, the pages, the chip it expects
    /// and what that allows) before it asks for a key, and asks for every key its authentication needs before the reader is called,
    /// from the provider when the slot is bound and from the project when it is not. A key that cannot be obtained, and a call that
    /// cannot be made, end the task without a card call. The embedded keys and the keys of the vault are different values in every
    /// test, so a call to the reader says which of the two it carries.
    /// </summary>
    public class UltralightTaskCommandTests : IDisposable
    {
        private const string Provider = "test.vault";
        private const string Payload = "A1B2C3D401020304";

        // What a legacy project holds in each slot, and what the vault holds. Different, so a key on the card line shows where it
        // came from. The key of a C is 16 bytes, the password 4 and the PACK 2.
        private static readonly Dictionary<string, (string Short, string Embedded, string Vault)> Slots = new Dictionary<string, (string, string, string)>
        {
            [SecureKeySlots.UltralightCKey] = ("CKey", Repeat(0xE0, 16), Repeat(0xB0, 16)),
            [SecureKeySlots.UltralightPassword] = ("Password", Repeat(0xE1, 4), Repeat(0xB1, 4)),
            [SecureKeySlots.UltralightPack] = ("Pack", Repeat(0xE2, 2), Repeat(0xB2, 2)),
        };

        private sealed record Scenario(TaskType_MifareUltralightTask Type, UltralightAuth Auth, UltralightExpectation Chip, string[] SlotIds, SecureKeyAlgorithm[] Algorithms, string Calls);

        private static readonly Dictionary<string, Scenario> Scenarios = new Dictionary<string, Scenario>
        {
            ["ReadC"] = new Scenario(
                TaskType_MifareUltralightTask.ReadData, UltralightAuth.UltralightC, UltralightExpectation.C,
                new[] { SecureKeySlots.UltralightCKey }, new[] { SecureKeyAlgorithm.TripleDes2K }, "Read[CKey]"),
            ["WriteC"] = new Scenario(
                TaskType_MifareUltralightTask.WriteData, UltralightAuth.UltralightC, UltralightExpectation.C,
                new[] { SecureKeySlots.UltralightCKey }, new[] { SecureKeyAlgorithm.TripleDes2K }, "Write[CKey]"),
            ["ReadPassword"] = new Scenario(
                TaskType_MifareUltralightTask.ReadData, UltralightAuth.Password, UltralightExpectation.Ntag215,
                new[] { SecureKeySlots.UltralightPassword, SecureKeySlots.UltralightPack },
                new[] { SecureKeyAlgorithm.UltralightPassword, SecureKeyAlgorithm.UltralightPack }, "Read[Password,Pack]"),
            ["WritePassword"] = new Scenario(
                TaskType_MifareUltralightTask.WriteData, UltralightAuth.Password, UltralightExpectation.Ntag215,
                new[] { SecureKeySlots.UltralightPassword, SecureKeySlots.UltralightPack },
                new[] { SecureKeyAlgorithm.UltralightPassword, SecureKeyAlgorithm.UltralightPack }, "Write[Password,Pack]"),
        };

        public UltralightTaskCommandTests()
        {
            KeyResolutionScope.SetDefault(null);
        }

        public void Dispose()
        {
            KeyResolutionScope.SetDefault(null);
        }

        public static IEnumerable<object[]> EveryScenario() => Scenarios.Keys.Select(name => new object[] { name });

        public static IEnumerable<object[]> EverySlotOfEveryScenario() =>
            Scenarios.SelectMany(entry => entry.Value.SlotIds.Select(slot => new object[] { entry.Key, slot }));

        private static string Repeat(byte value, int count) => string.Concat(Enumerable.Repeat(value.ToString("X2"), count));

        [Fact]
        public void EverySlotOfAnUltralightTask_IsInTheTableOfThisSuite()
        {
            Assert.Equal(SecureKeySlots.Ultralight.OrderBy(slot => slot, StringComparer.Ordinal), Slots.Keys.OrderBy(slot => slot, StringComparer.Ordinal));
        }

        // ── The keys ──

        [Theory]
        [MemberData(nameof(EveryScenario))]
        public async Task WithEverySlotBound_ATaskWorksWithTheKeysOfTheVaultOnly(string name)
        {
            var scenario = Scenarios[name];

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vault = new Vault();
                vault.Install();
                var task = NewTask(scenario);
                BindAll(task);

                var run = await RunAsync(task);

                // The same call as for embedded keys, but each key is the vault's: an embedded key would show up as "embedded:...".
                Assert.Equal(scenario.Calls, run.Calls);
                Assert.DoesNotContain("embedded:", run.Calls, StringComparison.Ordinal);
                Assert.Equal(ERROR.NoError, run.Error);
                Assert.True(run.Completed);

                // Each slot the call carries is asked for once, in the order of the call, as the kind of key it is, and no other.
                Assert.Equal(scenario.SlotIds, vault.Requests.Select(request => request.SlotId));
                Assert.Equal(scenario.Algorithms, vault.Requests.Select(request => request.Algorithm));
                Assert.All(vault.Requests, request =>
                {
                    Assert.Equal(SecureKeyUsage.Authenticate, request.Usage);
                    Assert.Null(request.Scope.ApplicationId);
                    Assert.Null(request.Scope.KeyNumber);
                    Assert.False(request.Interactive);
                });
            });
        }

        [Theory]
        [MemberData(nameof(EveryScenario))]
        public async Task AProjectThatMovedToTheProvider_HasNoEmbeddedKeysLeft_AndStillWorks(string name)
        {
            var scenario = Scenarios[name];

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                new Vault().Install();
                var task = NewTask(scenario, withEmbeddedKeys: false);
                BindAll(task);

                var run = await RunAsync(task);

                Assert.Equal(scenario.Calls, run.Calls);
                Assert.Equal(ERROR.NoError, run.Error);
            });
        }

        [Theory]
        [MemberData(nameof(EveryScenario))]
        public async Task ATaskWithoutBindings_NeverAsksAProvider_AndWorksAsItsKeysSay(string name)
        {
            var scenario = Scenarios[name];

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vault = new Vault();
                vault.Install();
                var task = NewTask(scenario);

                var run = await RunAsync(task);

                Assert.Equal(Embedded(scenario.Calls), run.Calls);
                Assert.Equal(ERROR.NoError, run.Error);
                Assert.Empty(vault.Requests);
            });
        }

        [Theory]
        [InlineData(TaskType_MifareUltralightTask.ReadData)]
        [InlineData(TaskType_MifareUltralightTask.WriteData)]
        public async Task ATaskThatDoesNotAuthenticate_NeverAsksAProvider_EvenWhenItsSlotsAreBound(TaskType_MifareUltralightTask type)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vault = new Vault();
                vault.Install();
                var task = NewTask(new Scenario(type, UltralightAuth.None, UltralightExpectation.Auto, new string[0], new SecureKeyAlgorithm[0], string.Empty));
                BindAll(task);

                var run = await RunAsync(task);

                Assert.Equal(type == TaskType_MifareUltralightTask.ReadData ? "Read[]" : "Write[]", run.Calls);
                Assert.Equal(ERROR.NoError, run.Error);
                Assert.Empty(vault.Requests);
            });
        }

        [Theory]
        [MemberData(nameof(EverySlotOfEveryScenario))]
        public async Task ASlotThatCannotBeAnswered_StopsTheTaskBeforeTheCard(string name, string slot)
        {
            var scenario = Scenarios[name];

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vault = new Vault();
                vault.Install();
                vault.FailSlots.Add(slot);
                var task = NewTask(scenario);
                BindAll(task);

                var run = await RunAsync(task);

                // All of its keys or none: a password without its PACK is not tried, and the reader is not called.
                Assert.Equal(ERROR.KeyUnavailable, run.Error);
                Assert.False(run.Completed);
                Assert.Equal(0, run.ReaderCalls);
                Assert.Contains(slot, run.Status, StringComparison.Ordinal);
                Assert.DoesNotContain("ref/", run.Status, StringComparison.Ordinal);
            });
        }

        [Theory]
        [InlineData(TaskType_MifareUltralightTask.ReadData)]
        [InlineData(TaskType_MifareUltralightTask.WriteData)]
        public async Task ASlotBoundToAKeyOfTheSam_ThatTheCommandDoesNotUse_IsNotAskedFor_AsWithAnyProvider(TaskType_MifareUltralightTask type)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                // The refusal is for the slots that a command uses: a task that does not authenticate has no key to get, from the SAM as from a
                // vault, and its card calls are the same as without the binding.
                var vault = new Vault();
                vault.Install();
                var task = NewTask(new Scenario(type, UltralightAuth.None, UltralightExpectation.Auto, new string[0], new SecureKeyAlgorithm[0], string.Empty));
                task.KeyBindings = Slots.Keys.Select(slot => new KeyBindingEntry { Slot = slot, ProviderId = SamKeyProvider.ProviderId, Reference = "1:0" }).ToArray();

                var run = await RunAsync(task);

                Assert.Equal(type == TaskType_MifareUltralightTask.ReadData ? "Read[]" : "Write[]", run.Calls);
                Assert.Equal(ERROR.NoError, run.Error);
                Assert.Empty(vault.Requests);
            });
        }

        [Theory]
        [MemberData(nameof(EveryScenario))]
        public async Task ASlotBoundToAKeyOfTheSam_StopsTheTaskBeforeTheCard(string name)
        {
            var scenario = Scenarios[name];

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                // A key of the SAM is never bytes, and no Ultralight command can hand a reference to the SAM: the task stops, asks no
                // provider, and does not fall back to the embedded key.
                var vault = new Vault();
                vault.Install();
                var task = NewTask(scenario);
                task.KeyBindings = Slots.Keys.Select(slot => new KeyBindingEntry { Slot = slot, ProviderId = SamKeyProvider.ProviderId, Reference = "1:0" }).ToArray();

                var run = await RunAsync(task);

                Assert.Equal(ERROR.KeyUnavailable, run.Error);
                Assert.False(run.Completed);
                Assert.Equal(0, run.ReaderCalls);
                Assert.DoesNotContain("1:0", StatusLines.WithoutTimestamps(run.Status), StringComparison.Ordinal);
                Assert.Empty(vault.Requests);
            });
        }

        [Fact]
        public async Task ABindingForASlotOfAnotherTask_StopsTheTask_EvenWhenItDoesNotAuthenticate()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vault = new Vault();
                vault.Install();
                var task = NewTask(new Scenario(TaskType_MifareUltralightTask.ReadData, UltralightAuth.None, UltralightExpectation.Auto, new string[0], new SecureKeyAlgorithm[0], string.Empty));
                task.KeyBindings = new[] { Bind(SecureKeySlots.DesfireRead) };

                var run = await RunAsync(task);

                Assert.Equal(ERROR.KeyUnavailable, run.Error);
                Assert.Equal(0, run.ReaderCalls);
                Assert.Empty(vault.Requests);
            });
        }

        // ── What is refused before the card, and before a key is asked for ──

        private static readonly Dictionary<string, Func<MifareUltralightSetupViewModel>> Refusals = new Dictionary<string, Func<MifareUltralightSetupViewModel>>
        {
            ["ProtectedPageWithoutTheOptIn"] = () => Write(chip: UltralightExpectation.Ntag215, start: 130),
            ["TheOptInWithoutANamedChip"] = () => Write(chip: UltralightExpectation.Auto, start: 130, allow: true),
            ["PageZeroToThree"] = () => Write(chip: UltralightExpectation.Ntag215, start: 0),
            ["AuthenticationWithoutANamedChip"] = () => Authenticated(UltralightAuth.UltralightC, UltralightExpectation.Auto),
            ["TheKeyOfAnotherKindOfChip"] = () => Authenticated(UltralightAuth.Password, UltralightExpectation.C),
            ["ThePasswordOfAChipWithoutOne"] = () => Authenticated(UltralightAuth.Password, UltralightExpectation.Plain),
            ["DataThatIsNoWholePages"] = () => Write(data: "A1B2C3"),
            ["DataThatIsNoHexadecimalText"] = () => Write(data: "ZZZZZZZZ"),
            ["NoData"] = () => Write(data: string.Empty),
            ["NoPagesToRead"] = () => Read(count: 0),
            ["ANegativeStartPage"] = () => Read(start: -1),
            ["PagesBeyondTheNamedChip"] = () => Read(chip: UltralightExpectation.Plain, start: 15, count: 2),
            ["TheKeyPagesOfAnUltralightC"] = () => Read(chip: UltralightExpectation.C, start: 44),
            ["AnExpectationThatIsNoValue"] = () => Read(chip: (UltralightExpectation)99),
        };

        public static IEnumerable<object[]> EveryRefusal() => Refusals.Keys.Select(name => new object[] { name });

        private static MifareUltralightSetupViewModel Read(UltralightExpectation chip = UltralightExpectation.Auto, int start = 4, int count = 2) =>
            new MifareUltralightSetupViewModel
            {
                SelectedTaskType = TaskType_MifareUltralightTask.ReadData, CurrentTaskIndex = "0", ExpectedChip = chip, StartPage = start, PageCount = count,
            };

        private static MifareUltralightSetupViewModel Write(UltralightExpectation chip = UltralightExpectation.Auto, int start = 4, bool allow = false, string data = Payload) =>
            new MifareUltralightSetupViewModel
            {
                SelectedTaskType = TaskType_MifareUltralightTask.WriteData, CurrentTaskIndex = "0", ExpectedChip = chip, StartPage = start, AllowProtectedPages = allow, WriteDataHex = data,
            };

        private static MifareUltralightSetupViewModel Authenticated(UltralightAuth auth, UltralightExpectation chip)
        {
            var task = Read(chip);
            task.Authentication = auth;

            return task;
        }

        [Theory]
        [MemberData(nameof(EveryRefusal))]
        public async Task ACallThatCannotBeMade_EndsAsAProtocolConstraint_BeforeAKeyIsAskedForAndBeforeTheCard(string name)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                // A vault that is down: a key that was asked for would end the task as KeyUnavailable instead.
                var vault = new Vault { Failure = SecureKeyFailureReason.ProviderUnavailable };
                vault.Install();
                var task = Refusals[name]();
                BindAll(task);

                var run = await RunAsync(task);

                Assert.Equal(ERROR.ProtocolConstraint, run.Error);
                Assert.False(run.Completed);
                Assert.Equal(0, run.ReaderCalls);
                Assert.Empty(vault.Requests);
            });
        }

        [Theory]
        [InlineData("ReadPassword")]
        [InlineData("WritePassword")]
        [InlineData("ReadC")]
        public async Task AnAccessThatTheReaderCannotMakeAtAll_EndsAsAProtocolConstraint_BeforeAKeyIsAskedFor(string scenario)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                // A PC/SC reader has no command for a password and knows no NTAG: that is known without a card, and a vault that is down
                // must not turn it into a key that is missing.
                var vault = new Vault { Failure = SecureKeyFailureReason.ProviderUnavailable };
                vault.Install();
                var task = NewTask(Scenarios[scenario]);
                BindAll(task);
                var reader = new KeyRecordingReader { Connected = true, AcceptsUltralightAccess = false };

                var run = await RunAsync(task, reader);

                Assert.Equal(ERROR.ProtocolConstraint, run.Error);
                Assert.False(run.Completed);
                Assert.Equal(0, run.ReaderCalls);
                Assert.Empty(vault.Requests);

                // The reader was asked about what the task is about to do, without a key.
                var asked = Assert.Single(reader.UltralightAccessesAsked);
                Assert.Equal(task.ExpectedChip, asked.Expected);
                Assert.Equal(task.Authentication, asked.Auth);
                Assert.Equal(task.AllowProtectedPages, asked.AllowProtectedPages);
                Assert.Null(asked.KeyHex);
                Assert.Null(asked.PackHex);
            });
        }

        [Fact]
        public async Task AReaderThatTakesTheAccess_IsAskedOnce_AndTheKeysAreAskedFor()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vault = new Vault();
                vault.Install();
                var task = NewTask(Scenarios["ReadPassword"]);
                BindAll(task);
                var reader = new KeyRecordingReader { Connected = true };

                var run = await RunAsync(task, reader);

                Assert.Equal(ERROR.NoError, run.Error);
                Assert.Equal(1, run.ReaderCalls);
                Assert.Single(reader.UltralightAccessesAsked);
                Assert.NotEmpty(vault.Requests);
            });
        }

        // ── What the reader says ──

        [Theory]
        [InlineData(ERROR.AuthFailure)]
        [InlineData(ERROR.PermissionDenied)]
        [InlineData(ERROR.ProtocolConstraint)]
        [InlineData(ERROR.TransportError)]
        [InlineData(ERROR.Unknown)]
        public async Task WhatTheReaderAnswers_IsTheErrorOfTheTask(ERROR answer)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                new Vault().Install();
                var task = NewTask(Scenarios["ReadC"]);
                var reader = new KeyRecordingReader { Connected = true, FailMethod = nameof(KeyRecordingReader.ReadMifareUltralightPagesAsync), FailWith = answer };

                var run = await RunAsync(task, reader);

                Assert.Equal(answer, run.Error);
                Assert.False(run.Completed);
                Assert.Contains(answer.ToString(), run.Status, StringComparison.Ordinal);
            });
        }

        [Fact]
        public async Task AReadLeavesItsPages_ForTheDialogToShow()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var task = NewTask(Scenarios["ReadC"]);
                task.StartPage = 6;
                task.PageCount = 2;

                var run = await RunAsync(task);

                Assert.Equal(ERROR.NoError, run.Error);
                Assert.Equal(6, task.LastReadStartPage);
                Assert.Equal(Enumerable.Range(0, 8).Select(i => (byte)(6 * 4 + i)).ToArray(), task.LastReadData);
            });
        }

        [Fact]
        public async Task AWriteSendsItsDataToTheReader_WithTheAccessOfTheTask()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var task = NewTask(Scenarios["WriteC"]);
                task.StartPage = 8;
                task.AllowProtectedPages = true;
                var reader = NewReader();

                await RunAsync(task, reader);

                var call = Assert.Single(reader.UltralightCalls);
                Assert.Equal(nameof(KeyRecordingReader.WriteMifareUltralightPagesAsync), call.Method);
                Assert.Equal(8, call.StartPage);
                Assert.Equal(2, call.PageCount);
                Assert.Equal(Convert.FromHexString(Payload), call.Data);
                Assert.Equal(UltralightExpectation.C, call.Access.Expected);
                Assert.Equal(UltralightAuth.UltralightC, call.Access.Auth);
                Assert.True(call.Access.AllowProtectedPages);
            });
        }

        [Fact]
        public async Task TheOutcomeOfARun_IsReportedAsNumbersAndFlagsOnly()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var task = NewTask(Scenarios["ReadPassword"]);
                var details = (ITaskOutcomeDetails)task;
                Assert.Empty(details.GetOutcomeDetails());

                await RunAsync(task);

                Assert.Equal("UltralightPages", details.OutcomeDetailsSection);
                var done = details.GetOutcomeDetails();
                Assert.Equal(2, done["PagesRequested"]);
                Assert.Equal(2, done["PagesDone"]);
                Assert.Equal(true, done["Authenticated"]);
                Assert.Equal((int)UltralightVariant.Ntag215, done["Variant"]);
                Assert.False(done.ContainsKey("FailedPage"));
                Assert.All(done.Values, value => Assert.True(value is int || value is bool, "Only numbers and flags: " + value));

                // A failure that stopped at a page.
                var reader = new KeyRecordingReader { Connected = true, ScriptedUltralightResult = new UltralightResult(ERROR.PermissionDenied, new byte[4], UltralightVariant.Plain, false, 1, 5) };
                await RunAsync(task, reader);

                var failed = details.GetOutcomeDetails();
                Assert.Equal(1, failed["PagesDone"]);
                Assert.Equal(5, failed["FailedPage"]);
                Assert.Equal(false, failed["Authenticated"]);
                Assert.Equal((int)UltralightVariant.Plain, failed["Variant"]);

                details.ClearOutcomeDetails();

                Assert.Empty(details.GetOutcomeDetails());
            });
        }

        [Fact]
        public async Task TheOutcomeReachesTheLogOfTheRun_AsNumbersAndFlagsOnly()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var task = NewTask(Scenarios["WriteC"]);
                await RunAsync(task);

                var metadata = Assert.IsType<Dictionary<string, object>>(TaskExecutionService.GetSafeIoMetadata(task, taskExecuted: true));
                var section = Assert.IsType<Dictionary<string, object>>(metadata["UltralightPages"]);

                Assert.Equal(2, section["PagesDone"]);
                Assert.DoesNotContain(Payload, string.Join(" ", section.Select(pair => pair.Key + "=" + pair.Value)), StringComparison.OrdinalIgnoreCase);
                Assert.Null(TaskExecutionService.GetSafeIoMetadata(NewTask(Scenarios["WriteC"]), taskExecuted: false));
            });
        }

        [Fact]
        public async Task AReaderThatThrows_EndsTheTaskAsUnknown_AndLeaksNothing()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var logs = CapturedLogs.Install();
                new Vault().Install();
                var task = NewTask(Scenarios["WriteC"]);
                BindAll(task);
                var reader = new KeyRecordingReader
                {
                    Connected = true,
                    ThrowMethod = nameof(KeyRecordingReader.WriteMifareUltralightPagesAsync),
                    ThrowText = "the reader failed for " + Slots[SecureKeySlots.UltralightCKey].Vault + " and " + Payload,
                };

                var run = await RunAsync(task, reader);

                Assert.Equal(ERROR.Unknown, run.Error);
                Assert.False(run.Completed);
                AssertNothingSecretIn(run.Status);
                Assert.DoesNotContain("the reader failed", run.Status, StringComparison.Ordinal);
                Assert.All(logs.All().Select(CapturedLogs.EverythingWrittenFor), logged =>
                {
                    AssertNothingSecretIn(logged);
                    Assert.DoesNotContain("the reader failed", logged, StringComparison.Ordinal);
                });
            });
        }

        [Fact]
        public async Task WithoutAReader_TheTaskEndsAsATransportError_AndNoKeyIsAskedFor()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vault = new Vault();
                vault.Install();
                var task = NewTask(Scenarios["ReadC"]);
                BindAll(task);

                using (ReaderDeviceScope.Use(null))
                {
                    await task.CommandDelegator.ExecuteAsync(task.SelectedTaskType);
                }

                Assert.Equal(ERROR.TransportError, task.CurrentTaskErrorLevel);
                Assert.Empty(vault.Requests);
            });
        }

        [Fact]
        public async Task NeitherTheLogNorTheStatusEverShowsAKeyAReferenceOrTheData()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var logs = CapturedLogs.Install();
                var vault = new Vault();
                vault.Install();
                var success = NewTask(Scenarios["WritePassword"]);
                BindAll(success);
                var failure = NewTask(Scenarios["WriteC"]);
                failure.KeyBindings = new[] { Bind(SecureKeySlots.UltralightCKey, "ref/missing") };
                vault.FailReferences.Add("ref/missing");
                var refused = Write(chip: UltralightExpectation.Ntag215, start: 130);

                var shown = new List<string>
                {
                    (await RunAsync(success)).Status,
                    (await RunAsync(failure)).Status,
                    (await RunAsync(refused)).Status,
                };
                shown.AddRange(logs.All().Select(CapturedLogs.EverythingWrittenFor));

                foreach (var text in shown)
                {
                    AssertNothingSecretIn(text);
                    Assert.DoesNotContain("ref/", text, StringComparison.Ordinal);
                }
            });
        }

        [Fact]
        public async Task AProviderThatThrowsWithTheKeyInItsMessage_LeaksNothing()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var logs = CapturedLogs.Install();
                new Vault { ThrowText = "the vault failed for " + Slots[SecureKeySlots.UltralightCKey].Vault }.Install();
                var task = NewTask(Scenarios["ReadC"]);
                BindAll(task);

                var run = await RunAsync(task);

                Assert.Equal(ERROR.KeyUnavailable, run.Error);
                AssertNothingSecretIn(run.Status);
                Assert.All(logs.All().Select(CapturedLogs.EverythingWrittenFor), AssertNothingSecretIn);
            });
        }

        [Fact]
        public async Task TheDialogTakesNoInput_WhileAProviderIsAsked_AndAgainAfterwards()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var provider = new BlockingKeyProvider(16);
                KeyResolutionScope.SetDefault(new KeyResolutionContext(provider.Registry(), "run-1", "0", null, interactive: false));
                var task = NewTask(Scenarios["ReadC"]);
                task.KeyBindings = new[] { new KeyBindingEntry { Slot = SecureKeySlots.UltralightCKey, ProviderId = BlockingKeyProvider.Id, Reference = "ref/c" } };
                var announced = new List<string>();
                task.PropertyChanged += (_, e) => announced.Add(e.PropertyName ?? string.Empty);

                using (ReaderDeviceScope.Use(NewReader()))
                {
                    var running = task.CommandDelegator.ExecuteAsync(task.SelectedTaskType);

                    // A provider that is never asked (a binding that is ignored) fails the test; it must not hang the run.
                    Assert.Same(provider.Asked, await Task.WhenAny(provider.Asked, Task.Delay(TimeSpan.FromSeconds(10))));

                    Assert.True(task.IsFetchingKeys);
                    Assert.False(task.AreInputsEnabled);

                    provider.Release();
                    await running;
                }

                Assert.False(task.IsFetchingKeys);
                Assert.True(task.AreInputsEnabled);
                Assert.Contains(nameof(task.AreInputsEnabled), announced);
            });
        }

        // ── What the buttons of the dialog may do, and what may run at the same time ──

        [Fact]
        public async Task AnOperationThatIsAskedForWhileOneIsInFlight_IsNotStarted()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var provider = new BlockingKeyProvider(16);
                KeyResolutionScope.SetDefault(new KeyResolutionContext(provider.Registry(), "run-1", "0", null, interactive: false));
                var task = NewTask(Scenarios["ReadC"]);
                task.KeyBindings = new[] { new KeyBindingEntry { Slot = SecureKeySlots.UltralightCKey, ProviderId = BlockingKeyProvider.Id, Reference = "ref/c" } };
                var reader = NewReader();

                using (ReaderDeviceScope.Use(reader))
                {
                    var first = task.CommandDelegator.ExecuteAsync(TaskType_MifareUltralightTask.ReadData);
                    Assert.Same(provider.Asked, await Task.WhenAny(provider.Asked, Task.Delay(TimeSpan.FromSeconds(10))));
                    Assert.True(task.IsRunning);
                    Assert.False(task.AreInputsEnabled);

                    // The other command and the same one again would share the outcome and the error of the task: neither starts, and
                    // neither changes what the first one has.
                    await task.CommandDelegator.ExecuteAsync(TaskType_MifareUltralightTask.WriteData);
                    await task.CommandDelegator.ExecuteAsync(TaskType_MifareUltralightTask.ReadData);
                    Assert.Empty(reader.UltralightCalls);
                    Assert.Equal(ERROR.Empty, task.CurrentTaskErrorLevel);

                    provider.Release();
                    await first;
                }

                var call = Assert.Single(reader.UltralightCalls);
                Assert.Equal(nameof(KeyRecordingReader.ReadMifareUltralightPagesAsync), call.Method);
                Assert.Equal(ERROR.NoError, task.CurrentTaskErrorLevel);
                Assert.False(task.IsRunning);
                Assert.True(task.AreInputsEnabled);

                // And the task can be run again.
                using (ReaderDeviceScope.Use(reader))
                {
                    KeyResolutionScope.SetDefault(null);
                    task.KeyBindings = new KeyBindingEntry[0];
                    await task.CommandDelegator.ExecuteAsync(TaskType_MifareUltralightTask.ReadData);
                }

                Assert.Equal(2, reader.UltralightCalls.Count);
            });
        }

        [Fact]
        public async Task AFaultInTheOperation_ReleasesTheGuard()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var task = NewTask(Scenarios["ReadC"]);
                var failing = NewReader();
                failing.ThrowMethod = nameof(KeyRecordingReader.ReadMifareUltralightPagesAsync);

                var first = await RunAsync(task, failing);
                var second = await RunAsync(task, NewReader());

                Assert.Equal(ERROR.Unknown, first.Error);
                Assert.Equal(ERROR.NoError, second.Error);
                Assert.False(task.IsRunning);
            });
        }

        [Fact]
        public async Task AReaderThatIsNotConnected_EndsTheTaskAsATransportError_BeforeAKeyIsAskedFor()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                // A provider that is there but has no reader attached: the answer is the reader, not a key that was asked for for nothing.
                var vault = new Vault();
                vault.Install();
                var task = NewTask(Scenarios["ReadC"]);
                BindAll(task);

                var run = await RunAsync(task, new KeyRecordingReader { Connected = false });

                Assert.Equal(ERROR.TransportError, run.Error);
                Assert.False(run.Completed);
                Assert.Equal(0, run.ReaderCalls);
                Assert.Empty(vault.Requests);
            });
        }

        [Fact]
        public async Task APcscReader_IsNotLookedAtForAConnection_BecauseItsSessionIsMadeByTheCall()
        {
            // The connection of a PC/SC reader says whether a card session is open, which the page operation opens itself.
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var task = NewTask(Scenarios["ReadC"]);

                var run = await RunAsync(task, new KeyRecordingReader { Connected = false, Pcsc = true });

                Assert.Equal(ERROR.NoError, run.Error);
                Assert.Equal(1, run.ReaderCalls);
            });
        }

        // ── Through the execution service ──

        [Fact]
        public async Task ARunAsksWithItsOwnId_TheTaskIndexAndTheCard()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vault = new Vault();
                var task = NewTask(Scenarios["ReadPassword"]);
                task.CurrentTaskIndex = "4";
                BindAll(task);
                var reader = new KeyRecordingReader { Connected = true, Card = new GenericChipModel("04AABBCCDD", CARD_TYPE.NTAG_215) };

                ExecResult result;
                using (ReaderDeviceScope.Use(reader))
                {
                    result = await ExecuteAsync(task, new TaskExecutionService(new ReaderDeviceProvider(), new NullDispatcherTimerAdapter(), new NullDispatcherTimerAdapter(), null, vault.Registry()));
                }

                Assert.Equal(Scenarios["ReadPassword"].SlotIds, vault.Requests.Select(request => request.SlotId));
                Assert.All(vault.Requests, request =>
                {
                    Assert.Equal(result.RunId, request.RunId);
                    Assert.Equal("4", request.TaskIndex);
                    Assert.Equal("04AABBCCDD", request.CardUid);
                    Assert.False(request.Interactive);
                });
                Assert.Equal(Scenarios["ReadPassword"].Calls, Describe(reader));
                Assert.Null(KeyResolutionScope.Current);
                Assert.Equal(ExecutionOutcome.Success, result.Outcome);
            });
        }

        [Fact]
        public async Task ARunWhoseKeyCannotBeObtained_ReportsKeyUnavailable_AndCountsAsFailed()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vault = new Vault { Failure = SecureKeyFailureReason.ProviderUnavailable };
                var task = NewTask(Scenarios["WriteC"]);
                BindAll(task);
                var reader = NewReader();

                ExecResult result;
                using (ReaderDeviceScope.Use(reader))
                {
                    result = await ExecuteAsync(task, new TaskExecutionService(new ReaderDeviceProvider(), new NullDispatcherTimerAdapter(), new NullDispatcherTimerAdapter(), null, vault.Registry()));
                }

                var outcome = Assert.Single(result.TaskOutcomes);
                Assert.Equal("KeyUnavailable", outcome.NormalizedErrorCode);
                Assert.False(outcome.IsSuccess);
                Assert.Equal(ExecutionOutcome.Failure, result.Outcome);
                Assert.Empty(reader.UltralightCalls);
            });
        }

        // ── Helpers ──

        private sealed record Run(string Calls, ERROR Error, string Status, bool? Completed, int ReaderCalls);

        // A reader that says it is connected, as the reader of an application with a reader attached does.
        private static KeyRecordingReader NewReader() => new KeyRecordingReader { Connected = true };

        private static MifareUltralightSetupViewModel NewTask(Scenario scenario, bool withEmbeddedKeys = true)
        {
            var task = new MifareUltralightSetupViewModel
            {
                SelectedTaskType = scenario.Type,
                CurrentTaskIndex = "0",
                StartPage = 4,
                PageCount = 2,
                WriteDataHex = Payload,
                ExpectedChip = scenario.Chip,
                Authentication = scenario.Auth,
            };

            if (withEmbeddedKeys)
            {
                task.UltralightCKey = Slots[SecureKeySlots.UltralightCKey].Embedded;
                task.UltralightPassword = Slots[SecureKeySlots.UltralightPassword].Embedded;
                task.UltralightPack = Slots[SecureKeySlots.UltralightPack].Embedded;
            }

            return task;
        }

        private static KeyBindingEntry Bind(string slot, string? reference = null) =>
            new KeyBindingEntry { Slot = slot, ProviderId = Provider, Reference = reference ?? "ref/" + slot };

        private static void BindAll(MifareUltralightSetupViewModel task) =>
            task.KeyBindings = Slots.Keys.Select(slot => Bind(slot)).ToArray();

        private static async Task<Run> RunAsync(MifareUltralightSetupViewModel task, KeyRecordingReader? reader = null)
        {
            reader ??= NewReader();

            using (ReaderDeviceScope.Use(reader))
            {
                await task.CommandDelegator.ExecuteAsync(task.SelectedTaskType);
            }

            return new Run(Describe(reader), task.CurrentTaskErrorLevel, task.StatusText ?? string.Empty, task.IsTaskCompletedSuccessfully, reader.UltralightCalls.Count);
        }

        private static async Task<ExecResult> ExecuteAsync(MifareUltralightSetupViewModel task, TaskExecutionService service)
        {
            // Its own folder: opening the project database resets the files of the application's profile.
            var root = Directory.CreateTempSubdirectory("UltralightKeyRun").FullName;

            try
            {
                var coordinator = new ExtensionOperationCoordinator();
                var project = new ProjectHostImpl(new DatabaseReaderWriter(new ProjectManager(root)), coordinator);
                var host = new TaskExecHostImpl(service, project, coordinator);
                project.ReplaceTaskCollection(new object[] { task });

                return await host.ExecuteAllAsync(ExecutionRequest.Empty, default);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        private static string Describe(KeyRecordingReader reader) =>
            string.Join(" | ", reader.UltralightCalls.Select(call =>
                $"{call.Method.Replace("MifareUltralightPagesAsync", string.Empty).Replace("MifareUltralightAsync", string.Empty)}[{string.Join(",", new[] { call.Access.KeyHex, call.Access.PackHex }.Where(key => key != null).Select(NameOf))}]"));

        // The line a task with embedded keys makes: each slot name carries the mark.
        private static string Embedded(string calls) => Regex.Replace(calls, @"(?<=[\[,])(\w+)(?=[,\]])", "embedded:$1");

        private static string NameOf(string? key)
        {
            foreach (var slot in Slots.Values)
            {
                if (key == slot.Vault)
                {
                    return slot.Short;
                }

                if (key == slot.Embedded)
                {
                    return "embedded:" + slot.Short;
                }
            }

            return string.IsNullOrEmpty(key) ? "<empty>" : "<unknown>";
        }

        private static void AssertNothingSecretIn(string text)
        {
            foreach (var slot in Slots.Values)
            {
                foreach (var key in new[] { slot.Embedded, slot.Vault })
                {
                    Assert.DoesNotContain(key, text, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain(string.Join(" ", Enumerable.Range(0, key.Length / 2).Select(i => key.Substring(i * 2, 2))), text, StringComparison.OrdinalIgnoreCase);
                }
            }

            Assert.DoesNotContain(Payload, text, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>A vault that answers from the table above.</summary>
        private sealed class Vault : SlotVault
        {
            public Vault()
                : base(Provider, slot => Slots.TryGetValue(slot, out var entry) ? entry.Vault : null, 0)
            {
            }
        }
    }
}
