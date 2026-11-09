using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

using RFiDGear.Extensions.CardCheckAssistant;
using RFiDGear.Extensions.CardCheckAssistant.Localization;
using RFiDGear.Extensions.CardCheckAssistant.Services;
using RFiDGear.Extensions.CardCheckAssistant.ViewModels;
using RFiDGear.Extensions.CardCheckAssistant.Views;

using Xunit;

using static RFiDGear.Tests.CardCheckAssistant.ViewModelTestSupport;

namespace RFiDGear.Tests.CardCheckAssistant
{
    /// <summary>
    /// The test mode of the page: with the setting off the page is as it was, with it on the page offers the jobs of the
    /// source, picking one fills in its numbers and proposes the report in the Test folder, and the mode follows the
    /// setting when it is saved. The source and the report target are fakes here; the real ones are tested apart.
    /// </summary>
    public class TestModeViewModelTests
    {
        private sealed class FakeSettings : IAssistantSettings
        {
            private int _loads;

            public AssistantDefaults Defaults { get; set; } = AssistantDefaults.None;

            /// <summary>Makes the first read wait until the test lets it go; it answers with what was set when it was asked.</summary>
            public ManualResetEventSlim? HoldFirstLoad { get; set; }

            public void RememberProjectFolder(string folder)
            {
                // Not needed here.
            }

            public AssistantDefaults Load()
            {
                var asked = Defaults;
                if (Interlocked.Increment(ref _loads) == 1)
                {
                    HoldFirstLoad?.Wait(TimeSpan.FromSeconds(30));
                }

                return asked;
            }
        }

        private sealed class FakeSettingsDialog : ISettingsDialog
        {
            public Action? WhileOpen { get; set; }

            public bool Saved { get; set; } = true;

            public bool Show(LocalizedStrings strings)
            {
                WhileOpen?.Invoke();

                return Saved;
            }
        }

        private sealed class FakeJobs : IJobSource
        {
            private int _lists;
            private int _threadId = -1;

            public List<JobEntry> Jobs { get; set; } = new List<JobEntry> { Job("A", "J-1", "1"), Job("B", "J-2", "2") };

            public Exception? Throws { get; set; }

            /// <summary>Makes the next lists wait until this task is done.</summary>
            public Task? Gate { get; set; }

            public int Lists => Volatile.Read(ref _lists);

            public int ThreadId => Volatile.Read(ref _threadId);

            public Task<IReadOnlyList<JobEntry>> ListAsync(CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref _lists);
                Volatile.Write(ref _threadId, Environment.CurrentManagedThreadId);

                if (Throws != null)
                {
                    throw Throws;
                }

                var gate = Gate;

                return gate == null ? Task.FromResult(Copies()) : ListWhenAsync(gate);
            }

            // Every list has entries of its own, as a real source makes them: a list is not told from another by reference.
            private IReadOnlyList<JobEntry> Copies() => Jobs.Select(job => job with { }).ToList();

            private async Task<IReadOnlyList<JobEntry>> ListWhenAsync(Task gate)
            {
                // A page that asks again while the test still holds the gate would wait for ever; this ends it as a failure.
                await gate.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

                return Copies();
            }
        }

        private sealed class FakeTestReports : ITestReportTarget
        {
            private readonly List<string> _prepared = new List<string>();
            private int _threadId = -1;

            public FakeTestReports(string root)
            {
                Root = root;
            }

            public string Root { get; }

            /// <summary>Makes <see cref="Prepare"/> of this job wait until the test lets it go.</summary>
            public (string JobId, TaskCompletionSource<bool> Release)? Hold { get; set; }

            /// <summary>What to answer instead of the path in the Test folder, if anything.</summary>
            public string? Answer { get; set; }

            public IReadOnlyList<string> Prepared
            {
                get
                {
                    lock (_prepared)
                    {
                        return _prepared.ToList();
                    }
                }
            }

            public int ThreadId => Volatile.Read(ref _threadId);

            public string PathOf(JobEntry job) => Path.Combine(Root, job.JobNumber + "-" + job.ChipNumber + ".pdf");

