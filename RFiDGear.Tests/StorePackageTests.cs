using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// What the Microsoft Store package says about itself: the manifest (its template and the identity), the version, and the
    /// rules that the build script keeps. These are the facts that must not change without somebody noticing: the only
    /// capability, how the program is started, that no driver can be in the package, and that the natives of the PC/SC reader are.
    /// </summary>
    public sealed class StorePackageTests
    {
        private static readonly XNamespace Foundation = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
        private static readonly XNamespace Restricted = "http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities";
        private static readonly XNamespace Desktop = "http://schemas.microsoft.com/appx/manifest/desktop/windows10";

        private static string PackagingFile(string name) => Path.Combine(DotNetTool.RepositoryRoot(), "RFiDGear.Store", "Packaging", name);

        private static XDocument Manifest() => XDocument.Load(PackagingFile("AppxManifest.template.xml"));

        [Fact]
        public void TheManifest_AsksForFullTrustAndNothingElse()
        {
            var capabilities = Manifest().Root!.Element(Foundation + "Capabilities")!.Elements().ToList();

            var only = Assert.Single(capabilities);
            Assert.Equal(Restricted + "Capability", only.Name);
            Assert.Equal("runFullTrust", (string?)only.Attribute("Name"));

            // No device capability: a packaged desktop program that runs with the rights of the user needs none for a COM port or
            // the smart card service, and a package cannot install a driver.
            Assert.Empty(Manifest().Descendants().Where(element => element.Name.LocalName == "DeviceCapability"));
        }

        [Fact]
        public void TheManifest_StartsTheProgramAsAFullTrustDesktopApp_ForWindows10AndTheX64Platform()
        {
            var package = Manifest().Root!;

            var application = Assert.Single(package.Element(Foundation + "Applications")!.Elements(Foundation + "Application"));
            Assert.Equal("RFiDGear.exe", (string?)application.Attribute("Executable"));
            Assert.Equal("Windows.FullTrustApplication", (string?)application.Attribute("EntryPoint"));

            var family = package.Element(Foundation + "Dependencies")!.Element(Foundation + "TargetDeviceFamily")!;
            Assert.Equal("Windows.Desktop", (string?)family.Attribute("Name"));
            Assert.Equal("10.0.19041.0", (string?)family.Attribute("MinVersion"));

            Assert.Equal("x64", (string?)package.Element(Foundation + "Identity")!.Attribute("ProcessorArchitecture"));

            // The natives of LibLogicalAccess import the Visual C++ runtime of 2015 and later.
            var dependencies = package.Element(Foundation + "Dependencies")!.Elements(Foundation + "PackageDependency").Select(e => (string?)e.Attribute("Name"));
            Assert.Contains("Microsoft.VCLibs.140.00.UWPDesktop", dependencies);

            // A program that starts RFiDGear for the external control bridge cannot know the folder of a package: it uses the alias.
            var alias = application.Descendants(Desktop + "ExecutionAlias").Single();
            Assert.Equal("RFiDGear.exe", (string?)alias.Attribute("Alias"));
        }

        [Fact]
        public void TheManifest_HasTheFourValuesThatTheScriptFillsIn_AndNoOther()
        {
            var text = File.ReadAllText(PackagingFile("AppxManifest.template.xml"));
            var script = File.ReadAllText(PackagingFile("Build-StorePackage.ps1"));

            var tokens = Regex.Matches(text, @"\{\{(\w+)\}\}").Select(match => match.Groups[1].Value).Distinct().OrderBy(token => token).ToArray();

            Assert.Equal(new[] { "IdentityName", "Publisher", "PublisherDisplayName", "Version" }, tokens);

            // The script has a value for each of them (the hashtable of the replacements).
            foreach (var token in tokens)
            {
                Assert.Matches(@"(?m)^\s*" + token + @"\s*=", script);
            }
        }

        [Fact]
        public void TheIdentityFile_HasWhatTheStoreWants_AndTheVersionOfTheProgramCanBeAStoreVersion()
        {
            using var identity = JsonDocument.Parse(File.ReadAllText(PackagingFile("store-identity.json")));
            var root = identity.RootElement;

            Assert.Matches(@"^[-.A-Za-z0-9]{3,50}$", root.GetProperty("name").GetString());
            Assert.StartsWith("CN=", root.GetProperty("publisher").GetString(), StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("publisherDisplayName").GetString()));

            // The Store keeps the fourth part of the version for itself: it must be 0.
            var csproj = XDocument.Load(Path.Combine(DotNetTool.RepositoryRoot(), "RFiDGear", "RFiDGear.csproj"));
            var version = csproj.Descendants("AssemblyVersion").Single().Value;
            Assert.Matches(@"^\d+\.\d+\.\d+\.0$", version);
        }

        [Fact]
        public void TheBuildScript_KeepsDriversOutOfThePackage_AndTheNativesOfTheReaderIn()
        {
            var script = File.ReadAllText(PackagingFile("Build-StorePackage.ps1"));

            foreach (var forbidden in new[] { "'*.sys'", "'*.inf'", "'*.cat'", "'pnputil.exe'", "'update.xml'", "'RFiDGearBundleSetup.exe'" })
            {
                Assert.Contains(forbidden, script, StringComparison.Ordinal);
            }

            // A publish for win-x64 leaves the 46 natives of LibLogicalAccess out (they are filed under win10-x64 in their package),
            // which would leave the PC/SC reader dead; the old runtime identifier needs the old runtime identifier graph.
            Assert.Contains("-r win10-x64", script, StringComparison.Ordinal);
            Assert.Contains("-p:UseRidGraph=true", script, StringComparison.Ordinal);
            Assert.Contains("'pcscreaders.dll'", script, StringComparison.Ordinal);
        }

        [Fact]
        public void TheBuildScript_AsksForThePackageOfTheExtensionByName_AndTakesItFromAFolderOfItsOwn()
        {
            var script = File.ReadAllText(PackagingFile("Build-StorePackage.ps1"));

            // Without the request a failed package step is only a warning, and the script would take an old package of an earlier build.
            Assert.Contains("-p:PackExtension=true", script, StringComparison.Ordinal);
            Assert.Contains("ExtensionPackageFolder=$extensionPackages", script, StringComparison.Ordinal);
            Assert.Contains("Get-ChildItem -LiteralPath $extensionPackages", script, StringComparison.Ordinal);
        }

        [Fact]
        public void TheBuildScript_ClearsTheOutputOfTheExtension_AndNeverDeploysIntoARealInstallation()
        {
            var script = File.ReadAllText(PackagingFile("Build-StorePackage.ps1"));

            // The package takes every library of the output folder, so nothing of an earlier build may be in it ...
            Assert.Contains("Remove-Item -LiteralPath $extensionOutput", script, StringComparison.Ordinal);
            Assert.Contains("--no-incremental", script, StringComparison.Ordinal);

            // ... and the deploy step of the extension build (deploy.local.props, RFIDGEAR_DEPLOY_EXTENSIONS) copies into the
            // ProgramData folder of a real RFiDGear: it is off whatever the machine says.
            Assert.Contains("-p:DeployExtensionsToRFiDGear=false", script, StringComparison.Ordinal);
        }

        [Fact]
        public void TheStoreAssembly_IsNotNamedLikeAnExtension_WhichTheProgramScansFor()
        {
            var csproj = XDocument.Load(Path.Combine(DotNetTool.RepositoryRoot(), "RFiDGear.Store", "RFiDGear.Store.csproj"));
            var name = csproj.Descendants("AssemblyName").Single().Value;

            Assert.False(name.StartsWith("RFiDGear.Extensions", StringComparison.OrdinalIgnoreCase));
            Assert.Equal("RFiDGear.Store", name);
        }
    }
}
