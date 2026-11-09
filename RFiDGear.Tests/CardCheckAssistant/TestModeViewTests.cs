using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

using RFiDGear.Extensions.CardCheckAssistant.Localization;
using RFiDGear.Extensions.CardCheckAssistant.Services;
using RFiDGear.Extensions.CardCheckAssistant.ViewModels;
using RFiDGear.Extensions.CardCheckAssistant.Views;

using Xunit;

using static RFiDGear.Tests.CardCheckAssistant.ViewModelTestSupport;

using Keys = RFiDGear.Extensions.CardCheckAssistant.Localization.ExtensionStrings.Keys;

namespace RFiDGear.Tests.CardCheckAssistant
{
    /// <summary>
    /// The test mode as the operator sees it: the notice and the drop-down of jobs are there only in test mode, picking a
    /// job fills in its numbers, the drop-down is switched off while a run is going, a language switch is followed, the
    /// page still fits at the smallest size it declares, and no binding of it is wrong.
    /// </summary>
    [Collection("WpfBindingTrace")]
    public class TestModeViewTests
    {
        private sealed class FakeSettings : IAssistantSettings
        {
            public AssistantDefaults Defaults { get; set; } = AssistantDefaults.None;

            public void RememberProjectFolder(string folder)
            {
                // Not needed here.
            }

            public AssistantDefaults Load() => Defaults;
        }

        private sealed class FixedJobs : IJobSource
        {
            public List<JobEntry> Jobs { get; set; } = new List<JobEntry>
            {
                new JobEntry("A", "J-1", "1", "Customer A", "Created"),
                new JobEntry("B", "J-2", "2", "Customer B", "InProgress"),
            };

            public Task<IReadOnlyList<JobEntry>> ListAsync(CancellationToken cancellationToken) =>
                Task.FromResult<IReadOnlyList<JobEntry>>(Jobs.ToList());
        }

        private sealed class NoReports : ITestReportTarget
        {
            public string Prepare(JobEntry job) => job == null ? throw new ArgumentNullException(nameof(job)) : string.Empty;
        }

        // Shows the page in a window and hands it to the check. A size makes the page exactly that big, as the smallest size
        // it declares, instead of the roomy window the other checks use.
        private static async Task Show(
            bool testMode,
            Func<CardCheckAssistantView, CardCheckAssistantViewModel, FakeExtensionHost, Task> check,
            FixedJobs? jobs = null,
            Size? pageSize = null)
        {
            var listener = BindingErrorListener.Start();
            try
            {
                await StaTestRunner.RunOnStaThreadAsync(async () =>
                {
                    var host = new FakeExtensionHost("en-US");
                    var settings = new FakeSettings { Defaults = new AssistantDefaults(string.Empty, string.Empty) { UseStubJobs = testMode } };
                    var viewModel = new CardCheckAssistantViewModel(
                        host, Dispatcher.CurrentDispatcher, new FakeFileDialogService(), null, settings, null, null, jobs ?? new FixedJobs(), new NoReports());
                    var view = new CardCheckAssistantView(viewModel);
                    var window = new Window { Content = view, Width = 900, Height = 700 };
                    if (pageSize is { } size)
                    {
                        view.Width = size.Width;
                        view.Height = size.Height;
                        window.SizeToContent = SizeToContent.WidthAndHeight;
                    }

                    window.Show();
                    try
                    {
                        await viewModel.Open();
                        await Flush();
                        window.UpdateLayout();

                        await check(view, viewModel, host);
                    }
                    finally
                    {
                        host.ExecutionHost.SetRunningQuietly(false);
                        window.Close();
                    }
                });
            }
            finally
            {
                listener.Stop();
            }

            Assert.Empty(listener.Messages);
        }

        private static T Named<T>(FrameworkElement root, string name)
            where T : class
        {
            var found = root.FindName(name);
            Assert.NotNull(found);

            return Assert.IsAssignableFrom<T>(found);
        }

        [Fact]
        public async Task WithTheOptionOff_ThereIsNeitherANoticeNorADropDown()
        {
            await Show(testMode: false, (view, _, _) =>
            {
                Assert.Equal(Visibility.Collapsed, Named<Border>(view, "TestModeNotice").Visibility);
                Assert.Equal(Visibility.Collapsed, Named<StackPanel>(view, "JobsPanel").Visibility);
                Assert.Empty(Named<ComboBox>(view, "JobList").Items);

                return Task.CompletedTask;
            });
        }