            public string Prepare(JobEntry job)
            {
                Volatile.Write(ref _threadId, Environment.CurrentManagedThreadId);
                lock (_prepared)
                {
                    _prepared.Add(job.Id);
                }

                if (Hold is { } hold && hold.JobId == job.Id)
                {
                    hold.Release.Task.Wait(TimeSpan.FromSeconds(30));
                }

                return Answer ?? PathOf(job);
            }
        }

        private static JobEntry Job(string id, string job, string chip) => new JobEntry(id, job, chip, "Customer " + id, "Created");

        private static CardCheckAssistantViewModel Create(
            FakeExtensionHost host,
            FakeSettings settings,
            FakeJobs jobs,
            FakeTestReports reports,
            FakeSettingsDialog? dialog = null) =>
            new CardCheckAssistantViewModel(
                host, Dispatcher.CurrentDispatcher, new FakeFileDialogService(), null, settings, null, dialog, jobs, reports);

        private static FakeSettings TestModeOn() => new FakeSettings { Defaults = new AssistantDefaults(string.Empty, string.Empty) { UseStubJobs = true } };

        [Fact]
        public async Task WithTheOptionOff_ThePageOffersNoJobs_AndNeverAsksTheSource()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var jobs = new FakeJobs();
                var reports = new FakeTestReports(Path.GetTempPath());
                using var viewModel = Create(new FakeExtensionHost(), new FakeSettings(), jobs, reports);

                await viewModel.Open();

