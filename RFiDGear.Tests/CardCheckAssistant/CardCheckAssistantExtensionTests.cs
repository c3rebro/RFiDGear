using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;

using RFiDGear.Contracts;
using RFiDGear.Extensions.CardCheckAssistant;
using RFiDGear.Extensions.CardCheckAssistant.Localization;
using RFiDGear.Extensions.CardCheckAssistant.ViewModels;
using RFiDGear.Extensions.CardCheckAssistant.Views;
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.UI.UIExtensions;
using RFiDGear.UI.UIExtensions.Interfaces;

using Xunit;

using static RFiDGear.Tests.CardCheckAssistant.ViewModelTestSupport;

namespace RFiDGear.Tests.CardCheckAssistant
{
    /// <summary>
    /// The extension as RFiDGear meets it: discovered from its metadata without being created, building a page per
    /// window, and, checked statically, depending on nothing but the contract and the SDK.
    /// </summary>
    [Collection("WpfBindingTrace")]
    public class CardCheckAssistantExtensionTests
    {
        private static readonly System.Reflection.Assembly ExtensionAssembly = typeof(CardCheckAssistantExtension).Assembly;

        [Fact]
        public void Mef_FindsTheExtension_WithItsMetadata_WithoutCreatingIt()
        {
            using var container = new CompositionContainer(new AssemblyCatalog(ExtensionAssembly));

            var export = Assert.Single(container.GetExports<IUIExtension, IUIExtensionDetails>());

            Assert.Equal("Card Check Assistant", export.Metadata.Name);
            Assert.Equal(HostPlacement.Application, export.Metadata.HostPlacement);
            Assert.Null(export.Metadata.Uri);   // a Uri would make the host open that page and never ask for content
            Assert.Equal(100, export.Metadata.SortOrder);
            Assert.False(export.IsValueCreated, "discovering the extension must not create it");
        }

        [Fact]
        public void TheExportedClass_IsAnApplicationExtensionFactory_AndCreatesAnInstanceOnDemand()
        {
            using var container = new CompositionContainer(new AssemblyCatalog(ExtensionAssembly));
            var export = Assert.Single(container.GetExports<IUIExtension, IUIExtensionDetails>());

            Assert.IsAssignableFrom<IApplicationUIExtension>(export.Value);
            Assert.True(export.IsValueCreated);
        }

        [Fact]
        public void TheAssemblyName_MatchesWhatTheHostScansFor()
        {
            Assert.StartsWith("RFiDGear.Extensions", ExtensionAssembly.GetName().Name, StringComparison.Ordinal);
            Assert.EndsWith(".dll", Path.GetFileName(ExtensionAssembly.Location), StringComparison.OrdinalIgnoreCase);
            Assert.Contains(
                Path.GetFileName(ExtensionAssembly.Location),
                Directory.EnumerateFiles(Path.GetDirectoryName(ExtensionAssembly.Location)!, "RFiDGear.Extensions*.dll").Select(Path.GetFileName));
        }

        [Fact]
        public async Task CreateContent_BuildsANewPagePerCall_ThatIsDisposableWithADistinctDisposableDataContext()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var host = new FakeExtensionHost();
                var extension = new CardCheckAssistantExtension();

                var first = extension.CreateContent(host);
                var second = extension.CreateContent(host);

                try
                {
                    Assert.NotSame(first, second);
                    Assert.Null(first.Parent);
                    var disposableContent = Assert.IsAssignableFrom<IDisposable>(first);
                    var disposableDataContext = Assert.IsAssignableFrom<IDisposable>(first.DataContext);
                    Assert.NotSame(disposableContent, disposableDataContext);
                    Assert.NotSame(first.DataContext, second.DataContext);
                    Assert.Equal(8, host.PageSubscriberCount);   // two pages, four events each
                }
                finally
                {
                    (first as IDisposable)?.Dispose();
                    (second as IDisposable)?.Dispose();
                }

