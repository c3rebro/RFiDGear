using RFiDGear.Contracts;
using RFiDGear.Infrastructure;

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
