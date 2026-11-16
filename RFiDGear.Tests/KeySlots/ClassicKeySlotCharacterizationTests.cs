using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.AccessControl;
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

            Assert.Equal("ReadMifareClassicSectorAsync[ACurrent,BCurrent]", run.Calls);
        }

        [Fact]
        public async Task Reading_a_sector_uses_the_current_keys()
        {
            var run = await RunAsync(TaskType_MifareClassicTask.ReadData);

            Assert.Equal("ReadMifareClassicSectorAsync[ACurrent,BCurrent]", run.Calls);
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

            Assert.Equal("ReadMifareClassicApplicationAsync[ACurrent,BCurrent,MadACurrent,MadBCurrent]", run.Calls);
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
                    reader.FailMethod = "ReadMifareClassicSectorAsync";
                    reader.FailWith = ERROR.AuthFailure;
                });

            Assert.Equal("ReadMifareClassicSectorAsync[ACurrent,BCurrent]", run.Calls);
            Assert.Equal(ERROR.AuthFailure, run.Error);
        }

        [Fact]
        public async Task Keys_are_handed_over_as_they_are_without_any_check()
        {
            // Today a Classic command does not validate a key: whatever the property holds goes to the reader, and the reader
            // decides. A bound slot is held to a stricter rule; an embedded one keeps this.
            var run = await RunAsync(TaskType_MifareClassicTask.ReadData, vm => vm.ClassicKeyBKeyCurrent = "ZZ");

            Assert.Equal("ReadMifareClassicSectorAsync[ACurrent,<unknown ZZ>]", run.Calls);
        }

        [Fact]
        public async Task An_empty_check_of_an_empty_sector_succeeds()
        {
            var run = await RunAsync(TaskType_MifareClassicTask.EmptyCheck);

            Assert.Equal(ERROR.NoError, run.Error);
        }

        [Fact]
        public async Task An_empty_check_of_a_sector_that_holds_data_says_so()
        {
            // The loop used to set IsNotTrue and the line after it NoError, so the check never found anything.
            var run = await RunAsync(TaskType_MifareClassicTask.EmptyCheck, reader: reader => reader.BlockFill = 0x11);

            Assert.Equal(ERROR.IsNotTrue, run.Error);
        }

        [Fact]
        public async Task An_empty_check_does_not_fail_on_a_block_that_no_key_could_read()
        {
            // The block has no data: it cannot be shown to be empty, and asking for its length used to throw.
            var run = await RunAsync(TaskType_MifareClassicTask.EmptyCheck, reader: reader => reader.UnreadableBlock = 1);

            Assert.Equal(ERROR.IsNotTrue, run.Error);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(3)]
        public async Task An_empty_check_does_not_call_a_sector_empty_that_a_reader_returned_in_part(int blocks)
        {
            // A reader that leaves out the blocks that it could not read gives fewer blocks than the sector has: all that it did show is zero,
            // and that is not the sector.
            var run = await RunAsync(TaskType_MifareClassicTask.EmptyCheck, reader: reader => reader.BlockCount = blocks);

            Assert.Equal(ERROR.IsNotTrue, run.Error);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(2)]
        public async Task A_read_does_not_fail_on_a_block_that_no_key_could_read(int block)
        {
            // The message about the block used to ask for the block before it, which is none for the first one.
            var run = await RunAsync(TaskType_MifareClassicTask.ReadData, reader: reader => reader.UnreadableBlock = block);

            Assert.Equal(ERROR.NoError, run.Error);
        }

        [Fact]
        public async Task A_read_empties_the_block_that_could_not_be_read_so_that_it_is_not_shown_or_written_back()
        {
            byte[]? readable = null;
            byte[]? unreadable = null;
            byte[]? other = null;

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var vm = NewViewModel(TaskType_MifareClassicTask.ReadData);
                var recording = new KeyRecordingReader { BlockFill = 0x11 };

                using (ReaderDeviceScope.Use(recording))
                {
                    // The first read fills the dialog with the data of the chip; the second finds the block 1 unreadable.
                    await vm.CommandDelegator.ExecuteAsync(TaskType_MifareClassicTask.ReadData);

                    recording.BlockFill = 0x22;
                    recording.UnreadableBlock = 1;

                    await vm.CommandDelegator.ExecuteAsync(TaskType_MifareClassicTask.ReadData);
                }

                readable = vm.ChildNodeViewModelTemp.Children[0].MifareClassicDataBlock.Data;
                unreadable = vm.ChildNodeViewModelTemp.Children[1].MifareClassicDataBlock.Data;
                other = vm.ChildNodeViewModelFromChip.Children[1].MifareClassicDataBlock.Data;
            });

            Assert.Equal(Enumerable.Repeat((byte)0x22, 16).ToArray(), readable);
            Assert.Null(unreadable);
            Assert.Null(other);
        }

        [Fact]
        public async Task A_write_of_a_block_without_data_sends_nothing_to_the_chip()
        {
            var run = await RunAsync(
                TaskType_MifareClassicTask.WriteData,
                vm => vm.ChildNodeViewModelTemp.Children[(int)vm.SelectedDataBlockToReadWrite].MifareClassicDataBlock.Data = null);

            Assert.Equal(string.Empty, run.Calls);
            Assert.Equal(ERROR.ProtocolConstraint, run.Error);
        }

        [Fact]
        public async Task A_write_of_a_block_that_is_not_sixteen_bytes_sends_nothing_to_the_chip()
        {
            var run = await RunAsync(
                TaskType_MifareClassicTask.WriteData,
                vm => vm.ChildNodeViewModelTemp.Children[(int)vm.SelectedDataBlockToReadWrite].MifareClassicDataBlock.Data = new byte[15]);

            Assert.Equal(string.Empty, run.Calls);
            Assert.Equal(ERROR.ProtocolConstraint, run.Error);
        }

        [Fact]
        public async Task A_write_of_a_trailer_whose_access_bits_are_not_consistent_is_refused_before_a_key_is_asked_for()
        {
            // The block that is written is the trailer (position 3 of the sector), and its bytes are zeros: the access bits are not the inverse of their copy.
            var run = await RunAsync(
                TaskType_MifareClassicTask.WriteData,
                vm =>
                {
                    vm.SelectedDataBlockToReadWrite = DataExplorer_DataBlock.Block3;
                    vm.ChildNodeViewModelTemp.Children[3].MifareClassicDataBlock.Data = new byte[16];
                });

            Assert.Equal(string.Empty, run.Calls);
            Assert.Equal(ERROR.ProtocolConstraint, run.Error);
            Assert.Contains("cannot be written", run.Status, StringComparison.Ordinal);
        }

        // ── what the status text says about a failure ──

        [Theory]
        [InlineData(TaskType_MifareClassicTask.ReadData, "ReadMifareClassicSectorAsync", ERROR.TransportError, "No chip answered")]
        [InlineData(TaskType_MifareClassicTask.ReadData, "ReadMifareClassicSectorAsync", ERROR.ProtocolConstraint, "cannot do that")]
        [InlineData(TaskType_MifareClassicTask.ReadData, "ReadMifareClassicSectorAsync", ERROR.AuthFailure, "Unable to Authenticate to Sector")]
        [InlineData(TaskType_MifareClassicTask.EmptyCheck, "ReadMifareClassicSectorAsync", ERROR.TransportError, "No chip answered")]
        [InlineData(TaskType_MifareClassicTask.EmptyCheck, "ReadMifareClassicSectorAsync", ERROR.AuthFailure, "Unable to Authenticate to Sector")]
        [InlineData(TaskType_MifareClassicTask.WriteData, "WriteMifareClassicSingleBlock", ERROR.TransportError, "No chip answered")]
        [InlineData(TaskType_MifareClassicTask.WriteData, "WriteMifareClassicSingleBlock", ERROR.PermissionDenied, "not allowed")]
        [InlineData(TaskType_MifareClassicTask.WriteData, "WriteMifareClassicSingleBlock", ERROR.Unknown, "did not keep the data")]
        [InlineData(TaskType_MifareClassicTask.WriteData, "WriteMifareClassicSingleBlock", ERROR.ProtocolConstraint, "cannot do that")]
        public async Task A_failed_call_says_what_failed_and_is_reported_as_it_was_not_as_a_refused_key(
            TaskType_MifareClassicTask type, string method, ERROR error, string text)
        {
            var run = await RunAsync(type, reader: reader =>
            {
                reader.FailMethod = method;
                reader.FailWith = error;
            });

            Assert.Equal(error, run.Error);
            Assert.Contains(text, run.Status, StringComparison.Ordinal);

            if (error != ERROR.AuthFailure)
            {
                Assert.DoesNotContain("Authenticate", run.Status, StringComparison.Ordinal);
            }
        }

        [Theory]
        [InlineData(false, "ReadMifareClassicApplicationAsync", ERROR.TransportError, "No chip answered")]
        [InlineData(false, "ReadMifareClassicApplicationAsync", ERROR.ProtocolConstraint, "Application Directory")]
        [InlineData(false, "ReadMifareClassicApplicationAsync", ERROR.AuthFailure, "Unable to Authenticate to MAD Sector")]
        [InlineData(true, "WriteMifareClassicWithMAD", ERROR.TransportError, "No chip answered")]
        [InlineData(true, "WriteMifareClassicWithMAD", ERROR.ProtocolConstraint, "Application Directory")]
        [InlineData(true, "WriteMifareClassicWithMAD", ERROR.AuthFailure, "Unable to Authenticate to MAD Sector")]
        public async Task A_failed_mad_call_says_what_failed(bool write, string method, ERROR error, string text)
        {
            var run = await RunAsync(
                write ? TaskType_MifareClassicTask.WriteData : TaskType_MifareClassicTask.ReadData,
                vm => vm.UseMAD = true,
                reader =>
                {
                    reader.FailMethod = method;
                    reader.FailWith = error;
                });

            Assert.Equal(error, run.Error);
            Assert.Contains(text, run.Status, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(ERROR.TransportError, "No chip answered")]
        [InlineData(ERROR.ProtocolConstraint, "cannot do that")]
        [InlineData(ERROR.PermissionDenied, "not allowed")]
        [InlineData(ERROR.Unknown, "did not keep")]
        [InlineData(ERROR.AuthFailure, "REFUSED")]
        [InlineData(ERROR.Empty, "REFUSED")]
        public void The_words_of_the_status_text_follow_the_answer_of_the_reader(ERROR error, string expectedStart)
        {
            Assert.Contains(expectedStart, MifareClassicSetupViewModel.ClassicFailureText(error, "REFUSED"), StringComparison.Ordinal);
        }

        private sealed record Run(string Calls, ERROR Error, string Status);

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

                run = new Run(Describe(recording), vm.CurrentTaskErrorLevel, vm.StatusText ?? string.Empty);
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

            // Sector 1: block 0 of sector 0 is the manufacturer block, which no write is allowed to touch.
            SelectedClassicSectorCurrent = "1",
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
