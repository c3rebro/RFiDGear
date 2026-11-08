using RFiDGear.Contracts;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.ViewModel.TaskSetupViewModels;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Xunit;

namespace RFiDGear.HIL.Tests
{
    /// <summary>
    /// Full DESFire CRUD workflow tests driven against real card hardware.
    /// Tests are automatically skipped when no reader / DESFire card is present.
    /// Run explicitly: <c>dotnet test RFiDGear.HIL.Tests</c>
    /// </summary>
    [Collection("HIL")]
    public sealed class DesfireWorkflowTests
    {
        private readonly HilFixture _fixture;

        public DesfireWorkflowTests(HilFixture fixture)
        {
            _fixture = fixture;
        }

        // ── Theory data ───────────────────────────────────────────────────────

        /// <summary>
        /// Combinations of (keyType, defaultKey, changedKey).
        /// Each row produces a separate test case.
        /// Deletion always uses the PICC master key (DESFire EV1+ protocol requirement).
        /// </summary>
        public static TheoryData<DESFireKeyType, string, string> WorkflowCases =>
            new TheoryData<DESFireKeyType, string, string>
            {
                // AES (programmed state)
                {
                    DESFireKeyType.DF_KEY_AES,
                    HilConstants.DefaultKeyAes,
                    "0102030405060708090A0B0C0D0E0F10"
                },
                // DES / 2K3DES (factory transport configuration; changedKey = 32 hex chars = 16 bytes)
                {
                    DESFireKeyType.DF_KEY_DES,
                    HilConstants.DefaultKeyDes,
                    "0102030405060708090A0B0C0D0E0F10"
                },
            };

