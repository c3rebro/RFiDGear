#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

using RFiDGear.Bridge;
using RFiDGear.Contracts;
using RFiDGear.Services.ExternalControl;

using Xunit;

namespace RFiDGear.Tests.ExternalControl
{
    /// <summary>What a request to run may ask for, how two requests are told apart, and how the result of the host becomes the result on the wire.</summary>
    public class BridgeRunPlanTests
    {
        private static StartRunRequest Request(string id = "r-1", params (string Name, string Value)[] variables) => new StartRunRequest
        {
            RequestId = id,
            Variables = variables.Select(v => new VariablePair { Name = v.Name, Value = v.Value }).ToList(),
        };

        private static BridgeFault Refusal(Func<RunPlan> make) => Assert.Throws<BridgeFault>(make);

        // ── The request ──

        [Theory]
        [InlineData("")]
        [InlineData("has space")]
        [InlineData("line\nbreak")]
        [InlineData("a/b")]
        public void ARequestIdThatIsNotOne_IsRefused_WithTheNameOfTheField(string id)
        {
            var fault = Refusal(() => RunPlan.From(Request(id)));

            Assert.Equal(BridgeErrorCodes.InvalidRequest, fault.Code);
            Assert.Equal("requestId", fault.Details!["field"]);
        }

        [Fact]
        public void ARequestIdOfTheLongestAllowedLength_IsFine_OneMoreIsNot()
        {
            Assert.NotNull(RunPlan.From(Request(new string('a', BridgeProtocol.MaxRequestIdLength))));
            Assert.Equal("requestId", Refusal(() => RunPlan.From(Request(new string('a', BridgeProtocol.MaxRequestIdLength + 1)))).Details!["field"]);
            Assert.Equal("request", Refusal(() => RunPlan.From(null)).Details!["field"]);
        }

        [Fact]
        public void VariablesAtTheLimits_AreFine_BeyondThemTheyAreRefused()
        {
            var most = Enumerable.Range(0, BridgeProtocol.MaxVariables).Select(i => ("V" + i, "x")).ToArray();
            Assert.Equal(BridgeProtocol.MaxVariables, RunPlan.From(Request("r-1", most)).Variables.Count);
            Assert.NotNull(RunPlan.From(Request("r-1", (new string('n', BridgeProtocol.MaxVariableNameLength), new string('v', BridgeProtocol.MaxVariableValueLength)))));

            var tooMany = Enumerable.Range(0, BridgeProtocol.MaxVariables + 1).Select(i => ("V" + i, "x")).ToArray();

            Assert.Equal("variables", Refusal(() => RunPlan.From(Request("r-1", tooMany))).Details!["field"]);
        }

        [Theory]
        [InlineData("", "x")]
        [InlineData("   ", "x")]
        [InlineData("na\nme", "x")]
        [InlineData("na\0me", "x")]
        public void AVariableWithAnUnusableName_IsRefused(string name, string value)
        {
            Assert.Equal("variables", Refusal(() => RunPlan.From(Request("r-1", (name, value)))).Details!["field"]);
        }

        [Fact]
        public void AVariableThatIsTooLong_OrHasANul_OrIsMissing_IsRefused_AndTheValueIsNotInTheAnswer()
        {
            var tooLong = new string('v', BridgeProtocol.MaxVariableValueLength + 1);

            var fault = Refusal(() => RunPlan.From(Request("r-1", ("$JOBNUMBER", tooLong))));
            Refusal(() => RunPlan.From(Request("r-1", ("$JOBNUMBER", "a\0b"))));
            Refusal(() => RunPlan.From(Request("r-1", ("N" + new string('n', BridgeProtocol.MaxVariableNameLength), "x"))));
            Refusal(() => RunPlan.From(new StartRunRequest { RequestId = "r-1", Variables = new[] { new VariablePair { Name = "A", Value = null! } } }));

            Assert.DoesNotContain(tooLong, string.Join(",", fault.Details!.Values), StringComparison.Ordinal);
        }

