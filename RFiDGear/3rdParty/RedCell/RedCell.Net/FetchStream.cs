using System;
using System.Net;
using System.Net.Http;
using System.Text;

namespace RedCell.Net
{
    /// <summary>
    /// Fetches streamed content.
    /// </summary>
    public class FetchStream
    {
        #region Properties

        /// <summary>
        /// Gets the response.
        /// </summary>
        public HttpResponseMessage Response { get; private set; }

        /// <summary>
        /// Gets the response data.
        /// </summary>
        public byte[] ResponseData { get; private set; }

        #endregion Properties

        #region Methods

        /// <summary>
        /// Gets the specified URL.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <returns></returns>
        public void Load(string url)
        {
            try
            {
                using (var handler = new HttpClientHandler { AllowAutoRedirect = false })
                using (var client = new HttpClient(handler))
                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                {
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
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine(":Exception " + ex.Message);
                Response = null;
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