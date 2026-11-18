using System;
using System.Collections.Generic;
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
    /// A web server that answers the requests one after the other: it reads a request, tells that it has it, holds it until it is
    /// told what to answer (or to drop the connection without an answer), and answers once. The requests are numbered from 0.
    /// </summary>
    internal sealed class ManifestServer : IDisposable
    {
        private readonly TcpListener _listener = new TcpListener(IPAddress.Loopback, 0);
        private readonly List<TaskCompletionSource<bool>> _requested = new List<TaskCompletionSource<bool>>();
        private readonly List<TaskCompletionSource<(string Status, string Body)>> _answers = new List<TaskCompletionSource<(string Status, string Body)>>();

        internal ManifestServer()
        {
            _listener.Start();
            _ = Task.Run(ServeAsync);
        }

        internal string Url => "http://127.0.0.1:" + ((IPEndPoint)_listener.LocalEndpoint).Port + "/update.xml";

        // The first request.
        internal Task RequestReceived => Requested(0).Task;

        internal bool Answered { get; private set; }

        private volatile bool _stopped;

        internal Task RequestReceivedNumber(int number) => Requested(number).Task;

        internal void Answer(string status, string body, int number = 0) => Answers(number).TrySetResult((status, body));

        // The request gets an answer that is no HTTP, and the connection is closed: the client fails at once and does not try again.
        internal void Drop(int number = 0) => Answers(number).TrySetResult((string.Empty, string.Empty));

        public void Dispose()
        {
            _stopped = true;

            lock (_requested)
            {
                foreach (var answer in _answers)
                {
                    answer.TrySetCanceled();
                }
            }

            _listener.Stop();
        }

        private TaskCompletionSource<bool> Requested(int number)
        {
            lock (_requested)
            {
                while (_requested.Count <= number)
                {
                    _requested.Add(new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
                }

                return _requested[number];
            }
        }

        private TaskCompletionSource<(string Status, string Body)> Answers(int number)
        {
            lock (_requested)
            {
                while (_answers.Count <= number)
                {
                    _answers.Add(new TaskCompletionSource<(string Status, string Body)>(TaskCreationOptions.RunContinuationsAsynchronously));
                }

                return _answers[number];
            }
        }

        private async Task ServeAsync()
        {
            try
            {
                var number = 0;   // the number of the request that is waiting for its answer
                while (!_stopped)
                {
                    using var client = await _listener.AcceptTcpClientAsync();
                    using var stream = client.GetStream();

                    var seen = new StringBuilder();
                    var buffer = new byte[1024];
                    var complete = true;
                    while (!seen.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
                    {
                        var read = await stream.ReadAsync(buffer);
                        if (read == 0)
                        {
                            complete = false;
                            break;
                        }

                        seen.Append(Encoding.ASCII.GetString(buffer, 0, read));
                    }

                    if (!complete)
                    {
                        continue;   // a connection that never sent a request is not one
                    }

                    Requested(number).TrySetResult(true);

                    var (status, body) = await Answers(number).Task;
                    number++;

                    if (status.Length == 0)
                    {
                        // Garbled: something that is no HTTP, and the connection is closed. (A connection that is closed with no
                        // answer at all is tried again by the HTTP client, and the retry would be held here.)
                        await stream.WriteAsync(Encoding.ASCII.GetBytes("this is not http\r\n\r\n"));
                        continue;
                    }

                    var bytes = Encoding.UTF8.GetBytes(body);
                    var head = "HTTP/1.1 " + status + "\r\nContent-Type: text/xml; charset=utf-8\r\nContent-Length: " + bytes.Length + "\r\nConnection: close\r\n\r\n";
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(head));
                    await stream.WriteAsync(bytes);
                    Answered = true;
                }
            }
            catch (Exception ex) when (ex is ObjectDisposedException || ex is SocketException || ex is IOException || ex is TaskCanceledException || ex is InvalidOperationException)
            {
                // The test is over and the listener was stopped.
            }
        }
    }
}
