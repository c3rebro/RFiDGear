using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Infrastructure;

using Xunit;

namespace RFiDGear.Tests.CardCheck
{
    /// <summary>
    /// The boundary of <c>RFiDGear.ExtensionLibraries.CardCheck</c>: a plain, UI-free class library that an
    /// extension loads from the extensions folder next to itself. These tests keep it that way, so a change that
    /// would make it fail to load there (a desktop framework, a private copy of a host-owned package) or leak into
    /// the host (a file name the extension scan would hand to MEF) fails here, not on an operator's machine.
    /// </summary>
    public sealed class CardCheckCoreBoundaryTests
    {
        private const string LibraryName = "RFiDGear.ExtensionLibraries.CardCheck";

        private static readonly Assembly Core = typeof(CardCheckServices).Assembly;

        /// <summary>Packages the library may reference; a new one is a decision, not an accident.</summary>
        private static readonly string[] AllowedPackages = { "Serilog", "GemBox.Pdf", "System.Data.SqlClient" };

        /// <summary>Packages the host loads itself: the library compiles against them and never ships a copy.</summary>
        private static readonly string[] HostOwnedPackages = { "Serilog", "GemBox.Pdf" };

        private static readonly string[] ForbiddenReferencePrefixes =
        {
            "PresentationFramework", "PresentationCore", "WindowsBase", "System.Windows", "System.Xaml",
            "Microsoft.UI", "Microsoft.Windows", "WinRT", "Elatec", "LibLogicalAccess", "RFiDGear",
            "CommunityToolkit", "Newtonsoft", "System.Diagnostics.Process", "System.Diagnostics.EventLog",
        };

        private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

        private static string LibraryFolder() => Path.Combine(RepositoryRoot(), "RFiDGear.ExtensionLibraries", "CardCheck");

        private static XDocument LibraryProject() => XDocument.Load(Path.Combine(LibraryFolder(), LibraryName + ".csproj"));

        private static XDocument HostProject() => XDocument.Load(Path.Combine(RepositoryRoot(), "RFiDGear", "RFiDGear.csproj"));

        private static IEnumerable<XElement> PackageReferences(XDocument project) =>
            project.Descendants("PackageReference");

        private static string PackageName(XElement reference) => (string)reference.Attribute("Include")!;

        // ── the project file ────────────────────────────────────────────────

        [Fact]
        public void Project_TargetsPlainNet10_WithoutAnyDesktopFramework()
        {
            var project = LibraryProject();

            Assert.Equal("net10.0", Assert.Single(project.Descendants("TargetFramework")).Value);
            foreach (var desktop in new[] { "UseWPF", "UseWindowsForms", "UseWinUI", "EnableWindowsTargeting", "TargetPlatformVersion" })
            {
                Assert.Empty(project.Descendants(desktop));
            }
        }

        [Fact]
        public void Project_ReferencesNoOtherProject_SoNothingOfTheHostCanLeakIn()
        {
            Assert.Empty(LibraryProject().Descendants("ProjectReference"));
        }

        [Fact]
        public void Project_ReferencesOnlyThePackagesItIsAllowedTo()
        {
            var names = PackageReferences(LibraryProject()).Select(PackageName).ToList();

            Assert.NotEmpty(names);
            Assert.All(names, name => Assert.Contains(name, AllowedPackages));
        }

        [Fact]
        public void Project_CompilesAgainstHostOwnedPackagesWithoutCopyingThem()
        {
            var hostOwned = PackageReferences(LibraryProject()).Where(r => HostOwnedPackages.Contains(PackageName(r))).ToList();

            Assert.NotEmpty(hostOwned);
            foreach (var reference in hostOwned)
            {
                var excluded = ((string?)reference.Attribute("ExcludeAssets") ?? string.Empty)
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                Assert.Contains("runtime", excluded);
                Assert.Contains("native", excluded);
                Assert.Equal("all", (string?)reference.Attribute("PrivateAssets"));
            }
        }

        [Fact]
        public void Project_HostOwnedPackagesHaveExactlyTheVersionTheHostLoads()
        {
            var hostVersions = PackageReferences(HostProject()).ToDictionary(PackageName, r => (string?)r.Attribute("Version"));

            foreach (var reference in PackageReferences(LibraryProject()).Where(r => HostOwnedPackages.Contains(PackageName(r))))
            {
                Assert.True(hostVersions.TryGetValue(PackageName(reference), out var hostVersion), $"{PackageName(reference)} is not a host package");
                Assert.Equal(hostVersion, (string?)reference.Attribute("Version"));
            }
        }

        [Fact]
        public void Project_DoesNotCopyThePackageClosureNextToTheLibrary()
        {
            Assert.Equal("false", Assert.Single(LibraryProject().Descendants("CopyLocalLockFileAssemblies")).Value);
        }

