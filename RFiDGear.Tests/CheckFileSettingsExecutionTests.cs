using RFiDGear.Contracts;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Services.TaskExecution;
using RFiDGear.ViewModel.TaskSetupViewModels;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// The Check File Settings task as a project runs it: the real view model, the real task execution service and the
    /// extension host, with only the reader replaced. What the extensions and the log are told about the run is checked
    /// here, in one place: one outcome, in order, the success or error of the check, the run it belongs to, the
    /// expected and the actual settings as plain numbers, and no key anywhere.
    /// </summary>
    public class CheckFileSettingsExecutionTests
    {
        // Not all zeros: a key that is all zeros would also match the zeros of a time stamp.
        private const string Key = "A1B2C3D4E5F60718293A4B5C6D7E8F90";
        private const string KeyWithSpaces = "A1 B2 C3 D4 E5 F6 07 18 29 3A 4B 5C 6D 7E 8F 90";

        [Theory]
        [InlineData(16u, ERROR.NoError, true)]
        [InlineData(17u, ERROR.IsNotTrue, false)]
        public async Task TheRunReportsOneOutcome_WithTheResultOfTheCheck_TheRunItBelongsTo_AndNoKey(uint sizeOnTheCard, ERROR expectedError, bool expectedMatch)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var logs = CapturedLogs.Install();
                var viewModel = CreateTask();
                var reader = new FakeFileSettingsProvider { FileSettings = Card(sizeOnTheCard) };

                ExecResult result;
                using (ReaderDeviceScope.Use(reader))
                {
                    result = await RunAsync(viewModel);
                }

                // What an extension is told: one task, its position, and the success or error of the check.
                var outcome = Assert.Single(result.TaskOutcomes);
                Assert.Equal(0, outcome.Position);
                Assert.Equal(expectedMatch, outcome.IsSuccess);
                Assert.Equal(expectedError.ToString(), outcome.NormalizedErrorCode);
                Assert.Equal(expectedError, viewModel.CurrentTaskErrorLevel);
                Assert.Equal((1, 1), (reader.AuthCalls, reader.GetFileSettingsCalls));

                // What the log is told, in the order it happened.
                var entries = TaskLog(logs);
                var stages = entries.Select(entry => entry.RootElement.GetProperty("Stage").GetString()).ToList();
                Assert.Equal(1, stages.Count(stage => stage == "Task.Outcome"));
                var outcomeIndex = stages.IndexOf("Task.Outcome");
                Assert.True(outcomeIndex < stages.IndexOf("TaskLoop.Summary"), string.Join(", ", stages));

                var details = entries[outcomeIndex].RootElement.GetProperty("Details");
                Assert.Equal(result.RunId, details.GetProperty("RunId").GetString());
                Assert.Equal("Executed", details.GetProperty("Decision").GetString());
                Assert.Equal(expectedError.ToString(), details.GetProperty("ErrorCode").GetString());

                var comparison = details.GetProperty("IO").GetProperty("FileSettingsComparison");
                Assert.Equal(expectedMatch, comparison.GetProperty("Matches").GetBoolean());
                Assert.Equal(sizeOnTheCard, comparison.GetProperty("ActualFileSize").GetUInt32());
                Assert.Equal(16, comparison.GetProperty("ExpectedFileSize").GetInt32());
                Assert.Equal(0x32, comparison.GetProperty("ActualAccessRights1").GetInt32());

                // Numbers and flags only.
                foreach (var field in comparison.EnumerateObject())
                {
                    Assert.True(
                        field.Value.ValueKind == JsonValueKind.Number || field.Value.ValueKind == JsonValueKind.True || field.Value.ValueKind == JsonValueKind.False,
                        field.Name);
                }

                // And the key is nowhere: not in this run's entries, and not in anything else the application logged.
                foreach (var logged in logs.All().Select(CapturedLogs.EverythingWrittenFor))
                {
                    Assert.DoesNotContain(Key, logged, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain(KeyWithSpaces, logged, StringComparison.OrdinalIgnoreCase);
                }
            });
        }

        [Fact]
        public async Task ASecondRun_DoesNotCarryTheFirstOnesComparison()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var logs = CapturedLogs.Install();
                var viewModel = CreateTask();
                var reader = new FakeFileSettingsProvider { FileSettings = Card(16) };

                using (ReaderDeviceScope.Use(reader))
                {
                    await RunAsync(viewModel);

                    // The next run cannot get past the authentication: it has no comparison, and must not be logged with
                    // the one of the run before.
                    reader.AuthenticationResult = ERROR.AuthFailure;
                    await RunAsync(viewModel);
                }

                var outcomes = TaskLog(logs)
                    .Where(entry => entry.RootElement.GetProperty("Stage").GetString() == "Task.Outcome")
                    .Select(entry => entry.RootElement.GetProperty("Details"))
                    .ToList();

                Assert.Equal(2, outcomes.Count);
                Assert.True(outcomes[0].GetProperty("IO").TryGetProperty("FileSettingsComparison", out _));
                Assert.Equal("AuthFailure", outcomes[1].GetProperty("ErrorCode").GetString());
                Assert.False(outcomes[1].TryGetProperty("IO", out var io) && io.ValueKind == JsonValueKind.Object && io.TryGetProperty("FileSettingsComparison", out _));
                Assert.NotEqual(outcomes[0].GetProperty("RunId").GetString(), outcomes[1].GetProperty("RunId").GetString());
            });
        }

        private static async Task<ExecResult> RunAsync(MifareDesfireSetupViewModel viewModel)
        {
            // Its own folder: opening the project database resets the files of the application's profile, which a test
            // must not do to the one of whoever runs it.
            var root = Directory.CreateTempSubdirectory("CheckFileSettingsRun").FullName;
            try
            {
                var service = new TaskExecutionService(new ReaderDeviceProvider(), new NullDispatcherTimerAdapter(), new NullDispatcherTimerAdapter());
                var coordinator = new ExtensionOperationCoordinator();
                var project = new ProjectHostImpl(new DatabaseReaderWriter(new ProjectManager(root)), coordinator);
                var host = new TaskExecHostImpl(service, project, coordinator);
                project.ReplaceTaskCollection(new object[] { viewModel });

                return await host.ExecuteAllAsync(ExecutionRequest.Empty);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        // What the task loop logged for this run, as parsed entries.
        private static List<JsonDocument> TaskLog(CapturedLogs logs) =>
            logs.From(nameof(NullTaskExecutionLogger))
                .Select(logEvent => logEvent.Properties.TryGetValue("SerializedTaskLog", out var value) ? (value as ScalarValue)?.Value as string : null)
                .Where(json => json != null)
                .Select(json => JsonDocument.Parse(json!))
                .ToList();

        private static DESFireFileSettings Card(uint size)
        {
            return new DESFireFileSettings
            {
                FileType = (byte)FileType_MifareDesfireFileType.StdDataFile,
                comSett = (byte)EncryptionMode.CM_ENCRYPT,
                accessRights = new byte[] { 0x10, 0x32 },
                dataFile = new DataFileSetting { fileSize = size }
            };
        }

        private static MifareDesfireSetupViewModel CreateTask()
        {
            return new MifareDesfireSetupViewModel
            {
                SelectedTaskType = TaskType_MifareDesfireTask.CheckFileSettings,
                CurrentTaskIndex = "0",
                AppNumberCurrent = "1",
                FileNumberCurrent = "2",
                FileSizeCurrent = "16",
                DesfireAppKeyCurrent = Key,
                SelectedDesfireAppKeyEncryptionTypeCurrent = DESFireKeyType.DF_KEY_AES,
                SelectedDesfireAppKeyNumberCurrent = "0",
                SelectedDesfireFileType = FileType_MifareDesfireFileType.StdDataFile,
                SelectedDesfireFileCryptoMode = EncryptionMode.CM_ENCRYPT,
                SelectedDesfireFileAccessRightRead = TaskAccessRights.AR_KEY0,
                SelectedDesfireFileAccessRightWrite = TaskAccessRights.AR_KEY1,
                SelectedDesfireFileAccessRightReadWrite = TaskAccessRights.AR_KEY2,
                SelectedDesfireFileAccessRightChange = TaskAccessRights.AR_KEY3
            };
        }
    }
}
