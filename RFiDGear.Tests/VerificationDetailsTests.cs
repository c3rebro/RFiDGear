#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using RFiDGear.Bridge;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Models;
using RFiDGear.Services.ExternalControl;
using RFiDGear.Services.TaskExecution;
using RFiDGear.Tests.CardCheckAssistant;

using Xunit;

using Contracts = RFiDGear.Contracts;

namespace RFiDGear.Tests
{
    /// <summary>
    /// What a check that compares values tells a program that has to say why it failed: the details of the file-settings check as
    /// the task writes them, how they travel through the task loop and the outcomes of a run, and what the wire has of them. The
    /// rules that matter: a setting that was not compared says so and nothing else; one that could not be read has no value read;
    /// every number and token goes through unchanged; and a task that gave up, was skipped or compares nothing has none.
    /// </summary>
    public sealed class VerificationDetailsTests
    {
        private const int All = 127;

        private static DesfireFileSettingsComparison Comparison(
            int selected = All,
            int matched = All,
            int unread = 0,
            bool any = false,
            int relation = 0,
            bool matches = true) => new DesfireFileSettingsComparison
            {
                ApplicationId = 0x0ABCDE,
                FileNumber = 3,
                ExpectedFileType = 0,
                ActualFileType = 0,
                ExpectedFileSize = 16,
                ActualFileSize = 32,
                ExpectedCommunicationMode = 3,
                ActualCommunicationMode = 3,
                ExpectedAccessRights0 = 0xFE,
                ActualAccessRights0 = 0xFE,
                ExpectedAccessRights1 = 0xED,
                ActualAccessRights1 = 0xED,
                SelectedSettings = selected,
                MatchedSettings = matched,
                UnreadSettings = unread,
                CompareModeAny = any,
                FileSizeRelation = relation,
                Matches = matches,
            };

        private static Contracts.VerificationCheck Check(Contracts.VerificationDetails details, string name) => details.Checks.Single(check => check.Name == name);

        [Fact]
        public void AllSettingsThatMatched_AreReportedWithTheirNumbers_InAFixedOrder()
        {
            var details = DesfireFileSettingsVerification.From(Comparison(relation: 1));

            Assert.Equal(1, details.SchemaVersion);
            Assert.Equal(Contracts.VerificationKinds.DesfireFileSettings, details.Kind);
            Assert.True(details.OverallMatch);
            Assert.Equal(Contracts.VerificationCompareModes.All, details.CompareMode);
            Assert.Equal((0x0ABCDE, 3), (details.ApplicationId, details.FileNumber));
            Assert.Null(details.ErrorCode);
            Assert.Equal(
                new[] { "fileType", "fileSize", "communicationMode", "accessRead", "accessWrite", "accessReadWrite", "accessChange" },
                details.Checks.Select(check => check.Name));
            Assert.All(details.Checks, check =>
            {
                Assert.True(check.Compared);
                Assert.Equal(Contracts.VerificationResults.Match, check.Result);
                Assert.Equal(check.Expected, check.Name == "fileSize" ? 16 : check.Actual);
            });

            var size = Check(details, "fileSize");
            Assert.Equal((16L, 32L, Contracts.VerificationRules.AtLeast), (size.Expected, size.Actual, size.Rule));

            // The nibbles: read in the low half and write in the high half of the first byte, read-write and change in the second.
            Assert.Equal(new long?[] { 14, 15, 13, 14 }, new[] { "accessRead", "accessWrite", "accessReadWrite", "accessChange" }.Select(name => Check(details, name).Expected));
            Assert.Null(Check(details, "accessRead").Rule);
        }

        [Fact]
        public void ASettingThatWasNotSelected_SaysSo_AndNothingMoreAboutIt()
        {
            // Only the size and the change right were selected.
            var details = DesfireFileSettingsVerification.From(Comparison(selected: 2 | 64, matched: 2 | 64));

            var notCompared = details.Checks.Where(check => check.Name != "fileSize" && check.Name != "accessChange").ToList();

            Assert.Equal(5, notCompared.Count);
            Assert.All(notCompared, check =>
            {
                Assert.False(check.Compared);
                Assert.Equal(Contracts.VerificationResults.NotCompared, check.Result);
                Assert.Null(check.Expected);
                Assert.Null(check.Actual);
                Assert.Null(check.Rule);
            });
            Assert.True(Check(details, "fileSize").Compared);
            Assert.True(Check(details, "accessChange").Compared);
        }