        [Fact]
        public async Task WithTheOptionOn_TheNoticeAndTheJobsAreShown_InTheLanguageOfThePage()
        {
            await Show(testMode: true, async (view, viewModel, host) =>
            {
                var notice = Named<Border>(view, "TestModeNotice");
                Assert.Equal(Visibility.Visible, notice.Visibility);
                Assert.Equal(viewModel.Strings[Keys.TestModeNotice], ((TextBlock)notice.Child).Text);
                Assert.Equal(Visibility.Visible, Named<StackPanel>(view, "JobsPanel").Visibility);
                Assert.Equal(Visibility.Collapsed, Named<TextBlock>(view, "NoJobsText").Visibility);

                var list = Named<ComboBox>(view, "JobList");
                Assert.Equal(new[] { "J-1", "J-2" }, list.Items.Cast<JobEntry>().Select(job => job.JobNumber));
                Assert.Equal(viewModel.Strings[Keys.JobsLabel], System.Windows.Automation.AutomationProperties.GetName(list));

                // The language of RFiDGear changes: the notice follows.
                host.Context.ChangeCulture("de-DE");
                await Flush();
                Assert.Equal(ExtensionStrings.Get(Keys.TestModeNotice, "de-DE"), ((TextBlock)notice.Child).Text);
                Assert.NotEqual(viewModel.Strings[Keys.TestModeNotice], ExtensionStrings.Get(Keys.TestModeNotice, "en-US"));
            });
        }

        [Fact]
        public async Task PickingAJobInTheDropDown_PicksTheJob_AndFillsInItsNumbers()
        {
            await Show(testMode: true, (view, viewModel, _) =>
            {
                var list = Named<ComboBox>(view, "JobList");

                list.SelectedItem = list.Items[1];

                Assert.Equal("B", viewModel.SelectedJob?.Id);
                Assert.Equal(("J-2", "2"), (viewModel.JobNumber, viewModel.ChipNumber));
                Assert.Equal("J-2", Named<TextBox>(view, "JobBox").Text);
                Assert.Equal("2", Named<TextBox>(view, "ChipBox").Text);

                return Task.CompletedTask;
            });
        }

        [Fact]
        public async Task ADropDownWithoutJobs_SaysSo()
        {
            await Show(testMode: true, (view, viewModel, _) =>
            {
                Assert.Equal(Visibility.Visible, Named<TextBlock>(view, "NoJobsText").Visibility);
                Assert.Equal(viewModel.Strings[Keys.NoJobsText], Named<TextBlock>(view, "NoJobsText").Text);

                return Task.CompletedTask;
            }, new FixedJobs { Jobs = new List<JobEntry>() });
        }

        [Fact]
        public async Task WhileARunIsGoing_TheDropDownIsSwitchedOff_LikeTheOtherFields()
        {
            await Show(testMode: true, async (view, _, host) =>
            {
                var list = Named<ComboBox>(view, "JobList");
                Assert.True(list.IsEnabled);

                host.ExecutionHost.SetRunning(true);
                await Flush();
                Assert.False(list.IsEnabled);

                host.ExecutionHost.SetRunningQuietly(false);
                host.ExecutionHost.SetRunning(false);
                await Flush();
                Assert.True(list.IsEnabled);
            });
        }

        [Fact]
        public async Task AtTheSmallestSizeThePageDeclares_TheTestModeTakesLittleRoomFromTheResult()
        {
            // The page has no scroll bar and the results are listed below the run button, so what the test mode adds above
            // it comes straight out of the room the results have. The notice and a line for the jobs are small, the list
            // of five jobs that was tried first was not.
            var smallest = new Size(620, 460);
            double without = 0;
            double with = 0;
            await Show(testMode: false, (view, _, _) =>
            {
                without = BottomOfTheRunButton(view);

                return Task.CompletedTask;
            }, pageSize: smallest);
            await Show(testMode: true, (view, _, _) =>
            {
                with = BottomOfTheRunButton(view);

                return Task.CompletedTask;
            }, pageSize: smallest);

            Assert.InRange(with - without, 0, 110);
        }

        private static double BottomOfTheRunButton(CardCheckAssistantView view)
        {
            var run = Named<Button>(view, "RunButton");

            return run.TransformToAncestor(view).Transform(new Point(0, run.ActualHeight)).Y;
        }
    }
}
