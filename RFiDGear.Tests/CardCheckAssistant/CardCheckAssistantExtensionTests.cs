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
using RFiDGear.Extensions.CardCheckAssistant.Services;
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
                    Assert.Equal(6, host.PageSubscriberCount);   // two pages, three events each
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
        public async Task OpenedByTheLauncher_ClosedAndReopened_LeavesThreeSubscriptionsOrNone()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var parts = new PartSet(typeof(CardCheckAssistantExtension));
                var errors = new List<string>();
                var navigated = new List<string>();
                var launcher = CreateLauncher(errors, navigated);
                var host = new FakeExtensionHost();

                var first = launcher.Open(parts.Single, host)!;
                Assert.Equal(3, host.PageSubscriberCount);
                Assert.Equal("Card Check Assistant", first.Title);
                first.Close();
                Assert.Equal(0, host.PageSubscriberCount);

                var second = launcher.Open(parts.Single, host)!;
                Assert.Equal(3, host.PageSubscriberCount);   // one functional page, not two sets of handlers

                // The reopened page works: it opened itself and read the list of orders, which ends in a list or in a
                // reason (the settings of the test run have no report folder), never in nothing.
                var view = Assert.IsType<GuidedCheckView>(second.Content);
                await WaitUntil(() => view.ViewModel.Orders.Count > 0 || view.ViewModel.HasMessages, "the first read of the list");
                Assert.DoesNotContain(view.ViewModel.Messages, message => message.Code == "workflow.unavailable");   // the real services, not the empty ones

                second.Close();
                Assert.Equal(0, host.PageSubscriberCount);
                Assert.Empty(errors);
                Assert.Empty(navigated);   // a page that names a Uri would be opened as that page, not asked for content
            });
        }

        [Fact]
        public async Task ThePageWithoutServices_SaysItHasNothingToWorkFrom()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                await AssertSaysUnavailable(CardCheckAssistantExtension.CreatePage(new FakeExtensionHost(), viewModel => new GuidedCheckView(viewModel)));
            });
        }

        [Fact]
        public async Task AWorkflowThatCannotBeBuilt_DoesNotStopThePage()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var services = new AssistantServices(
                    AssistantServices.None.ImportOffer,
                    AssistantServices.None.Editor,
                    _ => throw new InvalidOperationException("no workflow can be built"));

                await AssertSaysUnavailable(CardCheckAssistantExtension.CreatePage(new FakeExtensionHost(), viewModel => new GuidedCheckView(viewModel), _ => services));
            });
        }

        // Shows the page and waits for its first read of the list, which has nothing to read from.
        private static async Task AssertSaysUnavailable(FrameworkElement content)
        {
            var window = new Window { Content = content, Width = 900, Height = 600 };
            window.Show();
            try
            {
                var viewModel = Assert.IsType<GuidedCheckView>(content).ViewModel;
                await WaitUntil(() => viewModel.HasMessages, "the first read of the list");

                Assert.Equal("workflow.unavailable", Assert.Single(viewModel.Messages).Code);
                Assert.Empty(viewModel.Orders);
            }
            finally
            {
                window.Close();
                (content as IDisposable)?.Dispose();
            }
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
        public void TheAssembly_UsesNoMoreOfProcessThanTheShellOpenerNeeds_AndHasNoPInvokeAtAll()
        {
            // The one place that starts a program opens a document in the shell (ShellOpener). Whatever else of Process or
            // ProcessStartInfo the assembly would use (Arguments, Verb, WorkingDirectory, redirects, Kill...) fails here by
            // name, and so does every other way to start one that needs a declaration in the assembly: a P/Invoke.
            using var stream = File.OpenRead(ExtensionAssembly.Location);
            using var pe = new PEReader(stream);
            var metadata = pe.GetMetadataReader();

            var used = new Dictionary<string, List<byte[]>>();
            foreach (var handle in metadata.MemberReferences)
            {
                var member = metadata.GetMemberReference(handle);
                if (member.Parent.Kind != HandleKind.TypeReference)
                {
                    continue;
                }

                var type = metadata.GetTypeReference((TypeReferenceHandle)member.Parent);
                if (metadata.GetString(type.Namespace) == "System.Diagnostics" && metadata.GetString(type.Name).StartsWith("Process", StringComparison.Ordinal))
                {
                    var name = metadata.GetString(type.Name) + "::" + metadata.GetString(member.Name);
                    if (!used.TryGetValue(name, out var signatures))
                    {
                        used[name] = signatures = new List<byte[]>();
                    }

                    signatures.Add(metadata.GetBlobBytes(member.Signature));
                }
            }

            var allowed = new[] { "Process::Start", "ProcessStartInfo::.ctor", "ProcessStartInfo::set_FileName", "ProcessStartInfo::set_UseShellExecute" };
            Assert.Contains("Process::Start", used.Keys);   // not vacuous: the opener is in this assembly
            Assert.All(used.Keys, name => Assert.Contains(name, allowed));

            // The members by name are not enough: Start has overloads that take a program name and arguments, and the
            // constructor one that takes both.
            var start = Assert.Single(used["Process::Start"]);
            Assert.Equal(0x00, start[0]);                       // static
            Assert.Equal(1, start[1]);                          // one parameter ...
            Assert.NotEqual((byte)0x0E, start[^1]);             // ... which is not a string
            Assert.Equal(new byte[] { 0x20, 0x00, 0x01 }, Assert.Single(used["ProcessStartInfo::.ctor"]));   // instance, no parameter, void

            var imports = metadata.MethodDefinitions
                .Select(handle => metadata.GetMethodDefinition(handle))
                .Where(method => !method.GetImport().Module.IsNil)
                .Select(method => metadata.GetString(method.Name))
                .ToList();
            Assert.Empty(imports);
        }

        [Fact]
        public void ProcessAndTheOtherWaysToStartAProgram_AreNamedInNoFileOfTheExtension_ButTheShellOpener()
        {
            var root = Path.GetFullPath(Path.Combine(RepositoryRoot(), "RFiDGear.Extensions", "CardCheckAssistant"));
            var opener = Path.Combine(root, "Services", "ShellOpener.cs");
            var sep = Path.DirectorySeparatorChar;

            var offenders = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(file => file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                .Where(file => !file.Contains(sep + "obj" + sep) && !file.Contains(sep + "bin" + sep))
                .Where(file => !string.Equals(Path.GetFullPath(file), opener, StringComparison.OrdinalIgnoreCase))
                .Where(file => NamesAProgramStart(File.ReadAllText(file), file.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)))
                .Select(Path.GetFileName)
                .ToList();

            Assert.Empty(offenders);
        }

        [Fact]
        public void TheScanForProcess_SeesCode_AndNotWhatLooksLikeIt()
        {
            Assert.DoesNotContain("Process", WithoutCommentsAndStrings("var text = \"Process\"; // Process.Start\n/* ProcessStartInfo */"), StringComparison.Ordinal);
            Assert.Contains("Process", WithoutCommentsAndStrings("Process.Start(x);"), StringComparison.Ordinal);

            // Order must not matter: a // in a text is not a comment, a */ in a text does not end one, a quote in a char
            // is not a text.
            Assert.Contains("Process", WithoutCommentsAndStrings("var u = \"https://x\"; Process.Start(u);"), StringComparison.Ordinal);
            Assert.Contains("Process", WithoutCommentsAndStrings("var s = \"*/*\"; Process.Start(s); /* c */"), StringComparison.Ordinal);
            Assert.Contains("Process", WithoutCommentsAndStrings("var q = '\"'; Process.Start(q);"), StringComparison.Ordinal);
            Assert.Contains("Process", WithoutCommentsAndStrings("var v = @\"a \"\"b\"\" c\"; Process.Start(v);"), StringComparison.Ordinal);
        }

        [Fact]
        public void TheScanForProcess_ReadsTheHolesOfAnInterpolatedText_AsCode()
        {
            // The holes of $"..." are code, with braces of their own and texts inside, and the rest is text.
            Assert.True(NamesAProgramStart("var r = $\"{Process.Start(new ProcessStartInfo { FileName = p, UseShellExecute = true })}\";", false));
            Assert.True(NamesAProgramStart("var r = $\"{Get(\"a\") + Process.Start(p)}\";", false));       // a text in the hole does not end the text
            Assert.True(NamesAProgramStart("var r = $@\"a \"\"b\"\" {Process.Start(p)} c\";", false));
            Assert.True(NamesAProgramStart("var r = @$\"{Process.Start(p)}\";", false));
            Assert.True(NamesAProgramStart("var r = $\"{(a ? $\"{Process.Start(p)}\" : \"b\")}\";", false));   // an interpolated text in a hole
            Assert.False(NamesAProgramStart("var r = $\"Process: {name} and {Get(\"Process\")}\"; // Process.Start", false));

            // Whatever the scan cannot read, it does not vouch for: a raw interpolated text, and a quote that is left over.
            Assert.True(NamesAProgramStart("var r = $\"\"\"{Process.Start(p)}\"\"\";", false));
            Assert.True(NamesAProgramStart("var r = \"unfinished;", false));
            Assert.False(NamesAProgramStart("<Window x:Name=\"w\"><!-- Process --></Window>", true));
            Assert.True(NamesAProgramStart("<ObjectDataProvider MethodName=\"Start\" />", true));
        }

        // Whether a file names a way to start a program: Process and the other names, in code (not in comments and texts,
        // but in the holes of an interpolated text), or as the type's full name in a text handed to reflection.
        private static bool NamesAProgramStart(string text, bool isXaml)
        {
            const string Names = @"\b(Process|ProcessStartInfo|ShellExecute\w*|CreateProcess\w*|WinExec|DllImport|LibraryImport|NativeLibrary|ObjectDataProvider)\b";

            if (isXaml)
            {
                return Regex.IsMatch(Regex.Replace(text, @"<!--[\s\S]*?-->", string.Empty), Names);
            }

            var code = WithoutCommentsAndStrings(text);

            // A quote that is left over means the scan lost track of where a text begins and ends; a raw interpolated text
            // ($\"\"\"...\"\"\") has holes it does not read. Neither is vouched for.
            return Regex.IsMatch(code, Names)
                || text.Contains("System.Diagnostics.Process", StringComparison.Ordinal)
                || code.Contains('"')
                || Regex.IsMatch(text, @"\$+""{3,}");
        }

        // The holes of an interpolated text: {expression}, with braces of its own balanced (an object initializer) and texts
        // inside it that may hold braces.
        private const string Hole = @"\{(?:[^{}""]|""(?:[^""\\\r\n]|\\.)*""|(?<open>\{)|(?<-open>\}))*(?(open)(?!))\}";

        // Comments and texts are not code: one pass over all of them, so the first one that starts wins. An interpolated
        // text is matched whole, holes included, so that a quote inside a hole cannot end it early.
        private static readonly Regex NotCode = new Regex(
            @"(?<interpolated>\$""(?:[^""\\\r\n{}]|\\.|\{\{|\}\}|" + Hole + @")*""|(?:\$@|@\$)""(?:[^""{}]|""""|\{\{|\}\}|" + Hole + @")*"")"
            + @"|@""(?:[^""]|"""")*""|""(?:[^""\\\r\n]|\\.)*""|'(?:[^'\\\r\n]|\\.)*'|//[^\r\n]*|/\*[\s\S]*?\*/",
            RegexOptions.Compiled);

        private static readonly Regex Holes = new Regex(Hole, RegexOptions.Compiled);

        private static string WithoutCommentsAndStrings(string source) =>
            NotCode.Replace(source, match =>
            {
                if (match.Value.StartsWith("/", StringComparison.Ordinal))
                {
                    return string.Empty;
                }

                // What is left of a text is a mark; of an interpolated one, also the code of its holes (with their own texts gone).
                return match.Groups["interpolated"].Success
                    ? "\u0001" + string.Join(" ", Holes.Matches(match.Value).Select(hole => WithoutCommentsAndStrings(hole.Value.Substring(1, hole.Value.Length - 2))))
                    : "\u0001";
            });

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
        public async Task OpeningThePage_StartsNoRFiDGearOrCardCheckAssistantProcess()
        {
            // Best effort: it cannot see a child that starts and ends within the run. The static test above, which
            // shows the assembly cannot start a process at all, is the part that cannot be fooled.
            var before = CountProcesses();

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var host = new FakeExtensionHost();
                var content = new CardCheckAssistantExtension().CreateContent(host);
                try
                {
                    var viewModel = ((GuidedCheckView)content).ViewModel;
                    await viewModel.Open();
                    await WaitUntil(() => viewModel.Orders.Count > 0 || viewModel.HasMessages, "the first read of the list");
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
