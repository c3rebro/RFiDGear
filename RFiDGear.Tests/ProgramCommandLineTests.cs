using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;

using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Models;
using RFiDGear.ViewModel.TaskSetupViewModels;

using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// Serialises the tests that replace a static of the common task (the call that closes the application, the call that
    /// starts a program): two classes that save and restore one delegate must not run side by side.
    /// </summary>
    [CollectionDefinition("CommonTaskStatics", DisableParallelization = true)]
    public class CommonTaskStaticsCollection
    {
    }

    /// <summary>
    /// The program task: what the text of the task turns into (<see cref="ProgramCommandLine"/>) and that a run leaves the text
    /// alone, so that the next card gets its own values. The placeholders of the text, <c>%UID</c> and the others, used to be
    /// replaced in the text of the task itself, and not at all in the arguments.
    /// </summary>
    [Collection("CommonTaskStatics")]
    public class ProgramCommandLineTests
    {
        private static readonly DateTime Moment = new DateTime(2026, 11, 21, 9, 30, 15);

        private static ProgramTaskValues Values(IReadOnlyDictionary<string, string>? variables = null) => new ProgramTaskValues
        {
            Uid = "04AABBCC",
            ChipType = "Mifare 1K",
            Now = Moment,
            FreeMemory = "4096",
            ApplicationIds = "1, 2, 3",
            ApplicationCount = "3",
            Variables = variables
        };

        [Fact]
        public void TheFileName_IsWhatStandsInTheFirstQuotes_AndTheArguments_AreEverythingAfterThem()
        {
            var start = ProgramCommandLine.Build("\"C:\\Tools\\a.exe\" -x \"two words\" -y", Values());

            Assert.NotNull(start);
            Assert.Equal("C:\\Tools\\a.exe", start.FileName);
            Assert.Equal("-x \"two words\" -y", start.Arguments);
            Assert.False(start.UseShellExecute);
        }

        [Fact]
        public void EveryPlaceholder_IsReplacedInTheArguments()
        {
            var start = ProgramCommandLine.Build(
                "\"a.exe\" %UID|%CHIPTYPE|%DATETIME|%DATE|%FREEMEM|%LISTAPPS|%COUNTAPPS", Values());

            Assert.NotNull(start);
            var parts = start.Arguments.Split('|');
            Assert.Equal("04AABBCC", parts[0]);
            Assert.Equal("Mifare 1K", parts[1]);
            Assert.Equal(Moment.ToString(CultureInfo.CurrentCulture), parts[2]);
            Assert.Equal(Moment.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture), parts[3]);
            Assert.Equal("4096", parts[4]);
            Assert.Equal("1, 2, 3", parts[5]);
            Assert.Equal("3", parts[6]);
        }

        [Fact]
        public void ADateAndTime_IsNotCutAtTheDate()
        {
            var start = ProgramCommandLine.Build("\"a.exe\" %DATETIME", Values());

            Assert.NotNull(start);
            Assert.DoesNotContain("TIME", start.Arguments, StringComparison.Ordinal);
            Assert.Equal(Moment.ToString(CultureInfo.CurrentCulture), start.Arguments);
        }

        [Fact]
        public void APlaceholderInTheFileName_IsReplacedToo()
        {
            var start = ProgramCommandLine.Build("\"C:\\Out\\$FOLDER\\%UID.bat\"", Values(new Dictionary<string, string> { ["$FOLDER"] = "cards" }));

            Assert.NotNull(start);
            Assert.Equal("C:\\Out\\cards\\04AABBCC.bat", start.FileName);
            Assert.Equal(string.Empty, start.Arguments);
        }

        [Fact]
        public void VariablesAndPlaceholders_MayBeMixed()
        {
            var start = ProgramCommandLine.Build(
                "\"a.exe\" -job $JOB -uid %UID", Values(new Dictionary<string, string> { ["$JOB"] = "J-7" }));

            Assert.NotNull(start);
            Assert.Equal("-job J-7 -uid 04AABBCC", start.Arguments);
        }

        [Fact]
        public void TheLongerVariableName_IsTakenBeforeTheShorterOne()
        {
            var start = ProgramCommandLine.Build(
                "\"a.exe\" $JOBNUMBER $JOB",
                Values(new Dictionary<string, string> { ["$JOB"] = "short", ["$JOBNUMBER"] = "long" }));

            Assert.NotNull(start);
            Assert.Equal("long short", start.Arguments);
        }

        [Fact]
        public void WhatAVariableHolds_IsNeverReplacedAgain()
        {
            var start = ProgramCommandLine.Build(
                "\"a.exe\" $A $B %UID",
                Values(new Dictionary<string, string> { ["$A"] = "%UID", ["$B"] = "$A" }));

            Assert.NotNull(start);
            Assert.Equal("%UID $A 04AABBCC", start.Arguments);
        }

        [Fact]
        public void AVariableName_IsFoundWithoutRegardToCase_AsTheVariablesOfARunAreEverywhere()
        {
            var start = ProgramCommandLine.Build("\"a.exe\" $JOB $Job", Values(new Dictionary<string, string> { ["$job"] = "J-7" }));

            Assert.NotNull(start);
            Assert.Equal("J-7 J-7", start.Arguments);
        }

        [Fact]
        public void TwoSpellingsOfOneName_KeepTheirOwnValues_AndAThirdSpellingTakesTheFirstInOrder()
        {
            // A dictionary that is not case-insensitive can hold both. $JOB comes before $job in the ordinal order.
            var start = ProgramCommandLine.Build(
                "\"a.exe\" $JOB $job $Job",
                Values(new Dictionary<string, string> { ["$job"] = "lower", ["$JOB"] = "upper" }));

            Assert.NotNull(start);
            Assert.Equal("upper lower upper", start.Arguments);
        }

        [Fact]
        public void AVariableThatIsNotNamed_IsLeftAsTyped()
        {
            var start = ProgramCommandLine.Build("\"a.exe\" $OTHER", Values(new Dictionary<string, string> { ["$JOB"] = "x" }));

            Assert.NotNull(start);
            Assert.Equal("$OTHER", start.Arguments);
        }

        [Fact]
        public void ADollarSign_MayStandAnywhereInTheNameOfAVariable_AsTheStartUpArgumentsAcceptIt()
        {
            var start = ProgramCommandLine.Build(
                "\"a.exe\" JOB$ID cost$ $JOB",
                Values(new Dictionary<string, string> { ["JOB$ID"] = "7", ["$"] = "usd", ["$JOB"] = "j" }));

            Assert.NotNull(start);
            Assert.Equal("7 costusd j", start.Arguments);
        }

        [Fact]
        public void AnEntryWithoutADollarSign_IsNotAVariable()
        {
            var start = ProgramCommandLine.Build("\"a.exe\" JOB", Values(new Dictionary<string, string> { ["JOB"] = "x" }));

            Assert.NotNull(start);
            Assert.Equal("JOB", start.Arguments);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("no program here")]
        [InlineData("\"unclosed")]
        [InlineData("\"\" -x")]
        public void ATextWithoutAFileNameInQuotes_IsNoProgram(string? text)
        {
            Assert.Null(ProgramCommandLine.Build(text, Values()));
        }

        [Theory]
        [InlineData("\"tool.exe\"", false)]
        [InlineData("\"run.bat\"", false)]
        [InlineData("\"setup.msi\"", false)]
        [InlineData("\"report.pdf\"", true)]
        [InlineData("\"notes.txt\"", true)]
        public void AProgramIsCreatedDirectly_AndAnythingElseIsOpenedByTheShell(string text, bool shell)
        {
            var start = ProgramCommandLine.Build(text, Values());

            Assert.NotNull(start);
            Assert.Equal(shell, start.UseShellExecute);
        }

        [Fact]
        public void ACardThatIsNotKnown_GivesEmptyValues_AndNoFailure()
        {
            var start = ProgramCommandLine.Build(
                "\"a.exe\" [%UID][%CHIPTYPE][%FREEMEM][%LISTAPPS][%COUNTAPPS]", new ProgramTaskValues { Now = Moment });

            Assert.NotNull(start);
            Assert.Equal("[][][][][]", start.Arguments);
        }

        [Fact]
        public async Task ARun_LeavesTheTextOfTheTaskAlone_AndTheNextCardGetsItsOwnValues()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var starts = new RecordedStarts();
                const string text = "\"a.exe\" -uid %UID -count %COUNTAPPS -job $JOB";
                var task = new CommonTaskViewModel
                {
                    SelectedTaskType = TaskType_CommonTask.ExecuteProgram,
                    ProgramToExecute = text,
                    Args = new Dictionary<string, string> { ["$JOB"] = "J-1" },
                    GenericChip = new GenericChipModel("04000001", CARD_TYPE.Mifare1K),
                    DesfireChip = new MifareDesfireChipModel("04000001", CARD_TYPE.DESFire)
                };

                await task.ExecuteProgramCommand.ExecuteAsync(null);

                Assert.Equal(text, task.ProgramToExecute);
                Assert.Equal(ERROR.NoError, task.CurrentTaskErrorLevel);
                Assert.True(task.IsTaskCompletedSuccessfully);

                task.GenericChip = new GenericChipModel("04000002", CARD_TYPE.Mifare1K);
                task.Args = new Dictionary<string, string> { ["$JOB"] = "J-2" };

                await task.ExecuteProgramCommand.ExecuteAsync(null);

                Assert.Equal(text, task.ProgramToExecute);
                Assert.Collection(
                    starts.Started,
                    first =>
                    {
                        Assert.Equal("a.exe", first.FileName);
                        Assert.Equal("-uid 04000001 -count 0 -job J-1", first.Arguments);
                        Assert.False(first.UseShellExecute);
                    },
                    second => Assert.Equal("-uid 04000002 -count 0 -job J-2", second.Arguments));
            });
        }

        [Fact]
        public async Task ARunWithoutACard_StartsTheProgram_WithEmptyValues()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var starts = new RecordedStarts();
                var task = new CommonTaskViewModel
                {
                    SelectedTaskType = TaskType_CommonTask.ExecuteProgram,
                    ProgramToExecute = "\"a.exe\" [%UID][%CHIPTYPE][%LISTAPPS]"
                };

                await task.ExecuteProgramCommand.ExecuteAsync(null);

                Assert.Equal(ERROR.NoError, task.CurrentTaskErrorLevel);
                Assert.Equal("[][][]", Assert.Single(starts.Started).Arguments);
            });
        }

        [Fact]
        public async Task AReadThatFoundNoCard_GivesEmptyValues_NotTheNameOfNoTag()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var starts = new RecordedStarts();
                var task = new CommonTaskViewModel
                {
                    SelectedTaskType = TaskType_CommonTask.ExecuteProgram,
                    ProgramToExecute = "\"a.exe\" [%UID][%CHIPTYPE]",
                    GenericChip = new GenericChipModel()
                };

                await task.ExecuteProgramCommand.ExecuteAsync(null);

                Assert.Equal("[][]", Assert.Single(starts.Started).Arguments);
            });
        }

        [Fact]
        public async Task ATextWithoutAFileName_StartsNothing_AndEndsAsNotTrue()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var starts = new RecordedStarts();
                var task = new CommonTaskViewModel
                {
                    SelectedTaskType = TaskType_CommonTask.ExecuteProgram,
                    ProgramToExecute = "no program here"
                };

                await task.ExecuteProgramCommand.ExecuteAsync(null);

                Assert.Empty(starts.Started);
                Assert.Equal(ERROR.IsNotTrue, task.CurrentTaskErrorLevel);
                Assert.False(task.IsTaskCompletedSuccessfully);
            });
        }

        [Fact]
        public async Task AProgramThatDoesNotStart_EndsAsNotTrue_WhetherItReportsNoStartOrThrows()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var starts = new RecordedStarts(_ => false);
                var task = new CommonTaskViewModel
                {
                    SelectedTaskType = TaskType_CommonTask.ExecuteProgram,
                    ProgramToExecute = "\"a.exe\""
                };

                await task.ExecuteProgramCommand.ExecuteAsync(null);

                Assert.Equal(ERROR.IsNotTrue, task.CurrentTaskErrorLevel);
                Assert.False(task.IsTaskCompletedSuccessfully);

                CommonTaskViewModel.ProgramStarter = _ => throw new InvalidOperationException("no such program");
                task.CurrentTaskErrorLevel = ERROR.NoError;
                task.IsTaskCompletedSuccessfully = true;

                await task.ExecuteProgramCommand.ExecuteAsync(null);

                Assert.Equal(ERROR.IsNotTrue, task.CurrentTaskErrorLevel);
                Assert.False(task.IsTaskCompletedSuccessfully);
            });
        }

        [Fact]
        public async Task TheExitRequest_StillClosesTheApplication_AndStartsNothing()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var starts = new RecordedStarts();
                var previous = CommonTaskViewModel.ApplicationExit;
                var codes = new List<int>();
                CommonTaskViewModel.ApplicationExit = codes.Add;

                try
                {
                    var task = new CommonTaskViewModel
                    {
                        SelectedTaskType = TaskType_CommonTask.ExecuteProgram,
                        ProgramToExecute = "%EXIT"
                    };

                    await task.ExecuteProgramCommand.ExecuteAsync(null);

                    Assert.Equal(new[] { 0 }, codes);
                    Assert.Empty(starts.Started);
                }
                finally
                {
                    CommonTaskViewModel.ApplicationExit = previous;
                }
            });
        }

        // Replaces the call that starts a program, so that a test starts nothing.
        private sealed class RecordedStarts : IDisposable
        {
            private readonly Func<ProcessStartInfo, bool> _previous = CommonTaskViewModel.ProgramStarter;

            public RecordedStarts(Func<ProcessStartInfo, bool>? answer = null)
            {
                CommonTaskViewModel.ProgramStarter = info =>
                {
                    Started.Add(info);
                    return answer?.Invoke(info) ?? true;
                };
            }

            public List<ProcessStartInfo> Started { get; } = new List<ProcessStartInfo>();

            public void Dispose() => CommonTaskViewModel.ProgramStarter = _previous;
        }
    }
}
