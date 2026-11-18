using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using RedCell.Diagnostics.Update;

using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// What a release is made from: the version of the host, of the setup bundle and of the update manifest are written by
    /// hand in three files, and the installed programs find a new release by that manifest. These tests read the real files of
    /// the repository, so a release cannot be built with a version that differs between them, or with a manifest that the
    /// update check cannot read or would not accept.
    /// </summary>
    public sealed class ReleaseManifestTests
    {
        private static string Read(params string[] path) => File.ReadAllText(Path.Combine(new[] { DotNetTool.RepositoryRoot() }.Concat(path).ToArray()));

        private static string ManifestText => Read("RFiDGear", "update.xml");

        [Fact]
        public void TheVersionOfTheHost_TheBundleAndTheManifest_AreTheSame()
        {
            var host = XDocument.Parse(Read("RFiDGear", "RFiDGear.csproj")).Descendants("AssemblyVersion").Single().Value;
            var bundle = Regex.Match(Read("RFiDGearBundleSetup", "Bundle.wxs"), "<\\?define VersionNumber=\"([0-9.]+)\" \\?>");
            var manifest = new Manifest(ManifestText).Version;

            Assert.True(bundle.Success, "the bundle does not define its VersionNumber");
            Assert.Equal(host, bundle.Groups[1].Value);
            Assert.Equal(host, manifest?.ToString());
        }

        [Fact]
        public void TheManifest_IsStrictXml_AndHasWhatTheUpdateCheckAsksFor()
        {
            // The program reads a manifest that is not well-formed too (it escapes a stray ampersand), but other tools do not.
            Assert.NotNull(XDocument.Parse(ManifestText).Root);

            var manifest = new Manifest(ManifestText);

            Assert.Equal(new[] { "update.zip" }, manifest.Payloads);
            Assert.Equal("https://github.com/c3rebro/RFiDGear/releases/latest/download/update.xml", manifest.RemoteConfigUri);
            Assert.Equal("https://github.com/c3rebro/RFiDGear/releases/latest/download/", manifest.BaseUri);

            // A program offers an update only when the token of the manifest equals its own: a new token would cut every
            // installed version off from this release.
            Assert.Equal("D68EF3A7-E787-4CC4-B020-878BA649B4DC", manifest.SecurityToken);
        }

        [Fact]
        public void TheNewestNotesOfTheManifest_AreThoseOfItsVersion()
        {
            var manifest = new Manifest(ManifestText);
            var newest = Regex.Match(manifest.VersionInfoText, @"^Version ([0-9.]+)\s*$", RegexOptions.Multiline);

            Assert.True(newest.Success, "the notes hold no 'Version x.y.z.w' heading");
            Assert.Equal(manifest.Version!.ToString(), newest.Groups[1].Value);
        }
    }
}
