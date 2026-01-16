using System;
using RedCell.Diagnostics.Update;
using Xunit;

namespace RFiDGear.Tests
{
    public class ManifestTests
    {
        [Fact]
        public void Load_WhenManifestHasNoNamespace_ParsesValues()
        {
            var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Manifest version=""2.0.0"">
  <CheckInterval>900</CheckInterval>
  <RemoteConfigUri>https://github.com/c3rebro/RFiDGear/releases/latest/download/update.xml</RemoteConfigUri>
  <SecurityToken>D68EF3A7-E787-4CC4-B020-878BA649B4CD</SecurityToken>
  <BaseUri>https://github.com/c3rebro/RFiDGear/releases/latest/download/</BaseUri>
  <Payload>update.zip</Payload>
  <VersionInfoText>Version Info

goes here!
==></VersionInfoText>
</Manifest>";

            var manifest = new Manifest(xml);

            Assert.Equal(new Version("2.0.0"), manifest.Version);
            Assert.Equal(900, manifest.CheckInterval);
            Assert.Equal("https://github.com/c3rebro/RFiDGear/releases/latest/download/update.xml", manifest.RemoteConfigUri);
            Assert.Equal("D68EF3A7-E787-4CC4-B020-878BA649B4CD", manifest.SecurityToken);
            Assert.Equal("https://github.com/c3rebro/RFiDGear/releases/latest/download/", manifest.BaseUri);
            Assert.Equal(new[] { "update.zip" }, manifest.Payloads);
            Assert.Equal("Version Info\n\ngoes here!\n==>", manifest.VersionInfoText);
        }

        [Fact]
        public void Load_WhenManifestHasDefaultNamespace_ParsesValues()
        {
            var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Manifest xmlns=""urn:rfidgear-update"" version=""2.0.0"">
  <CheckInterval>900</CheckInterval>
  <RemoteConfigUri>https://github.com/c3rebro/RFiDGear/releases/latest/download/update.xml</RemoteConfigUri>
  <SecurityToken>D68EF3A7-E787-4CC4-B020-878BA649B4CD</SecurityToken>
  <BaseUri>https://github.com/c3rebro/RFiDGear/releases/latest/download/</BaseUri>
  <Payload>update.zip</Payload>
  <VersionInfoText>Version Info

goes here!
==></VersionInfoText>
</Manifest>";

            var manifest = new Manifest(xml);

            Assert.Equal(new Version("2.0.0"), manifest.Version);
            Assert.Equal(900, manifest.CheckInterval);
            Assert.Equal("https://github.com/c3rebro/RFiDGear/releases/latest/download/update.xml", manifest.RemoteConfigUri);
            Assert.Equal("D68EF3A7-E787-4CC4-B020-878BA649B4CD", manifest.SecurityToken);
            Assert.Equal("https://github.com/c3rebro/RFiDGear/releases/latest/download/", manifest.BaseUri);
            Assert.Equal(new[] { "update.zip" }, manifest.Payloads);
            Assert.Equal("Version Info\n\ngoes here!\n==>", manifest.VersionInfoText);
        }
    }
}
