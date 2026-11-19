using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using RFiDGear.Infrastructure.ExtensionPackages;
using RFiDGear.Tests.CardCheck;

using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>The path a person takes to install an extension update: what is asked, what is told, and that nothing is put in place without a yes.</summary>
    public sealed class ExtensionInstallFlowTests : IDisposable
    {
        private const string Id = "RFiDGear.Extensions.Sample";

        private readonly TempFolder _folder = new TempFolder();

        public void Dispose() => _folder.Dispose();

        private string UserRoot => Path.Combine(_folder.Path, "user");

        // The texts the flow asks for, with the number of arguments it gives each (a text may use fewer, never more).
        private static readonly Dictionary<string, int> Texts = new Dictionary<string, int>
        {
            ["messageExtensionInstallConfirm"] = 5,
            ["messageExtensionInstalled"] = 2,
            ["textExtensionInstalledNow"] = 1,
            ["textExtensionNotInstalled"] = 0,
            ["textExtensionNotSigned"] = 0,
            ["textExtensionSignedBy"] = 1,
            ["textExtensionSignedByUntrusted"] = 1,
            ["messageExtensionProblemNotAPackage"] = 1,
            ["messageExtensionProblemHostTooOld"] = 1,
            ["messageExtensionProblemAlreadyInstalled"] = 1,
            ["messageExtensionProblemNotNewer"] = 1,
            ["messageExtensionProblemCannotWrite"] = 1,
            ["messageExtensionProblemGeneric"] = 1,
        };

        // In these tests a text is its key followed by its arguments, so a test can see which text was chosen and what it was given.
        private static string Text(string key) => key + string.Concat(Enumerable.Range(0, Texts[key]).Select(i => "|{" + i + "}"));

        [Theory]
        [InlineData("en")]
        [InlineData("de")]
        public void EveryTextTheFlowAsksFor_ExistsInBothLanguages_AndNeedsNoMoreThanItIsGiven(string language)
        {
            var resources = new System.Resources.ResourceManager("RFiDGear.Resources.Manifest", typeof(MefHelper).Assembly);
            var culture = new System.Globalization.CultureInfo(language);
            var others = new[] { "menuItemInstallExtension", "captionExtensionInstall", "windowCaptionExtensionPackage", "filterStringExtensionPackages" };

            foreach (var key in Texts.Keys.Concat(others))
            {
                var text = resources.GetString(key, culture);
                Assert.False(string.IsNullOrWhiteSpace(text), language + " " + key);
                _ = string.Format(System.Globalization.CultureInfo.InvariantCulture, text!, Enumerable.Range(0, Texts.GetValueOrDefault(key)).Select(i => (object)("a" + i)).ToArray());
            }

            Assert.NotEqual(resources.GetString("menuItemInstallExtension", new System.Globalization.CultureInfo("en")), resources.GetString("menuItemInstallExtension", new System.Globalization.CultureInfo("de")));
        }

        private sealed class Dialogs : IExtensionInstallDialogs
        {
            public string? Package { get; init; }

            public bool Answer { get; init; } = true;

            public List<string> Asked { get; } = new List<string>();

            public List<(string Text, bool IsError)> Told { get; } = new List<(string, bool)>();

            public string? ChoosePackage() => Package;

            public bool Confirm(string text)
            {
                Asked.Add(text);
                return Answer;
            }

            public void Inform(string text, bool isError) => Told.Add((text, isError));

            // How many times the application was shown as busy, and finished being so.
            public int Worked { get; private set; }

            public IDisposable Working() => new Busy(this);

            private sealed class Busy : IDisposable
            {
                private readonly Dialogs _owner;

                public Busy(Dialogs owner) => _owner = owner;

                public void Dispose() => _owner.Worked++;
            }
        }

        // The name of a version folder, such as 1.1.0.0, written as what it is: a version with four parts.
        private static string Ver(int major, int minor) => new Version(major, minor, 0, 0).ToString();

        private string WritePackage(string version = "1.1.0", string? notes = null, string? minHost = null)
        {
            Directory.CreateDirectory(_folder.Path);
            var main = Encoding.UTF8.GetBytes("main");
            var json = $"{{\"schema\":1,\"id\":\"{Id}\",\"displayName\":\"Sample Extension\",\"version\":\"{version}\"," +
                (notes == null ? string.Empty : $"\"notes\":\"{notes}\",") +
                (minHost == null ? string.Empty : $"\"minHostVersion\":\"{minHost}\",") +
                $"\"files\":[{{\"name\":\"{Id}.dll\",\"sha256\":\"{Convert.ToHexString(SHA256.HashData(main))}\"}}]}}";
            var path = Path.Combine(_folder.Path, Guid.NewGuid().ToString("N") + ".rfext");
            using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                foreach (var (name, content) in new[] { (Id + ".dll", main), ("extension.json", Encoding.UTF8.GetBytes(json)) })
                {
                    using var stream = archive.CreateEntry(name).Open();
                    stream.Write(content, 0, content.Length);
                }
            }

            return path;
        }

        // The file of a test package is no real assembly, so its version is what the test says; it is not the version of any other file.
        private ExtensionPackageInstaller Installer(Func<string, SignerInfo?>? signer = null) =>
            new ExtensionPackageInstaller(
                UserRoot,
                Path.Combine(_folder.Path, "machine"),
                new Version(2, 1, 0, 0),
                path => path.Contains(ExtensionPackageLayout.StagingFolderName, StringComparison.Ordinal) ? new Version(1, 1, 0, 0) : null,
                signer ?? (_ => null));

        private static ExtensionInstallFlow Flow(ExtensionPackageInstaller installer, Dialogs dialogs) =>
            new ExtensionInstallFlow(installer, dialogs, Text);

        [Fact]
        public async Task WhoGivesUp_AtTheFileDialog_InstallsNothing()
        {
            var dialogs = new Dialogs { Package = null };

            await Flow(Installer(), dialogs).RunAsync();

            Assert.Empty(dialogs.Asked);
            Assert.Empty(dialogs.Told);
            Assert.False(Directory.Exists(UserRoot));
        }

        [Fact]
        public async Task WhoSaysYes_GetsTheExtension_AndIsToldToRestart()
        {
            var dialogs = new Dialogs { Package = WritePackage(notes: "Fixes the header.") };

            await Flow(Installer(_ => new SignerInfo("Example Corp", true)), dialogs).RunAsync();

            var asked = Assert.Single(dialogs.Asked);
            Assert.StartsWith("messageExtensionInstallConfirm", asked, StringComparison.Ordinal);
            Assert.Contains("Sample Extension", asked, StringComparison.Ordinal);
            Assert.Contains("textExtensionNotInstalled", asked, StringComparison.Ordinal);
            Assert.Contains("textExtensionSignedBy|Example Corp", asked, StringComparison.Ordinal);
            Assert.Contains("Fixes the header.", asked, StringComparison.Ordinal);

            var told = Assert.Single(dialogs.Told);
            Assert.False(told.IsError);
            Assert.StartsWith("messageExtensionInstalled", told.Text, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(UserRoot, Id, Ver(1, 1), Id + ".dll")));
            Assert.Equal(2, dialogs.Worked);   // busy while the package was prepared, and again while it was put in place
            Assert.Empty(Directory.EnumerateDirectories(Path.Combine(UserRoot, ExtensionPackageLayout.StagingFolderName)));
        }

        [Fact]
        public async Task WhoSaysNo_GetsNothing_AndNothingIsLeftBehind()
        {
            var dialogs = new Dialogs { Package = WritePackage(), Answer = false };

            await Flow(Installer(), dialogs).RunAsync();

            Assert.Single(dialogs.Asked);
            Assert.Empty(dialogs.Told);
            Assert.False(Directory.Exists(Path.Combine(UserRoot, Id)));
            Assert.Empty(Directory.EnumerateDirectories(Path.Combine(UserRoot, ExtensionPackageLayout.StagingFolderName)));
        }

        [Fact]
        public async Task AnUnsignedPackage_IsSaidToBeUnsigned_AndAnUntrustedSignerIsSaidToBeUntrusted()
        {
            var unsigned = new Dialogs { Package = WritePackage(), Answer = false };
            await Flow(Installer(), unsigned).RunAsync();
            Assert.Contains("textExtensionNotSigned", Assert.Single(unsigned.Asked), StringComparison.Ordinal);

            var untrusted = new Dialogs { Package = WritePackage(), Answer = false };
            await Flow(Installer(_ => new SignerInfo("Somebody", false)), untrusted).RunAsync();
            Assert.Contains("textExtensionSignedByUntrusted|Somebody", Assert.Single(untrusted.Asked), StringComparison.Ordinal);
        }

        [Fact]
        public async Task APackageThatCannotBeUsed_IsExplained_AndNeverAsksForAYes()
        {
            Directory.CreateDirectory(_folder.Path);
            var notAPackage = Path.Combine(_folder.Path, "text.rfext");
            File.WriteAllText(notAPackage, "no zip");
            var dialogs = new Dialogs { Package = notAPackage };

            await Flow(Installer(), dialogs).RunAsync();

            Assert.Empty(dialogs.Asked);
            var told = Assert.Single(dialogs.Told);
            Assert.True(told.IsError);
            Assert.StartsWith("messageExtensionProblemNotAPackage", told.Text, StringComparison.Ordinal);
            Assert.DoesNotContain(_folder.Path, told.Text, StringComparison.OrdinalIgnoreCase);   // no path of the person's in a message
        }

        [Fact]
        public async Task APackageThatNeedsANewerRFiDGear_SaysSo_InItsOwnWords()
        {
            var olderHost = new ExtensionPackageInstaller(UserRoot, Path.Combine(_folder.Path, "machine"), new Version(2, 0, 0, 0), _ => new Version(1, 1, 0, 0));
            var dialogs = new Dialogs { Package = WritePackage(minHost: "2.1.0") };

            await new ExtensionInstallFlow(olderHost, dialogs, Text).RunAsync();

            Assert.StartsWith("messageExtensionProblemHostTooOld", Assert.Single(dialogs.Told).Text, StringComparison.Ordinal);
            Assert.Empty(dialogs.Asked);
        }
    }
}
