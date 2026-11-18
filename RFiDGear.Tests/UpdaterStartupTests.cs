using System;
using System.IO;
using System.Threading.Tasks;

using RedCell.Diagnostics.Update;

using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// The program creates its updater while the main window is being built, on the thread of the window, and the updater asks
    /// the server for the update manifest. A server that answers slowly, or not at all (a machine that has no way to GitHub
    /// waits about 20 seconds for the connection), must not hold the start of the program: the manifest is asked for in the
    /// background (issue 387), and whoever shows what it found waits for <see cref="Updater.InitialCheck"/>.
    /// </summary>
    public sealed class UpdaterStartupTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "rfidgear-updater-" + Guid.NewGuid().ToString("N"));

        public UpdaterStartupTests() => Directory.CreateDirectory(_folder);

        public void Dispose()
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, recursive: true);
            }
        }

        [Fact]
        public async Task TheConstructor_DoesNotWaitForTheServerOfTheUpdateManifest()
        {
            using var server = new ManifestServer();

            var construct = Task.Run(() => new Updater(UpdateManifestTexts.WriteLocalManifest(_folder, server.Url)));
            await server.RequestReceived.WaitAsync(TimeSpan.FromSeconds(30));   // the program has asked for the manifest...

            // ...and the server is holding the request: the constructor, and so the start of the program, must be through.
            var first = await Task.WhenAny(construct, Task.Delay(TimeSpan.FromSeconds(10)));

            Assert.Same(construct, first);
            Assert.False(server.Answered);

            server.Answer("503 Service Unavailable", string.Empty);   // an answer without a manifest changes nothing
            var updater = await construct;
            await updater.InitialCheck.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.False(updater.UpdateAvailable);
        }

        [Fact]
        public async Task TheInitialCheck_IsThroughWhenTheServerHasAnswered_AndANewerManifestIsFound()
        {
            using var server = new ManifestServer();

            var construct = Task.Run(() => new Updater(UpdateManifestTexts.WriteLocalManifest(_folder, server.Url)));
            await server.RequestReceived.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Same(construct, await Task.WhenAny(construct, Task.Delay(TimeSpan.FromSeconds(10))));   // not held by the server
            var updater = await construct;

            // Nothing is known while the server holds the request, and whoever waits for the check is not let through.
            Assert.False(updater.UpdateAvailable);
            Assert.False(updater.InitialCheck.IsCompleted);

            server.Answer("200 OK", UpdateManifestTexts.ManifestText(UpdateManifestTexts.NewerVersion, server.Url));
            await updater.InitialCheck.WaitAsync(TimeSpan.FromSeconds(30));

            Assert.True(updater.UpdateAvailable);
        }

        [Fact]
        public async Task AFailedCheck_KeepsWhatAnEarlierCheckFound()
        {
            // At the start the window's updater has two checks that overlap (the constructor's and the one of the monitoring), and
            // a later one can fail: the update that was found must not be offered with its manifest gone.
            using var server = new ManifestServer();
            var updater = await Task.Run(() => new Updater(UpdateManifestTexts.WriteLocalManifest(_folder, server.Url)));
            server.Answer("200 OK", UpdateManifestTexts.ManifestText(UpdateManifestTexts.NewerVersion, server.Url));
            await updater.InitialCheck.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.True(updater.UpdateAvailable);
            Assert.True(HasManifest(updater));

            var second = updater.CheckAsync();
            await server.RequestReceivedNumber(1).WaitAsync(TimeSpan.FromSeconds(30));
            server.Drop(1);   // an answer that is no HTTP: the request fails
            await second.WaitAsync(TimeSpan.FromSeconds(30));

            Assert.True(updater.UpdateAvailable);
            Assert.True(HasManifest(updater));
        }

        private static bool HasManifest(Updater updater) =>
            typeof(Updater).GetField("_remoteConfig", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(updater) != null;

        [Fact]
        public async Task TheInitialCheck_OfAnUpdaterWithoutAManifest_IsThroughAtOnce()
        {
            var updater = await Task.Run(() => new Updater(new FileInfo(Path.Combine(_folder, "missing", "update.xml"))));

            await updater.InitialCheck.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.False(updater.UpdateAvailable);
        }
    }
}
