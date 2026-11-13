#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Models;
using RFiDGear.Services;

using Xunit;

namespace RFiDGear.Tests.ExternalControl
{
    /// <summary>The setting that switches the bridge on, the start-up argument that does the same for one run, and the gate that lets the start-up run once.</summary>
    [Collection("SettingsFileAccess")]
    public class ExternalControlSettingsTests
    {
        private static string NewTempRoot()
        {
            var root = Path.Combine(Path.GetTempPath(), "RFiDGear.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            return root;
        }

        // ── The setting ──

        [Fact]
        public void TheSettingIsOffByDefault_AlsoInTheRuntimeDefaults()
        {
            Assert.False(new DefaultSpecificationRuntimeDefaults().EnableExternalControl);
            Assert.False(new DefaultSpecification().EnableExternalControl);
        }

        [Fact]
        public void TheSetting_SurvivesBeingSavedAndRead()
        {
            var root = NewTempRoot();
            var path = Path.Combine(root, "settings.xml");

            using var writer = new SettingsReaderWriter(root, loadSettings: false);
            writer.SaveSettings(new DefaultSpecification(true) { EnableExternalControl = true }, path);
            var on = writer.ReadSettings(path);
            writer.SaveSettings(new DefaultSpecification(true) { EnableExternalControl = false }, path);
            var off = writer.ReadSettings(path);

            Assert.True(on.EnableExternalControl);
            Assert.False(off.EnableExternalControl);
        }

        [Fact]
        public void ASettingsFileFromBeforeTheSettingExisted_ReadsAsOff()
        {
            var root = NewTempRoot();
            var path = Path.Combine(root, "settings.xml");

            using var writer = new SettingsReaderWriter(root, loadSettings: false);
            writer.SaveSettings(new DefaultSpecification(true) { EnableExternalControl = true }, path);

            // An older RFiDGear wrote no such element.
            var withoutElement = string.Join(
                Environment.NewLine,
                File.ReadAllLines(path).Where(line => !line.Contains("EnableExternalControl", StringComparison.Ordinal)));
            File.WriteAllText(path, withoutElement);

            Assert.DoesNotContain("EnableExternalControl", File.ReadAllText(path), StringComparison.Ordinal);
            Assert.False(writer.ReadSettings(path).EnableExternalControl);
        }

        // ── The start-up argument ──

        [Theory]
        [InlineData("EXTERNALCONTROL=1", true)]
        [InlineData("externalcontrol=1", true)]
        [InlineData("ExternalControl=1", true)]
        [InlineData("EXTERNALCONTROL=0", false)]
        [InlineData("EXTERNALCONTROL=true", false)]
        [InlineData("EXTERNALCONTROL=", false)]
        [InlineData("EXTERNALCONTROL", false)]
        [InlineData("EXTERNALCONTROLS=1", false)]
        [InlineData("EXTERNALCONTROL=1=1", false)]
        [InlineData("AUTOMODE=1", false)]
        public void TheArgument_IsReadTheSameWay_ByTheProcessorAndByTheEarlyCheck(string argument, bool expected)
        {
            var arguments = new[] { "RFiDGear.exe", argument };

            Assert.Equal(expected, new StartupArgumentProcessor().Process(arguments).ExternalControl);
            Assert.Equal(expected, StartupArgumentProcessor.IsExternalControlRequested(arguments));
        }

        [Fact]
        public void TheLastArgumentWins_ForBoth()
        {
            var on = new[] { "EXTERNALCONTROL=0", "EXTERNALCONTROL=1" };
            var off = new[] { "EXTERNALCONTROL=1", "EXTERNALCONTROL=0" };

            Assert.True(new StartupArgumentProcessor().Process(on).ExternalControl);
            Assert.True(StartupArgumentProcessor.IsExternalControlRequested(on));
            Assert.False(new StartupArgumentProcessor().Process(off).ExternalControl);
            Assert.False(StartupArgumentProcessor.IsExternalControlRequested(off));
        }

        [Fact]
        public void NoArguments_MeanNo()
        {
            Assert.False(StartupArgumentProcessor.IsExternalControlRequested(null!));
            Assert.False(StartupArgumentProcessor.IsExternalControlRequested(Array.Empty<string>()));
            Assert.False(new StartupArgumentProcessor().Process(null!).ExternalControl);
        }

        [Fact]
        public void TheEarlyCheck_HasNoEffectsOfItsOwn()
        {
            // Process() writes a setting for LASTUSEDPROJECTPATH and takes the next free report number; the early check must not.
            var root = NewTempRoot();
            var file = Path.Combine(root, "report-??.pdf");
            File.WriteAllText(Path.Combine(root, "report-07.pdf"), "x");

            var requested = StartupArgumentProcessor.IsExternalControlRequested(new[] { "REPORTTARGETPATH=" + file, "EXTERNALCONTROL=1" });

            Assert.True(requested);
            Assert.Equal(new[] { "report-07.pdf" }, Directory.GetFiles(root).Select(Path.GetFileName).ToArray());
        }

        // ── The gate for the start-up ──

        [Fact]
        public async Task OfManyTriggers_ExactlyOneRunsTheStartUp()
        {
            var gate = new OnceGate();
            var winners = 0;

            Assert.False(gate.HasEntered);
            await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() =>
            {
                if (gate.TryEnter())
                {
                    System.Threading.Interlocked.Increment(ref winners);
                }
            })));

            Assert.Equal(1, winners);
            Assert.True(gate.HasEntered);
            Assert.False(gate.TryEnter());
        }
    }
}
