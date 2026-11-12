using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.ViewModel.TaskSetupViewModels;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Xunit;

namespace RFiDGear.Tests.KeySlots
{
    /// <summary>
    /// Pins which key slot of a MIFARE Classic task reaches which parameter of the reader, today. Every slot gets its own
    /// recognizable key, the task runs against a reader that records the keys it receives, and the test compares the
    /// recorded calls with the slot names. Keys that come from a key store instead of the property must reach the reader
    /// in the same places; this is the proof that a legacy project keeps authenticating with the same keys.
    /// </summary>
    public class ClassicKeySlotCharacterizationTests
    {
        private static readonly Dictionary<string, string> SlotsByKey = new Dictionary<string, string>
        {
            ["111111111111"] = "ACurrent",
            ["222222222222"] = "BCurrent",
            ["333333333333"] = "ATarget",
            ["444444444444"] = "BTarget",
            ["555555555555"] = "MadACurrent",
            ["666666666666"] = "MadBCurrent",
            ["777777777777"] = "MadATarget",
            ["888888888888"] = "MadBTarget",
        };

        [Fact]
        public async Task An_empty_check_reads_the_sector_with_the_current_keys()
        {
            var run = await RunAsync(TaskType_MifareClassicTask.EmptyCheck);

            Assert.Equal("ReadMifareClassicSingleSector[ACurrent,BCurrent]", run.Calls);
        }

        [Fact]
        public async Task Reading_a_sector_uses_the_current_keys()
        {
            var run = await RunAsync(TaskType_MifareClassicTask.ReadData);

            Assert.Equal("ReadMifareClassicSingleSector[ACurrent,BCurrent]", run.Calls);
            Assert.Equal(ERROR.NoError, run.Error);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Reading_with_the_mad_hands_over_the_current_keys_and_the_mad_keys(bool authenticateWithMad)
        {
            var run = await RunAsync(TaskType_MifareClassicTask.ReadData, vm =>
            {
                vm.UseMAD = true;
                vm.UseMadAuth = authenticateWithMad;
            });

            Assert.Equal("ReadMifareClassicWithMAD[ACurrent,BCurrent,MadACurrent,MadBCurrent]", run.Calls);
            Assert.Equal(ERROR.NoError, run.Error);
        }

        [Fact]
        public async Task Writing_a_block_uses_the_current_keys()
        {
            var run = await RunAsync(TaskType_MifareClassicTask.WriteData);

            Assert.Equal("WriteMifareClassicSingleBlock[ACurrent,BCurrent]", run.Calls);
            Assert.Equal(ERROR.NoError, run.Error);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Writing_with_the_mad_hands_over_all_eight_keys(bool authenticateWithMad)
        {
            var run = await RunAsync(TaskType_MifareClassicTask.WriteData, vm =>
            {
                vm.UseMAD = true;
                vm.UseMadAuth = authenticateWithMad;
            });

            Assert.Equal(
                "WriteMifareClassicWithMAD[ACurrent,BCurrent,ATarget,BTarget,MadACurrent,MadBCurrent,MadATarget,MadBTarget]",
                run.Calls);
            Assert.Equal(ERROR.NoError, run.Error);
        }

        [Fact]
        public async Task A_task_type_that_reads_nothing_makes_no_call()
        {
            var run = await RunAsync(TaskType_MifareClassicTask.ChangeDefault);

            Assert.Equal(string.Empty, run.Calls);
        }

        [Fact]
        public async Task A_refused_authentication_ends_the_task_after_one_call()
        {
            // The caller owns retries: a command that changes the card makes one attempt.
            var run = await RunAsync(
                TaskType_MifareClassicTask.WriteData,
                reader: reader =>
                {
                    reader.FailMethod = "WriteMifareClassicSingleBlock";
                    reader.FailWith = ERROR.AuthFailure;
                });

            Assert.Equal("WriteMifareClassicSingleBlock[ACurrent,BCurrent]", run.Calls);
            Assert.Equal(ERROR.AuthFailure, run.Error);
        }

        [Fact]
        public async Task A_refused_sector_read_ends_the_task_after_one_call()
        {
            var run = await RunAsync(
                TaskType_MifareClassicTask.ReadData,
                reader: reader =>
                {
                    reader.FailMethod = "ReadMifareClassicSingleSector";
                    reader.FailWith = ERROR.AuthFailure;
                });

            Assert.Equal("ReadMifareClassicSingleSector[ACurrent,BCurrent]", run.Calls);
            Assert.Equal(ERROR.AuthFailure, run.Error);
        }

        [Fact]
        public async Task Keys_are_handed_over_as_they_are_without_any_check()
        {
            // Today a Classic command does not validate a key: whatever the property holds goes to the reader, and the reader
            // decides. A bound slot is held to a stricter rule; an embedded one keeps this.
            var run = await RunAsync(TaskType_MifareClassicTask.ReadData, vm => vm.ClassicKeyBKeyCurrent = "ZZ");

            Assert.Equal("ReadMifareClassicSingleSector[ACurrent,<unknown ZZ>]", run.Calls);
        }

        private sealed record Run(string Calls, ERROR Error);

        private static async Task<Run> RunAsync(
            TaskType_MifareClassicTask type,
            Action<MifareClassicSetupViewModel>? viewModel = null,
            Action<KeyRecordingReader>? reader = null)
        {
            Run? run = null;

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vm = NewViewModel(type);
                viewModel?.Invoke(vm);

                var recording = new KeyRecordingReader();
                reader?.Invoke(recording);

                using (ReaderDeviceScope.Use(recording))
                {
                    await vm.CommandDelegator.ExecuteAsync(type);
                }

                run = new Run(Describe(recording), vm.CurrentTaskErrorLevel);
            });

            return run!;
        }

        private static string Describe(KeyRecordingReader reader) =>
            string.Join(" | ", reader.Calls.Select(call => $"{call.Method}[{string.Join(",", call.Keys.Select(NameOf))}]"));

        private static string NameOf(string key) =>
            key.Length == 0 ? "<empty>" : SlotsByKey.TryGetValue(key, out var slot) ? slot : $"<unknown {key}>";

        private static MifareClassicSetupViewModel NewViewModel(TaskType_MifareClassicTask type) => new MifareClassicSetupViewModel
        {
            SelectedTaskType = type,
            CurrentTaskIndex = "0",
            ClassicKeyAKeyCurrent = "111111111111",
            ClassicKeyBKeyCurrent = "222222222222",
            ClassicKeyAKeyTarget = "333333333333",
            ClassicKeyBKeyTarget = "444444444444",
            ClassicMADKeyAKeyCurrent = "555555555555",
            ClassicMADKeyBKeyCurrent = "666666666666",
            ClassicMADKeyAKeyTarget = "777777777777",
            ClassicMADKeyBKeyTarget = "888888888888",
        };
    }
}
