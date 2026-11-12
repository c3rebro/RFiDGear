using RFiDGear.Contracts;
using RFiDGear.ExtensionSdk;
using RFiDGear.Infrastructure;
using RFiDGear.Services.SecureKeys;

using Serilog.Events;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace RFiDGear.Tests.SecureKeys
{
    /// <summary>
    /// A provider reaches the host through MEF, from an extensions folder that may contain anything. These tests compose
    /// a <see cref="MefHelper"/> through its test seam (as <c>MefHelperLoadingTests</c> does) and check that providers are
    /// found, that two extensions cannot fight over an id, and that a broken provider export costs the host nothing.
    /// </summary>
    public class SecureKeyProviderMefTests
    {
        private const string GoodFile = "RFiDGear.Extensions.A.Good.dll";
        private const string OtherFile = "RFiDGear.Extensions.B.Other.dll";

        [Fact]
        public void The_attribute_exports_the_provider_contract_and_carries_its_id_and_name()
        {
            var attribute = typeof(AcmeProvider).GetCustomAttribute<SecureKeyProviderAttribute>()!;

            Assert.Equal("acme.vault", attribute.Id);
            Assert.Equal("Acme vault", attribute.DisplayName);
            Assert.Equal(typeof(ISecureKeyProvider), attribute.ContractType);
            Assert.NotNull(typeof(SecureKeyProviderAttribute).GetCustomAttribute<MetadataAttributeAttribute>());
        }

        [Fact]
        public async Task A_provider_with_the_attribute_is_found_through_mef_and_hands_out_its_key()
        {
            using var folder = new TempFolder();
            folder.Touch(GoodFile);
            using var composed = new Composed(folder.FullPath, Catalogs((GoodFile, () => new TypeCatalog(typeof(AcmeProvider)))));

            var registry = SecureKeyRegistry.FromContainer(composed.Helper.Container, Array.Empty<SecureKeyProviderRegistration>());
            using var answer = await registry.ResolveAsync("acme.vault", SecureKeyTestData.Request(), CancellationToken.None);

            Assert.Equal(new[] { new SecureKeyProviderInfo("acme.vault", "Acme vault") }, registry.Providers);
            Assert.Equal(Convert.ToHexString(AcmeProvider.Key), Assert.IsType<SecureKeyLease>(answer.Key).ToHexString());
        }

        [Fact]
        public void Two_extension_files_that_use_one_id_give_neither_provider()
        {
            using var folder = new TempFolder();
            folder.Touch(GoodFile);
            folder.Touch(OtherFile);
            using var composed = new Composed(
                folder.FullPath,
                Catalogs(
                    (GoodFile, () => new TypeCatalog(typeof(AcmeProvider), typeof(OtherVaultProvider))),
                    (OtherFile, () => new TypeCatalog(typeof(SecondAcmeProvider)))));

            var registry = SecureKeyRegistry.FromContainer(composed.Helper.Container, Array.Empty<SecureKeyProviderRegistration>());

            Assert.Equal(new[] { "other.vault" }, registry.Providers.Select(p => p.Id));
        }

        [Fact]
        public void An_extension_whose_provider_metadata_cannot_be_read_is_skipped_and_the_other_stays()
        {
            using var logs = CapturedLogs.Install();
            using var folder = new TempFolder();
            folder.Touch(GoodFile);
            var broken = folder.Touch(OtherFile);
            using var composed = new Composed(
                folder.FullPath,
                Catalogs(
                    (GoodFile, () => new TypeCatalog(typeof(AcmeProvider))),
                    (OtherFile, () => new TypeCatalog(typeof(UnreadableMetadataProvider)))));

            var registry = SecureKeyRegistry.FromContainer(composed.Helper.Container, Array.Empty<SecureKeyProviderRegistration>());

            Assert.Equal(new[] { "acme.vault" }, registry.Providers.Select(p => p.Id));
            var skipped = Assert.Single(composed.Helper.LastLoadResults, result => !result.Loaded);
            Assert.Equal(broken, skipped.AssemblyPath);
            Assert.Contains(logs.From(nameof(MefHelper)), e => e.Level == LogEventLevel.Error && e.RenderMessage().Contains("skipped", StringComparison.Ordinal));
        }

        [Fact]
        public void A_provider_export_that_breaks_after_the_probe_costs_the_host_nothing()
        {
            using var logs = CapturedLogs.Install();
            using var folder = new TempFolder();
            var fragile = folder.Touch(GoodFile);

            // Passes its probe (the first read of the metadata), then fails like an export that changes under the host.
            // It exports only the provider contract, so only the query for providers can notice.
            using var composed = new Composed(folder.FullPath, Catalogs((GoodFile, () => new LateBreakingProviderCatalog())));

            var registry = SecureKeyRegistry.FromContainer(composed.Helper.Container, Array.Empty<SecureKeyProviderRegistration>());

            Assert.Empty(registry.Providers);
            var result = Assert.Single(composed.Helper.LastLoadResults);
            Assert.False(result.Loaded);
            Assert.Equal(fragile, result.AssemblyPath);
            Assert.IsType<InvalidOperationException>(result.Error);
            Assert.Contains(logs.From(nameof(MefHelper)), e => e.Level == LogEventLevel.Error && e.RenderMessage().Contains("export queries", StringComparison.Ordinal));
        }

        private static Func<string, ComposablePartCatalog> Catalogs(params (string File, Func<ComposablePartCatalog> Create)[] byFile) =>
            path => byFile.FirstOrDefault(entry => entry.File == Path.GetFileName(path)).Create?.Invoke() ?? new AssemblyCatalog(path);

        [SecureKeyProvider("acme.vault", "Acme vault")]
        public sealed class AcmeProvider : ISecureKeyProvider
        {
            internal static byte[] Key { get; } = Enumerable.Repeat((byte)0xAC, 16).ToArray();

            public ValueTask<SecureKeyResolution> ResolveAsync(SecureKeyRequest request, CancellationToken cancellationToken) =>
                new ValueTask<SecureKeyResolution>(SecureKeyResolution.Resolved(new SecureKeyLease(Key)));
        }

        [SecureKeyProvider("acme.vault", "Second Acme vault")]
        public sealed class SecondAcmeProvider : ISecureKeyProvider
        {
            public ValueTask<SecureKeyResolution> ResolveAsync(SecureKeyRequest request, CancellationToken cancellationToken) =>
                new ValueTask<SecureKeyResolution>(SecureKeyResolution.Failed(SecureKeyFailureReason.NotFound));
        }

        [SecureKeyProvider("other.vault", "Other vault")]
        public sealed class OtherVaultProvider : ISecureKeyProvider
        {
            public ValueTask<SecureKeyResolution> ResolveAsync(SecureKeyRequest request, CancellationToken cancellationToken) =>
                new ValueTask<SecureKeyResolution>(SecureKeyResolution.Failed(SecureKeyFailureReason.NotFound));
        }

        [Export(typeof(ISecureKeyProvider))]
        [UnreadableProviderMetadataAttribute]
        public sealed class UnreadableMetadataProvider : ISecureKeyProvider
        {
            public ValueTask<SecureKeyResolution> ResolveAsync(SecureKeyRequest request, CancellationToken cancellationToken) =>
                new ValueTask<SecureKeyResolution>(SecureKeyResolution.Failed(SecureKeyFailureReason.NotFound));
        }

        [MetadataAttribute]
        [AttributeUsage(AttributeTargets.Class)]
        public sealed class UnreadableProviderMetadataAttribute : Attribute
        {
            public string Id => throw new InvalidOperationException("The metadata cannot be read.");

            public string DisplayName => "Unreadable";
        }

        /// <summary>A catalog with one provider export whose metadata can be read once and then throws.</summary>
        private sealed class LateBreakingProviderCatalog : ComposablePartCatalog
        {
            private readonly IQueryable<ComposablePartDefinition> _parts =
                new ComposablePartDefinition[] { new LateBreakingProviderPart() }.AsQueryable();

            public override IQueryable<ComposablePartDefinition> Parts => _parts;
        }

        private sealed class LateBreakingProviderPart : ComposablePartDefinition
        {
            public override IEnumerable<ExportDefinition> ExportDefinitions { get; } = new ExportDefinition[] { new FlakyProviderExport() };

            public override IEnumerable<ImportDefinition> ImportDefinitions => Array.Empty<ImportDefinition>();

            public override ComposablePart CreatePart() => throw new NotSupportedException();
        }

        private sealed class FlakyProviderExport : ExportDefinition
        {
            private readonly Dictionary<string, object?> _metadata = new Dictionary<string, object?>
            {
                ["ExportTypeIdentity"] = AttributedModelServices.GetTypeIdentity(typeof(ISecureKeyProvider)),
                ["Id"] = "flaky.vault",
                ["DisplayName"] = "Flaky",
            };

            private int _reads;

            public override string ContractName => AttributedModelServices.GetContractName(typeof(ISecureKeyProvider));

            public override IDictionary<string, object?> Metadata =>
                Interlocked.Increment(ref _reads) == 1
                    ? _metadata
                    : throw new InvalidOperationException("The metadata changed after it was probed.");
        }

        private sealed class Composed : IDisposable
        {
            public Composed(string extensionsPath, Func<string, ComposablePartCatalog> catalogFactory)
            {
                Helper = new MefHelper(extensionsPath, typeof(IRFiDGearExtensionHost).Assembly, catalogFactory);
                Helper.Compose();
            }

            public MefHelper Helper { get; }

            public void Dispose()
            {
                // MefHelper implements IDisposable explicitly, so it is only reachable through the interface.
                IDisposable helper = Helper;
                helper.Dispose();
            }
        }

        private sealed class TempFolder : IDisposable
        {
            public TempFolder()
            {
                FullPath = Directory.CreateTempSubdirectory("RFiDGearKeyProviders").FullName;
            }

            public string FullPath { get; }

            public string Touch(string fileName)
            {
                var path = Path.Combine(FullPath, fileName);
                File.WriteAllBytes(path, Array.Empty<byte>());
                return path;
            }

            public void Dispose()
            {
                try
                {
                    Directory.Delete(FullPath, true);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    // A loaded assembly keeps its file mapped; the OS temp cleanup removes the folder later.
                }
            }
        }
    }
}
