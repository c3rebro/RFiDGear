#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Threading.Tasks;

using RFiDGear.UI.UIExtensions;
using RFiDGear.UI.UIExtensions.Interfaces;
using RFiDGear.ViewModel.TaskSetupViewModels;

using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// Extensions that were compiled against RFiDGear.exe, before the extension SDK existed, must keep working
    /// without being rebuilt. The binaries under <c>Fixtures/LegacyExtensions</c> are exactly that: see the README
    /// next to them for how they were produced and why the sample carries its own assembly name. They are loaded
    /// with <see cref="Assembly.LoadFrom(string)"/>, which is how MEF loads a real extension.
    /// </summary>
    [Collection("SettingsFileAccess")]
    public class LegacyExtensionCompatibilityTests
    {
        private const string SampleFile = "RFiDGear.Extensions.LegacyDesfireSample.dll";
        private const string AppFixtureFile = "RFiDGear.Extensions.LegacyAppFixture.dll";

        // Pinned: these must never be regenerated from current sources (see the README).
        private const string SampleSha256 = "d1428043a548e425a58df32f313d1dd1ee15fda5d6444b34eaf1133496977847";
        private const string AppFixtureSha256 = "83295450d586bb7cc02deafbab81a6a128be7daa7e68bd1e6cbf6eb27c8167b3";

        // ── the fixtures are what they claim to be ──

        [Theory]
        [InlineData(SampleFile, SampleSha256)]
        [InlineData(AppFixtureFile, AppFixtureSha256)]
        public void Fixture_IsTheExactPinnedBinary(string file, string expectedSha256)
        {
            var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(FixturePath(file)))).ToLowerInvariant();

            Assert.Equal(expectedSha256, actual);
        }

        [Theory]
        [InlineData(SampleFile)]
        [InlineData(AppFixtureFile)]
        public void Fixture_WasBuiltAgainstTheExecutable_NotTheSdk(string file)
        {
            var metadata = ReadMetadata(FixturePath(file));

            Assert.Contains("RFiDGear", metadata.AssemblyReferences);
            Assert.DoesNotContain("RFiDGear.ExtensionSdk", metadata.AssemblyReferences);
            Assert.Equal("RFiDGear", metadata.ScopeOf("RFiDGear.UI.UIExtensions.UiExtensionAttribute"));
            Assert.Equal("RFiDGear", metadata.ScopeOf("RFiDGear.UI.UIExtensions.Interfaces.IUIExtension"));
        }

        [Fact]
        public void AppFixture_ReferencesTheHostPlacementEnumThroughTheExecutable()
        {
            // The enum named argument of [UiExtension(HostPlacement = ...)] is what a forwarder must resolve.
            var metadata = ReadMetadata(FixturePath(AppFixtureFile));

            Assert.Equal("RFiDGear", metadata.ScopeOf("RFiDGear.UI.UIExtensions.HostPlacement"));
        }

        // ── MEF sees the legacy metadata ──

        [Fact]
        public void Mef_ReadsTheLegacyCardTaskMetadata_AndDefaultsToCardTask()
        {
            using var container = ContainerFor(LoadLegacy(SampleFile));

            var export = Assert.Single(container.GetExports<IUIExtension, IUIExtensionDetails>());

            Assert.Equal("DESFire Sample Plugin", export.Metadata.Name);
            Assert.Equal("Mifare DESFire", export.Metadata.Category);
            Assert.Equal(100, export.Metadata.SortOrder);
            Assert.Equal(
                "pack://application:,,,/RFiDGear.Extensions.DesfirePluginSample;component/View/DesfireSampleView.xaml",
                export.Metadata.Uri);
            Assert.Null(export.Metadata.IconUri);
            Assert.Equal(HostPlacement.CardTask, export.Metadata.HostPlacement);
        }

        [Fact]
        public void Mef_ReadsTheLegacyApplicationPlacement()
        {
            using var container = ContainerFor(LoadLegacy(AppFixtureFile));

            var export = Assert.Single(container.GetExports<IUIExtension, IUIExtensionDetails>());

            Assert.Equal("Legacy App", export.Metadata.Name);
            Assert.Equal(50, export.Metadata.SortOrder);
            Assert.Equal(HostPlacement.Application, export.Metadata.HostPlacement);
        }

        [Theory]
        [InlineData(SampleFile)]
        [InlineData(AppFixtureFile)]
        public void LegacyParts_Instantiate_AsTheExtensionContract(string file)
        {
            using var container = ContainerFor(LoadLegacy(file));

            var export = Assert.Single(container.GetExports<IUIExtension, IUIExtensionDetails>());

            Assert.IsAssignableFrom<IUIExtension>(export.Value);
        }

        // ── the host's own consumers of those metadata ──

        [Fact]
        public async Task CardTaskConsumers_KeepTheLegacyCardTaskExtension_AndIgnoreTheApplicationOne()
        {
            var all = ExportsOf(SampleFile).Concat(ExportsOf(AppFixtureFile)).ToArray();

            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var viewModel = new MifareDesfireSetupViewModel();

                viewModel.Items = all;

                var kept = Assert.Single(viewModel.Items);
                Assert.Equal("DESFire Sample Plugin", kept.Metadata.Name);
            });
        }

        [Fact]
        public async Task ApplicationMenu_ShowsTheLegacyApplicationExtension_AndIgnoresTheCardTaskOne()
        {
            var all = ExportsOf(SampleFile).Concat(ExportsOf(AppFixtureFile)).ToArray();
            var tempRoot = Path.Combine(Path.GetTempPath(), "RFiDGear.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            try
            {
                await StaTestRunner.RunOnStaThreadAsync(async () =>
                {
                    var viewModel = await MainWindowViewModelTests.CreateInitializedViewModelAsync(tempRoot);

                    viewModel.ApplicationExtensionItems = all;

                    var item = Assert.Single(viewModel.ApplicationExtensionMenuItems);
                    Assert.Equal("Legacy App", item.Header);
                    Assert.True(viewModel.HasApplicationExtensions);
                });
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        // ── helpers ──

        private static string FixturePath(string file) =>
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "LegacyExtensions", file);

        private static Assembly LoadLegacy(string file) => Assembly.LoadFrom(FixturePath(file));

        private static CompositionContainer ContainerFor(Assembly assembly) =>
            new CompositionContainer(new AssemblyCatalog(assembly));

        private static IReadOnlyList<Lazy<IUIExtension, IUIExtensionDetails>> ExportsOf(string file)
        {
            // The container stays alive for the test: the Lazy exports belong to it.
            var container = ContainerFor(LoadLegacy(file));
            return container.GetExports<IUIExtension, IUIExtensionDetails>().ToList();
        }

        private static FixtureMetadata ReadMetadata(string path)
        {
            using var stream = File.OpenRead(path);
            using var pe = new PEReader(stream);
            var reader = pe.GetMetadataReader();

            var assemblies = reader.AssemblyReferences
                .Select(h => reader.GetString(reader.GetAssemblyReference(h).Name))
                .ToList();

            var scopes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var handle in reader.TypeReferences)
            {
                var typeRef = reader.GetTypeReference(handle);
                if (typeRef.ResolutionScope.Kind != HandleKind.AssemblyReference)
                {
                    continue;
                }

                var scope = reader.GetAssemblyReference((AssemblyReferenceHandle)typeRef.ResolutionScope);
                scopes[reader.GetString(typeRef.Namespace) + "." + reader.GetString(typeRef.Name)] = reader.GetString(scope.Name);
            }

            return new FixtureMetadata(assemblies, scopes);
        }

        private sealed record FixtureMetadata(IReadOnlyList<string> AssemblyReferences, IReadOnlyDictionary<string, string> Scopes)
        {
            public string? ScopeOf(string fullTypeName) => Scopes.TryGetValue(fullTypeName, out var scope) ? scope : null;
        }
    }
}
