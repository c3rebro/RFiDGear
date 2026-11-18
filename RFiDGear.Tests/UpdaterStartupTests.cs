using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using RedCell.Diagnostics.Update;

using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// The program creates its updater while the main window is being built, on the thread of the window, and the updater asks
    /// the server for the update manifest. A server that answers slowly, or not at all (a machine that has no way to GitHub
    /// waits about 20 seconds for the connection), must not hold the start of the program: the manifest is asked for in the
    /// background (issue 387).
    /// </summary>
    public sealed class UpdaterStartupTests : IDisposable
    {
        private const string Token = "11111111-2222-3333-4444-555555555555";

        private readonly string _folder = Path.Combine(Path.GetTempPath(), "rfidgear-updater-" + Guid.NewGuid().ToString("N"));

        public UpdaterStartupTests() => Directory.CreateDirectory(_folder);

        public void Dispose()
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, recursive: true);
            }
        }

        private static string ManifestText(string version, string remoteUri) =>
            "<?xml version=\"1.0\" encoding=\"utf-8\" ?>\n"
            + "<Manifest version=\"" + version + "\">\n"
            + "  <CheckInterval>900</CheckInterval>\n"
            + "  <RemoteConfigUri>" + remoteUri + "</RemoteConfigUri>\n"
            + "  <SecurityToken>" + Token + "</SecurityToken>\n"
            + "  <BaseUri>http://127.0.0.1/</BaseUri>\n"
            + "  <Payload>update.zip</Payload>\n"
            + "  <VersionInfoText>Version " + version + "</VersionInfoText>\n"
            + "</Manifest>";

        private FileInfo WriteLocalManifest(string remoteUri)
        {
            var path = Path.Combine(_folder, "update.xml");
            File.WriteAllText(path, ManifestText("1.0.0.0", remoteUri), new UTF8Encoding(false));

            return new FileInfo(path);
        }

        [Fact]
        public async Task TheConstructor_DoesNotWaitForTheServerOfTheUpdateManifest()
        {
            using var server = new ManifestServer();

            var construct = Task.Run(() => new Updater(WriteLocalManifest(server.Url)));
            await server.RequestReceived.WaitAsync(TimeSpan.FromSeconds(30));   // the program has asked for the manifest...

            // ...and the server is holding the request: the constructor, and so the start of the program, must be through.
            var first = await Task.WhenAny(construct, Task.Delay(TimeSpan.FromSeconds(10)));

            Assert.Same(construct, first);
            Assert.False(server.Answered);

            server.Answer("503 Service Unavailable", string.Empty);   // an answer without a manifest changes nothing
            Assert.False((await construct).UpdateAvailable);
        }

        [Fact]
        public async Task ANewerManifest_IsFoundAfterTheConstructorHasReturned()
        {
            using var server = new ManifestServer();

            var construct = Task.Run(() => new Updater(WriteLocalManifest(server.Url)));
            await server.RequestReceived.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Same(construct, await Task.WhenAny(construct, Task.Delay(TimeSpan.FromSeconds(10))));   // not held by the server
            var updater = await construct;
            Assert.False(updater.UpdateAvailable);   // nothing is known yet

            server.Answer("200 OK", ManifestText("99.0.0.0", server.Url));

            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (!updater.UpdateAvailable && DateTime.UtcNow < deadline)
            {
                await Task.Delay(50);
            }

            Assert.True(updater.UpdateAvailable);
        }

        // A web server of one request: it reads the request, tells that it has it, holds it until it is told what to answer, and
        // answers once.
        private sealed class ManifestServer : IDisposable
        {
            private readonly TcpListener _listener = new TcpListener(IPAddress.Loopback, 0);
            private readonly TaskCompletionSource<bool> _requested = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource<(string Status, string Body)> _answer = new TaskCompletionSource<(string Status, string Body)>(TaskCreationOptions.RunContinuationsAsynchronously);

            public ManifestServer()
            {
                _listener.Start();
                _ = Task.Run(ServeAsync);
            }

            public string Url => "http://127.0.0.1:" + ((IPEndPoint)_listener.LocalEndpoint).Port + "/update.xml";

            public Task RequestReceived => _requested.Task;

            public bool Answered { get; private set; }

            public void Answer(string status, string body) => _answer.TrySetResult((status, body));

            public void Dispose()
            {
                _answer.TrySetCanceled();
                _listener.Stop();
            }

            private async Task ServeAsync()
            {
                try
                {
                    using var client = await _listener.AcceptTcpClientAsync();
                    using var stream = client.GetStream();

                    var seen = new StringBuilder();
                    var buffer = new byte[1024];
                    while (!seen.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
                    {
                        var read = await stream.ReadAsync(buffer);
                        if (read == 0)
                        {
                            return;
                        }

                        seen.Append(Encoding.ASCII.GetString(buffer, 0, read));
                    }

                    _requested.TrySetResult(true);

                    var (status, body) = await _answer.Task;
                    var bytes = Encoding.UTF8.GetBytes(body);
                    var head = "HTTP/1.1 " + status + "\r\nContent-Type: text/xml; charset=utf-8\r\nContent-Length: " + bytes.Length + "\r\nConnection: close\r\n\r\n";
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(head));
                    await stream.WriteAsync(bytes);
                    Answered = true;
                }
                catch (Exception ex) when (ex is ObjectDisposedException || ex is SocketException || ex is IOException || ex is TaskCanceledException)
                {
                    // The test is over and the listener was stopped.
                }
            }
        }
    }
}