        [Fact]
        public void TheSameNameTwice_AlsoInAnotherCase_IsRefused_BecauseTheHostWouldKeepOnlyOne()
        {
            Assert.Equal("variables", Refusal(() => RunPlan.From(Request("r-1", ("$JOBNUMBER", "1"), ("$jobnumber", "2")))).Details!["field"]);
        }

        [Fact]
        public void NoVariablesAtAll_IsARequest()
        {
            Assert.Empty(RunPlan.From(new StartRunRequest { RequestId = "r-1", Variables = null! }).Variables);
            Assert.Empty(RunPlan.From(Request("r-1")).Variables);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(@"projects\one.rfPrj")]
        [InlineData(@"C:one.rfPrj")]
        public void AnExpectedProjectThatIsNotAFullPath_IsRefused(string path)
        {
            Assert.Equal("expectProjectPath", Refusal(() => RunPlan.From(new StartRunRequest { RequestId = "r-1", ExpectProjectPath = path })).Details!["field"]);
        }

        [Fact]
        public void AnExpectedTaskCountBelowZero_IsRefused_ZeroIsNot()
        {
            Assert.Equal("expectTaskCount", Refusal(() => RunPlan.From(new StartRunRequest { RequestId = "r-1", ExpectTaskCount = -1 })).Details!["field"]);
            Assert.Equal(0, RunPlan.From(new StartRunRequest { RequestId = "r-1", ExpectTaskCount = 0 }).ExpectTaskCount);
        }

        [Fact]
        public void TheExpectedProject_IsNormalised()
        {
            var plan = RunPlan.From(new StartRunRequest { RequestId = "r-1", ExpectProjectPath = @"C:\projects\sub\..\one.rfPrj" });

            Assert.Equal(@"C:\projects\one.rfPrj", plan.ExpectProjectPath);
        }

        // ── Telling two requests apart ──

        [Fact]
        public void TheSameVariables_InAnotherOrderOrCase_AreTheSameRequest()
        {
            var a = RunPlan.From(Request("r-1", ("$JOBNUMBER", "J-42"), ("REPORTTARGETPATH", @"C:\r\x.pdf")));
            var b = RunPlan.From(Request("r-1", ("reporttargetpath", @"C:\r\x.pdf"), ("$jobnumber", "J-42")));

            Assert.Equal(a.PayloadHash, b.PayloadHash);
        }

        [Fact]
        public void AnythingElse_IsAnotherRequest()
        {
            var baseline = RunPlan.From(Request("r-1", ("$JOBNUMBER", "J-42"))).PayloadHash;

            var hashes = new[]
            {
                RunPlan.From(Request("r-1", ("$JOBNUMBER", "J-43"))).PayloadHash,                                  // another value
                RunPlan.From(Request("r-1", ("$JOBNUMBER", "j-42"))).PayloadHash,                                  // values keep their case
                RunPlan.From(Request("r-1", ("$CHIPNUMBER", "J-42"))).PayloadHash,                                 // another name
                RunPlan.From(Request("r-1")).PayloadHash,                                                           // none
                RunPlan.From(Request("r-1", ("$JOBNUMBER", "J-42"), ("A", "b"))).PayloadHash,                       // one more
                RunPlan.From(new StartRunRequest { RequestId = "r-1", Variables = Request("r-1", ("$JOBNUMBER", "J-42")).Variables, ExpectTaskCount = 2 }).PayloadHash,
                RunPlan.From(new StartRunRequest { RequestId = "r-1", Variables = Request("r-1", ("$JOBNUMBER", "J-42")).Variables, ExpectProjectPath = @"C:\p\one.rfPrj" }).PayloadHash,
            };

            Assert.DoesNotContain(baseline, hashes);
            Assert.Equal(hashes.Length, hashes.Distinct().Count());
        }

