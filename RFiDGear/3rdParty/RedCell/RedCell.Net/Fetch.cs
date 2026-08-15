using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RedCell.Net
{
    /// <summary>
    /// Fetches web pages.
    /// </summary>
    public class Fetch
    {
        #region Initialiation

        /// <summary>
        /// Initializes a new instance of the <see cref="Fetch"/> class.
        /// </summary>
        public Fetch()
        {
            Headers = new WebHeaderCollection();
            Retries = 5;
            Timeout = 60000;
        }

        #endregion Initialiation

        #region Properties

        /// <summary>
        /// Gets the headers.
        /// </summary>
        /// <value>The headers.</value>
        public WebHeaderCollection Headers { get; private set; }

        /// <summary>
        /// Gets the response.
        /// </summary>
        public HttpResponseMessage Response { get; private set; }

        /// <summary>
        ///
        /// </summary>
        public NetworkCredential Credential { get; set; }

        /// <summary>
        /// Gets the response data.
        /// </summary>
        public byte[] ResponseData { get; private set; }

        /// <summary>
        /// Gets or sets the retries.
        /// </summary>
        /// <value>The retries.</value>
        public int Retries { get; set; }

        /// <summary>
        /// Gets or sets the timeout.
        /// </summary>
        /// <value>The timeout.</value>
        public int Timeout { get; set; }

        /// <summary>
        /// Gets or sets the retry sleep in milliseconds.
        /// </summary>
        /// <value>The retry sleep.</value>
        public int RetrySleep { get; set; }

        /// <summary>
        /// Gets a value indicating whether this <see cref="Fetch"/> is success.
        /// </summary>
        /// <value><c>true</c> if success; otherwise, <c>false</c>.</value>
        public bool Success { get; private set; }

        #endregion Properties

        #region Methods

        /// <summary>
        /// Gets the specified URL.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <returns></returns>
        public void Load(string url)
        {
            for (var retry = 0; retry < Retries; retry++)
            {
                try
                {
                    using (var handler = new HttpClientHandler())
                    {
                        handler.AllowAutoRedirect = true;
                        handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                        if (Credential != null)
                        {
                            handler.Credentials = Credential;
                        }

                        using (var client = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(Timeout) })
                        using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                        {
                            foreach (var key in Headers.AllKeys)
                            {
                                request.Headers.TryAddWithoutValidation(key, Headers[key]);
                            }

                            Response = client.SendAsync(request).GetAwaiter().GetResult();
                            switch (Response.StatusCode)
                            {
                                case HttpStatusCode.Found:
                                    // This is a redirect to an error page, so ignore.
                                    Console.WriteLine("Found (302), ignoring ");
                                    break;

                                case HttpStatusCode.OK:
                                    // This is a valid page.
                                    ResponseData = Response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                                    break;

                                default:
                                    // This is unexpected.
                                    Console.WriteLine(Response.StatusCode);
                                    break;
                            }
                            Success = true;
                            break;
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    Response = null;
                    Thread.Sleep(RetrySleep);
                    continue;
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine(":Exception " + ex.Message);
                    Response = null;
                    break;
                }
            }
        }

        /// <summary>
        /// Gets the specified URL.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <returns></returns>
        public static byte[] Get(string url)
        {
            var f = new Fetch();
            f.Load(url);
            return f.ResponseData;
        }

        /// <summary>
        /// Downloads a URL asynchronously and reports byte-level progress.
        /// Falls back to indeterminate progress when <c>Content-Length</c> is absent.
        /// </summary>
        /// <param name="url">The URL to fetch.</param>
        /// <param name="progress">Receives <c>(bytesReceived, totalBytes)</c>; <c>totalBytes</c> is -1 when unknown.</param>
        /// <param name="cancellationToken">Token that aborts the download.</param>
        /// <returns>The downloaded bytes, or <see langword="null"/> on failure.</returns>
        public static async Task<byte[]> GetWithProgressAsync(
            string url,
            IProgress<(long bytesReceived, long totalBytes)> progress,
            CancellationToken cancellationToken)
        {
            using var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(60000) };

            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return null;

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var dest = new System.IO.MemoryStream(
                totalBytes > 0 ? (int)Math.Min(totalBytes, int.MaxValue) : 65536);

            var buffer = new byte[81920];
            long bytesReceived = 0;
            int read;

            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
            {
                dest.Write(buffer, 0, read);
                bytesReceived += read;
                progress?.Report((bytesReceived, totalBytes));
            }

            return dest.ToArray();
        }

        /// <summary>
        /// Gets the string.
        /// </summary>
        /// <returns></returns>
        public string GetString()
        {
            var charSet = Response?.Content?.Headers?.ContentType?.CharSet;
            var encoder = string.IsNullOrEmpty(charSet) ? Encoding.UTF8 : Encoding.GetEncoding(charSet);
            if (ResponseData == null)
            {
                return string.Empty;
            }

            return encoder.GetString(ResponseData);
        }

        #endregion Methods
    }
}