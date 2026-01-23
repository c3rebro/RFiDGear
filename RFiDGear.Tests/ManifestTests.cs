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
  <SecurityToken>D68EF3A7-E787-4CC4-B020-878BA649B4DC</SecurityToken>
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
            Assert.Equal("D68EF3A7-E787-4CC4-B020-878BA649B4DC", manifest.SecurityToken);
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
  <SecurityToken>D68EF3A7-E787-4CC4-B020-878BA649B4DC</SecurityToken>
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
            Assert.Equal("D68EF3A7-E787-4CC4-B020-878BA649B4DC", manifest.SecurityToken);
            Assert.Equal("https://github.com/c3rebro/RFiDGear/releases/latest/download/", manifest.BaseUri);
            Assert.Equal(new[] { "update.zip" }, manifest.Payloads);
            Assert.Equal("Version Info\n\ngoes here!\n==>", manifest.VersionInfoText);
        }

        [Fact]
        public void Load_WhenManifestHasStrayAmpersand_ParsesValues()
        {
            var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Manifest version=""2.0.0"">
  <CheckInterval>900</CheckInterval>
  <RemoteConfigUri>https://github.com/c3rebro/RFiDGear/releases/latest/download/update.xml</RemoteConfigUri>
  <SecurityToken>D68EF3A7-E787-4CC4-B020-878BA649B4DC</SecurityToken>
  <BaseUri>https://github.com/c3rebro/RFiDGear/releases/latest/download/</BaseUri>
  <Payload>update.zip</Payload>
  <VersionInfoText>UI & dialogs</VersionInfoText>
</Manifest>";

            var manifest = new Manifest(xml);

            Assert.Equal("UI & dialogs", manifest.VersionInfoText);
        }

        [Fact]
        public void Load_WhenManifestHasUnescapedAmpersandsAndPrintableCharacters_ParsesValues()
        {
            var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Manifest version=""2.0.0"">
  <CheckInterval>900</CheckInterval>
  <RemoteConfigUri>https://github.com/c3rebro/RFiDGear/releases/latest/download/update.xml</RemoteConfigUri>
  <SecurityToken>D68EF3A7-E787-4CC4-B020-878BA649B4DC</SecurityToken>
  <BaseUri>https://github.com/c3rebro/RFiDGear/releases/latest/download/</BaseUri>
  <Payload>update.zip</Payload>
  <VersionInfoText>UI & dialogs & settings (v2) 100% 'ready' / 'ok'</VersionInfoText>
</Manifest>";

            var manifest = new Manifest(xml);

            Assert.Equal("UI & dialogs & settings (v2) 100% \"ready\" / 'ok'", manifest.VersionInfoText);
        }

        [Fact]
        public void Load_WhenManifestHasInvalidXmlCharacters_StripsInvalidCharacters()
        {
            var xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
                + "<Manifest version=\"2.0.0\">"
                + "<CheckInterval>900</CheckInterval>"
                + "<RemoteConfigUri>https://github.com/c3rebro/RFiDGear/releases/latest/download/update.xml</RemoteConfigUri>"
                + "<SecurityToken>D68EF3A7-E787-4CC4-B020-878BA649B4DC</SecurityToken>"
                + "<BaseUri>https://github.com/c3rebro/RFiDGear/releases/latest/download/</BaseUri>"
                + "<Payload>update.zip</Payload>"
                + "<VersionInfoText>Update" + '\u001F' + "notes</VersionInfoText>"
                + "</Manifest>";

            var manifest = new Manifest(xml);

            Assert.Equal("Updatenotes", manifest.VersionInfoText);
        }

        [Fact]
        public void Load_WhenManifestHasMultipleInvalidControlCharacters_StripsInvalidCharacters()
        {
            var xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
                + "<Manifest version=\"2.0.0\">"
                + "<CheckInterval>900</CheckInterval>"
                + "<RemoteConfigUri>https://github.com/c3rebro/RFiDGear/releases/latest/download/update.xml</RemoteConfigUri>"
                + "<SecurityToken>D68EF3A7-E787-4CC4-B020-878BA649B4DC</SecurityToken>"
                + "<BaseUri>https://github.com/c3rebro/RFiDGear/releases/latest/download/</BaseUri>"
                + "<Payload>update.zip</Payload>"
                + "<VersionInfoText>UI" + '\u0001' + '\u0008' + " notes" + '\u001F' + " here</VersionInfoText>"
                + "</Manifest>";

            var manifest = new Manifest(xml);

            Assert.Equal("UI notes here", manifest.VersionInfoText);
        }
    }
}