        [Fact]
        public void TwoDifferentSplitsOfTheSameCharacters_AreNotTheSameRequest()
        {
            // Length-prefixed: ("AB", "C") and ("A", "BC") do not write the same bytes.
            var a = RunPlan.From(Request("r-1", ("AB", "C"))).PayloadHash;
            var b = RunPlan.From(Request("r-1", ("A", "BC"))).PayloadHash;

            Assert.NotEqual(a, b);
        }

        [Fact]
        public void TheRequestIdIsNotPartOfTheContent()
        {
            Assert.Equal(RunPlan.From(Request("r-1")).PayloadHash, RunPlan.From(Request("r-2")).PayloadHash);
        }

        // ── The result ──

        private static ExecResult HostResult(ExecutionOutcome outcome, ExecutionFailureReason reason = ExecutionFailureReason.None, int tasks = 2, int outcomes = -1)
        {
            var list = Enumerable.Range(0, outcomes < 0 ? tasks : outcomes)
                .Select(i => new TaskOutcome(i, (i + 1).ToString(), "MifareDesfireSetupViewModel", i == 0, i == 0 ? "NoError" : "AuthFailure", i == 0 ? null : TaskSkipReasons.NotReached))
                .ToList();

            return new ExecResult("host-run", outcome, tasks, 1, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddSeconds(3), list, reason) { CardUid = "04AABBCC" };
        }

        [Theory]
        [InlineData(ExecutionOutcome.Success, ExecutionFailureReason.None, RunStatuses.Success, FailureReasons.None)]
        [InlineData(ExecutionOutcome.CompletedWithIntentionalFailures, ExecutionFailureReason.None, RunStatuses.CompletedWithIntentionalFailures, FailureReasons.None)]
        [InlineData(ExecutionOutcome.Failure, ExecutionFailureReason.None, RunStatuses.Failure, FailureReasons.None)]
        [InlineData(ExecutionOutcome.Failure, ExecutionFailureReason.Faulted, RunStatuses.Failure, FailureReasons.Faulted)]
        [InlineData(ExecutionOutcome.Failure, ExecutionFailureReason.Cancelled, RunStatuses.Cancelled, FailureReasons.Cancelled)]
        [InlineData(ExecutionOutcome.Failure, ExecutionFailureReason.TimedOut, RunStatuses.TimedOut, FailureReasons.TimedOut)]
        [InlineData((ExecutionOutcome)99, ExecutionFailureReason.None, RunStatuses.Failure, FailureReasons.Unknown)]
        [InlineData((ExecutionOutcome)99, ExecutionFailureReason.Faulted, RunStatuses.Failure, FailureReasons.Faulted)]
        [InlineData(ExecutionOutcome.Success, (ExecutionFailureReason)99, RunStatuses.Success, FailureReasons.Unknown)]
        [InlineData(ExecutionOutcome.Failure, (ExecutionFailureReason)99, RunStatuses.Failure, FailureReasons.Unknown)]
        public void EveryOutcomeAndReasonOfTheHost_HasAStatusOnTheWire_AndAnUnknownOneIsSaidSo(ExecutionOutcome outcome, ExecutionFailureReason reason, string status, string wireReason)
        {
            var dto = BridgeMapper.ToDto(HostResult(outcome, reason), "run-9", "r-1", 7);

            Assert.Equal(status, dto.Status);
            Assert.Equal(wireReason, dto.FailureReason);
        }