                Assert.False(viewModel.IsTestMode);
                Assert.Empty(viewModel.Jobs);
                Assert.False(viewModel.HasNoJobs);
                Assert.Null(viewModel.SelectedJob);
                Assert.Equal(0, jobs.Lists);
                Assert.Empty(reports.Prepared);
            });
        }

        [Fact]
        public async Task WithTheOptionOn_TheJobsAreListedOffTheUiThread_InTheOrderTheSourceGaveThem()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var jobs = new FakeJobs();
                using var viewModel = Create(new FakeExtensionHost(), TestModeOn(), jobs, new FakeTestReports(Path.GetTempPath()));

                await viewModel.Open();

                Assert.True(viewModel.IsTestMode);
                Assert.Equal(jobs.Jobs, viewModel.Jobs);
                Assert.False(viewModel.HasNoJobs);
                Assert.NotEqual(Environment.CurrentManagedThreadId, jobs.ThreadId);
            });
        }

        [Fact]
        public async Task ASourceWithoutJobs_GivesAnEmptyList_ThatSaysSo()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var jobs = new FakeJobs { Jobs = new List<JobEntry>() };
                using var viewModel = Create(new FakeExtensionHost(), TestModeOn(), jobs, new FakeTestReports(Path.GetTempPath()));

                await viewModel.Open();

                Assert.True(viewModel.IsTestMode);
                Assert.True(viewModel.HasNoJobs);
            });
        }

        [Fact]
        public async Task ASourceThatThrows_DoesNotStopThePage_ItStillPrefillsTheProject()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var settings = new FakeSettings { Defaults = new AssistantDefaults("default.rfPrj", string.Empty) { UseStubJobs = true } };
                var jobs = new FakeJobs { Throws = new InvalidOperationException("the source is broken") };
                using var viewModel = Create(new FakeExtensionHost(), settings, jobs, new FakeTestReports(Path.GetTempPath()));

                await viewModel.Open();

                Assert.Equal("default.rfPrj", viewModel.ProjectPath);
                Assert.False(viewModel.IsTestMode);
                Assert.Empty(viewModel.Jobs);
            });
        }

        [Fact]
        public async Task PickingAJob_FillsInItsNumbers_AndProposesTheReportInTheTestFolder_MadeOffTheUiThread()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var jobs = new FakeJobs();
                var reports = new FakeTestReports(Path.Combine(Path.GetTempPath(), "Test"));
                using var viewModel = Create(new FakeExtensionHost(), TestModeOn(), jobs, reports);
                await viewModel.Open();

                viewModel.SelectedJob = jobs.Jobs[1];
                await viewModel.PendingReportProposal;

                Assert.Equal(("J-2", "2"), (viewModel.JobNumber, viewModel.ChipNumber));
                Assert.Equal(reports.PathOf(jobs.Jobs[1]), viewModel.ReportTargetPath);
                Assert.Equal(new[] { "B" }, reports.Prepared);
                Assert.NotEqual(Environment.CurrentManagedThreadId, reports.ThreadId);
            });
        }

        [Fact]
        public async Task AProposal_ReplacesTheEarlierProposal_ButNeverWhatTheOperatorTyped()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var jobs = new FakeJobs();
                var reports = new FakeTestReports(Path.Combine(Path.GetTempPath(), "Test"));
                using var viewModel = Create(new FakeExtensionHost(), TestModeOn(), jobs, reports);
                await viewModel.Open();

                // The second pick replaces the page's own first proposal.
                viewModel.SelectedJob = jobs.Jobs[0];
                await viewModel.PendingReportProposal;
                viewModel.SelectedJob = jobs.Jobs[1];
                await viewModel.PendingReportProposal;
                Assert.Equal(reports.PathOf(jobs.Jobs[1]), viewModel.ReportTargetPath);

                // Text of the operator's own is left alone.
                var typed = Path.Combine(Path.GetTempPath(), "mine.pdf");
                viewModel.ReportTargetPath = typed;
                viewModel.SelectedJob = jobs.Jobs[0];
                await viewModel.PendingReportProposal;

                Assert.Equal(typed, viewModel.ReportTargetPath);
                Assert.Equal(("J-1", "1"), (viewModel.JobNumber, viewModel.ChipNumber));
            });
        }

        [Fact]
        public async Task AProposalForAJobThatIsNoLongerPicked_IsDropped()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var jobs = new FakeJobs();
                var release = NewGate();
                var reports = new FakeTestReports(Path.Combine(Path.GetTempPath(), "Test")) { Hold = ("A", release) };
                using var viewModel = Create(new FakeExtensionHost(), TestModeOn(), jobs, reports);
                await viewModel.Open();

                viewModel.SelectedJob = jobs.Jobs[0];
                var first = viewModel.PendingReportProposal;
                await WaitUntil(() => reports.Prepared.Contains("A"), "the first folder being made");
                viewModel.SelectedJob = jobs.Jobs[1];
                var second = viewModel.PendingReportProposal;
                await second;

                release.SetResult(true);
                await first;

                Assert.Equal(reports.PathOf(jobs.Jobs[1]), viewModel.ReportTargetPath);
            });
        }

        [Fact]
        public async Task AProposalForTheJobThatIsStillPicked_SurvivesANewList()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var jobs = new FakeJobs();
                var release = NewGate();
                var reports = new FakeTestReports(Path.Combine(Path.GetTempPath(), "Test")) { Hold = ("A", release) };
                using var viewModel = Create(new FakeExtensionHost(), TestModeOn(), jobs, reports, new FakeSettingsDialog());
                await viewModel.Open();
                viewModel.SelectedJob = viewModel.Jobs[0];
                var proposal = viewModel.PendingReportProposal;
                await WaitUntil(() => reports.Prepared.Contains("A"), "the folder being made");

                // The list is made again while the folder is still being made: the entries are new ones, the job is the same.
                await viewModel.OpenSettingsCommand.ExecuteAsync();
                release.SetResult(true);
                await proposal;

                Assert.Equal(reports.PathOf(jobs.Jobs[0]), viewModel.ReportTargetPath);
            });
        }

        [Fact]
        public async Task AProposalThatArrivesAfterARunWasStarted_IsDropped_ForTheRunHasCheckedTheField()
        {
            using var area = new TempArea();
            using var checker = new BlockingPathChecker();
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var host = new FakeExtensionHost();
                var jobs = new FakeJobs();
                var release = NewGate();
                var reports = new FakeTestReports(area.Directory) { Hold = ("A", release) };
                using var viewModel = new CardCheckAssistantViewModel(
                    host, Dispatcher.CurrentDispatcher, new FakeFileDialogService(), checker, TestModeOn(), null, null, jobs, reports);
                await viewModel.Open();
                viewModel.ProjectPath = area.ProjectPath;
                viewModel.SelectedJob = viewModel.Jobs[0];
                var proposal = viewModel.PendingReportProposal;
                await WaitUntil(() => reports.Prepared.Contains("A"), "the folder being made");

                // The run starts and checks the (empty) report field; only then the proposal is ready.
                var run = viewModel.RunCommand.ExecuteAsync();
                await WaitUntil(() => checker.Started, "the checks of the run");
                release.SetResult(true);
                await proposal;
                Assert.Equal(string.Empty, viewModel.ReportTargetPath);

                checker.Release();
                await run;

                var request = Assert.Single(host.ExecutionHost.Requests);
                Assert.False(request.Variables.ContainsKey("REPORTTARGETPATH"));
            });
        }

        [Fact]
        public async Task SwitchingTheModeOff_TakesBackTheNumbersOfThePickedJob_ButNotOnesTheOperatorChanged()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var settings = TestModeOn();
                var dialog = new FakeSettingsDialog { WhileOpen = () => settings.Defaults = settings.Defaults with { UseStubJobs = false } };
                using var viewModel = Create(new FakeExtensionHost(), settings, new FakeJobs(), new FakeTestReports(Path.GetTempPath()), dialog);
                await viewModel.Open();
                viewModel.SelectedJob = viewModel.Jobs[1];
                viewModel.JobNumber = "changed by the operator";

                await viewModel.OpenSettingsCommand.ExecuteAsync();

                // The job number was changed since, so it is the operator's; the chip number is still the job's.
                Assert.Equal("changed by the operator", viewModel.JobNumber);
                Assert.Equal(string.Empty, viewModel.ChipNumber);
            });
        }

        [Fact]
        public async Task SwitchingTheModeOff_LeavesNoStubNumbersForTheNextRealRun()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var settings = TestModeOn();
                var dialog = new FakeSettingsDialog { WhileOpen = () => settings.Defaults = settings.Defaults with { UseStubJobs = false } };
                using var viewModel = Create(new FakeExtensionHost(), settings, new FakeJobs(), new FakeTestReports(Path.GetTempPath()), dialog);
                await viewModel.Open();
                viewModel.SelectedJob = viewModel.Jobs[1];
                Assert.Equal(("J-2", "2"), (viewModel.JobNumber, viewModel.ChipNumber));

                await viewModel.OpenSettingsCommand.ExecuteAsync();

                Assert.Equal((string.Empty, string.Empty), (viewModel.JobNumber, viewModel.ChipNumber));
            });
        }

        [Fact]
        public async Task AnEarlierListThatIsStillWaiting_DoesNotUndoALaterSetting()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var settings = TestModeOn();
                var gate = NewGate();
                var jobs = new FakeJobs { Gate = gate.Task };
                var dialog = new FakeSettingsDialog { WhileOpen = () => settings.Defaults = settings.Defaults with { UseStubJobs = false } };
                using var viewModel = Create(new FakeExtensionHost(), settings, jobs, new FakeTestReports(Path.GetTempPath()), dialog);

                // The first reading of the settings is waiting for its list when the operator switches the mode off.
                var open = viewModel.Open();
                await WaitUntil(() => jobs.Lists > 0, "the list being asked for");
                await viewModel.OpenSettingsCommand.ExecuteAsync();
                Assert.False(viewModel.IsTestMode);

                gate.SetResult(true);
                await open;

                Assert.False(viewModel.IsTestMode);
                Assert.Empty(viewModel.Jobs);
            });
        }

        [Fact]
        public async Task AnEarlierReadOfTheSettings_ThatIsStillWaiting_DoesNotUndoALaterOne()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var settings = TestModeOn();
                using var hold = new ManualResetEventSlim(false);
                settings.HoldFirstLoad = hold;
                var dialog = new FakeSettingsDialog { WhileOpen = () => settings.Defaults = settings.Defaults with { UseStubJobs = false } };
                using var viewModel = Create(new FakeExtensionHost(), settings, new FakeJobs(), new FakeTestReports(Path.GetTempPath()), dialog);

                // The first read was asked while the mode was on and is slow; the mode is switched off and read again meanwhile.
                var open = viewModel.Open();
                await viewModel.OpenSettingsCommand.ExecuteAsync();
                hold.Set();
                await open;

                Assert.False(viewModel.IsTestMode);
                Assert.Empty(viewModel.Jobs);
            });
        }

        [Fact]
        public async Task AnEarlierReadThatSaysOff_ThatIsStillWaiting_DoesNotSwitchOffALaterOneThatSaysOn()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var settings = new FakeSettings();
                using var hold = new ManualResetEventSlim(false);
                settings.HoldFirstLoad = hold;
                var dialog = new FakeSettingsDialog { WhileOpen = () => settings.Defaults = settings.Defaults with { UseStubJobs = true } };
                using var viewModel = Create(new FakeExtensionHost(), settings, new FakeJobs(), new FakeTestReports(Path.GetTempPath()), dialog);

                // The first read was asked while the mode was off and is slow; the mode is switched on and read again meanwhile.
                var open = viewModel.Open();
                await viewModel.OpenSettingsCommand.ExecuteAsync();
                Assert.True(viewModel.IsTestMode);
                hold.Set();
                await open;

                Assert.True(viewModel.IsTestMode);
                Assert.Equal(2, viewModel.Jobs.Count);
            });
        }

        [Fact]
        public async Task TheRealPage_IsGivenTheJobsAndTheTestReportsOfTheServices()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var jobs = new FakeJobs();
                var reports = new FakeTestReports(Path.GetTempPath());
                var services = new AssistantServices(TestModeOn(), AssistantServices.None.ImportOffer, AssistantServices.None.Editor, jobs, reports);

                var page = CardCheckAssistantExtension.CreatePage(new FakeExtensionHost(), viewModel => new CardCheckAssistantView(viewModel), _ => services);
                try
                {
                    var viewModel = ((CardCheckAssistantView)page).ViewModel;
                    await viewModel.Open();
                    viewModel.SelectedJob = viewModel.Jobs[0];
                    await viewModel.PendingReportProposal;

                    Assert.Equal(jobs.Jobs, viewModel.Jobs);
                    Assert.Equal(reports.PathOf(jobs.Jobs[0]), viewModel.ReportTargetPath);
                }
                finally
                {
                    ((IDisposable)page).Dispose();
                }
            });
        }

        [Fact]
        public async Task WhenTheFolderCannotBeMade_TheReportFieldIsLeftAlone()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var jobs = new FakeJobs();
                var reports = new FakeTestReports(Path.GetTempPath()) { Answer = string.Empty };
                using var viewModel = Create(new FakeExtensionHost(), TestModeOn(), jobs, reports);
                await viewModel.Open();

                viewModel.SelectedJob = jobs.Jobs[0];
                await viewModel.PendingReportProposal;

                Assert.Equal(string.Empty, viewModel.ReportTargetPath);
                Assert.Equal(("J-1", "1"), (viewModel.JobNumber, viewModel.ChipNumber));
            });
        }

        [Fact]
        public async Task TheMode_FollowsTheSettings_WhenTheyAreSaved()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var settings = new FakeSettings();
                var jobs = new FakeJobs();
                var reports = new FakeTestReports(Path.Combine(Path.GetTempPath(), "Test"));
                var dialog = new FakeSettingsDialog();
                using var viewModel = Create(new FakeExtensionHost(), settings, jobs, reports, dialog);
                await viewModel.Open();
                Assert.False(viewModel.IsTestMode);

                // Switched on in the settings window: the jobs appear.
                dialog.WhileOpen = () => settings.Defaults = settings.Defaults with { UseStubJobs = true };
                await viewModel.OpenSettingsCommand.ExecuteAsync();
                Assert.True(viewModel.IsTestMode);
                Assert.Equal(2, viewModel.Jobs.Count);

                // A job picked and proposed, then the mode is switched off: the list goes, and so does the proposal.
                viewModel.SelectedJob = viewModel.Jobs[0];
                await viewModel.PendingReportProposal;
                Assert.NotEqual(string.Empty, viewModel.ReportTargetPath);
                dialog.WhileOpen = () => settings.Defaults = settings.Defaults with { UseStubJobs = false };
                await viewModel.OpenSettingsCommand.ExecuteAsync();

                Assert.False(viewModel.IsTestMode);
                Assert.Empty(viewModel.Jobs);
                Assert.Null(viewModel.SelectedJob);
                Assert.Equal(string.Empty, viewModel.ReportTargetPath);
            });
        }

        [Fact]
        public async Task SwitchingTheModeOff_KeepsAReportFileTheOperatorTyped()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var settings = TestModeOn();
                var dialog = new FakeSettingsDialog();
                using var viewModel = Create(new FakeExtensionHost(), settings, new FakeJobs(), new FakeTestReports(Path.GetTempPath()), dialog);
                await viewModel.Open();
                var typed = Path.Combine(Path.GetTempPath(), "mine.pdf");
                viewModel.ReportTargetPath = typed;

                dialog.WhileOpen = () => settings.Defaults = settings.Defaults with { UseStubJobs = false };
                await viewModel.OpenSettingsCommand.ExecuteAsync();

                Assert.False(viewModel.IsTestMode);
                Assert.Equal(typed, viewModel.ReportTargetPath);
            });
        }

        [Fact]
        public async Task ANewList_KeepsThePickedJob_AndDoesNotFillInTheNumbersAgain()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var settings = TestModeOn();
                var jobs = new FakeJobs();
                var reports = new FakeTestReports(Path.Combine(Path.GetTempPath(), "Test"));
                var dialog = new FakeSettingsDialog();
                using var viewModel = Create(new FakeExtensionHost(), settings, jobs, reports, dialog);
                await viewModel.Open();
                viewModel.SelectedJob = jobs.Jobs[0];
                await viewModel.PendingReportProposal;
                viewModel.JobNumber = "changed by the operator";

                await viewModel.OpenSettingsCommand.ExecuteAsync();

                Assert.Equal(2, jobs.Lists);
                Assert.Equal(jobs.Jobs[0], viewModel.SelectedJob);
                Assert.Equal("changed by the operator", viewModel.JobNumber);
                Assert.Equal(new[] { "A" }, reports.Prepared);
            });
        }

        [Fact]
        public async Task ARun_SendsTheNumbersOfThePickedJob_AndTheReportInTheTestFolder()
        {
            using var area = new TempArea();
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var host = new FakeExtensionHost();
                var jobs = new FakeJobs();
                var reports = new FakeTestReports(area.Directory);
                using var viewModel = Create(host, TestModeOn(), jobs, reports);
                await viewModel.Open();
                viewModel.ProjectPath = area.ProjectPath;
                viewModel.SelectedJob = jobs.Jobs[1];
                await viewModel.PendingReportProposal;

                await viewModel.RunCommand.ExecuteAsync();

                var request = Assert.Single(host.ExecutionHost.Requests);
                Assert.Equal("J-2", request.Variables["$JOBNUMBER"]);
                Assert.Equal("2", request.Variables["$CHIPNUMBER"]);
                Assert.Equal(reports.PathOf(jobs.Jobs[1]), request.Variables["REPORTTARGETPATH"]);
                Assert.False(viewModel.HasReportTargetError);
            });
        }

        [Fact]
        public async Task WhenTheWindowIsDisposed_TheJobsAreDropped()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = Create(new FakeExtensionHost(), TestModeOn(), new FakeJobs(), new FakeTestReports(Path.GetTempPath()));
                await viewModel.Open();
                Assert.NotEmpty(viewModel.Jobs);

                viewModel.Dispose();

                Assert.Empty(viewModel.Jobs);
            });
        }
    }
}
