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
        /// Combinations of (keyType, defaultKey, changedKey, deleteAuthMethod).
        /// Each row produces a separate test case.
        /// </summary>
        public static TheoryData<DESFireKeyType, string, string, DesfireDeleteAuthMethod> WorkflowCases =>
            new TheoryData<DESFireKeyType, string, string, DesfireDeleteAuthMethod>
            {
                // AES — delete via App master key 0
                {
                    DESFireKeyType.DF_KEY_AES,
                    HilConstants.DefaultKeyAes,
                    "0102030405060708090A0B0C0D0E0F10",
                    DesfireDeleteAuthMethod.ApplicationMasterKey0
                },
                // 3K3DES — delete via App master key 0
                {
                    DESFireKeyType.DF_KEY_3K3DES,
                    HilConstants.DefaultKey3K3Des,
                    "0102030405060708090A0B0C0D0E0F101112131415161718",
                    DesfireDeleteAuthMethod.ApplicationMasterKey0
                },
                // DES — delete via PICC master key (factory default behavior)
                {
                    DESFireKeyType.DF_KEY_DES,
                    HilConstants.DefaultKeyDes,
                    "0102030405060708",
                    DesfireDeleteAuthMethod.PiccMasterKey
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
            string changedKey,
            DesfireDeleteAuthMethod deleteAuthMethod)
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
                        deleteAuthMethod,
                        payloadPath);

                    _fixture.Host!.Project.ReplaceTaskCollection(tasks);
                    var result = await _fixture.Host.Execution.ExecuteAllAsync();

                    Assert.Equal(ExecutionOutcome.Success, result.Outcome);
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
        [InlineData(DESFireKeyType.DF_KEY_AES, HilConstants.DefaultKeyAes)]
        [InlineData(DESFireKeyType.DF_KEY_3K3DES, HilConstants.DefaultKey3K3Des)]
        [InlineData(DESFireKeyType.DF_KEY_DES, HilConstants.DefaultKeyDes)]
        public async Task CreateAndDeleteApplication(DESFireKeyType keyType, string defaultKey)
        {
            _fixture.SkipIfNoHardware();

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                await _fixture.CleanupTestAidAsync();

                // Create
                _fixture.Host!.Project.ReplaceTaskCollection(
                    HilTaskBuilder.BuildCreateApplication(
                        HilConstants.TestAppId, keyType, defaultKey, keyType));

                var createResult = await _fixture.Host.Execution.ExecuteAllAsync();
                _fixture.Host.Execution.ResetStatus();

                Assert.Equal(ExecutionOutcome.Success, createResult.Outcome);

                // Delete
                _fixture.Host.Project.ReplaceTaskCollection(
                    HilTaskBuilder.BuildDeleteApplication(
                        HilConstants.TestAppId,
                        keyType,
                        defaultKey,
                        DesfireDeleteAuthMethod.ApplicationMasterKey0));

                var deleteResult = await _fixture.Host.Execution.ExecuteAllAsync();
                _fixture.Host.Execution.ResetStatus();

                Assert.Equal(ExecutionOutcome.Success, deleteResult.Outcome);
            });
        }
    }
}