        // ── Tests ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Full DESFire workflow:
        /// CreateApp → CreateFile → Write → Read → ChangeKey → Read (new key) → DeleteFile → DeleteApp.
        /// Parameterized over key type and delete-auth method.
        /// </summary>
        [Theory]
        [MemberData(nameof(WorkflowCases))]
        public async Task FullCrudWorkflow(
            DESFireKeyType keyType,
            string defaultKey,
            string changedKey)
        {
            _fixture.SkipIfNoHardware();

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                // Guard: clean up any leftover test AID from a previous interrupted run.
                await _fixture.CleanupTestAidAsync();

                var payloadPath = HilTaskBuilder.WriteTestPayloadTempFile();
                try
                {
                    var tasks = HilTaskBuilder.BuildFullWorkflow(
                        HilConstants.TestAppId,
                        keyType,
                        defaultKey,
                        changedKey,
                        payloadPath);

                    _fixture.Host!.Project.ReplaceTaskCollection(tasks);
                    var result = await _fixture.Host.Execution.ExecuteAllAsync();

                    var diag = _fixture.Host.GetTaskErrorSummary();
                    Assert.True(result.Outcome == ExecutionOutcome.Success,
                        $"Outcome={result.Outcome} Failed={result.FailedTasks}/{result.TotalTasks} keyType={keyType}\n{diag}");
                    Assert.Equal(0, result.FailedTasks);
                }
                finally
                {
                    if (File.Exists(payloadPath))
                        File.Delete(payloadPath);

                    _fixture.Host!.Execution.ResetStatus();
                }
            });
        }

        /// <summary>
        /// The read-only Check File Settings task against a real card: a file with four different access rights is
        /// created; the task returns success when the project expects exactly that, and IsNotTrue when the project alone
        /// differs (another size, another communication mode, two access rights swapped, another file type); the same
        /// task succeeds again afterwards, which shows that the settings it compares are still what they were. Which
        /// reader ran (Elatec first, PC/SC otherwise) is not asserted, and the expected and the actual values of a
        /// difference are in the Task.Outcome entry of the log of the run (stage Task.Outcome, IO.FileSettingsComparison).
        /// </summary>
        [Theory]
        [InlineData(DESFireKeyType.DF_KEY_AES, HilConstants.DefaultKeyAes)]
        [InlineData(DESFireKeyType.DF_KEY_DES, HilConstants.DefaultKeyDes)]
        public async Task CheckFileSettings_MatchesTheCard_RejectsADifference_AndLeavesTheCardUnchanged(
            DESFireKeyType keyType,
            string defaultKey)
        {
            _fixture.SkipIfNoHardware();

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                await _fixture.CleanupTestAidAsync();
                var onTheCard = HilFileSettings.Distinct;

                try
                {
                    var (setup, setupDiag) = await RunAsync(HilTaskBuilder.BuildCreateApplicationWithFile(
                        HilConstants.TestAppId, keyType, defaultKey, onTheCard));
                    Assert.True(setup.Outcome == ExecutionOutcome.Success, $"Setting the file up failed — keyType={keyType}\n{setupDiag}");

                    // The project expects what is on the card.
                    var (match, matchDiag) = await RunAsync(HilTaskBuilder.BuildCheckFileSettings(
                        HilConstants.TestAppId, keyType, defaultKey, onTheCard));
                    Assert.True(match.Outcome == ExecutionOutcome.Success, $"The exact match was not accepted — keyType={keyType}\n{matchDiag}\nSee the Task.Outcome entry of the log for the values.");
                    Assert.Equal(nameof(ERROR.NoError), Assert.Single(match.TaskOutcomes).NormalizedErrorCode);

                    // The project alone differs, in one field each time.
                    var differences = new[]
                    {
                        onTheCard with { Size = onTheCard.Size + 1 },
                        onTheCard with { CommunicationMode = EncryptionMode.CM_MAC },
                        onTheCard with { Read = onTheCard.Write, Write = onTheCard.Read },
                        onTheCard with { FileType = FileType_MifareDesfireFileType.BackupFile },
                    };

                    foreach (var expected in differences)
                    {
                        var (mismatch, mismatchDiag) = await RunAsync(HilTaskBuilder.BuildCheckFileSettings(
                            HilConstants.TestAppId, keyType, defaultKey, expected));
                        var outcome = Assert.Single(mismatch.TaskOutcomes);
                        Assert.False(outcome.IsSuccess, $"A difference was accepted: {expected}\n{mismatchDiag}");
                        Assert.Equal(nameof(ERROR.IsNotTrue), outcome.NormalizedErrorCode);
                    }

                    // The compared settings are as they were: the exact match is still accepted.
                    var (after, afterDiag) = await RunAsync(HilTaskBuilder.BuildCheckFileSettings(
                        HilConstants.TestAppId, keyType, defaultKey, onTheCard));
                    Assert.True(after.Outcome == ExecutionOutcome.Success, $"The settings changed during the checks — keyType={keyType}\n{afterDiag}\nSee the Task.Outcome entry of the log for the values.");
                    Assert.Equal(nameof(ERROR.NoError), Assert.Single(after.TaskOutcomes).NormalizedErrorCode);
                }
                finally
                {
                    await _fixture.CleanupTestAidAsync();
                }
            });
        }

        /// <summary>
        /// The Check File Settings task against a real card compares only what is selected. The project differs from the
        /// file in one setting at a time and the task is accepted when that setting is not selected (all selected must
        /// match), when the size relation lets the size pass ("at least" a smaller size, "at most" a larger one) and when
        /// another selected setting matches (any selected); it is refused with IsNotTrue when the relation does not let
        /// the size pass or when no selected setting matches, and with ProtocolConstraint when nothing is selected. Nothing
        /// on the card changes. Needs a reader and a DESFire card; it is skipped without them.
        /// </summary>
        [Theory]
        [InlineData(DESFireKeyType.DF_KEY_AES, HilConstants.DefaultKeyAes)]
        [InlineData(DESFireKeyType.DF_KEY_DES, HilConstants.DefaultKeyDes)]
        public async Task CheckFileSettings_ComparesOnlyWhatIsSelected(
            DESFireKeyType keyType,
            string defaultKey)
        {
            _fixture.SkipIfNoHardware();

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                await _fixture.CleanupTestAidAsync();
                var onTheCard = HilFileSettings.Distinct;
                var larger = onTheCard with { Size = onTheCard.Size + 1 };
                var smaller = onTheCard with { Size = onTheCard.Size - 1 };

                var cases = new (string What, HilFileSettings Expected, Action<MifareDesfireSetupViewModel> Choose, ERROR Error)[]
                {
                    ("a size that differs is not selected", larger, task => task.IsFileSizeCompared = false, ERROR.NoError),
                    ("the file is at least as large as the project says", smaller, task => task.FileSizeRelation = NumericRelation.ActualAtLeastExpected, ERROR.NoError),
                    ("the file is at most as large as the project says", larger, task => task.FileSizeRelation = NumericRelation.ActualAtMostExpected, ERROR.NoError),
                    ("the file is larger than the project says, which allows at most", smaller, task => task.FileSizeRelation = NumericRelation.ActualAtMostExpected, ERROR.IsNotTrue),
                    ("the file is smaller than the project says, which asks for at least", larger, task => task.FileSizeRelation = NumericRelation.ActualAtLeastExpected, ERROR.IsNotTrue),
                    ("the size differs but the communication mode, which is also selected, matches", larger, task => OnlySizeAndCommunicationMode(task, DesfireFileSettingsCompareMode.AnySelected), ERROR.NoError),
                    ("the size and the communication mode both differ", larger with { CommunicationMode = EncryptionMode.CM_MAC }, task => OnlySizeAndCommunicationMode(task, DesfireFileSettingsCompareMode.AnySelected), ERROR.IsNotTrue),
                    ("the size differs and the communication mode, which is also selected, matches", larger, task => OnlySizeAndCommunicationMode(task, DesfireFileSettingsCompareMode.AllSelected), ERROR.IsNotTrue),
                    ("nothing is selected", onTheCard, NothingSelected, ERROR.ProtocolConstraint),
                };

                try
                {
                    var (setup, setupDiag) = await RunAsync(HilTaskBuilder.BuildCreateApplicationWithFile(
                        HilConstants.TestAppId, keyType, defaultKey, onTheCard));
                    Assert.True(setup.Outcome == ExecutionOutcome.Success, $"Setting the file up failed — keyType={keyType}\n{setupDiag}");

                    foreach (var (what, expected, choose, error) in cases)
                    {
                        var (result, diag) = await RunAsync(HilTaskBuilder.BuildCheckFileSettings(
                            HilConstants.TestAppId, keyType, defaultKey, expected, choose));
                        var outcome = Assert.Single(result.TaskOutcomes);
                        Assert.True(error.ToString() == outcome.NormalizedErrorCode,
                            $"Expected {error} when {what}, got {outcome.NormalizedErrorCode} — keyType={keyType}\n{diag}\nSee the Task.Outcome entry of the log for the values.");
                        Assert.Equal(error == ERROR.NoError, outcome.IsSuccess);
                    }

                    // The compared settings are as they were: the exact match, everything selected, is still accepted.
                    var (after, afterDiag) = await RunAsync(HilTaskBuilder.BuildCheckFileSettings(
                        HilConstants.TestAppId, keyType, defaultKey, onTheCard));
                    Assert.True(after.Outcome == ExecutionOutcome.Success, $"The settings changed during the checks — keyType={keyType}\n{afterDiag}");
                    Assert.Equal(nameof(ERROR.NoError), Assert.Single(after.TaskOutcomes).NormalizedErrorCode);
                }
                finally
                {
                    await _fixture.CleanupTestAidAsync();
                }
            });
        }

        private static void OnlySizeAndCommunicationMode(MifareDesfireSetupViewModel task, DesfireFileSettingsCompareMode mode)
        {
            NothingSelected(task);
            task.IsFileSizeCompared = true;
            task.IsFileCommunicationModeCompared = true;
            task.FileSettingsCompareMode = mode;
        }

        private static void NothingSelected(MifareDesfireSetupViewModel task)
        {
            task.IsFileTypeCompared = false;
            task.IsFileSizeCompared = false;
            task.IsFileCommunicationModeCompared = false;
            task.IsFileReadRightCompared = false;
            task.IsFileWriteRightCompared = false;
            task.IsFileReadWriteRightCompared = false;
            task.IsFileChangeRightCompared = false;
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

        /// <summary>
        /// Smoke test: CreateApplication with each supported key type.
        /// Verifies that the fixture can successfully create and then delete a test application.
        /// </summary>
        [Theory]
        [InlineData(DESFireKeyType.DF_KEY_AES)]
        [InlineData(DESFireKeyType.DF_KEY_DES)]
        public async Task CreateAndDeleteApplication(DESFireKeyType keyType)
        {
            _fixture.SkipIfNoHardware();

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                await _fixture.CleanupTestAidAsync();

                // Create
                _fixture.Host!.Project.ReplaceTaskCollection(
                    HilTaskBuilder.BuildCreateApplication(
                        HilConstants.TestAppId, keyType));

                var createResult = await _fixture.Host.Execution.ExecuteAllAsync();
                var createDiag = _fixture.Host.GetTaskErrorSummary();
                _fixture.Host.Execution.ResetStatus();

                Assert.True(createResult.Outcome == ExecutionOutcome.Success,
                    $"CreateApplication failed — keyType={keyType}\n{createDiag}");

                // Delete via PICC master key (factory default, never changed by tests).
                _fixture.Host.Project.ReplaceTaskCollection(
                    HilTaskBuilder.BuildDeleteApplication(
                        HilConstants.TestAppId,
                        DESFireKeyType.DF_KEY_DES,
                        HilConstants.DefaultKeyDes,
                        DesfireDeleteAuthMethod.PiccMasterKey));

                var deleteResult = await _fixture.Host.Execution.ExecuteAllAsync();
                var deleteDiag = _fixture.Host.GetTaskErrorSummary();
                _fixture.Host.Execution.ResetStatus();

                Assert.True(deleteResult.Outcome == ExecutionOutcome.Success,
                    $"DeleteApplication failed — keyType={keyType}\n{deleteDiag}");
            });
        }
    }
}
