using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace RFiDGear.Tests
{
    /// <summary>The update manifests and the web server that the tests of the update check use.</summary>
    internal static class UpdateManifestTexts
    {
        internal const string Token = "11111111-2222-3333-4444-555555555555";

        // Built from numbers: a text of four numbers with dots is taken for an IP address by static analysis.
        internal static readonly string OldVersion = new Version(1, 0, 0, 0).ToString();

        internal static readonly string NewerVersion = new Version(99, 0, 0, 0).ToString();

        internal static string ManifestText(string version, string remoteUri) =>
            "<?xml version=\"1.0\" encoding=\"utf-8\" ?>\n"
            + "<Manifest version=\"" + version + "\">\n"
            + "  <CheckInterval>900</CheckInterval>\n"
            + "  <RemoteConfigUri>" + remoteUri + "</RemoteConfigUri>\n"
            + "  <SecurityToken>" + Token + "</SecurityToken>\n"
            + "  <BaseUri>http://127.0.0.1/</BaseUri>\n"
            + "  <Payload>update.zip</Payload>\n"
            + "  <VersionInfoText>Version " + version + "</VersionInfoText>\n"
            + "</Manifest>";

        // The manifest that is installed with the program, in a folder of the test, asking for the remote one at remoteUri.
        internal static FileInfo WriteLocalManifest(string folder, string remoteUri)
        {
            var path = Path.Combine(folder, "update.xml");
            File.WriteAllText(path, ManifestText(OldVersion, remoteUri), new UTF8Encoding(false));

            return new FileInfo(path);
        }
    }

    /// <summary>
    /// A web server of one request: it reads the request, tells that it has it, holds it until it is told what to answer, and
    /// answers once.
    /// </summary>
    internal sealed class ManifestServer : IDisposable
    {
        private readonly TcpListener _listener = new TcpListener(IPAddress.Loopback, 0);
        private readonly TaskCompletionSource<bool> _requested = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<(string Status, string Body)> _answer = new TaskCompletionSource<(string Status, string Body)>(TaskCreationOptions.RunContinuationsAsynchronously);

        internal ManifestServer()
        {
            _listener.Start();
            _ = Task.Run(ServeAsync);
        }

        internal string Url => "http://127.0.0.1:" + ((IPEndPoint)_listener.LocalEndpoint).Port + "/update.xml";

        internal Task RequestReceived => _requested.Task;

        internal bool Answered { get; private set; }

        internal void Answer(string status, string body) => _answer.TrySetResult((status, body));

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
