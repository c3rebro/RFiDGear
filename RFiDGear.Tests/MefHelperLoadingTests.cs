using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading;

using RFiDGear.Contracts;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.DI;
using RFiDGear.UI.UIExtensions;
using RFiDGear.UI.UIExtensions.Interfaces;

using Serilog.Events;
using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// A broken extension must never stop the others, or the application, from loading. These tests compose a
    /// <see cref="MefHelper"/> through its test seam over a neutral host assembly (the MEF-free Contracts assembly
    /// exports nothing) and break the extensions folder in each way an extension author or a user can.
    /// </summary>
    public class MefHelperLoadingTests
    {
        private const string GoodFile = "RFiDGear.Extensions.A.Good.dll";
        private const string BadFile = "RFiDGear.Extensions.B.Bad.dll";
        private const string LegacyAppFixtureFile = "RFiDGear.Extensions.LegacyAppFixture.dll";

        [Fact]
        public void Compose_WithNoExtensions_StartsWithTheHostOnly()
        {
            using var folder = new ExtensionsFolder();
            using var composed = new Composed(folder.FullPath);

            Assert.Empty(composed.Extensions);
            Assert.Empty(composed.Helper.LastLoadResults);
            Assert.NotNull(composed.Helper.Container);
        }

        [Fact]
        public void Compose_WithAMissingFolder_StartsWithTheHostOnly()
        {
            using var logs = CapturedLogs.Install();
            var missing = Path.Combine(Path.GetTempPath(), "RFiDGearMefMissing", Guid.NewGuid().ToString("N"));

            using var composed = new Composed(missing);

            Assert.Empty(composed.Extensions);
            Assert.Empty(composed.Helper.LastLoadResults);
            Assert.False(Directory.Exists(missing), "composing must not create the folder");
            Assert.DoesNotContain(logs.From(nameof(MefHelper)), e => e.Level >= LogEventLevel.Error);
        }

        [Fact]
        public void Compose_LoadsAnExtensionFromDisk_WithTheDefaultCatalogFactory()
        {
            using var folder = new ExtensionsFolder();
            folder.Copy(FixturePath(LegacyAppFixtureFile), LegacyAppFixtureFile);

            using var composed = new Composed(folder.FullPath);

            Assert.Equal(new[] { "Legacy App" }, composed.Names);
            var result = Assert.Single(composed.Helper.LastLoadResults);
            Assert.True(result.Loaded);
            Assert.Equal(1, result.PartCount);
            Assert.Null(result.Error);
            Assert.EndsWith(LegacyAppFixtureFile, result.AssemblyPath, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Compose_SkipsAFileThatIsNotAnAssembly_AndLogsIt()
        {
            using var logs = CapturedLogs.Install();
            using var folder = new ExtensionsFolder();
            var corrupt = folder.WriteBytes(BadFile, "this is not a dll"u8.ToArray());

            using var composed = new Composed(folder.FullPath);

            Assert.Empty(composed.Extensions);
            var result = Assert.Single(composed.Helper.LastLoadResults);
            Assert.False(result.Loaded);
            Assert.IsType<BadImageFormatException>(result.Error);
            AssertLoggedAsSkipped(logs, corrupt, result.Error);
        }

        [Fact]
        public void Compose_SkipsANativeDll_AndLogsIt()
        {
            using var logs = CapturedLogs.Install();
            using var folder = new ExtensionsFolder();
            var native = folder.Copy(Path.Combine(Environment.SystemDirectory, "kernel32.dll"), BadFile);

            using var composed = new Composed(folder.FullPath);

            Assert.Empty(composed.Extensions);
            var result = Assert.Single(composed.Helper.LastLoadResults);
            Assert.False(result.Loaded);
            Assert.IsType<BadImageFormatException>(result.Error);
            AssertLoggedAsSkipped(logs, native, result.Error);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Compose_KeepsTheGoodExtension_WhicheverSideOfTheBadOneItIsOn(bool goodFirst)
        {
            using var logs = CapturedLogs.Install();
            using var folder = new ExtensionsFolder();
            var goodName = goodFirst ? "RFiDGear.Extensions.A.dll" : "RFiDGear.Extensions.Z.dll";
            var badName = goodFirst ? "RFiDGear.Extensions.Z.dll" : "RFiDGear.Extensions.A.dll";
            folder.Touch(goodName);
            var bad = folder.WriteBytes(badName, "garbage"u8.ToArray());

            var factory = FactoryFor(new Dictionary<string, Func<ComposablePartCatalog>>
            {
                [goodName] = () => new TypeCatalog(typeof(InProcessGoodExtension)),
            });

            using var composed = new Composed(folder.FullPath, factory);

            Assert.Equal(new[] { "In-process good" }, composed.Names);
            Assert.Equal(2, composed.Helper.LastLoadResults.Count);
            Assert.Single(composed.Helper.LastLoadResults, r => r.Loaded);
            var skipped = Assert.Single(composed.Helper.LastLoadResults, r => !r.Loaded);
            Assert.Equal(bad, skipped.AssemblyPath);
            AssertLoggedAsSkipped(logs, bad, skipped.Error);
        }

        [Fact]
        public void Compose_SkipsAnExtensionWhoseMetadataThrows_AndKeepsTheOthers()
        {
            using var logs = CapturedLogs.Install();
            using var folder = new ExtensionsFolder();
            folder.Touch(GoodFile);
            var broken = folder.Touch(BadFile);

            var factory = FactoryFor(new Dictionary<string, Func<ComposablePartCatalog>>
            {
                [GoodFile] = () => new TypeCatalog(typeof(InProcessGoodExtension)),
                [BadFile] = () => new TypeCatalog(typeof(ThrowingMetadataExtension)),
            });

            using var composed = new Composed(folder.FullPath, factory);

            Assert.Equal(new[] { "In-process good" }, composed.Names);
            var skipped = Assert.Single(composed.Helper.LastLoadResults, r => !r.Loaded);
            Assert.Equal(broken, skipped.AssemblyPath);
            Assert.NotNull(skipped.Error);
            AssertLoggedAsSkipped(logs, broken, skipped.Error);
        }

        [Fact]
        public void Compose_DoesNotRunAnExtensionsConstructor()
        {
            using var folder = new ExtensionsFolder();
            folder.Touch(BadFile);
            ThrowingConstructorExtension.Constructed = 0;

            var factory = FactoryFor(new Dictionary<string, Func<ComposablePartCatalog>>
            {
                [BadFile] = () => new TypeCatalog(typeof(ThrowingConstructorExtension)),
            });

            using var composed = new Composed(folder.FullPath, factory);

            // Start-up only reads metadata. A constructor that throws is only reached when a user opens the
            // extension, and is handled there (see the activation PR), never while the application starts.
            Assert.Equal(new[] { "Throwing constructor" }, composed.Names);
            Assert.Equal(0, ThrowingConstructorExtension.Constructed);
            Assert.True(Assert.Single(composed.Helper.LastLoadResults).Loaded);
        }

        [Fact]
        public void Compose_RejectsAnExtensionWithAnUnsatisfiedImport_WithoutAffectingTheOthers()
        {
            using var folder = new ExtensionsFolder();
            folder.Touch(GoodFile);
            folder.Touch(BadFile);

            var factory = FactoryFor(new Dictionary<string, Func<ComposablePartCatalog>>
            {
                [GoodFile] = () => new TypeCatalog(typeof(InProcessGoodExtension)),
                [BadFile] = () => new TypeCatalog(typeof(UnsatisfiedImportExtension)),
            });

            using var composed = new Composed(folder.FullPath, factory);

            Assert.Equal(new[] { "In-process good" }, composed.Names);
        }

        [Fact]
        public void Compose_SkipsAnExtensionWhoseImportsCannotBeEvaluated_AndKeepsTheOthers()
        {
            using var logs = CapturedLogs.Install();
            using var folder = new ExtensionsFolder();
            folder.Touch(GoodFile);
            var invalid = folder.Touch(BadFile);

            // MEF evaluates a part's imports lazily, and no attribute shape makes that throw in-process (the usual
            // real cause is an import whose type cannot be loaded), so the part definition is written by hand.
            var factory = FactoryFor(new Dictionary<string, Func<ComposablePartCatalog>>
            {
                [GoodFile] = () => new TypeCatalog(typeof(InProcessGoodExtension)),
                [BadFile] = () => new UnevaluableImportsCatalog(),
            });

            using var composed = new Composed(folder.FullPath, factory);

            Assert.Equal(new[] { "In-process good" }, composed.Names);
            var skipped = Assert.Single(composed.Helper.LastLoadResults, r => !r.Loaded);
            AssertLoggedAsSkipped(logs, invalid, skipped.Error);
        }

        [Fact]
        public void Compose_FallsBackToTheHostAlone_WhenTheLoadedExtensionsBreakTheExportQueries()
        {
            using var logs = CapturedLogs.Install();
            using var folder = new ExtensionsFolder();
            var fragile = folder.Touch(GoodFile);

            // Passes its own probe (the first read of Parts), then fails the way a catalog that changes under the
            // host would: on every later read.
            var factory = FactoryFor(new Dictionary<string, Func<ComposablePartCatalog>>
            {
                [GoodFile] = () => new SingleReadCatalog(new TypeCatalog(typeof(InProcessGoodExtension))),
            });

            using var composed = new Composed(folder.FullPath, factory);

            Assert.Empty(composed.Extensions);
            Assert.NotNull(composed.Helper.Container);
            var result = Assert.Single(composed.Helper.LastLoadResults);
            Assert.False(result.Loaded);
            Assert.Equal(fragile, result.AssemblyPath);
            Assert.IsType<InvalidOperationException>(result.Error);

            var logged = Assert.Single(
                logs.From(nameof(MefHelper)),
                e => e.Level == LogEventLevel.Error && e.RenderMessage().Contains("export queries", StringComparison.Ordinal));
            Assert.Same(result.Error, logged.Exception);
        }

        [Fact]
        public void Compose_ReportsEveryFileItConsidered()
        {
            using var folder = new ExtensionsFolder();
            folder.Touch("RFiDGear.Extensions.One.dll");
            folder.WriteBytes("RFiDGear.Extensions.Two.dll", "garbage"u8.ToArray());
            folder.Touch("RFiDGear.Extensions.Three.dll");
            folder.Touch("NotAnExtension.dll");

            var factory = FactoryFor(new Dictionary<string, Func<ComposablePartCatalog>>
            {
                ["RFiDGear.Extensions.One.dll"] = () => new TypeCatalog(typeof(InProcessGoodExtension)),
                ["RFiDGear.Extensions.Three.dll"] = () => new TypeCatalog(),
            });

            using var composed = new Composed(folder.FullPath, factory);

            var byFile = composed.Helper.LastLoadResults.ToDictionary(
                r => Path.GetFileName(r.AssemblyPath),
                StringComparer.OrdinalIgnoreCase);
            Assert.Equal(3, byFile.Count);
            Assert.True(byFile["RFiDGear.Extensions.One.dll"].Loaded);
            Assert.Equal(1, byFile["RFiDGear.Extensions.One.dll"].PartCount);
            Assert.False(byFile["RFiDGear.Extensions.Two.dll"].Loaded);
            Assert.True(byFile["RFiDGear.Extensions.Three.dll"].Loaded);
            Assert.Equal(0, byFile["RFiDGear.Extensions.Three.dll"].PartCount);
        }

        [Fact]
        public void Constructor_RejectsAMissingHostAssembly()
        {
            Assert.Throws<ArgumentNullException>(() => new MefHelper("anywhere", null!));
        }

        [Fact]
        public void ViewModelLocator_LogsTheFailureInsteadOfWritingToTheConsole()
        {
            using var logs = CapturedLogs.Install();
            var name = "NoSuchViewModel_" + Guid.NewGuid().ToString("N");

            var handled = new ViewModelLocator().TryGetMember(new NameOnlyBinder(name), out var resolved);

            // The lookup keeps its old contract (no exception, no view model); what changes is that the reason is
            // now in the log file instead of a console nobody reads.
            Assert.True(handled);
            Assert.Null(resolved);
            var logged = Assert.Single(
                logs.From(nameof(ViewModelLocator)),
                e => e.Level == LogEventLevel.Error);
            Assert.NotNull(logged.Exception);
            Assert.Equal(name, Assert.IsType<ScalarValue>(logged.Properties["ViewModelName"]).Value);
        }

        private static void AssertLoggedAsSkipped(CapturedLogs logs, string assemblyPath, Exception? error)
        {
            var logged = Assert.Single(
                logs.From(nameof(MefHelper)),
                e => e.Level == LogEventLevel.Error
                    && e.Properties.TryGetValue("ExtensionAssembly", out var value)
                    && value is ScalarValue { Value: string path }
                    && string.Equals(path, assemblyPath, StringComparison.OrdinalIgnoreCase));

            Assert.Same(error, logged.Exception);
            Assert.Contains("skipped", logged.RenderMessage(), StringComparison.Ordinal);
        }

        private static Func<string, ComposablePartCatalog> FactoryFor(
            IReadOnlyDictionary<string, Func<ComposablePartCatalog>> catalogsByFileName) =>
            path => catalogsByFileName.TryGetValue(Path.GetFileName(path), out var create)
                ? create()
                : new AssemblyCatalog(path);

        private static string FixturePath(string file) =>
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "LegacyExtensions", file);

        /// <summary>A composed helper plus the extensions a surrogate importer receives from it.</summary>
        private sealed class Composed : IDisposable
        {
            public Composed(string extensionsPath, Func<string, ComposablePartCatalog>? catalogFactory = null)
            {
                Helper = new MefHelper(extensionsPath, typeof(IRFiDGearExtensionHost).Assembly, catalogFactory);
                Helper.Compose();

                var importer = new ExtensionImporter();
                Helper.Container.ComposeParts(importer);
                Extensions = importer.Extensions.ToList();
            }

            public MefHelper Helper { get; }

            public IReadOnlyList<Lazy<IUIExtension, IUIExtensionDetails>> Extensions { get; }

            public IReadOnlyList<string> Names =>
                Extensions.Select(e => e.Metadata.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();

            public void Dispose() => ((IDisposable)Helper).Dispose();
        }

        /// <summary>Stands in for the host's own import, which the real MainWindowViewModel cannot be used for.</summary>
        private sealed class ExtensionImporter
        {
            [ImportMany]
            public IEnumerable<Lazy<IUIExtension, IUIExtensionDetails>> Extensions { get; set; } =
                Array.Empty<Lazy<IUIExtension, IUIExtensionDetails>>();
        }

        private sealed class ExtensionsFolder : IDisposable
        {
            public ExtensionsFolder()
            {
                FullPath = Directory.CreateTempSubdirectory("RFiDGearMef").FullName;
            }

            public string FullPath { get; }

            public string Touch(string fileName) => WriteBytes(fileName, Array.Empty<byte>());

            public string WriteBytes(string fileName, byte[] bytes)
            {
                var path = Path.Combine(FullPath, fileName);
                File.WriteAllBytes(path, bytes);
                return path;
            }

            public string Copy(string source, string fileName)
            {
                var path = Path.Combine(FullPath, fileName);
                File.Copy(source, path);
                return path;
            }

            public void Dispose()
            {
                try
                {
                    Directory.Delete(FullPath, true);
                }
                catch (IOException)
                {
                    // A loaded assembly keeps its file mapped; the OS temp cleanup removes the folder later.
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        /// <summary>Carries just a member name, which is all <see cref="ViewModelLocator.TryGetMember"/> reads.</summary>
        private sealed class NameOnlyBinder : GetMemberBinder
        {
            public NameOnlyBinder(string name)
                : base(name, false)
            {
            }

            public override DynamicMetaObject FallbackGetMember(DynamicMetaObject target, DynamicMetaObject? errorSuggestion) =>
                throw new NotSupportedException();
        }

        /// <summary>Allows exactly one read of <see cref="Parts"/> (the probe), then fails like a catalog that changed.</summary>
        private sealed class SingleReadCatalog : ComposablePartCatalog
        {
            private readonly ComposablePartCatalog _inner;
            private int _reads;

            public SingleReadCatalog(ComposablePartCatalog inner)
            {
                _inner = inner;
            }

            public override IQueryable<ComposablePartDefinition> Parts =>
                Interlocked.Increment(ref _reads) == 1
                    ? _inner.Parts
                    : throw new InvalidOperationException("The catalog changed after it was probed.");
        }

        [UiExtension(Name = "In-process good")]
        public sealed class InProcessGoodExtension : IUIExtension
        {
        }

        [UiExtension(Name = "Throwing constructor")]
        public sealed class ThrowingConstructorExtension : IUIExtension
        {
            public static int Constructed;

            public ThrowingConstructorExtension()
            {
                Interlocked.Increment(ref Constructed);
                throw new InvalidOperationException("The constructor always fails.");
            }
        }

        [UiExtension(Name = "Unsatisfied import")]
        public sealed class UnsatisfiedImportExtension : IUIExtension
        {
            [Import("RFiDGear.Tests.NoSuchContract")]
            public object? Missing { get; set; }
        }

        /// <summary>A catalog with one part that exports <see cref="IUIExtension"/> but whose imports throw.</summary>
        private sealed class UnevaluableImportsCatalog : ComposablePartCatalog
        {
            private readonly IQueryable<ComposablePartDefinition> _parts =
                new ComposablePartDefinition[] { new UnevaluableImportsPart() }.AsQueryable();

            public override IQueryable<ComposablePartDefinition> Parts => _parts;
        }

        private sealed class UnevaluableImportsPart : ComposablePartDefinition
        {
            public override IEnumerable<ExportDefinition> ExportDefinitions { get; } = new[]
            {
                new ExportDefinition(
                    AttributedModelServices.GetContractName(typeof(IUIExtension)),
                    new Dictionary<string, object?>
                    {
                        ["ExportTypeIdentity"] = AttributedModelServices.GetTypeIdentity(typeof(IUIExtension)),
                        ["Name"] = "Unevaluable imports",
                    }),
            };

            public override IEnumerable<ImportDefinition> ImportDefinitions =>
                throw new TypeLoadException("An imported type cannot be loaded.");

            public override ComposablePart CreatePart() => throw new NotSupportedException();
        }

        [Export(typeof(IUIExtension))]
        [ThrowingMetadata]
        public sealed class ThrowingMetadataExtension : IUIExtension
        {
        }

        [MetadataAttribute]
        [AttributeUsage(AttributeTargets.Class)]
        public sealed class ThrowingMetadataAttribute : Attribute
        {
            public string Name => throw new InvalidOperationException("The metadata cannot be read.");
        }
    }
}