                Assert.Equal(0, host.PageSubscriberCount);
            });
        }

        [Fact]
        public async Task CreateContent_RejectsAMissingHost()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                Assert.Throws<ArgumentNullException>(() => new CardCheckAssistantExtension().CreateContent(null!));
            });
        }

        [Fact]
        public async Task OpenedByTheLauncher_ClosedAndReopened_LeavesFourSubscriptionsOrNone()
        {
            using var area = new TempArea();
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var parts = new PartSet(typeof(CardCheckAssistantExtension));
                var errors = new List<string>();
                var navigated = new List<string>();
                var launcher = CreateLauncher(errors, navigated);
                var host = new FakeExtensionHost();

                var first = launcher.Open(parts.Single, host)!;
                Assert.Equal(4, host.PageSubscriberCount);
                Assert.Equal("Card Check Assistant", first.Title);
                first.Close();
                Assert.Equal(0, host.PageSubscriberCount);

                var second = launcher.Open(parts.Single, host)!;
                Assert.Equal(4, host.PageSubscriberCount);   // one functional page, not two sets of handlers

                // The reopened page works.
                var view = Assert.IsType<CardCheckAssistantView>(second.Content);
                view.ViewModel.ProjectPath = area.ProjectPath;
                await view.ViewModel.RunCommand.ExecuteAsync();
                Assert.True(view.ViewModel.HasResult);

                second.Close();
                Assert.Equal(0, host.PageSubscriberCount);
                Assert.Empty(errors);
                Assert.Empty(navigated);   // a page that names a Uri would be opened as that page, not asked for content
            });
        }

        [Fact]
        public async Task TheWindow_CannotBeClosedInTheMiddleOfARun_ButCanOnceItIsDone()
        {
            using var area = new TempArea();
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var parts = new PartSet(typeof(CardCheckAssistantExtension));
                var errors = new List<string>();
                var navigated = new List<string>();
                var launcher = CreateLauncher(errors, navigated);
                var host = new FakeExtensionHost();
                host.ExecutionHost.RunGate = NewGate();
                var window = launcher.Open(parts.Single, host)!;
                var view = Assert.IsType<CardCheckAssistantView>(window.Content);
                view.ViewModel.ProjectPath = area.ProjectPath;
                await Flush();   // let the window load, which is when the page hooks the closing event
                var run = view.ViewModel.RunCommand.ExecuteAsync();
                await WaitUntil(() => host.ExecutionHost.Requests.Count == 1, "the run to start");

                window.Close();

                Assert.True(window.IsVisible, "closing during a run must be refused");
                Assert.True(view.ViewModel.ShowCloseHint);
                Assert.Equal(4, host.PageSubscriberCount);

                host.ExecutionHost.RunGate.SetResult(true);
                await run;
                window.Close();

                Assert.False(window.IsVisible);
                Assert.Equal(0, host.PageSubscriberCount);
                Assert.Empty(errors);
                Assert.Empty(navigated);   // a page that names a Uri would be opened as that page, not asked for content
            });
        }

        [Fact]
        public async Task TheView_ShowsEveryStateWithoutABindingError()
        {
            using var area = new TempArea();
            var listener = BindingErrorListener.Start();

            try
            {
                await StaTestRunner.RunOnStaThreadAsync(async () =>
                {
                    var host = new FakeExtensionHost("de-DE");
                    host.ExecutionHost.Result = FakeResults.WithOutcomes(
                        ExecutionOutcome.Failure,
                        ExecutionFailureReason.Faulted,
                        new TaskOutcome(0, "1", "Classic", true, "NoError"),
                        new TaskOutcome(1, "2", "Desfire", false, "AuthFailure"),
                        new TaskOutcome(2, "3", "Ultralight", null, "Empty", TaskSkipReasons.NotReached));
                    var content = new CardCheckAssistantExtension().CreateContent(host);
                    var window = new Window { Content = content, Width = 900, Height = 600 };
                    window.Show();
                    try
                    {
                        var viewModel = ((CardCheckAssistantView)content).ViewModel;
                        await Flush();
                        viewModel.ProjectPath = area.ProjectPath;
                        viewModel.ReportTargetPath = "relative.pdf";   // an error next to a field
                        await viewModel.RunCommand.ExecuteAsync();
                        viewModel.ReportTargetPath = string.Empty;
                        await viewModel.RunCommand.ExecuteAsync();      // a result with every kind of row
                        host.ExecutionHost.SetRunning(true);            // the busy state
                        host.Context.ChangeCulture("en-US");
                        await Flush();
                        window.UpdateLayout();

                        Assert.Equal(3, viewModel.Rows.Count);
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

        [Fact]
        public async Task WhenTheViewCannotBeBuilt_NothingStaysSubscribedToTheHost()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var host = new FakeExtensionHost();

                var failure = Assert.Throws<InvalidOperationException>(
                    () => CardCheckAssistantExtension.CreatePage(host, _ => throw new InvalidOperationException("the page is broken")));

                Assert.Equal("the page is broken", failure.Message);   // the original failure, not a replacement
                Assert.Equal(0, host.PageSubscriberCount);
            });
        }

        [Fact]
        public async Task TheView_IsWiredToTheViewModel_ControlByControl()
        {
            using var area = new TempArea();
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var host = new FakeExtensionHost();
                var content = new CardCheckAssistantExtension().CreateContent(host);
                var window = new Window { Content = content, Width = 900, Height = 600 };
                window.Show();
                try
                {
                    var view = (CardCheckAssistantView)content;
                    var viewModel = view.ViewModel;
                    await Flush();

                    var run = Named<System.Windows.Controls.Button>(view, "RunButton");
                    var browse = Named<System.Windows.Controls.Button>(view, "BrowseButton");
                    var fields = Named<System.Windows.Controls.Panel>(view, "FieldsPanel");
                    var projectError = Named<System.Windows.Controls.TextBlock>(view, "ProjectErrorText");
                    var reportError = Named<System.Windows.Controls.TextBlock>(view, "ReportErrorText");
                    var progress = Named<System.Windows.Controls.ProgressBar>(view, "ProgressIndicator");
                    var closeHint = Named<System.Windows.Controls.TextBlock>(view, "CloseHint");

                    // The buttons do what the view model says, and Enter inside a text box must not start a card run.
                    Assert.Same(viewModel.RunCommand, run.Command);
                    Assert.Same(viewModel.BrowseCommand, browse.Command);
                    var settingsButton = Named<System.Windows.Controls.Button>(view, "SettingsButton");
                    Assert.Same(viewModel.OpenSettingsCommand, settingsButton.Command);
                    Assert.Equal(Visibility.Visible, settingsButton.Visibility);
                    Assert.All(Descendants<System.Windows.Controls.Button>(view), button => Assert.False(button.IsDefault));

                    // Idle: fields usable, nothing to report, no progress.
                    Assert.True(fields.IsEnabled);
                    Assert.Equal(Visibility.Collapsed, projectError.Visibility);
                    Assert.Equal(Visibility.Collapsed, reportError.Visibility);
                    Assert.Equal(Visibility.Collapsed, progress.Visibility);
                    Assert.Equal(Visibility.Collapsed, closeHint.Visibility);

                    // A report error appears under the report field only.
                    viewModel.ProjectPath = area.ProjectPath;
                    viewModel.ReportTargetPath = "relative.pdf";
                    await viewModel.RunCommand.ExecuteAsync();
                    Assert.Equal(Visibility.Visible, reportError.Visibility);
                    Assert.Equal(viewModel.ReportTargetError, reportError.Text);
                    Assert.Equal(Visibility.Collapsed, projectError.Visibility);

                    // And a project error under the project field only.
                    viewModel.ReportTargetPath = string.Empty;
                    viewModel.ProjectPath = string.Empty;
                    await viewModel.RunCommand.ExecuteAsync();
                    Assert.Equal(Visibility.Visible, projectError.Visibility);
                    Assert.Equal(viewModel.ProjectError, projectError.Text);
                    Assert.Equal(Visibility.Collapsed, reportError.Visibility);

                    // A run: the list shows one line per task.
                    viewModel.ProjectPath = area.ProjectPath;
                    await viewModel.RunCommand.ExecuteAsync();
                    window.UpdateLayout();
                    await Flush();
                    var list = Named<System.Windows.Controls.ItemsControl>(view, "ResultList");
                    Assert.Equal(viewModel.Rows.Count, list.Items.Count);
                    Assert.Equal(
                        viewModel.Rows.Count,
                        Descendants<System.Windows.Controls.TextBlock>(list).Count(block => block.Text == "OK"));

                    // Another run holds the host: the fields are disabled and progress shows.
                    host.ExecutionHost.SetRunning(true);
                    await Flush();
                    Assert.False(fields.IsEnabled);
                    Assert.False(settingsButton.IsEnabled);
                    Assert.Equal(Visibility.Visible, progress.Visibility);

                    host.ExecutionHost.SetRunning(false);
                    await Flush();
                    Assert.True(fields.IsEnabled);
                    Assert.True(settingsButton.IsEnabled);

                    // The hint that the window cannot close appears when the page is busy and asked to close.
                    host.ProjectHost.LoadGate = NewGate();
                    var pending = viewModel.RunCommand.ExecuteAsync();
                    await WaitUntil(() => host.ProjectHost.LoadedPaths.Count > 0, "the load to start");
                    Assert.False(viewModel.TryRequestClose());
                    await Flush();
                    Assert.Equal(Visibility.Visible, closeHint.Visibility);
                    host.ProjectHost.LoadGate.SetResult(true);
                    await pending;
                }
                finally
                {
                    host.ExecutionHost.SetRunningQuietly(false);
                    window.Close();
                }
            });
        }

        // ── what the assembly may depend on ──

        // The extension's own support library; allowed by its exact name only.
        private const string OwnLibrary = "RFiDGear.ExtensionLibraries.CardCheck";

        [Fact]
        public void TheAssembly_ReferencesOnlyTheContractTheSdkItsOwnLibraryAndTheFramework()
        {
            var references = ReadAssemblyReferences();
            var allowed = new[]
            {
                "System", "Microsoft", "netstandard", "mscorlib", "WindowsBase", "PresentationFramework", "PresentationCore",
                "UIAutomationTypes", "UIAutomationProvider", "RFiDGear.Contracts", "RFiDGear.ExtensionSdk"
            };

            Assert.Contains("RFiDGear.Contracts", references);
            Assert.Contains("RFiDGear.ExtensionSdk", references);
            Assert.Contains(OwnLibrary, references);
            Assert.All(references, name =>
                Assert.True(
                    name == OwnLibrary || allowed.Any(prefix => name == prefix || name.StartsWith(prefix + ".", StringComparison.Ordinal)),
                    $"the extension references '{name}'"));
        }

        [Theory]
        [InlineData("RFiDGear")]
        [InlineData("Elatec")]
        [InlineData("LibLogicalAccess")]
        [InlineData("Serilog")]
        [InlineData("GemBox")]
        [InlineData("Newtonsoft")]
        [InlineData("CommunityToolkit")]
        [InlineData("System.Diagnostics.Process")]
        [InlineData("System.Data.SqlClient")]   // the database driver belongs to the library, never to the page
        [InlineData("System.Data.SQLite")]
        [InlineData("Microsoft.Data")]
        [InlineData("SQLitePCLRaw")]
        public void TheAssembly_DoesNotReference(string forbidden)
        {
            var references = ReadAssemblyReferences();

            Assert.DoesNotContain(
                references,
                name => name == forbidden || name.StartsWith(forbidden + ".", StringComparison.Ordinal) && name != "RFiDGear.Contracts" && name != "RFiDGear.ExtensionSdk" && name != OwnLibrary);
        }

        [Fact]
        public void TheAssembly_NeverStartsAProcess()
        {
            using var stream = File.OpenRead(ExtensionAssembly.Location);
            using var pe = new PEReader(stream);
            var metadata = pe.GetMetadataReader();

            var types = metadata.TypeReferences
                .Select(handle => metadata.GetTypeReference(handle))
                .Select(type => metadata.GetString(type.Namespace) + "." + metadata.GetString(type.Name))
                .ToList();

            Assert.DoesNotContain("System.Diagnostics.Process", types);
            Assert.DoesNotContain("System.Diagnostics.ProcessStartInfo", types);
        }

        [Fact]
        public void TheProject_ReferencesTheContractAndTheSdkWithoutCopyingThem_AndItsOwnLibraryWithACopy()
        {
            var project = XDocument.Load(Path.Combine(RepositoryRoot(), "RFiDGear.Extensions", "CardCheckAssistant", "RFiDGear.Extensions.CardCheckAssistant.csproj"));

            var references = project.Descendants("ProjectReference").ToList();
            bool Includes(XElement reference, string file) => ((string?)reference.Attribute("Include") ?? string.Empty).EndsWith(file, StringComparison.Ordinal);

            Assert.Equal(3, references.Count);
            var contract = Assert.Single(references, r => Includes(r, "RFiDGear.Contracts.csproj"));
            var sdk = Assert.Single(references, r => Includes(r, "RFiDGear.ExtensionSdk.csproj"));
            var library = Assert.Single(references, r => Includes(r, OwnLibrary + ".csproj"));
            Assert.Equal("false", (string?)contract.Element("Private"));
            Assert.Equal("false", (string?)sdk.Element("Private"));
            Assert.Null(library.Element("Private"));   // the host does not own it, so it is copied beside the extension
            Assert.Empty(project.Descendants("PackageReference"));
        }

        [Fact]
        public void TheProject_RejectsPrivateCopiesOfEveryAssemblyTheHostOwns()
        {
            var project = File.ReadAllText(Path.Combine(RepositoryRoot(), "RFiDGear.Extensions", "CardCheckAssistant", "RFiDGear.Extensions.CardCheckAssistant.csproj"));

            foreach (var owned in new[] { "RFiDGear.Contracts", "RFiDGear.ExtensionSdk", "GemBox.Pdf", "Serilog", "SkiaSharp", "HarfBuzzSharp", "Newtonsoft.Json" })
            {
                Assert.Contains("$(OutDir)" + owned + ".dll", project, StringComparison.Ordinal);
            }
        }

        [Theory]
        [InlineData("CardCheckAssistantView.xaml")]
        [InlineData("GuidedCheckView.xaml")]
        [InlineData("SettingsView.xaml")]
        public void EveryTextTheXamlBindsTo_ExistsInBothLanguages(string xamlFile)
        {
            var xaml = File.ReadAllText(Path.Combine(RepositoryRoot(), "RFiDGear.Extensions", "CardCheckAssistant", "Views", xamlFile));

            var used = Regex.Matches(xaml, @"Strings\[(\w+)\]").Select(match => match.Groups[1].Value).Distinct().ToList();

            Assert.NotEmpty(used);
            Assert.All(used, key =>
            {
                Assert.Contains(key, ExtensionStrings.AllKeys);
                Assert.Contains(key, ExtensionStrings.GermanKeys);
            });
        }

        [Fact]
        public void EveryText_IsUsedBySomething()
        {
            // A text nobody asks for is a label somebody forgot to put on the screen.
            var root = Path.Combine(RepositoryRoot(), "RFiDGear.Extensions", "CardCheckAssistant");
            var sources = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(file => !file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) && !file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
                .Where(file => file.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase) || (file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && !file.EndsWith("ExtensionStrings.cs", StringComparison.OrdinalIgnoreCase)))
                .Select(File.ReadAllText)
                .ToList();
            var all = string.Join("\n", sources);

            Assert.All(ExtensionStrings.AllKeys, name => Assert.True(
                all.Contains("Strings[" + name + "]", StringComparison.Ordinal) || Regex.IsMatch(all, @"Keys\." + Regex.Escape(name) + @"\b"),
                name + " is not used anywhere"));
        }

        [Fact]
        public async Task OpeningAndRunning_StartsNoRFiDGearOrCardCheckAssistantProcess()
        {
            // Best effort: it cannot see a child that starts and ends within the run. The static test above, which
            // shows the assembly cannot start a process at all, is the part that cannot be fooled.
            using var area = new TempArea();
            var before = CountProcesses();

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var host = new FakeExtensionHost();
                var content = new CardCheckAssistantExtension().CreateContent(host);
                try
                {
                    var viewModel = ((CardCheckAssistantView)content).ViewModel;
                    viewModel.ProjectPath = area.ProjectPath;
                    await viewModel.RunCommand.ExecuteAsync();
                    Assert.True(viewModel.HasResult);
                }
                finally
                {
                    (content as IDisposable)?.Dispose();
                }
            });

            Assert.Equal(before, CountProcesses());
        }

        /// <summary>
        /// A launcher whose page-navigation path only records where it was sent, so a regression that makes the host open
        /// a page fails an assertion instead of trying to load a page that does not exist.
        /// </summary>
        private static ApplicationExtensionLauncher CreateLauncher(List<string> errors, List<string> navigatedTo) =>
            new ApplicationExtensionLauncher(
                errors.Add,
                (uri, _) =>
                {
                    navigatedTo.Add(uri);
                    return null;
                });

        private static T Named<T>(FrameworkElement root, string name)
            where T : class
        {
            var found = root.FindName(name);
            Assert.NotNull(found);
            return Assert.IsAssignableFrom<T>(found);
        }

        private static IEnumerable<T> Descendants<T>(DependencyObject root)
            where T : DependencyObject
        {
            for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                if (child is T match)
                {
                    yield return match;
                }

                foreach (var nested in Descendants<T>(child))
                {
                    yield return nested;
                }
            }
        }

        private static int CountProcesses() =>
            Process.GetProcessesByName("RFiDGear").Length + Process.GetProcessesByName("CardCheckAssistant").Length;

        private static List<string> ReadAssemblyReferences()
        {
            using var stream = File.OpenRead(ExtensionAssembly.Location);
            using var pe = new PEReader(stream);
            var metadata = pe.GetMetadataReader();
            return metadata.AssemblyReferences
                .Select(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name))
                .ToList();
        }

        private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));
    }
}
