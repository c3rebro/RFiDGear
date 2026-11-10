using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

using RFiDGear.Contracts;
using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Extensions.CardCheckAssistant.Localization;
using RFiDGear.Extensions.CardCheckAssistant.ViewModels;
using RFiDGear.Extensions.CardCheckAssistant.Views;
using RFiDGear.Extensions.CardCheckAssistant.Workflow;

using Xunit;

using static RFiDGear.Tests.CardCheckAssistant.ViewModelTestSupport;

using Keys = RFiDGear.Extensions.CardCheckAssistant.Localization.ExtensionStrings.Keys;

namespace RFiDGear.Tests.CardCheckAssistant
{
    [Collection("WpfBindingTrace")]
    public class GuidedCheckViewTests
    {
        private static readonly Size Smallest = new Size(620, 490);

        private static readonly string[] Sections =
        {
            "StepOrders", "StepPrepare", "StepReplace", "StepCard", "StepCheck", "StepVerdict", "StepFinish", "StepEnd",
        };

        private sealed class Scene
        {
            public Scene(GuidedCheckView view, GuidedCheckViewModel viewModel, ScriptedWorkflow workflow, FakeExtensionHost host, Window window)
            {
                View = view;
                Vm = viewModel;
                Workflow = workflow;
                Host = host;
                Window = window;
            }

            public GuidedCheckView View { get; }

            public GuidedCheckViewModel Vm { get; }

            public ScriptedWorkflow Workflow { get; }

            public FakeExtensionHost Host { get; }

            public Window Window { get; }

            public async Task PushAsync(WorkflowState state, Func<WorkflowSnapshot, WorkflowSnapshot>? edit = null, bool busy = false)
            {
                Workflow.Push(state, edit, busy);
                await Flush();
                Window.UpdateLayout();
            }

            public T Named<T>(string name)
                where T : class
            {
                var found = View.FindName(name);
                Assert.NotNull(found);

                return Assert.IsAssignableFrom<T>(found);
            }

            public string Text(string name) => Named<TextBlock>(name).Text;

            public bool Shown(string name) => Named<FrameworkElement>(name).IsVisible;

            public Button Button(string name) => Named<Button>(name);
        }