        // ── the built assembly ──────────────────────────────────────────────

        private static List<string> ReferencedAssemblyNames()
        {
            using var stream = File.OpenRead(Core.Location);
            using var pe = new PEReader(stream);
            var metadata = pe.GetMetadataReader();

            return metadata.AssemblyReferences
                .Select(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name))
                .ToList();
        }

        [Fact]
        public void Assembly_ReferencesNoDesktopFrameworkNoReaderSdkAndNothingOfTheHost()
        {
            var references = ReferencedAssemblyNames();

            Assert.NotEmpty(references);
            foreach (var forbidden in ForbiddenReferencePrefixes)
            {
                Assert.DoesNotContain(references, name => name.StartsWith(forbidden, StringComparison.OrdinalIgnoreCase));
            }
        }

        [Fact]
        public void Assembly_ReferencesOnlyTheFrameworkAndTheAllowedPackages()
        {
            var allowedNames = new[] { "mscorlib", "netstandard", "Serilog", "GemBox.Pdf" };

            Assert.All(
                ReferencedAssemblyNames(),
                name => Assert.True(
                    name.StartsWith("System", StringComparison.Ordinal) || allowedNames.Contains(name),
                    $"unexpected reference: {name}"));
        }

        [Fact]
        public void PublicSurface_ExposesNoThirdPartyType()
        {
            var offenders = new List<string>();

            foreach (var type in Core.GetExportedTypes())
            {
                foreach (var used in TypesInPublicSignaturesOf(type))
                {
                    if (!IsFrameworkOrOwn(used))
                    {
                        offenders.Add($"{type.FullName} exposes {used.FullName} ({used.Assembly.GetName().Name})");
                    }
                }
            }

            Assert.Empty(offenders);
        }

        [Fact]
        public void PublicSurfaceGuard_DoesNotTakeTheDatabaseDriverForTheFramework()
        {
            // It starts with "System" but is a package the library ships beside itself; a public member that exposed one
            // of its types would make every consumer bind to it.
            Assert.False(IsFrameworkOrOwn(typeof(System.Data.SqlClient.SqlException)));
            Assert.True(IsFrameworkOrOwn(typeof(System.Data.Common.DbConnection)));
        }

        private static bool IsFrameworkOrOwn(Type type)
        {
            var assembly = type.Assembly;
            var name = assembly.GetName().Name ?? string.Empty;

            return assembly == Core
                || name == "mscorlib"
                || name == "netstandard"
                || (name.StartsWith("System", StringComparison.Ordinal) && name != "System.Data.SqlClient");
        }

        private static IEnumerable<Type> TypesInPublicSignaturesOf(Type type)
        {
            var found = new List<Type>();

            if (type.BaseType != null)
            {
                found.Add(type.BaseType);
            }

            found.AddRange(type.GetInterfaces());

            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            foreach (var member in type.GetMembers(all))
            {
                switch (member)
                {
                    case MethodBase method when method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly:
                        found.AddRange(method.GetParameters().Select(p => p.ParameterType));
                        if (method is MethodInfo info)
                        {
                            found.Add(info.ReturnType);
                        }

                        break;
                    case FieldInfo field when field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly:
                        found.Add(field.FieldType);
                        break;
                    case PropertyInfo property when (property.GetMethod?.IsPublic ?? false) || (property.SetMethod?.IsPublic ?? false):
                        found.Add(property.PropertyType);
                        break;
                    case EventInfo evt when evt.AddMethod?.IsPublic ?? false:
                        found.Add(evt.EventHandlerType!);
                        break;
                    default:
                        // A member that is not visible outside the type (private, internal, a nested type).
                        break;
                }
            }

            return found.SelectMany(Flatten);
        }

        private static IEnumerable<Type> Flatten(Type type)
        {
            if (type.HasElementType)
            {
                foreach (var inner in Flatten(type.GetElementType()!))
                {
                    yield return inner;
                }

                yield break;
            }

            yield return type;

            if (type.IsGenericType)
            {
                foreach (var argument in type.GetGenericArguments().SelectMany(Flatten))
                {
                    yield return argument;
                }
            }
        }

        [Fact]
        public void NoType_HoldsMutableStaticState()
        {
            // A static that can change is state shared by every window and every test. Constants and read-only
            // statics are fine. (The GemBox licence holder will be the one named exception, by its exact name.)
            var exemptTypes = new HashSet<string>();
            var offenders = new List<string>();

            foreach (var type in Core.GetTypes().Where(t => !IsCompilerGenerated(t) && !exemptTypes.Contains(t.Name)))
            {
                foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (!field.IsInitOnly && !field.IsLiteral)
                    {
                        offenders.Add($"{type.FullName}.{field.Name}");
                    }
                }
            }