        [Fact]
        public void ASettingThatDiffered_IsAMismatch_WithBothValues_AndTheWholeIsNotAMatch()
        {
            var comparison = Comparison(matched: All & ~2, matches: false) with { ActualFileSize = 8 };

            var details = DesfireFileSettingsVerification.From(comparison);

            var size = Check(details, "fileSize");
            Assert.False(details.OverallMatch);
            Assert.Equal(Contracts.VerificationResults.Mismatch, size.Result);
            Assert.Equal((16L, 8L, Contracts.VerificationRules.Exact), (size.Expected, size.Actual, size.Rule));
            Assert.Equal(6, details.Checks.Count(check => check.Result == Contracts.VerificationResults.Match));
        }

        [Fact]
        public void ASettingThatCouldNotBeRead_HasNoValueRead_AndTheDetailsSayWhy()
        {
            // A data file that reports a size of 0: the check cannot tell, and says so instead of calling it a mismatch.
            var comparison = Comparison(matched: All & ~2, unread: 2, matches: false) with { ActualFileSize = 0 };

            var details = DesfireFileSettingsVerification.From(comparison);

            var size = Check(details, "fileSize");
            Assert.Equal(Contracts.VerificationResults.NotRead, size.Result);
            Assert.True(size.Compared);
            Assert.Equal(16L, size.Expected);
            Assert.Null(size.Actual);
            Assert.Equal(Contracts.VerificationErrors.NotRead, details.ErrorCode);
        }

        [Fact]
        public void TheCompareMode_IsAnyWhenOneMatchWasEnough()
        {
            var details = DesfireFileSettingsVerification.From(Comparison(any: true));

            Assert.Equal(Contracts.VerificationCompareModes.Any, details.CompareMode);
        }

        [Theory]
        [InlineData(0, "exact")]
        [InlineData(1, "atLeast")]
        [InlineData(2, "atMost")]
        [InlineData(7, null)]
        public void TheRuleOfTheSize_IsOneOfTheThreeTokens_AndAnUnknownRelationNamesNoRule(int relation, string? rule)
        {
            var details = DesfireFileSettingsVerification.From(Comparison(relation: relation));

            Assert.Equal(rule, Check(details, "fileSize").Rule);
        }

        [Fact]
        public void TheTokensOfTheTwoContracts_AreTheSameWords()
        {
            Assert.Equal(Contracts.VerificationKinds.DesfireFileSettings, DetailKinds.DesfireFileSettings);
            Assert.Equal(
                new[]
                {
                    Contracts.VerificationCheckNames.FileType, Contracts.VerificationCheckNames.FileSize, Contracts.VerificationCheckNames.CommunicationMode,
                    Contracts.VerificationCheckNames.AccessRead, Contracts.VerificationCheckNames.AccessWrite, Contracts.VerificationCheckNames.AccessReadWrite,
                    Contracts.VerificationCheckNames.AccessChange,
                },
                new[] { CheckNames.FileType, CheckNames.FileSize, CheckNames.CommunicationMode, CheckNames.AccessRead, CheckNames.AccessWrite, CheckNames.AccessReadWrite, CheckNames.AccessChange });
            Assert.Equal(
                new[] { Contracts.VerificationResults.Match, Contracts.VerificationResults.Mismatch, Contracts.VerificationResults.NotRead, Contracts.VerificationResults.NotCompared },
                new[] { CheckResults.Match, CheckResults.Mismatch, CheckResults.NotRead, CheckResults.NotCompared });
            Assert.Equal(
                new[] { Contracts.VerificationRules.Exact, Contracts.VerificationRules.AtLeast, Contracts.VerificationRules.AtMost },
                new[] { CheckRules.Exact, CheckRules.AtLeast, CheckRules.AtMost });
            Assert.Equal(new[] { Contracts.VerificationCompareModes.All, Contracts.VerificationCompareModes.Any }, new[] { CompareModes.All, CompareModes.Any });
            Assert.Equal(Contracts.VerificationErrors.NotRead, DetailErrors.NotRead);
        }