        [Fact]
        public void TheResult_CarriesEverythingTheHostReported_UnderTheIdsOfTheBridge()
        {
            var dto = BridgeMapper.ToDto(HostResult(ExecutionOutcome.CompletedWithIntentionalFailures), "run-9", "r-1", 7);

            Assert.Equal(new[] { "run-9", "r-1", RunSources.Bridge }, new[] { dto.RunId, dto.RequestId!, dto.Source });
            Assert.Equal(7, dto.ResultSeq);
            Assert.Equal(new[] { 2, 1 }, new[] { dto.TotalTasks, dto.FailedTasks });
            Assert.Equal(DateTimeOffset.UnixEpoch, dto.StartedAt);
            Assert.Equal(DateTimeOffset.UnixEpoch.AddSeconds(3), dto.CompletedAt);
            Assert.Equal("04AABBCC", dto.CardUid);
            Assert.Equal(2, dto.TaskOutcomes.Count);
            Assert.Equal(new[] { 0, 1 }, dto.TaskOutcomes.Select(o => o.Position));
            Assert.Equal(new bool?[] { true, false }, dto.TaskOutcomes.Select(o => o.IsSuccess));
            Assert.Equal(new[] { "NoError", "AuthFailure" }, dto.TaskOutcomes.Select(o => o.NormalizedErrorCode));
            Assert.Equal(new[] { null, TaskSkipReasons.NotReached }, dto.TaskOutcomes.Select(o => o.SkipReason));
            Assert.All(dto.TaskOutcomes, o => Assert.Equal("MifareDesfireSetupViewModel", o.TaskTypeName));
        }

        [Fact]
        public void AResultWithAnotherNumberOfOutcomesThanTasks_IsNotACompleteResult()
        {
            var dto = BridgeMapper.ToDto(HostResult(ExecutionOutcome.Success, tasks: 3, outcomes: 2), "run-9", "r-1", 1);

            Assert.Equal(RunStatuses.Failure, dto.Status);
            Assert.Equal(FailureReasons.BridgeFault, dto.FailureReason);
            Assert.Equal(3, dto.TotalTasks);
        }

        [Fact]
        public void AResultWithNoTasksAtAll_IsComplete()
        {
            var dto = BridgeMapper.ToDto(HostResult(ExecutionOutcome.Success, tasks: 0), "run-9", null, 1);

            Assert.Equal(RunStatuses.Success, dto.Status);
            Assert.Null(dto.RequestId);
            Assert.Empty(dto.TaskOutcomes);
        }

        [Fact]
        public void ARunTheBridgeCouldNotFinish_IsAFailure_WithoutOutcomes()
        {
            var dto = BridgeMapper.ToFaultedDto("run-9", "r-1", 4, DateTimeOffset.UnixEpoch);

            Assert.Equal(RunStatuses.Failure, dto.Status);
            Assert.Equal(FailureReasons.BridgeFault, dto.FailureReason);
            Assert.Equal(0, dto.TotalTasks);
            Assert.Empty(dto.TaskOutcomes);
            Assert.Equal(4, dto.ResultSeq);
            Assert.Equal(DateTimeOffset.UnixEpoch, dto.StartedAt);
        }

        [Fact]
        public void TheMembersOfTheHostResult_AreTheOnesTheMapperHandles()
        {
            // A member that the host adds fails this until somebody decides whether a client gets it.
            Assert.Equal(
                new[] { "CardUid", "CompletedAt", "FailedTasks", "FailureReason", "Outcome", "RunId", "StartedAt", "TaskOutcomes", "TotalTasks" },
                typeof(ExecResult).GetProperties().Where(p => p.Name != "EqualityContract").Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
            Assert.Equal(
                new[] { "IsSuccess", "NormalizedErrorCode", "Position", "ProjectTaskIndex", "SkipReason", "TaskTypeName" },
                typeof(TaskOutcome).GetProperties().Where(p => p.Name != "EqualityContract").Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
            Assert.Equal(new[] { "Cancelled", "Faulted", "None", "TimedOut" }, Enum.GetNames<ExecutionFailureReason>().OrderBy(n => n, StringComparer.Ordinal));
            Assert.Equal(new[] { "CompletedWithIntentionalFailures", "Failure", "Success" }, Enum.GetNames<ExecutionOutcome>().OrderBy(n => n, StringComparer.Ordinal));
        }

        [Fact]
        public void EveryFailureReasonOfTheHost_HasAWireNameOfItsOwn()
        {
            var names = Enum.GetValues<ExecutionFailureReason>().Select(BridgeMapper.ToWire).ToList();

            Assert.DoesNotContain(FailureReasons.Unknown, names);
            Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
        }
    }
}