            Assert.Empty(offenders);
        }

        private static bool IsCompilerGenerated(Type type)
        {
            for (var current = type; current != null; current = current.DeclaringType)
            {
                if (current.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
                {
                    return true;
                }
            }

            return false;
        }

        // ── the sources ─────────────────────────────────────────────────────

        [Fact]
        public void Sources_WriteNothingToAConsoleATraceTheEventLogOrTheStaticLoggerOutsideTheSerilogAdapter()
        {
            var forbidden = new Regex(@"Debug\.WriteLine|Trace\.Write|\bConsole\.|\bEventLog\b|\bLog\.", RegexOptions.Compiled);
            var adapter = Path.Combine("Logging", "SerilogCardCheckLog.cs");
            var offenders = new List<string>();

            foreach (var file in Directory.EnumerateFiles(LibraryFolder(), "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(LibraryFolder(), file);

                if (relative.StartsWith("obj", StringComparison.OrdinalIgnoreCase)
                    || relative.StartsWith("bin", StringComparison.OrdinalIgnoreCase)
                    || relative.Equals(adapter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var code = StripComments(File.ReadAllText(file));

                foreach (Match match in forbidden.Matches(code))
                {
                    offenders.Add($"{relative}: {match.Value}");
                }
            }

            Assert.Empty(offenders);
        }

        [Fact]
        public void Sources_TheAdapterIsTheOnlyPlaceThatNamesSerilog()
        {
            var adapter = Path.Combine("Logging", "SerilogCardCheckLog.cs");
            var offenders = Directory.EnumerateFiles(LibraryFolder(), "*.cs", SearchOption.AllDirectories)
                .Select(file => (File: Path.GetRelativePath(LibraryFolder(), file), Code: StripComments(File.ReadAllText(file))))
                .Where(f => !f.File.StartsWith("obj", StringComparison.OrdinalIgnoreCase)
                    && !f.File.StartsWith("bin", StringComparison.OrdinalIgnoreCase)
                    && !f.File.Equals(adapter, StringComparison.OrdinalIgnoreCase)
                    && Regex.IsMatch(f.Code, @"\bSerilog\b"))
                .Select(f => f.File)
                .ToList();

            Assert.Empty(offenders);
        }

        private static string StripComments(string code)
        {
            var withoutBlocks = Regex.Replace(code, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

            return Regex.Replace(withoutBlocks, @"//[^\r\n]*", string.Empty);
        }

        // ── the name ────────────────────────────────────────────────────────

        [Fact]
        public void AssemblyName_FollowsTheLibraryConvention()
        {
            var name = Core.GetName().Name!;

            Assert.Equal(LibraryName, name);
            Assert.StartsWith("RFiDGear.ExtensionLibraries.", name, StringComparison.Ordinal);
            Assert.False(name.StartsWith("RFiDGear.Extensions", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void TheHostsOwnExtensionScan_DoesNotPickUpTheLibrary()
        {
            var folder = Path.GetDirectoryName(Core.Location)!;
            // The file that was really built, whatever it is called: a constant name could not notice a rename.
            var libraryFile = Path.GetFileName(Core.Location);

            var found = MefHelper.GetExtensionAssemblyPaths(folder).Select(Path.GetFileName).ToList();

            // The scan sees real extensions in this folder and the library is in it, so the check is not vacuous.
            Assert.True(File.Exists(Path.Combine(folder, libraryFile)));
            Assert.Contains("RFiDGear.Extensions.CardCheckAssistant.dll", found, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain(libraryFile, found, StringComparer.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("RFiDGear.ExtensionsNative.CardCheck")]
        [InlineData("RFiDGear.ExtensionSupport.CardCheck")]
        [InlineData("rfidgear.extensions.cardcheck")]
        public void TheScanPattern_WouldHandOtherNamesToMef_WhichIsWhyTheConventionExists(string wouldBeName)
        {
            var folder = Directory.CreateTempSubdirectory("rfidgear-scan-").FullName;

            try
            {
                File.WriteAllBytes(Path.Combine(folder, wouldBeName + ".dll"), new byte[] { 0 });
                File.WriteAllBytes(Path.Combine(folder, LibraryName + ".dll"), new byte[] { 0 });

                var found = MefHelper.GetExtensionAssemblyPaths(folder).Select(Path.GetFileName).ToList();

                Assert.Contains(wouldBeName + ".dll", found, StringComparer.OrdinalIgnoreCase);
                Assert.DoesNotContain(LibraryName + ".dll", found, StringComparer.OrdinalIgnoreCase);
            }
            finally
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }
}