        // ── From the observation of the task loop to the outcome and the wire ──

        private static TaskExecutionObservation Observed(Contracts.VerificationDetails? details, string decision = "Executed", bool? success = false) =>
            new TaskExecutionObservation(0, "10", null!, decision, success, ERROR.IsNotTrue, null, details);

        [Fact]
        public void AnOutcome_CarriesTheDetailsOfItsObservation_AndASkippedTaskHasNone()
        {
            var details = DesfireFileSettingsVerification.From(Comparison());
            var snapshot = new object?[] { new object(), new object(), new object() };
            var observations = new[]
            {
                Observed(details),
                new TaskExecutionObservation(1, "11", null!, "Skipped", null, ERROR.Empty, "ExecuteConditionNotMet", details),
            };

            var outcomes = TaskOutcomeMapper.Build(snapshot, observations);

            Assert.Same(details, outcomes[0].VerificationDetails);
            Assert.Null(outcomes[1].VerificationDetails);
            Assert.Null(outcomes[2].VerificationDetails);
        }

        [Fact]
        public void TheOutcomeStillDeconstructsAndComparesAsBefore_TheDetailsAreOnlyAnExtraMember()
        {
            var plain = new Contracts.TaskOutcome(0, "1", "MifareDesfireSetupViewModel", true, "NoError");

            var (position, index, type, success, error, skip) = plain;

            Assert.Equal((0, "1", "MifareDesfireSetupViewModel", (bool?)true, "NoError", (string?)null), (position, index, type, success, error, skip));
            Assert.Null(plain.VerificationDetails);
            Assert.Equal(plain, plain with { });
            Assert.NotEqual(plain, plain with { VerificationDetails = DesfireFileSettingsVerification.From(Comparison()) });
        }

        [Fact]
        public void TheWire_HasEveryNumberAndTokenOfTheDetails_AsTheTaskWroteThem()
        {
            var details = DesfireFileSettingsVerification.From(Comparison(matched: All & ~2, unread: 2, any: true, relation: 2, matches: false) with { ActualFileSize = 0 });

            var wire = BridgeMapper.ToVerificationDto(details)!;

            Assert.Equal((1, DetailKinds.DesfireFileSettings, false, CompareModes.Any, 0x0ABCDE, 3, DetailErrors.NotRead),
                (wire.SchemaVersion, wire.Kind, wire.OverallMatch, wire.CompareMode, wire.ApplicationId, wire.FileNumber, wire.ErrorCode));
            Assert.Equal(details.Checks.Count, wire.Checks.Count);

            for (var i = 0; i < wire.Checks.Count; i++)
            {
                var from = details.Checks[i];
                var to = wire.Checks[i];

                Assert.Equal((from.Name, from.Compared, from.Result, from.Rule, from.Expected, from.Actual), (to.Name, to.Compared, to.Result, to.Rule, to.Expected, to.Actual));
            }

            Assert.Null(BridgeMapper.ToVerificationDto(null));
        }

        [Fact]
        public void ARunResultOfTheBridge_CarriesTheDetailsOfItsTask_AndATaskWithoutHasNone()
        {
            var details = DesfireFileSettingsVerification.From(Comparison(matched: All & ~2, matches: false) with { ActualFileSize = 8 });
            var result = FakeResults.Success(2) with
            {
                TaskOutcomes = new[]
                {
                    new Contracts.TaskOutcome(0, "1", "MifareDesfireSetupViewModel", false, "IsNotTrue") { VerificationDetails = details },
                    new Contracts.TaskOutcome(1, "2", "MifareDesfireSetupViewModel", true, "NoError"),
                },
            };

            var dto = BridgeMapper.ToDto(result, "run-1", "r-1", 1);

            var size = dto.TaskOutcomes[0].VerificationDetails!.Checks.Single(check => check.Name == CheckNames.FileSize);
            Assert.Equal((CheckResults.Mismatch, 16L, 8L), (size.Result, size.Expected, size.Actual));
            Assert.Null(dto.TaskOutcomes[1].VerificationDetails);
        }

