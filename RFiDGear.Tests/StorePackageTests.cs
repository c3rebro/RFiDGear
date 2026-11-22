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
        public void TheBuildScript_LooksAtTheCertificateBeforeItBuildsAnything_AndTakesThePublisherFromIt()
        {
            var script = File.ReadAllText(PackagingFile("Build-StorePackage.ps1"));

            // A wrong password or a certificate that is not valid would otherwise show only after minutes of building.
            var checkedAt = script.IndexOf("Test-StoreSigningCertificate -Certificate", StringComparison.Ordinal);
            var builtAt = script.IndexOf("Invoke-Step 'publish the program", StringComparison.Ordinal);
            Assert.True(checkedAt > 0, "The script does not look at the certificate.");
            Assert.True(builtAt > checkedAt, "The certificate has to be looked at before the first build step.");

            // Windows refuses a package whose publisher is not the subject of the certificate: the manifest gets the publisher that
            // the script worked out (the certificate's for a placeholder identity), never the one of the file unchecked.
            Assert.Matches(@"(?m)^\s*Publisher\s*=\s*\$publisher\s*$", script);
            Assert.DoesNotMatch(@"(?m)^\s*Publisher\s*=\s*\$identity\.publisher", script);
            Assert.Contains("$certificate.Subject", script, StringComparison.Ordinal);
        }

        [Fact]
        public void TheInteractiveScript_KeepsThePasswordOutOfFilesAndOutOfTheEnvironmentAfterTheRun()
        {
            var script = File.ReadAllText(PackagingFile("Make-StorePackage.ps1"));

            // Typed hidden, never a parameter of a string type ...
            Assert.Contains("-AsSecureString", script, StringComparison.Ordinal);
            Assert.Contains("[securestring]$Password", script, StringComparison.Ordinal);

            // ... written nowhere ...
            Assert.DoesNotMatch(@"Out-File|Set-Content|Add-Content|WriteAllText|Export-Clixml|Start-Transcript|\s>\s", script);

            // ... and in the environment only while the build runs: the two variables are put back in a finally block.
            var started = script.IndexOf("$env:RFIDGEAR_SIGN_PASSWORD = $plain", StringComparison.Ordinal);
            var restored = script.IndexOf("$env:RFIDGEAR_SIGN_PASSWORD = $savedPassword", StringComparison.Ordinal);
            Assert.True(started > 0 && restored > started, "The script sets the password for the run and puts the old value back afterwards.");
            Assert.Matches(@"finally\s*\{[^}]*RFIDGEAR_SIGN_PASSWORD = \$savedPassword", script);

            // It signs through the build script and installs nothing without being told to.
            Assert.Contains("Build-StorePackage.ps1", script, StringComparison.Ordinal);
            Assert.Matches(@"Install the package for your user now\?[^\r\n]*\[y/N\]", script);

            // What it offers to install is the file that this build reported, never the newest .msix of the folder (the folder may
            // hold the package of another build) ...
            Assert.Contains("-Sign -PassThru", script, StringComparison.Ordinal);
            Assert.DoesNotMatch(@"Sort-Object\s+LastWriteTime", script);

            // ... and an install that failed is a failed run, so that the batch file (and any caller) can tell.
            Assert.Matches(@"(?s)if \(\$installed\)\s*\{[^}]*\}\s*else\s*\{[^}]*\bthrow\b", script);
        }

        [Fact]
        public void TheBuildScript_HandsBackThePackageItMade_WhenAsked()
        {
            var script = File.ReadAllText(PackagingFile("Build-StorePackage.ps1"));

            Assert.Matches(@"\[switch\]\$PassThru\b", script);
            Assert.Matches(@"(?s)if \(\$PassThru\)\s*\{\s*\$info\s*\}", script);
        }

        [Fact]
        public void TheTrustCheck_FollowsTheChainToARootOfTheMachine_NotOnlyTheStoresForTheCertificateItself()
        {
            var script = File.ReadAllText(PackagingFile("StoreCertificate.ps1"));

            // A certificate that a certificate authority issued is in neither store itself: it is trusted when its chain ends in a root
            // of the machine. Without the chain a good certificate would get a warning that says Windows will not install the package.
            Assert.Contains(@"Cert:\LocalMachine\TrustedPeople", script, StringComparison.Ordinal);
            Assert.Contains("X509Chain", script, StringComparison.Ordinal);
            Assert.Contains("ChainElements", script, StringComparison.Ordinal);
            Assert.Contains(@"Join-Path 'Cert:\LocalMachine\Root' $root.Thumbprint", script, StringComparison.Ordinal);
        }

        [Fact]
        public void TheBatchFile_RunsTheInteractiveScript_WithPowerShell7IfThereIsOne()
        {
            var batch = File.ReadAllText(PackagingFile("Make-StorePackage.bat"));

            Assert.Contains("Make-StorePackage.ps1", batch, StringComparison.Ordinal);
            Assert.Contains("pwsh.exe", batch, StringComparison.Ordinal);
            Assert.Contains("powershell.exe", batch, StringComparison.Ordinal);
            Assert.True(File.Exists(PackagingFile("Make-StorePackage.ps1")));
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
