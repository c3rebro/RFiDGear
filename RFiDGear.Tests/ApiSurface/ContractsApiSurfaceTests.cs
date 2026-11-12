#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using RFiDGear.Contracts;
using RFiDGear.UI.UIExtensions;

using Xunit;

namespace RFiDGear.Tests.ApiSurface
{
    /// <summary>
    /// Guards the public surface of <c>RFiDGear.Contracts</c>. Extensions are compiled against that assembly, so every
    /// change to a public type or member is a change to a contract: an addition is usually fine, a removal or a changed
    /// signature, nullable annotation, default value, enum value or constant breaks extensions that are already built.
    /// <para>
    /// The comparison and the way to accept an intended change are described on <see cref="ApiSurfaceGuard"/>. The
    /// approved snapshot is <c>RFiDGear.Contracts.api.approved</c>.
    /// </para>
    /// </summary>
    public class ContractsApiSurfaceTests
    {
        private const string BaselineFileName = "RFiDGear.Contracts.api.approved";

        private static readonly Assembly Contracts = typeof(IRFiDGearExtensionHost).Assembly;

        [Fact]
        public void PublicSurface_MatchesTheApprovedSnapshot()
        {
            ApiSurfaceGuard.AssertMatchesSnapshot(Contracts, BaselineFileName);
        }

        [Fact]
        public void Snapshot_IsNotEmpty_SoAnEmptiedFileCannotApproveEverything()
        {
            var expected = ApiSurfaceGuard.ReadBaseline(BaselineFileName);

            foreach (var core in new[] { "public interface RFiDGear.Contracts.ITaskExecHost", "public interface RFiDGear.Contracts.IProjectHost",
                "public interface RFiDGear.Contracts.IAppContext", "public interface RFiDGear.Contracts.IRFiDGearExtensionHost", "public record RFiDGear.Contracts.ExecResult" })
            {
                Assert.Contains(expected, line => line.StartsWith(core, StringComparison.Ordinal));
            }
        }

        [Fact]
        public void Snapshot_ListsTheSecureKeyContract_ThatProvidersAreCompiledAgainst()
        {
            var expected = ApiSurfaceGuard.ReadBaseline(BaselineFileName);

            foreach (var declaration in new[]
            {
                "public interface RFiDGear.Contracts.ISecureKeyProvider",
                "public abstract class RFiDGear.Contracts.ResolvedSecureKey",
                "public sealed class RFiDGear.Contracts.SecureKeyLease",
                "public sealed class RFiDGear.Contracts.SecureKeyRequest",
                "public sealed class RFiDGear.Contracts.SecureKeyResolution",
                "public enum RFiDGear.Contracts.SecureKeyFailureReason",
            })
            {
                Assert.Contains(expected, line => line.StartsWith(declaration, StringComparison.Ordinal));
            }
        }

        [Fact]
        public void ContractsAssembly_DependsOnlyOnTheFramework()
        {
            // An extension author must be able to reference the contract without the application. A reference to any
            // other assembly would drag it in, and make the contract's surface depend on code that is not frozen. In
            // particular the contract stays free of MEF: that belongs to the extension SDK.
            var allowed = new[] { "System", "Microsoft", "netstandard", "mscorlib", "WindowsBase" };
            var references = Contracts.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty).ToList();

            Assert.All(references, name =>
                Assert.True(
                    allowed.Any(prefix => name == prefix || name.StartsWith(prefix + ".", StringComparison.Ordinal)),
                    $"RFiDGear.Contracts references '{name}', which is not part of the framework."));
            Assert.DoesNotContain("System.ComponentModel.Composition", references);
        }
    }

    /// <summary>
    /// Guards the public surface of <c>RFiDGear.ExtensionSdk</c>, the MEF-facing extension ABI. It is versioned on its
    /// own: a change here is a separate, deliberate review from a change to the host contract.
    /// </summary>
    public class ExtensionSdkApiSurfaceTests
    {
        private const string BaselineFileName = "RFiDGear.ExtensionSdk.api.approved";

        private static readonly Assembly Sdk = typeof(UiExtensionAttribute).Assembly;

        private static readonly ApiSurfaceOptions Options = new(
            new HashSet<string>(StringComparer.Ordinal)
            {
                "RFiDGear.UI.UIExtensions",
                "RFiDGear.UI.UIExtensions.Interfaces",
            },
            new HashSet<Type>
            {
                // Without [MetadataAttribute] every [UiExtension] silently loses its MEF metadata.
                typeof(System.ComponentModel.Composition.MetadataAttributeAttribute),
                typeof(AttributeUsageAttribute),
            });

        [Fact]
        public void PublicSurface_MatchesTheApprovedSnapshot()
        {
            ApiSurfaceGuard.AssertMatchesSnapshot(Sdk, BaselineFileName, Options);
        }

        [Fact]
        public void Snapshot_ListsTheFourMovedTypes()
        {
            var expected = ApiSurfaceGuard.ReadBaseline(BaselineFileName);

            foreach (var name in new[]
            {
                "RFiDGear.UI.UIExtensions.UiExtensionAttribute",
                "RFiDGear.UI.UIExtensions.HostPlacement",
                "RFiDGear.UI.UIExtensions.Interfaces.IUIExtension",
                "RFiDGear.UI.UIExtensions.Interfaces.IUIExtensionDetails",
            })
            {
                Assert.Contains(expected, line => line.Contains(name, StringComparison.Ordinal) && !line.StartsWith("    ", StringComparison.Ordinal));
            }
        }

        [Fact]
        public void Snapshot_RecordsTheMetadataAttribute_WhichTheWholeMefContractRestsOn()
        {
            var expected = ApiSurfaceGuard.ReadBaseline(BaselineFileName);

            Assert.Contains(expected, line => line.Contains("[MetadataAttribute]", StringComparison.Ordinal)
                && line.Contains("UiExtensionAttribute", StringComparison.Ordinal));
        }

        [Fact]
        public void Snapshot_RecordsTheProviderAttributeAndItsMetadataView()
        {
            var expected = ApiSurfaceGuard.ReadBaseline(BaselineFileName);

            Assert.Contains(expected, line => line.Contains("[MetadataAttribute]", StringComparison.Ordinal)
                && line.Contains("RFiDGear.ExtensionSdk.SecureKeyProviderAttribute", StringComparison.Ordinal));
            Assert.Contains(expected, line => line.StartsWith("public interface RFiDGear.ExtensionSdk.ISecureKeyProviderMetadata", StringComparison.Ordinal));
        }

        [Fact]
        public void HostPlacement_KeepsItsExplicitValues()
        {
            // Persisted in compiled extensions as numbers: CardTask must stay 0 (the default for every extension
            // built before placement existed) and Application 1.
            Assert.Equal(0, (int)HostPlacement.CardTask);
            Assert.Equal(1, (int)HostPlacement.Application);
        }

        [Fact]
        public void SdkAssembly_DependsOnlyOnTheFrameworkAndMef()
        {
            var allowed = new[] { "System", "Microsoft", "netstandard", "mscorlib", "WindowsBase", "PresentationFramework", "PresentationCore" };
            var references = Sdk.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty).ToList();

            Assert.All(references, name =>
                Assert.True(
                    name == "RFiDGear.Contracts"
                    || allowed.Any(prefix => name == prefix || name.StartsWith(prefix + ".", StringComparison.Ordinal)),
                    $"RFiDGear.ExtensionSdk references '{name}', which is neither the framework, MEF nor the host contract."));
            Assert.DoesNotContain("RFiDGear", references);
        }
    }
}