        // Shows the page in a window the size of its smallest layout and hands it to the check; any binding that
        // cannot be resolved fails the test.
        private static async Task Show(
            Func<Scene, Task> check,
            WorkflowState state = WorkflowState.Idle,
            string culture = "en-US",
            Size? pageSize = null,
            bool open = true)
        {
            var listener = BindingErrorListener.Start();
            try
            {
                await StaTestRunner.RunOnStaThreadAsync(async () =>
                {
                    var host = new FakeExtensionHost(culture);
                    var workflow = new ScriptedWorkflow(state);
                    var viewModel = new GuidedCheckViewModel(
                        host,
                        Dispatcher.CurrentDispatcher,
                        workflow,
                        new StubSettingsDialog(),
                        new StubImportOffer(),
                        new FakeCloseConfirmation(),
                        new ManualTimer().Start);
                    var view = new GuidedCheckView(viewModel);
                    var size = pageSize ?? Smallest;
                    view.Width = size.Width;
                    view.Height = size.Height;
                    var window = new Window { Content = view, SizeToContent = SizeToContent.WidthAndHeight };

                    window.Show();
                    try
                    {
                        if (open)
                        {
                            await viewModel.Open();
                        }

                        await Flush();
                        window.UpdateLayout();

                        await check(new Scene(view, viewModel, workflow, host, window)).WaitAsync(TimeSpan.FromSeconds(30));
                    }
                    finally
                    {
                        view.Dispose();
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

        private static IEnumerable<Button> VisibleButtons(DependencyObject root)
        {
            var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                if (child is Button { IsVisible: true } button)
                {
                    yield return button;
                }

                foreach (var inner in VisibleButtons(child))
                {
                    yield return inner;
                }
            }
        }

        private static void AssertInside(FrameworkElement view, FrameworkElement element, string what)
        {
            var bounds = element.TransformToAncestor(view).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
            var page = new Rect(-0.5, -0.5, view.ActualWidth + 1, view.ActualHeight + 1);

            Assert.True(page.Contains(bounds), what + " lies outside the page: " + bounds + " in " + page);
        }

        // The contrast ratio of two colours as WCAG defines it.
        private static double Contrast(System.Windows.Media.Color a, System.Windows.Media.Color b)
        {
            static double Channel(byte value)
            {
                var c = value / 255.0;

                return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
            }

            static double Luminance(System.Windows.Media.Color color) =>
                (0.2126 * Channel(color.R)) + (0.7152 * Channel(color.G)) + (0.0722 * Channel(color.B));

            var (light, dark) = Luminance(a) >= Luminance(b) ? (Luminance(a), Luminance(b)) : (Luminance(b), Luminance(a));

            return (light + 0.05) / (dark + 0.05);
        }

        private static OrderRecord Order(string id, OrderStatus status = OrderStatus.InProgress) => new OrderRecord
        {
            Id = id,
            JobNumber = "J" + id,
            ChipNumber = "1",
            CustomerName = "Customer " + id,
            StatusText = status.ToString(),
        };

        private static ExecResult Rows(int count)
        {
            var now = DateTimeOffset.UtcNow;

            return new ExecResult(
                "run",
                ExecutionOutcome.Failure,
                count,
                1,
                now,
                now,
                Enumerable.Range(0, count).Select(i => new TaskOutcome(i, (i + 1).ToString(), "Classic", i != 1, i != 1 ? "NoError" : "AuthFailure")).ToList());
        }

        [Fact]
        public Task EveryState_ShowsItsOwnSection_AndItsButtons_InsideTheSmallestPage() => Show(async scene =>
        {
            var findings = new ReportAnalysis { Supported = true, Programmable = true, ChipType = "DESFire EV1", FreeMemoryText = "1760", Notes = "A long note that has to wrap inside the narrow page and must not push anything out of it." };
            var selection = new TemplateSelection(ChipVerdict.NotSupported, FreeMemory.Unknown, Array.Empty<LsmTemplate>(), null);
            var check = new CheckRunResult(CheckRunStatus.Finished, null, Rows(3), findings, "C:/a/very/long/path/to/the/reports/of/the/order/J1-1.step1.pdf");
            var failure = new Failure(FailureKind.Unexpected, CheckFailureCodes.RunFailed, "The run failed.");
            var expected = new Dictionary<WorkflowState, (string Section, string[] Buttons)>
            {
                [WorkflowState.Idle] = ("StepOrders", new[] { "CheckOrderButton", "RefreshButton", "SettingsButton" }),
                [WorkflowState.LoadingOrders] = ("StepOrders", new[] { "CheckOrderButton", "RefreshButton", "SettingsButton", "CancelButton" }),
                [WorkflowState.Starting] = ("StepPrepare", new[] { "CancelButton" }),
                [WorkflowState.ConfirmingReplace] = ("StepReplace", new[] { "ReplaceButton", "KeepButton" }),
                [WorkflowState.WaitingForCard] = ("StepCard", new[] { "StartCheckButton", "CancelButton" }),
                [WorkflowState.Checking] = ("StepCheck", new[] { "RetryRunButton", "CancelButton" }),
                [WorkflowState.RunFailed] = ("StepCheck", new[] { "RetryRunButton", "CancelButton" }),
                [WorkflowState.Assessing] = ("StepVerdict", new[] { "FinishButton", "CancelButton" }),
                [WorkflowState.Completing] = ("StepFinish", new[] { "RetryFinishButton" }),
                [WorkflowState.CompletionFailed] = ("StepFinish", new[] { "RetryFinishButton", "CancelButton" }),
                [WorkflowState.Completed] = ("StepEnd", new[] { "BackButton" }),
                [WorkflowState.Cancelled] = ("StepEnd", new[] { "BackButton" }),
                [WorkflowState.Faulted] = ("StepEnd", new[] { "BackButton" }),
            };

            Assert.Equal(WorkflowTables.States.Count, expected.Count);
            foreach (var state in WorkflowTables.States)
            {
                var busy = state == WorkflowState.LoadingOrders || state == WorkflowState.Starting || state == WorkflowState.Checking || state == WorkflowState.Completing;
                await scene.PushAsync(
                    state,
                    s => s with
                    {
                        Orders = new[] { Order("A"), Order("B", OrderStatus.Created) },
                        Order = state is WorkflowState.Idle or WorkflowState.LoadingOrders ? null : new WorkflowOrder("A", "JA", "1"),
                        Card = Snapshots.Card("04AABBCC"),
                        Phase = CheckPhase.Running,
                        Check = check,
                        Findings = findings,
                        Selection = selection,
                        Failure = state is WorkflowState.RunFailed or WorkflowState.CompletionFailed ? failure : null,
                        ExistingReports = new[] { ReportFile.Step1, ReportFile.Final },
                        IsTestMode = true,
                    },
                    busy);

                var (section, buttons) = expected[state];
                foreach (var name in Sections)
                {
                    Assert.Equal(name == section, scene.Shown(name));
                }

                var visible = VisibleButtons(scene.View).Select(button => button.Name).Where(name => name.Length > 0).ToList();
                foreach (var button in buttons)
                {
                    Assert.Contains(button, visible);
                }

                Assert.All(VisibleButtons(scene.View), button => AssertInside(scene.View, button, state + " " + button.Name));
                Assert.True(scene.Shown("ReaderLine"));
                Assert.True(scene.Shown("TestModeBanner"));
            }

            // The page declares the smallest size it is laid out for, and is never smaller.
            Assert.Equal(Smallest.Width, scene.View.MinWidth);
            Assert.Equal(Smallest.Height, scene.View.MinHeight);
            Assert.True(scene.View.ActualWidth >= Smallest.Width - 0.5);
            Assert.True(scene.View.ActualHeight >= Smallest.Height - 0.5);
        });

        [Fact]
        public Task TheListOfOrders_ShowsTheOrders_AndAChoiceReachesTheButton() => Show(async scene =>
        {
            await scene.PushAsync(WorkflowState.Idle, s => s with { Orders = new[] { Order("A"), Order("B", OrderStatus.Created) } });
            var list = scene.Named<ListBox>("OrderList");

            Assert.Equal(2, list.Items.Count);
            Assert.False(scene.Button("CheckOrderButton").IsEnabled);

            list.SelectedItem = scene.Vm.Orders[1];
            await Flush();
            Assert.False(scene.Button("CheckOrderButton").IsEnabled);
            Assert.NotEmpty(scene.Text("OrderHint"));

            list.SelectedItem = scene.Vm.Orders[0];
            await Flush();
            Assert.True(scene.Button("CheckOrderButton").IsEnabled);
            Assert.Equal(string.Empty, scene.Text("OrderHint"));

            scene.Workflow.Calls.Clear();
            scene.Button("CheckOrderButton").Command.Execute(null);
            Assert.Equal(new[] { "Start:A:German" }, scene.Workflow.Calls);
        });

        [Fact]
        public void AnOrderRow_ReadsAsOneLine_WithoutTheEmptyParts()
        {
            Assert.Equal("J1, 3, Muster GmbH, InProgress", new OrderRowViewModel(new OrderRecord { Id = "A", JobNumber = "J1", ChipNumber = "3", CustomerName = "Muster GmbH", StatusText = "InProgress" }).ToString());
            Assert.Equal("J1, InProgress", new OrderRowViewModel(new OrderRecord { Id = "A", JobNumber = "J1", StatusText = "InProgress" }).ToString());
        }

        [Fact]
        public Task AnEmptyList_SaysWhy_AndATimerReadOnlyAddsALine() => Show(async scene =>
        {
            Assert.True(scene.Shown("OrdersEmptyText"));
            Assert.Equal(scene.Vm.Strings[Keys.GuidedOrdersLoading], scene.Text("OrdersEmptyText"));

            await scene.PushAsync(WorkflowState.LoadingOrders, busy: true);
            await scene.PushAsync(WorkflowState.Idle);
            Assert.Equal(scene.Vm.Strings[Keys.GuidedOrdersNone], scene.Text("OrdersEmptyText"));
            Assert.False(scene.Shown("UpdatingLine"));
        });

        [Fact]
        public Task AProgrammableVerdict_OffersOnlyToLeaveTheOrderOpen_AndAnUnclearOneIsAWarning() => Show(async scene =>
        {
            var programmable = new TemplateSelection(ChipVerdict.Programmable, FreeMemory.Unknown, Array.Empty<LsmTemplate>(), null);
            await scene.PushAsync(
                WorkflowState.Assessing,
                s => s with { Selection = programmable, Controls = WorkflowControls.Cancel, Order = new WorkflowOrder("A", "JA", "1") });

            Assert.False(scene.Button("FinishButton").IsEnabled);
            Assert.Equal(scene.Vm.Strings[Keys.GuidedLeaveOpenButton], scene.Button("CancelButton").Content);
            Assert.Equal(scene.Vm.Strings[Keys.GuidedVerdictProgrammable], scene.Text("VerdictLine"));

            var unclear = new TemplateSelection(ChipVerdict.Indeterminate, FreeMemory.Unknown, Array.Empty<LsmTemplate>(), null);
            await scene.PushAsync(WorkflowState.Assessing, s => s with { Selection = unclear, Order = new WorkflowOrder("A", "JA", "1") });

            Assert.True(scene.Button("FinishButton").IsEnabled);
            Assert.Equal(scene.Vm.Strings[Keys.CancelButton], scene.Button("CancelButton").Content);
            // A dark amber, not DarkOrange: the text has to be readable on the white window (at least 4.5 : 1).
            var warning = ((System.Windows.Media.SolidColorBrush)scene.Named<TextBlock>("VerdictLine").Foreground).Color;
            Assert.Equal(System.Windows.Media.Color.FromRgb(0x9A, 0x5B, 0x00), warning);
            Assert.True(Contrast(warning, System.Windows.Media.Colors.White) >= 4.5, "The warning is hard to read.");
        });

        [Fact]
        public Task ALongCustomerName_IsTrimmed_InTheSummaryOfTheOrder() => Show(async scene =>
        {
            var longName = string.Join(" ", Enumerable.Repeat("Mueller Metallbau GmbH", 12));
            await scene.PushAsync(
                WorkflowState.WaitingForCard,
                s => s with { Order = new WorkflowOrder("A", "JA", "1"), Orders = new[] { new OrderRecord { Id = "A", JobNumber = "JA", ChipNumber = "1", CustomerName = longName, StatusText = "InProgress" } } });

            AssertInside(scene.View, scene.Named<Grid>("OrderSummary"), "the summary of the order");
            var customer = VisualDescendants<TextBlock>(scene.Named<Grid>("OrderSummary")).Single(block => block.Text == longName);
            AssertInside(scene.View, customer, "the customer");
        });

        [Fact]
        public Task LongNotesOfTheReport_ScrollInTheirOwnArea_AndTheButtonsStayInsideThePage() => Show(async scene =>
        {
            var notes = string.Join(Environment.NewLine, Enumerable.Range(1, 60).Select(i => "Note line " + i));
            var findings = new ReportAnalysis { ChipType = "DESFire EV1", FreeMemoryText = "1760", Notes = notes };
            var check = new CheckRunResult(CheckRunStatus.Finished, null, Rows(12), findings, "first-report.pdf");
            var selection = new TemplateSelection(ChipVerdict.NotSupported, FreeMemory.Unknown, Array.Empty<LsmTemplate>(), null);
            await scene.PushAsync(WorkflowState.Assessing, s => s with { Order = new WorkflowOrder("A", "JA", "1"), Findings = findings, Check = check, Selection = selection });
            var expander = VisualDescendants<Expander>(scene.View).Single();
            expander.IsExpanded = true;
            scene.Window.UpdateLayout();

            var scroll = scene.Named<ScrollViewer>("VerdictScroll");
            Assert.True(scroll.ScrollableHeight > 0, "The notes did not need the scroll area.");
            Assert.All(VisibleButtons(scene.View), button => AssertInside(scene.View, button, button.Name));
            AssertInside(scene.View, scene.Button("FinishButton"), "the Finish button");
        });

        [Fact]
        public Task AFailure_ShowsItsTextAndItsCode_AndLongTextsStayInsideThePage() => Show(async scene =>
        {
            var failure = new Failure(FailureKind.Unreachable, "orders.unreachable", "No server.");
            await scene.PushAsync(WorkflowState.Idle, s => s with { OrdersFailure = failure });

            var list = scene.Named<ItemsControl>("MessageList");
            Assert.Single(list.Items);
            Assert.True(scene.Shown("MessageList"));
            AssertInside(scene.View, list, "the message list");

            var texts = VisualDescendants<TextBlock>(list).Select(block => block.Text).ToList();
            Assert.Contains(scene.Vm.Strings[Keys.FailOrdersUnreachable], texts);
            Assert.Contains("orders.unreachable", texts);
            Assert.Contains(scene.Vm.Strings[Keys.GuidedCodeLabel], texts);
        });

        [Fact]
        public Task TheTasksOfAFailedCheck_AreListedInsideThePage() => Show(async scene =>
        {
            var check = new CheckRunResult(CheckRunStatus.Failed, new Failure(FailureKind.Unexpected, CheckFailureCodes.RunFailed, "x"), Rows(12), null, null);
            await scene.PushAsync(WorkflowState.RunFailed, s => s with { Check = check, Order = new WorkflowOrder("A", "JA", "1") });

            Assert.True(scene.Vm.HasRows);
            var results = VisualDescendants<TextBlock>(scene.Named<Grid>("StepCheck")).Where(block => block.Text == scene.Vm.Strings[Keys.RowFailed]).ToList();
            Assert.Single(results);
            Assert.All(VisibleButtons(scene.View), button => AssertInside(scene.View, button, button.Name));
        });

        [Fact]
        public Task TheTextsFollowTheLanguageOfTheHost() => Show(
            async scene =>
            {
                Assert.Equal(ExtensionStrings.Get(Keys.GuidedCheckOrderButton, "de-DE"), scene.Button("CheckOrderButton").Content);

                scene.Host.Context.ChangeCulture("en-US");
                await Flush();

                Assert.Equal(ExtensionStrings.Get(Keys.GuidedCheckOrderButton, "en-US"), scene.Button("CheckOrderButton").Content);
            },
            culture: "de-DE");

        [Fact]
        public Task Closing_IsRefusedWhileAStepRuns_AndAllowedInTheList() => Show(async scene =>
        {
            await scene.PushAsync(WorkflowState.Starting, busy: true);

            scene.Window.Close();
            await Flush();

            Assert.True(scene.Window.IsVisible, "The window closed in the middle of a step.");
            Assert.True(scene.Shown("CloseHint"));

            await scene.PushAsync(WorkflowState.Idle);
            scene.Window.Close();

            Assert.False(scene.Window.IsVisible);
        });

        [Fact]
        public Task WhenItIsShown_TheViewOpensThePage_Once() => Show(
            scene =>
            {
                // Nobody called Open: the view did, when it was loaded, and the first read of the list is the sign.
                Assert.Equal(new[] { "Refresh" }, scene.Workflow.Calls);

                return Task.CompletedTask;
            },
            open: false);

        [Fact]
        public Task Dispose_DisposesThePage_Once() => Show(async scene =>
        {
            Assert.Equal(3, scene.Host.PageSubscriberCount);

            scene.View.Dispose();
            scene.View.Dispose();
            await Flush();

            Assert.True(scene.Vm.IsDisposed);
            Assert.Equal(0, scene.Host.PageSubscriberCount);
            Assert.Equal(1, scene.Workflow.DisposeCount);
        });

        [Fact]
        public Task TheView_NeedsAPage() =>
            StaTestRunner.RunOnStaThreadAsync(() => Assert.Throws<ArgumentNullException>(() => new GuidedCheckView(null!)));

        private static IEnumerable<T> VisualDescendants<T>(DependencyObject root)
            where T : DependencyObject
        {
            var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                if (child is T match)
                {
                    yield return match;
                }

                foreach (var inner in VisualDescendants<T>(child))
                {
                    yield return inner;
                }
            }
        }
    }
}