        [Fact]
        public void TheWireForm_LeavesOutWhatIsNotThere_AndNamesEverythingElseExactly()
        {
            var details = DesfireFileSettingsVerification.From(Comparison(selected: 2, matched: 2));
            var json = System.Text.Json.JsonSerializer.Serialize(BridgeMapper.ToVerificationDto(details), BridgeJson.CreateClientOptions());

            Assert.Contains("\"schemaVersion\":1", json);
            Assert.Contains("\"kind\":\"desfire.fileSettings\"", json);
            Assert.Contains("\"overallMatch\":true", json);
            Assert.Contains("{\"name\":\"fileType\",\"compared\":false,\"result\":\"notCompared\"}", json);
            Assert.Contains("{\"name\":\"fileSize\",\"compared\":true,\"result\":\"match\",\"rule\":\"exact\",\"expected\":16,\"actual\":32}", json);
            Assert.DoesNotContain("errorCode", json);
        }

        // ── In the task loop ──

        private sealed class NoReader : IReaderDeviceProvider
        {
            public RFiDGear.Infrastructure.ReaderProviders.ReaderDevice GetInstance() => null!;
        }

        private sealed class NoTimer : IDispatcherTimerAdapter
        {
            public bool IsEnabled { get; set; }

            public object Tag { get; set; } = string.Empty;

            public void Start()
            {
            }

            public void Stop()
            {
            }

            public void Dispose()
            {
            }
        }
        private sealed class VerifyingTask : StubTaskModel, ITaskVerificationSource
        {
            public Contracts.VerificationDetails? Details { get; set; }

            public bool Throws { get; set; }

            public Contracts.VerificationDetails? GetVerificationDetails() => Throws ? throw new InvalidOperationException("the task has a problem") : Details;
        }

        private static async Task<IReadOnlyList<TaskExecutionObservation>> RunAsync(params (VerifyingTask Task, bool Fails)[] tasks)
        {
            var observations = new List<TaskExecutionObservation>();
            var descriptors = tasks
                .Select((entry, position) => new TaskDescriptor(position, entry.Task, _ =>
                {
                    entry.Task.CurrentTaskErrorLevel = entry.Fails ? ERROR.IsNotTrue : ERROR.NoError;
                    entry.Task.IsTaskCompletedSuccessfully = !entry.Fails;

                    return Task.CompletedTask;
                }))
                .ToList();
            var request = new TaskExecutionRequest
            {
                TaskDescriptors = descriptors,
                Timeouts = TaskExecutionTimeouts.Default,
                Observations = observations,
            };
            var service = new TaskExecutionService(new NoReader(), new NoTimer(), new NoTimer());

            await StaTestRunner.RunOnStaThreadAsync(async () => await service.ExecuteOnceAsync(request));

            return observations;
        }

        [Fact]
        public async Task TheLoop_TakesTheDetailsOfATaskThatRan_AlsoWhenItFailed_AndOfNoOtherTask()
        {
            var details = DesfireFileSettingsVerification.From(Comparison(matched: All & ~2, matches: false));
            var failed = new VerifyingTask { CurrentTaskIndex = "0", Details = details };
            var succeeded = new VerifyingTask { CurrentTaskIndex = "1", Details = details };
            var silent = new VerifyingTask { CurrentTaskIndex = "2" };

            var observations = await RunAsync((failed, true), (succeeded, false), (silent, false));

            Assert.Same(details, observations.Single(o => o.TaskPosition == 0).VerificationDetails);
            Assert.Same(details, observations.Single(o => o.TaskPosition == 1).VerificationDetails);
            Assert.Null(observations.Single(o => o.TaskPosition == 2).VerificationDetails);
        }

        [Fact]
        public async Task ATaskThatCannotSayWhatItCompared_NeverStopsTheRun_AndHasNoDetails()
        {
            var broken = new VerifyingTask { CurrentTaskIndex = "0", Throws = true };
            var next = new VerifyingTask { CurrentTaskIndex = "1" };

            var observations = await RunAsync((broken, true), (next, false));

            Assert.Equal(2, observations.Count);
            Assert.Null(observations[0].VerificationDetails);
            Assert.Equal(false, observations[0].WasSuccessful);
            Assert.Equal(true, observations[1].WasSuccessful);
        }
    }
}
