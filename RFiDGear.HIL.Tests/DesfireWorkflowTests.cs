using RFiDGear.Contracts;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks;

using System;
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
        /// task succeeds again afterwards, which shows that the checks did not change the card. Which reader ran (Elatec
        /// first, PC/SC otherwise) is not asserted: see the log of the run.
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
                    Assert.True(match.Outcome == ExecutionOutcome.Success, $"The exact match was not accepted — keyType={keyType}\n{matchDiag}");

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

                    // The card is as it was: the exact match is still accepted.
                    var (after, afterDiag) = await RunAsync(HilTaskBuilder.BuildCheckFileSettings(
                        HilConstants.TestAppId, keyType, defaultKey, onTheCard));
                    Assert.True(after.Outcome == ExecutionOutcome.Success, $"The card changed during the checks — keyType={keyType}\n{afterDiag}");
                }
                finally
                {
                    await _fixture.CleanupTestAidAsync();
                }
            });
        }

        private async Task<(ExecResult Result, string Diagnostics)> RunAsync(System.Collections.Generic.IEnumerable<object> tasks)
        {
            _fixture.Host!.Project.ReplaceTaskCollection(tasks);
            var result = await _fixture.Host.Execution.ExecuteAllAsync(ExecutionRequest.Empty);
            var diagnostics = _fixture.Host.GetTaskErrorSummary();
            _fixture.Host.Execution.ResetStatus();

            return (result, diagnostics);
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
