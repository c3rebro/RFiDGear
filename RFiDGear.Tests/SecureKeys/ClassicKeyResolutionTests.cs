using RFiDGear.Contracts;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.AccessControl;
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Infrastructure.Tasks;
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
    /// A MIFARE Classic task whose key slots are bound to a provider gets every key its reader call carries from the
    /// provider, before the card is touched, and never from the key it still holds in the project. The embedded keys and the
    /// keys of the vault are different values in every test, so a call to the reader says which of the two it carries.
    /// </summary>
    public class ClassicKeyResolutionTests : IDisposable
    {
        private const string Provider = "test.vault";

        // What a legacy project holds in each slot, and what the vault holds. Different, so a key on the card line shows
        // where it came from.
        private static readonly Dictionary<string, (string Short, string Embedded, string Vault)> Slots = new Dictionary<string, (string, string, string)>
        {
            [SecureKeySlots.ClassicACurrent] = ("ACurrent", Repeat(0xE0), Repeat(0xB0)),
            [SecureKeySlots.ClassicBCurrent] = ("BCurrent", Repeat(0xE1), Repeat(0xB1)),
            [SecureKeySlots.ClassicATarget] = ("ATarget", Repeat(0xE2), Repeat(0xB2)),
            [SecureKeySlots.ClassicBTarget] = ("BTarget", Repeat(0xE3), Repeat(0xB3)),
            [SecureKeySlots.ClassicMadACurrent] = ("MadACurrent", Repeat(0xE4), Repeat(0xB4)),
            [SecureKeySlots.ClassicMadBCurrent] = ("MadBCurrent", Repeat(0xE5), Repeat(0xB5)),
            [SecureKeySlots.ClassicMadATarget] = ("MadATarget", Repeat(0xE6), Repeat(0xB6)),
            [SecureKeySlots.ClassicMadBTarget] = ("MadBTarget", Repeat(0xE7), Repeat(0xB7)),
        };

        // Every way a Classic task reaches the reader: the call, and the slots it carries in the order of its parameters.
        private sealed record Scenario(TaskType_MifareClassicTask Type, bool UseMad, string Calls, string[] SlotIds);

        private static readonly Dictionary<string, Scenario> Scenarios = new Dictionary<string, Scenario>
        {
            ["EmptyCheck"] = new Scenario(
                TaskType_MifareClassicTask.EmptyCheck,
                false,
                "ReadMifareClassicSingleSector[ACurrent,BCurrent]",
                new[] { SecureKeySlots.ClassicACurrent, SecureKeySlots.ClassicBCurrent }),
            ["Read"] = new Scenario(
                TaskType_MifareClassicTask.ReadData,
                false,
                "ReadMifareClassicSingleSector[ACurrent,BCurrent]",
                new[] { SecureKeySlots.ClassicACurrent, SecureKeySlots.ClassicBCurrent }),
            ["ReadMad"] = new Scenario(
                TaskType_MifareClassicTask.ReadData,
                true,
                "ReadMifareClassicWithMAD[ACurrent,BCurrent,MadACurrent,MadBCurrent]",
                new[] { SecureKeySlots.ClassicACurrent, SecureKeySlots.ClassicBCurrent, SecureKeySlots.ClassicMadACurrent, SecureKeySlots.ClassicMadBCurrent }),
            ["Write"] = new Scenario(
                TaskType_MifareClassicTask.WriteData,
                false,
                "WriteMifareClassicSingleBlock[ACurrent,BCurrent]",
                new[] { SecureKeySlots.ClassicACurrent, SecureKeySlots.ClassicBCurrent }),
            ["WriteMad"] = new Scenario(
                TaskType_MifareClassicTask.WriteData,
                true,
                "WriteMifareClassicWithMAD[ACurrent,BCurrent,ATarget,BTarget,MadACurrent,MadBCurrent,MadATarget,MadBTarget]",
                new[]
                {
                    SecureKeySlots.ClassicACurrent, SecureKeySlots.ClassicBCurrent, SecureKeySlots.ClassicATarget, SecureKeySlots.ClassicBTarget,
                    SecureKeySlots.ClassicMadACurrent, SecureKeySlots.ClassicMadBCurrent, SecureKeySlots.ClassicMadATarget, SecureKeySlots.ClassicMadBTarget,
                }),
        };

        public ClassicKeyResolutionTests()
        {
            KeyResolutionScope.SetDefault(null);
        }

        public void Dispose()
        {
            KeyResolutionScope.SetDefault(null);
        }

        /// <summary>Gets the name of every scenario.</summary>
        public static IEnumerable<object[]> EveryScenario() => Scenarios.Keys.Select(name => new object[] { name });

        /// <summary>Gets every scenario with each of the slots its call carries.</summary>
        public static IEnumerable<object[]> EverySlotOfEveryScenario() =>
            Scenarios.SelectMany(entry => entry.Value.SlotIds.Select(slot => new object[] { entry.Key, slot }));

        private static string Repeat(byte value) => string.Concat(Enumerable.Repeat(value.ToString("X2"), 6));

        [Fact]
        public void Every_slot_of_a_classic_task_is_in_the_table_of_this_suite()
        {
            Assert.Equal(SecureKeySlots.Classic.OrderBy(slot => slot, StringComparer.Ordinal), Slots.Keys.OrderBy(slot => slot, StringComparer.Ordinal));
        }

        // ── Every way to the reader ──

        [Theory]
        [MemberData(nameof(EveryScenario))]
        public async Task With_every_slot_bound_a_task_works_with_the_keys_of_the_vault_only(string name)
        {
            var scenario = Scenarios[name];

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vault = new Vault();
                vault.Install();
                var task = NewTask(scenario);
                BindAll(task);

                var run = await RunAsync(task);

                // The same call with the same slots as for embedded keys, but each key is the vault's: an embedded key
                // would show up as "embedded:..." in this line.
                Assert.Equal(scenario.Calls, run.Calls);
                Assert.DoesNotContain("embedded:", run.Calls, StringComparison.Ordinal);
                Assert.Equal(ERROR.NoError, run.Error);

                // Each slot the call carries is asked for once, in the order of the call, and no other.
                Assert.Equal(scenario.SlotIds, vault.Requests.Select(request => request.SlotId));
                Assert.DoesNotContain(vault.Requests, request => request.Interactive);
            });
        }

        [Theory]
        [MemberData(nameof(EveryScenario))]
        public async Task A_project_that_moved_to_the_provider_has_no_embedded_keys_left_and_still_works(string name)
        {
            var scenario = Scenarios[name];

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                new Vault().Install();

                // As the project looks after a load: the reference is there, the hexadecimal text is not, and the
                // properties were never set.
                var task = NewTask(scenario, withEmbeddedKeys: false);
                BindAll(task);

                var run = await RunAsync(task);

                Assert.Equal(scenario.Calls, run.Calls);
                Assert.Equal(ERROR.NoError, run.Error);
            });
        }

        [Theory]
        [MemberData(nameof(EveryScenario))]
        public async Task A_task_without_bindings_never_asks_a_provider_and_works_as_before(string name)
        {
            var scenario = Scenarios[name];

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vault = new Vault();
                vault.Install();
                var task = NewTask(scenario);

                var run = await RunAsync(task);

                Assert.Equal(Embedded(scenario.Calls), run.Calls);
                Assert.Empty(vault.Requests);
            });
        }

        // ── A key that cannot be obtained ──

        [Theory]
        [MemberData(nameof(EverySlotOfEveryScenario))]
        public async Task A_key_that_cannot_be_obtained_stops_the_task_before_the_card_is_touched(string name, string failingSlot)
        {
            var scenario = Scenarios[name];

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vault = new Vault { FailSlots = { failingSlot } };
                vault.Install();
                var task = NewTask(scenario);
                BindAll(task);

                var run = await RunAsync(task);

                // Not even the keys that were obtained before the failing one reach the card.
                Assert.Equal(string.Empty, run.Calls);
                Assert.Equal(ERROR.KeyUnavailable, run.Error);
                Assert.False(run.Completed);
                Assert.Contains(failingSlot, run.Status, StringComparison.Ordinal);
                AssertNothingSecretIn(run.Status);
            });
        }

        [Fact]
        public async Task A_bound_key_B_that_cannot_be_obtained_does_not_leave_the_reader_to_try_key_A_alone()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vault = new Vault { FailSlots = { SecureKeySlots.ClassicBCurrent } };
                vault.Install();
                var task = NewTask(Scenarios["Read"]);

                // Key A is the project's own; only key B comes from the vault, and it is missing.
                task.KeyBindings = new[] { Bind(SecureKeySlots.ClassicBCurrent) };

                var run = await RunAsync(task);

                Assert.Equal(string.Empty, run.Calls);
                Assert.Equal(ERROR.KeyUnavailable, run.Error);
            });
        }

        [Theory]
        [InlineData(SecureKeyFailureReason.NotFound)]
        [InlineData(SecureKeyFailureReason.NotAuthorized)]
        [InlineData(SecureKeyFailureReason.ProviderUnavailable)]
        [InlineData(SecureKeyFailureReason.Timeout)]
        [InlineData(SecureKeyFailureReason.Cancelled)]
        [InlineData(SecureKeyFailureReason.InvalidKey)]
        [InlineData(SecureKeyFailureReason.Unsupported)]
        public async Task Whatever_the_reason_the_task_ends_with_key_unavailable_and_no_card_call(SecureKeyFailureReason reason)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                new Vault { Failure = reason }.Install();
                var task = NewTask(Scenarios["Read"]);
                BindAll(task);

                var run = await RunAsync(task);

                Assert.Equal(string.Empty, run.Calls);
                Assert.Equal(ERROR.KeyUnavailable, run.Error);
            });
        }

        [Fact]
        public async Task A_key_of_the_wrong_length_is_refused()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                // A DESFire key is 16 bytes; a Classic key is 6. The slot decides what a key has to look like.
                new Vault { KeyLength = 16 }.Install();
                var task = NewTask(Scenarios["Read"]);
                BindAll(task);

                var run = await RunAsync(task);

                Assert.Equal(string.Empty, run.Calls);
                Assert.Equal(ERROR.KeyUnavailable, run.Error);
            });
        }

        [Fact]
        public async Task A_provider_nobody_installed_is_a_failure_and_never_the_embedded_key()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                new Vault().Install();
                var task = NewTask(Scenarios["Read"]);
                task.KeyBindings = new[] { new KeyBindingEntry { Slot = SecureKeySlots.ClassicACurrent, ProviderId = "missing.vault", Reference = "ref/a" } };

                var run = await RunAsync(task);

                Assert.Equal(string.Empty, run.Calls);
                Assert.Equal(ERROR.KeyUnavailable, run.Error);
            });
        }

        [Fact]
        public async Task A_bound_slot_with_no_way_to_ask_is_a_failure_and_never_the_embedded_key()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                // Nothing sets up a context: a command with no run and no application behind it.
                var task = NewTask(Scenarios["Read"]);
                BindAll(task);

                var run = await RunAsync(task);

                Assert.Equal(string.Empty, run.Calls);
                Assert.Equal(ERROR.KeyUnavailable, run.Error);
            });
        }

        [Theory]
        [InlineData("unknown slot")]
        [InlineData("empty reference")]
        [InlineData("same slot twice")]
        public async Task A_broken_binding_anywhere_in_the_task_stops_it_even_if_the_command_does_not_use_that_slot(string problem)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vault = new Vault();
                vault.Install();
                var task = NewTask(Scenarios["Read"]);

                // A single-sector read uses neither of the target keys; the binding of the MAD target key is wrong.
                var broken = problem switch
                {
                    "unknown slot" => new[] { new KeyBindingEntry { Slot = SecureKeySlots.DesfireRead, ProviderId = Provider, Reference = "ref/x" } },
                    "empty reference" => new[] { new KeyBindingEntry { Slot = SecureKeySlots.ClassicMadATarget, ProviderId = Provider, Reference = string.Empty } },
                    _ => new[] { Bind(SecureKeySlots.ClassicMadATarget), Bind(SecureKeySlots.ClassicMadATarget) },
                };
                task.KeyBindings = broken;

                var run = await RunAsync(task);

                Assert.Equal(string.Empty, run.Calls);
                Assert.Equal(ERROR.KeyUnavailable, run.Error);
                Assert.Empty(vault.Requests);
            });
        }

        // ── What each key is asked for ──

        [Fact]
        public async Task A_mix_of_bound_and_embedded_slots_uses_each_where_it_belongs()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vault = new Vault();
                vault.Install();
                var task = NewTask(Scenarios["Read"]);
                task.KeyBindings = new[] { Bind(SecureKeySlots.ClassicBCurrent) };

                var run = await RunAsync(task);

                Assert.Equal("ReadMifareClassicSingleSector[embedded:ACurrent,BCurrent]", run.Calls);
                Assert.Equal(SecureKeySlots.ClassicBCurrent, Assert.Single(vault.Requests).SlotId);
            });
        }

        [Fact]
        public async Task A_provider_is_told_what_each_key_is_for()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vault = new Vault();
                vault.Install();
                var task = NewTask(Scenarios["WriteMad"]);
                BindAll(task);

                await RunAsync(task);

                Assert.Equal(8, vault.Requests.Count);

                foreach (var request in vault.Requests)
                {
                    Assert.Equal(SecureKeyAlgorithm.Classic6Byte, request.Algorithm);
                    Assert.Equal("ref/" + request.SlotId, request.Reference);
                    Assert.Equal("run-1", request.RunId);
                    Assert.Equal("0", request.TaskIndex);

                    // A Classic key belongs to no application and has no number.
                    Assert.Null(request.Scope.ApplicationId);
                    Assert.Null(request.Scope.KeyNumber);

                    // The keys written into the trailers are new keys; the others open the sectors.
                    var isTarget = request.SlotId == SecureKeySlots.ClassicATarget || request.SlotId == SecureKeySlots.ClassicBTarget
                        || request.SlotId == SecureKeySlots.ClassicMadATarget || request.SlotId == SecureKeySlots.ClassicMadBTarget;
                    Assert.Equal(isTarget ? SecureKeyUsage.ChangeKeyNew : SecureKeyUsage.Authenticate, request.Usage);
                }
            });
        }

        [Fact]
        public async Task A_second_task_never_gets_the_key_of_the_first()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vault = new Vault();
                vault.Install();
                var first = NewTask(Scenarios["Read"]);
                BindAll(first);
                var second = NewTask(Scenarios["Read"]);
                second.KeyBindings = new[] { Bind(SecureKeySlots.ClassicACurrent, "ref/missing"), Bind(SecureKeySlots.ClassicBCurrent) };
                vault.FailReferences.Add("ref/missing");
                var reader = new KeyRecordingReader();

                await RunAsync(first, reader);
                var run = await RunAsync(second, reader);

                // The reader saw the first task's call and nothing of the second.
                Assert.Equal(Scenarios["Read"].Calls, run.Calls);
                Assert.Equal(ERROR.KeyUnavailable, run.Error);
            });
        }

        // ── Nothing secret is shown ──

        [Fact]
        public async Task Neither_the_log_nor_the_status_ever_shows_a_key_or_a_reference()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var logs = CapturedLogs.Install();
                var vault = new Vault();
                vault.Install();
                var success = NewTask(Scenarios["WriteMad"]);
                BindAll(success);
                var failure = NewTask(Scenarios["Read"]);
                failure.KeyBindings = new[] { Bind(SecureKeySlots.ClassicACurrent, "ref/missing") };
                vault.FailReferences.Add("ref/missing");

                var shown = new List<string>();
                shown.Add((await RunAsync(success)).Status);
                shown.Add((await RunAsync(failure)).Status);

                shown.AddRange(logs.All().Select(CapturedLogs.EverythingWrittenFor));
                foreach (var text in shown)
                {
                    AssertNothingSecretIn(text);
                    Assert.DoesNotContain("ref/", text, StringComparison.Ordinal);
                }
            });
        }

        [Fact]
        public async Task A_provider_that_throws_with_the_key_in_its_message_leaks_nothing()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var logs = CapturedLogs.Install();
                new Vault { ThrowText = "the vault failed for " + Slots[SecureKeySlots.ClassicACurrent].Vault }.Install();
                var task = NewTask(Scenarios["Read"]);
                BindAll(task);

                var run = await RunAsync(task);

                Assert.Equal(ERROR.KeyUnavailable, run.Error);
                AssertNothingSecretIn(run.Status);
                foreach (var logged in logs.All().Select(CapturedLogs.EverythingWrittenFor))
                {
                    AssertNothingSecretIn(logged);
                }
            });
        }

        // ── Through the execution service ──

        [Fact]
        public async Task A_run_asks_with_its_own_id_the_task_index_and_the_card()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vault = new Vault();
                var task = NewTask(Scenarios["Read"]);
                task.CurrentTaskIndex = "4";
                BindAll(task);
                var reader = new KeyRecordingReader { Card = new GenericChipModel("04AABBCCDD", CARD_TYPE.EM4102) };

                ExecResult result;
                using (ReaderDeviceScope.Use(reader))
                {
                    result = await ExecuteAsync(task, new TaskExecutionService(new ReaderDeviceProvider(), new NullDispatcherTimerAdapter(), new NullDispatcherTimerAdapter(), null, vault.Registry()));
                }

                Assert.Equal(Scenarios["Read"].SlotIds, vault.Requests.Select(request => request.SlotId));
                Assert.All(vault.Requests, request =>
                {
                    Assert.Equal(result.RunId, request.RunId);
                    Assert.Equal("4", request.TaskIndex);
                    Assert.Equal("04AABBCCDD", request.CardUid);
                    Assert.False(request.Interactive);
                });
                Assert.Equal(Scenarios["Read"].Calls, Describe(reader));
                Assert.Null(KeyResolutionScope.Current);
            });
        }

        [Fact]
        public async Task A_run_whose_key_cannot_be_obtained_reports_key_unavailable_and_counts_as_failed()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vault = new Vault { Failure = SecureKeyFailureReason.ProviderUnavailable };
                var task = NewTask(Scenarios["Write"]);
                BindAll(task);
                var reader = new KeyRecordingReader();

                ExecResult result;
                using (ReaderDeviceScope.Use(reader))
                {
                    result = await ExecuteAsync(task, new TaskExecutionService(new ReaderDeviceProvider(), new NullDispatcherTimerAdapter(), new NullDispatcherTimerAdapter(), null, vault.Registry()));
                }

                var outcome = Assert.Single(result.TaskOutcomes);
                Assert.Equal("KeyUnavailable", outcome.NormalizedErrorCode);
                Assert.False(outcome.IsSuccess);
                Assert.Equal(ExecutionOutcome.Failure, result.Outcome);
                Assert.Empty(reader.Calls);
            });
        }

        // ── Helpers ──

        private sealed record Run(string Calls, ERROR Error, string Status, bool? Completed);

        private static MifareClassicSetupViewModel NewTask(Scenario scenario, bool withEmbeddedKeys = true)
        {
            var task = new MifareClassicSetupViewModel
            {
                SelectedTaskType = scenario.Type,
                CurrentTaskIndex = "0",
                UseMAD = scenario.UseMad,
            };

            if (withEmbeddedKeys)
            {
                task.ClassicKeyAKeyCurrent = Slots[SecureKeySlots.ClassicACurrent].Embedded;
                task.ClassicKeyBKeyCurrent = Slots[SecureKeySlots.ClassicBCurrent].Embedded;
                task.ClassicKeyAKeyTarget = Slots[SecureKeySlots.ClassicATarget].Embedded;
                task.ClassicKeyBKeyTarget = Slots[SecureKeySlots.ClassicBTarget].Embedded;
                task.ClassicMADKeyAKeyCurrent = Slots[SecureKeySlots.ClassicMadACurrent].Embedded;
                task.ClassicMADKeyBKeyCurrent = Slots[SecureKeySlots.ClassicMadBCurrent].Embedded;
                task.ClassicMADKeyAKeyTarget = Slots[SecureKeySlots.ClassicMadATarget].Embedded;
                task.ClassicMADKeyBKeyTarget = Slots[SecureKeySlots.ClassicMadBTarget].Embedded;
            }

            return task;
        }

        private static KeyBindingEntry Bind(string slot, string? reference = null) =>
            new KeyBindingEntry { Slot = slot, ProviderId = Provider, Reference = reference ?? "ref/" + slot };

        private static void BindAll(MifareClassicSetupViewModel task) =>
            task.KeyBindings = Slots.Keys.Select(slot => Bind(slot)).ToArray();

        private static async Task<Run> RunAsync(MifareClassicSetupViewModel task, KeyRecordingReader? reader = null)
        {
            reader ??= new KeyRecordingReader();

            using (ReaderDeviceScope.Use(reader))
            {
                await task.CommandDelegator.ExecuteAsync(task.SelectedTaskType);
            }

            return new Run(Describe(reader), task.CurrentTaskErrorLevel, task.StatusText ?? string.Empty, task.IsTaskCompletedSuccessfully);
        }

        private static async Task<ExecResult> ExecuteAsync(MifareClassicSetupViewModel task, TaskExecutionService service)
        {
            // Its own folder: opening the project database resets the files of the application's profile.
            var root = Directory.CreateTempSubdirectory("KeyResolutionRun").FullName;

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
            string.Join(" | ", reader.Calls.Select(call => $"{call.Method}[{string.Join(",", call.Keys.Select(NameOf))}]"));

        // The line a task with embedded keys makes: each slot name carries the mark.
        private static string Embedded(string calls) => Regex.Replace(calls, @"(?<=[\[,])(\w+)(?=[,\]])", "embedded:$1");

        private static string NameOf(string key)
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

            return key.Length == 0 ? "<empty>" : "<unknown>";
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
        }

        /// <summary>A vault that answers from the table above.</summary>
        private sealed class Vault : SlotVault
        {
            public Vault()
                : base(Provider, slot => Slots.TryGetValue(slot, out var entry) ? entry.Vault : null, 6)
            {
            }
        }
    }
}
