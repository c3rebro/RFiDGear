#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Text.Json.Serialization;

using RFiDGear.Bridge;

using Xunit;

namespace RFiDGear.Tests.ApiSurface
{
    /// <summary>
    /// Guards the public surface of <c>RFiDGear.Bridge.Contracts</c>, the assembly that an external application compiles
    /// against to talk to RFiDGear over the pipe. It is the wire protocol written as types: a method, a field, a capability
    /// name, a constant or a JSON property name that changes or disappears breaks every client that is already built. An
    /// addition is a minor change of the protocol.
    /// <para>
    /// The comparison and the way to accept an intended change are described on <see cref="ApiSurfaceGuard"/>. The approved
    /// snapshot is <c>RFiDGear.Bridge.Contracts.api.approved</c>. It records the attributes that fix the wire names
    /// (<see cref="JsonPropertyNameAttribute"/>) and the capability of each method (<see cref="CapabilityAttribute"/>).
    /// </para>
    /// </summary>
    public class BridgeContractsApiSurfaceTests
    {
        private const string BaselineFileName = "RFiDGear.Bridge.Contracts.api.approved";

        private static readonly Assembly Bridge = typeof(IBridgeService).Assembly;

        private static readonly ApiSurfaceOptions Options = new(
            new HashSet<string>(StringComparer.Ordinal) { "RFiDGear.Bridge" },
            new HashSet<Type> { typeof(JsonPropertyNameAttribute), typeof(JsonPropertyOrderAttribute) });

        [Fact]
        public void PublicSurface_MatchesTheApprovedSnapshot()
        {
            ApiSurfaceGuard.AssertMatchesSnapshot(Bridge, BaselineFileName, Options);
        }

        [Fact]
        public void Snapshot_IsNotEmpty_SoAnEmptiedFileCannotApproveEverything()
        {
            var expected = ApiSurfaceGuard.ReadBaseline(BaselineFileName);

            foreach (var core in new[]
            {
                "public interface RFiDGear.Bridge.IBridgeService",
                "public interface RFiDGear.Bridge.IBridgeClientEvents",
                "public static class RFiDGear.Bridge.BridgeProtocol",
                "public static class RFiDGear.Bridge.BridgeCapabilities",
                "public static class RFiDGear.Bridge.BridgeErrorCodes",
            })
            {
                Assert.Contains(expected, line => line.StartsWith(core, StringComparison.Ordinal));
            }
        }

        [Fact]
        public void Snapshot_FreezesTheWireNames_TheCapabilitiesAndTheConstants()
        {
            // These are what a client depends on without the compiler noticing: a name on the wire, the capability a
            // method needs, and a value of a constant. The snapshot would be of no use if it left them out.
            var expected = ApiSurfaceGuard.ReadBaseline(BaselineFileName);

            Assert.Contains(expected, line => line.Contains("JsonPropertyName(\"clientName\")", StringComparison.Ordinal));
            Assert.Contains(expected, line => line.Contains("JsonPropertyName(\"taskOutcomes\")", StringComparison.Ordinal));
            Assert.Contains(expected, line => line.Contains("Capability(\"execution.run\")", StringComparison.Ordinal));
            Assert.Contains(expected, line => line.Contains("Capability(\"project.load\")", StringComparison.Ordinal));
            Assert.Contains(expected, line => line.Contains("\"startRun\"", StringComparison.Ordinal));
            Assert.Contains(expected, line => line.Contains("1001", StringComparison.Ordinal));
        }

        [Fact]
        public void BridgeAssembly_DependsOnlyOnTheFramework()
        {
            // A client must be able to reference the contract with nothing else: not RFiDGear, not the JSON-RPC library,
            // not a second JSON library. The serializer is System.Text.Json from the framework.
            var allowed = new[] { "System", "Microsoft", "netstandard", "mscorlib" };
            var references = Bridge.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty).ToList();

            Assert.All(references, name =>
                Assert.True(
                    allowed.Any(prefix => name == prefix || name.StartsWith(prefix + ".", StringComparison.Ordinal)),
                    $"RFiDGear.Bridge.Contracts references '{name}', which is not part of the framework."));
            Assert.DoesNotContain(references, name => name.Contains("StreamJsonRpc", StringComparison.Ordinal));
            Assert.DoesNotContain(references, name => name.Contains("Newtonsoft", StringComparison.Ordinal));
            Assert.DoesNotContain(references, name => name.StartsWith("RFiDGear", StringComparison.Ordinal));
        }

        [Fact]
        public void BridgeAssembly_IsVersion1_AndStaysThatUntilTheProtocolBreaks()
        {
            Assert.Equal(new Version(1, 0, 0, 0), Bridge.GetName().Version);
            Assert.Equal(1, BridgeProtocol.Major);
        }

        [Fact]
        public void BridgeAssembly_IsAnyCpu_WhateverPlatformTheInstallersBuildWith()
        {
            // The installers build the solution with Platform=x64 and Platform=x86, and the SDK would stamp the assembly with
            // that bitness. A client of the other bitness takes the copy from the install folder and could not load it.
            // The built assembly here is of the platform of the test run, so the project file is what has to be pinned.
            Bridge.ManifestModule.GetPEKind(out var kind, out var machine);
            Assert.Equal(PortableExecutableKinds.ILOnly, kind);
            Assert.Equal(ImageFileMachine.I386, machine);

            var project = FindUpwards("RFiDGear.Bridge.Contracts", "RFiDGear.Bridge.Contracts.csproj");
            Assert.NotNull(project);
            Assert.Contains("<PlatformTarget>AnyCPU</PlatformTarget>", System.IO.File.ReadAllText(project!), StringComparison.Ordinal);
        }

        private static string? FindUpwards(string folder, string file)
        {
            for (var directory = new System.IO.DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                var candidate = System.IO.Path.Combine(directory.FullName, folder, file);
                if (System.IO.File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        [Fact]
        public void BridgeAssembly_NeedsNeitherWindowsNorTheRFiDGearPrefixOfAnExtension()
        {
            // The assembly is shipped next to RFiDGear.exe. MefHelper loads files named RFiDGear.Extensions*.dll from the
            // extensions folder as extensions; this name must never match (see CLAUDE.md), and it is a plain net10.0 assembly.
            var name = Bridge.GetName().Name ?? string.Empty;

            Assert.False(name.StartsWith("RFiDGear.Extension", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(Bridge.GetReferencedAssemblies(), r => string.Equals(r.Name, "WindowsBase", StringComparison.Ordinal));
        }
    }
}
