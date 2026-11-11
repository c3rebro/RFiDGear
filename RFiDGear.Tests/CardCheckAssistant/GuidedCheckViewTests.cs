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
            "StepOrders", "StepPrepare", "StepReplace", "StepCard", "StepCheck", "StepVerdict", "StepProgram", "StepReview", "StepFinish", "StepEnd",
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
                        new FakePageServices(),
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
            var readOut = new CheckRunResult(CheckRunStatus.Finished, null, Rows(3), new ReportAnalysis { Supported = false, ChipType = "MIFARE DESFire EV2" }, "C:/a/very/long/path/to/the/reports/of/the/order/J1-1.step3.pdf");
            var move = new CardMoveResult(CardWaitOutcome.Done, CardMoveStage.WaitingForReinsertion, "04AABBCC", "04DDEEFF", null);
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
                [WorkflowState.Programming] = ("StepProgram", new[] { "ContinueButton", "CancelButton" }),
                [WorkflowState.WritingReport] = ("StepProgram", new[] { "CancelButton" }),
                [WorkflowState.ProgrammingFailed] = ("StepProgram", new[] { "ProgramRetryButton", "CancelButton" }),
                [WorkflowState.ReadingOut] = ("StepCheck", new[] { "CancelButton" }),
                [WorkflowState.ReadOutFailed] = ("StepCheck", new[] { "RetryRunButton", "FinishWithoutReadOutButton", "CancelButton" }),
                [WorkflowState.Reviewing] = ("StepReview", new[] { "ReviewFinishButton", "OpenReportButton", "OpenFolderButton", "CancelButton" }),
                [WorkflowState.Completing] = ("StepFinish", new[] { "RetryFinishButton" }),
                [WorkflowState.CompletionFailed] = ("StepFinish", new[] { "RetryFinishButton", "CancelButton" }),
                [WorkflowState.Completed] = ("StepEnd", new[] { "BackButton" }),
                [WorkflowState.Cancelled] = ("StepEnd", new[] { "BackButton" }),
                [WorkflowState.Faulted] = ("StepEnd", new[] { "BackButton" }),
            };

            Assert.Equal(WorkflowTables.States.Count, expected.Count);
            foreach (var state in WorkflowTables.States)
            {
                var busy = state is WorkflowState.LoadingOrders or WorkflowState.Starting or WorkflowState.Checking or WorkflowState.Completing
                    or WorkflowState.WritingReport or WorkflowState.ReadingOut;
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
                        ReadOut = readOut,
                        Move = move,
                        Failure = state is WorkflowState.RunFailed or WorkflowState.CompletionFailed or WorkflowState.ProgrammingFailed or WorkflowState.ReadOutFailed ? failure : null,
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
                Assert.True(VisibleButtons(scene.View).Count(button => button.IsDefault) <= 1, "Enter must have one meaning.");
                Assert.True(scene.Shown("ReaderLine"));
                Assert.True(scene.Shown("TestModeBanner"));

                // A failed step says so, with its text and its code, in every state that has one.
                var failed = state is WorkflowState.RunFailed or WorkflowState.CompletionFailed or WorkflowState.ProgrammingFailed or WorkflowState.ReadOutFailed;
                var messages = scene.Named<ItemsControl>("MessageList").Items;
                Assert.Equal(failed ? 1 : 0, messages.Count);
                if (failed)
                {
                    var message = Assert.IsType<GuidedCheckViewModel.FailureMessage>(messages[0]);
                    Assert.Equal(failure.Code, message.Code);
                    Assert.NotEmpty(message.Text);
                }

                // Finishing without the read-out is offered nowhere but after a failed read-out.
                Assert.Equal(state == WorkflowState.ReadOutFailed, scene.Shown("FinishWithoutReadOutButton"));
            }

            // The page declares the smallest size it is laid out for, and is never smaller.
            Assert.Equal(Smallest.Width, scene.View.MinWidth);
            Assert.Equal(Smallest.Height, scene.View.MinHeight);
            Assert.True(scene.View.ActualWidth >= Smallest.Width - 0.5);
            Assert.True(scene.View.ActualHeight >= Smallest.Height - 0.5);
        });

        [Theory]
        [InlineData("en-US")]
        [InlineData("de-DE")]
        public Task TheProgramStep_FitsThePage_WithTheFullestForm_AndWhatIsTypedSurvivesTheHost(string culture) => Show(async scene =>
        {
            var templates = Enumerable.Range(1, 12)
                .Select(i => new LsmTemplate("MD" + i, "Template number " + i + " with a long and descriptive name for the list", i == 12 ? TemplateFamily.Classic : TemplateFamily.Desfire, 100 * i, i))
                .ToArray();
            var blocks = new[]
            {
                new TextTemplate { Name = "A text block with quite a long name that has to fit the box", Content = "Block text one." },
                new TextTemplate { Name = "Second", Content = "Block text two." },
                new TextTemplate { Name = "Third", Content = "Block text three." },
                new TextTemplate { Name = new string('W', 140), Content = "Long." },
            };
            var notes = string.Join(Environment.NewLine, Enumerable.Range(1, 12).Select(i => "Note line " + i));
            await scene.PushAsync(
                WorkflowState.Programming,
                s => s with
                {
                    Order = new WorkflowOrder("A", "JA", "1"),
                    Selection = new TemplateSelection(ChipVerdict.Programmable, FreeMemory.Unknown, templates, templates[0]),
                    Findings = new ReportAnalysis { ChipType = "DESFire EV1 with a long type name", FreeMemoryText = "1760", Notes = notes },
                    TextBlocks = blocks,
                    Move = new CardMoveResult(CardWaitOutcome.Done, CardMoveStage.WaitingForReinsertion, "04AABBCCDDEEFF", "04112233445566", null),
                });
            var form = scene.Vm.Form;

            Assert.True(scene.Shown("StepProgram"));
            Assert.Equal(12, scene.Named<ComboBox>("TemplateBox").Items.Count);
            Assert.False(scene.Shown("RestrictionPanel"));
            Assert.False(scene.Shown("SectorsPanel"));
            Assert.True(scene.Shown("MoveWarning"));                             // another chip was put back

            // Everything that can be shown at once: a restriction with its text blocks, and a Classic chip's sectors.
            form.Programming.IsNo = true;
            form.FunctionTest.IsNo = true;
            form.Restricted.IsYes = true;
            form.SelectedTemplate = templates[11];
            await Flush();
            scene.Window.UpdateLayout();

            Assert.True(scene.Shown("RestrictionPanel"));
            Assert.True(scene.Shown("BlockPanel"));
            Assert.True(scene.Shown("SectorsPanel"));

            // Screen readers hear the question with the answer, not just "Yes" three times.
            Assert.Equal(scene.Vm.Strings[Keys.GuidedQuestionFunctionTest] + ": " + scene.Vm.Strings[Keys.GuidedAnswerNo],
                System.Windows.Automation.AutomationProperties.GetName(scene.Named<RadioButton>("FunctionTestNo")));
            Assert.Equal(scene.Vm.Strings[Keys.GuidedQuestionRestricted] + ": " + scene.Vm.Strings[Keys.GuidedAnswerYes],
                System.Windows.Automation.AutomationProperties.GetName(scene.Named<RadioButton>("RestrictedYes")));

            // The form scrolls in its own area (at the smallest page it does not all fit); what is outside it never leaves the page.
            var scroll = scene.Named<ScrollViewer>("ProgramScroll");
            AssertInside(scene.View, scroll, "the area of the form");
            Assert.True(scroll.ScrollableHeight > 0, "The fullest form should need the scroll area at the smallest page.");
            var inTheForm = VisualDescendants<Button>(scroll).ToList();
            Assert.All(VisibleButtons(scene.View).Where(button => !inTheForm.Contains(button)), button => AssertInside(scene.View, button, button.Name));
            AssertInside(scene.View, scene.Named<TextBlock>("StatusLine"), "the status line");
            AssertInside(scene.View, scene.Named<TextBlock>("MoveWarning"), "the warning");
            Assert.False(scene.Button("ContinueButton").IsEnabled);              // the sectors are still open

            // Typing is not undone by what the host announces while the chip goes off and on again.
            var box = scene.Named<TextBox>("RestrictionBox");
            box.Text = "Typed by the operator.";
            var sectors = scene.Named<TextBox>("SectorsBox");
            sectors.Text = "1-3";
            scene.Host.ReaderHost.Show(Snapshots.NoCard());
            scene.Host.ExecutionHost.SetRunning(true);
            await Flush();
            scene.Host.ExecutionHost.SetRunning(false);
            scene.Host.ReaderHost.Show(Snapshots.Card("04AA"));
            await Flush();

            Assert.Equal("Typed by the operator.", box.Text);
            Assert.Equal("Typed by the operator.", form.RestrictionText);
            Assert.Equal("1-3", sectors.Text);
            Assert.Equal("1-3", form.SectorsUsed);
            Assert.True(scene.Button("ContinueButton").IsEnabled);

            // A text block with a very long name takes the width that is left; the button next to it stays on the page.
            scene.Named<ComboBox>("BlockBox").SelectedItem = blocks[3];
            await Flush();
            scene.Window.UpdateLayout();
            var insert = scene.Button("InsertButton");
            var right = insert.TransformToAncestor(scroll).TransformBounds(new Rect(0, 0, insert.ActualWidth, insert.ActualHeight)).Right;
            Assert.True(right <= scroll.ViewportWidth + 0.5, "The insert button lies outside the form: " + right + " > " + scroll.ViewportWidth);

            // A text block is appended, not put in place of what was typed.
            scene.Named<ComboBox>("BlockBox").SelectedItem = blocks[1];
            scene.Button("InsertButton").Command.Execute(null);
            await Flush();
            Assert.StartsWith("Typed by the operator.", box.Text, StringComparison.Ordinal);
            Assert.EndsWith("Block text two.", box.Text, StringComparison.Ordinal);
        }, culture: culture);

        [Fact]
        public Task TheAnswers_AreShownLocked_WhenTheWorkflowHasTakenThem() => Show(async scene =>
        {
            var selection = new TemplateSelection(ChipVerdict.Programmable, FreeMemory.Unknown, new[] { new LsmTemplate("MD1", "One", TemplateFamily.Desfire, 1, 0) }, null);
            Func<WorkflowSnapshot, WorkflowSnapshot> shared = s => s with { Order = new WorkflowOrder("A", "JA", "1"), Selection = selection, Findings = new ReportAnalysis { Notes = "N" } };
            await scene.PushAsync(WorkflowState.Programming, shared);
            scene.Vm.Form.Restricted.IsYes = true;
            await Flush();
            Assert.True(scene.Named<ComboBox>("TemplateBox").IsEnabled);
            Assert.True(scene.Named<RadioButton>("ProgrammingYes").IsEnabled);
            Assert.False(scene.Named<TextBox>("RestrictionBox").IsReadOnly);
            Assert.False(scene.Shown("LockedNote"));

            // Locked: nothing can be changed, but the text can still be read, scrolled and copied (a disabled box cannot).
            await scene.PushAsync(WorkflowState.ProgrammingFailed, shared);
            Assert.False(scene.Named<ComboBox>("TemplateBox").IsEnabled);
            Assert.False(scene.Named<RadioButton>("ProgrammingYes").IsEnabled);
            Assert.False(scene.Named<RadioButton>("RestrictedNo").IsEnabled);
            Assert.True(scene.Named<TextBox>("RestrictionBox").IsReadOnly);
            Assert.True(scene.Named<TextBox>("RestrictionBox").IsEnabled);
            Assert.True(scene.Shown("LockedNote"));
            Assert.Equal(scene.Vm.Strings[Keys.GuidedAnswersLocked], scene.Text("LockedNote"));

            // The retry button is the only way on, and it is the retry of the workflow, nothing else.
            scene.Workflow.Calls.Clear();
            scene.Button("ProgramRetryButton").Command.Execute(null);
            Assert.Equal(new[] { "Retry" }, scene.Workflow.Calls);
        });

        [Fact]
        public Task TheReview_ShowsThePath_TheCommitSentenceAndTheWarnings_InsideThePage() => Show(async scene =>
        {
            var readOut = new CheckRunResult(
                CheckRunStatus.Finished,
                null,
                Rows(3),
                new ReportAnalysis { Supported = false, ChipType = "MIFARE DESFire EV2" },
                "C:/a/very/long/path/to/the/reports/of/the/order/with/many/folders/J1-1.step3.pdf");
            var move = new CardMoveResult(CardWaitOutcome.Done, CardMoveStage.WaitingForReinsertion, "04AABBCCDDEEFF", "04112233445566", null);

            await scene.PushAsync(
                WorkflowState.Reviewing,
                s => s with { Order = new WorkflowOrder("A", "JA", "1"), Findings = new ReportAnalysis { ChipType = "DESFire EV1" }, ReadOut = readOut, Move = move });

            Assert.True(scene.Shown("StepReview"));
            Assert.Contains("step3.pdf", scene.Named<TextBox>("ReadOutPathBox").Text, StringComparison.Ordinal);
            Assert.Equal(3, scene.Named<ItemsControl>("ReviewWarningList").Items.Count);
            Assert.True(scene.Shown("ReviewWarningList"));
            AssertInside(scene.View, scene.Named<ItemsControl>("ReviewWarningList"), "the warnings");

            // What the button commits to is on the page, and inside it.
            var commit = VisualDescendants<TextBlock>(scene.Named<Grid>("StepReview")).Single(block => block.Text == scene.Vm.Strings[Keys.GuidedReviewCommit]);
            AssertInside(scene.View, commit, "the commit sentence");
            Assert.True(scene.Button("ReviewFinishButton").IsEnabled);
            Assert.True(scene.Button("OpenReportButton").IsEnabled);
            Assert.True(scene.Button("OpenFolderButton").IsEnabled);
            Assert.All(VisibleButtons(scene.View), button => AssertInside(scene.View, button, button.Name));

            // Without anything odd there is no list at all.
            await scene.PushAsync(
                WorkflowState.Reviewing,
                s => s with { Order = new WorkflowOrder("A", "JA", "1"), ReadOut = readOut with { Findings = new ReportAnalysis { Supported = true, ChipType = "DESFire EV1" } }, Findings = new ReportAnalysis { ChipType = "DESFire EV1" } });
            Assert.False(scene.Shown("ReviewWarningList"));
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
        public Task TheButtons_AreWiredToTheCommandsOfThePage_AndFinishIsNotTheEnterButton() => Show(scene =>
        {
            var vm = scene.Vm;
            Assert.Same(vm.StartOrderCommand, scene.Button("CheckOrderButton").Command);
            Assert.Same(vm.RefreshCommand, scene.Button("RefreshButton").Command);
            Assert.Same(vm.OpenSettingsCommand, scene.Button("SettingsButton").Command);
            Assert.Same(vm.ConfirmReplaceCommand, scene.Button("ReplaceButton").Command);
            Assert.Same(vm.KeepReportsCommand, scene.Button("KeepButton").Command);
            Assert.Same(vm.StartCheckCommand, scene.Button("StartCheckButton").Command);
            Assert.Same(vm.RetryCommand, scene.Button("RetryRunButton").Command);
            Assert.Same(vm.RetryCommand, scene.Button("RetryFinishButton").Command);
            Assert.Same(vm.FinishCommand, scene.Button("FinishButton").Command);
            Assert.Same(vm.FinishCommand, scene.Button("ReviewFinishButton").Command);
            Assert.Same(vm.ContinueCommand, scene.Button("ContinueButton").Command);
            Assert.Same(vm.BeginCommand, scene.Button("BeginButton").Command);
            Assert.Same(vm.RetryCommand, scene.Button("ProgramRetryButton").Command);
            Assert.Same(vm.Form.InsertTextBlockCommand, scene.Button("InsertButton").Command);
            Assert.Same(vm.FinishWithoutReadOutCommand, scene.Button("FinishWithoutReadOutButton").Command);
            Assert.Same(vm.OpenReportCommand, scene.Button("OpenReportButton").Command);
            Assert.Same(vm.OpenFolderCommand, scene.Button("OpenFolderButton").Command);
            Assert.Same(vm.BackCommand, scene.Button("BackButton").Command);
            Assert.Same(vm.CancelCommand, scene.Button("CancelButton").Command);

            // Finishing stores the report and finishes the order: that is not what Enter should do by accident. Neither is
            // finishing without the read-out, nor opening a viewer.
            Assert.False(scene.Button("FinishButton").IsDefault);
            Assert.False(scene.Button("ReviewFinishButton").IsDefault);
            Assert.False(scene.Button("FinishWithoutReadOutButton").IsDefault);
            Assert.False(scene.Button("OpenReportButton").IsDefault);
            Assert.False(scene.Button("OpenFolderButton").IsDefault);
            Assert.False(scene.Button("ContinueButton").IsDefault);
            Assert.False(scene.Button("BeginButton").IsDefault);
            Assert.False(scene.Button("ProgramRetryButton").IsDefault);
            Assert.False(scene.Button("InsertButton").IsDefault);

            return Task.CompletedTask;
        });

        [Fact]
        public Task TheTestModeBanner_IsOnlyThereInTheTestMode() => Show(async scene =>
        {
            await scene.PushAsync(WorkflowState.Idle, s => s with { IsTestMode = false });
            Assert.False(scene.Shown("TestModeBanner"));

            await scene.PushAsync(WorkflowState.Idle, s => s with { IsTestMode = true });
            Assert.True(scene.Shown("TestModeBanner"));

            await scene.PushAsync(WorkflowState.WaitingForCard, s => s with { IsTestMode = false });
            Assert.False(scene.Shown("TestModeBanner"));
        });

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
        public Task AProgrammableVerdict_OffersToBeginProgramming_OrOnlyToLeaveTheOrderOpen_AndAnUnclearOneIsAWarning() => Show(async scene =>
        {
            var programmable = new TemplateSelection(ChipVerdict.Programmable, FreeMemory.Unknown, Array.Empty<LsmTemplate>(), null);
            await scene.PushAsync(
                WorkflowState.Assessing,
                s => s with { Selection = programmable, Order = new WorkflowOrder("A", "JA", "1") });

            // A chip that can be programmed is not finished here: it is programmed.
            Assert.False(scene.Shown("FinishButton"));
            Assert.True(scene.Shown("BeginButton"));
            Assert.True(scene.Button("BeginButton").IsEnabled);
            Assert.Equal(scene.Vm.Strings[Keys.GuidedLeaveOpenButton], scene.Button("CancelButton").Content);
            Assert.Equal(scene.Vm.Strings[Keys.GuidedVerdictProgrammable], scene.Text("VerdictLine"));

            // When it cannot be programmed now, the way on is to leave the order open, and the reason is there.
            var problem = new Failure(FailureKind.NotConfigured, WorkflowFailureCodes.MemoryProjectInvalid, "No project.");
            await scene.PushAsync(WorkflowState.Assessing, s => s with { Selection = programmable, Order = new WorkflowOrder("A", "JA", "1"), ProgrammingProblem = problem });
            Assert.True(scene.Shown("BeginButton"));
            Assert.False(scene.Button("BeginButton").IsEnabled);
            Assert.Equal(scene.Vm.Strings[Keys.GuidedVerdictProgrammableBlocked], scene.Text("VerdictLine"));
            Assert.Single(scene.Named<ItemsControl>("MessageList").Items);
            Assert.All(VisibleButtons(scene.View), button => AssertInside(scene.View, button, button.Name));

            var unclear = new TemplateSelection(ChipVerdict.Indeterminate, FreeMemory.Unknown, Array.Empty<LsmTemplate>(), null);
            await scene.PushAsync(WorkflowState.Assessing, s => s with { Selection = unclear, Order = new WorkflowOrder("A", "JA", "1") });

            Assert.True(scene.Shown("FinishButton"));
            Assert.False(scene.Shown("BeginButton"));
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
