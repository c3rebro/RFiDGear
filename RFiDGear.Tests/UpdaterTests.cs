using System;
using RedCell.Diagnostics.Update;
using Xunit;

namespace RFiDGear.Tests
{
    public class UpdaterTests
    {
        [Fact]
        public void ResolveEffectiveLocalVersion_WhenRunningIsNewer_PrefersRunningVersion()
        {
            var manifestVersion = new Version(1, 0, 0);
            var runningVersion = new Version(2, 0, 0);

            var resolved = Updater.ResolveEffectiveLocalVersion(manifestVersion, runningVersion);

            Assert.Equal(runningVersion, resolved);
        }

        [Fact]
        public void ResolveEffectiveLocalVersion_WhenManifestIsNewer_PrefersManifestVersion()
        {
            var manifestVersion = new Version(3, 0, 0);
            var runningVersion = new Version(2, 0, 0);

            var resolved = Updater.ResolveEffectiveLocalVersion(manifestVersion, runningVersion);

            Assert.Equal(manifestVersion, resolved);
        }

        [Fact]
        public void ResolveEffectiveLocalVersion_WhenManifestMissing_UsesRunningVersion()
        {
            var runningVersion = new Version(2, 1, 0);

            var resolved = Updater.ResolveEffectiveLocalVersion(null, runningVersion);

            Assert.Equal(runningVersion, resolved);
        }
    }
}
