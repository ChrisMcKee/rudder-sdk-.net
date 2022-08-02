using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RudderStack.Exception;
using RudderStack.Model;

namespace RudderStack.Request
{
    class WebProxy : System.Net.IWebProxy
    {
        private string _proxy;
        private static Uri NullUrlObj = new Uri("");

        public WebProxy(string proxy)
        {
            _proxy = proxy;
            GetProxy(new Uri(proxy)); // ** What does this do?
        }

        public System.Net.ICredentials Credentials { get; set; }

        public Uri GetProxy(Uri destination)
        {
            if (!string.IsNullOrWhiteSpace(destination.ToString()))
                return destination;

            return NullUrlObj;
        }

        public bool IsBypassed(Uri host)
        {
            return !string.IsNullOrWhiteSpace(host.ToString());
        }
    }

    internal class BlockingRequestHandler : IRequestHandler
    {
        /// <summary>
        /// RudderStack client to mark statistics
        /// </summary>
        private readonly RudderClient _client;

        private readonly Backoff _backoff;

        private readonly int _maxBackOffDuration;

        private readonly HttpClient _httpClient;

        /// <summary>
        /// The maximum amount of time to wait before calling
        /// the HTTP flush a timeout failure.
        /// </summary>
        public TimeSpan Timeout { get; set; }

        internal BlockingRequestHandler(RudderClient client, TimeSpan timeout) : this(client, timeout, null,
            new Backoff(max: 10000, jitter: 5000)) // Set maximum waiting limit to 10s and jitter to 5s
        {
        }

        internal BlockingRequestHandler(RudderClient client, TimeSpan timeout, Backoff backoff) : this(client, timeout,
            null, backoff)
        {
        }

        internal BlockingRequestHandler(RudderClient client, TimeSpan timeout, HttpClient httpClient, Backoff backoff)
        {
            this._client = client;
            _backoff = backoff;

            this.Timeout = timeout;

            if (httpClient != null)
            {
                _httpClient = httpClient;
            }
            else
            {
                var handler = new HttpClientHandler();

                // Set proxy information
                if (!string.IsNullOrEmpty(_client.Config.Proxy))
                {
                    handler.Proxy = new WebProxy(_client.Config.Proxy);
                    handler.UseProxy = true;
                }

                // Initialize HttpClient instance with given configuration
                _httpClient = new HttpClient(handler) { Timeout = Timeout };
            }

            // Send user agent in the form of {library_name}/{library_version} as per RFC 7231.
            var userAgent = _client.Config.UserAgent;

            _httpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);
        }

        public async Task MakeRequest(Batch batch)
        {
            Stopwatch watch = new Stopwatch();

            try
            {
                Uri uri = new Uri(_client.Config.DataPlaneUrl + "/v1/batch");

                // set the current request time
                batch.SentAt = DateTime.UtcNow.ToString("o");

                string json = JsonConvert.SerializeObject(batch);

                // Basic Authentication
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Basic", BasicAuthHeader(batch.WriteKey, string.Empty));

                // Prepare request data;
                var requestData = Encoding.UTF8.GetBytes(json);

                // Compress request data if compression is set
//                 if (_client.Config.Gzip)
//                 {
// #if NET35
//                     _httpClient.Headers.Set(HttpRequestHeader.ContentEncoding, "gzip");
// #else
//                     //_httpClient.DefaultRequestHeaders.Add("Content-Encoding", "gzip");
// #endif
//
//                     // Compress request data with GZip
//                     using (MemoryStream memory = new MemoryStream())
//                     {
//                         using (GZipStream gzip = new GZipStream(memory, CompressionMode.Compress, true))
//                         {
//                             gzip.Write(requestData, 0, requestData.Length);
//                         }
//                         requestData = memory.ToArray();
//                     }
//                 }

                Logger.Info("Sending analytics request to RudderStack ..",
                    new string[,]
                    {
                        { "batch id", batch.MessageId }, { "json size", json.Length.ToString() },
                        { "batch size", batch.batch.Count.ToString() }
                    });

                // Retries with exponential backoff
                int statusCode = (int)HttpStatusCode.OK;
                string responseStr = "";

                while (!_backoff.HasReachedMax)
                {
                    watch.Start();

                    ByteArrayContent content = new ByteArrayContent(requestData);
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
//                     if (_client.Config.Gzip)
//                     {
//                       content.Headers.ContentEncoding.Add("gzip");
//                     }

                    HttpResponseMessage response = null;
                    bool retry = false;
                    try
                    {
                        response = await _httpClient.PostAsync(uri, content).ConfigureAwait(false);
                    }
                    catch (TaskCanceledException e)
                    {
                        Logger.Info("HTTP Post failed with exception of type TaskCanceledException",
                            new string[,]
                            {
                                { "batch id", batch.MessageId }, { "reason", e.Message },
                                { "duration (ms)", watch.ElapsedMilliseconds.ToString() }
                            });
                        retry = true;
                    }
                    catch (HttpRequestException e)
                    {
                        Logger.Info("HTTP Post failed with exception of type HttpRequestException",
                            new string[,]
                            {
                                { "batch id", batch.MessageId }, { "reason", e.Message },
                                { "duration (ms)", watch.ElapsedMilliseconds.ToString() }
                            });
                        retry = true;
                    }

                    watch.Stop();
                    statusCode = response != null ? (int)response.StatusCode : 0;

                    if (response is { StatusCode: HttpStatusCode.OK })
                    {
                        Succeed(batch, watch.ElapsedMilliseconds);
                        break;
                    }

                    if (statusCode is >= 500 and <= 600 or 429 || retry)
                    {
                        // If status code is greater than 500 and less than 600, it indicates server error
                        // Error code 429 indicates rate limited.
                        // Retry uploading in these cases.
                        await _backoff.AttemptAsync().ConfigureAwait(false);

                        Logger.Info(
                            statusCode == 429
                                ? $"Too many request at the moment CurrentAttempt:{_backoff.CurrentAttempt} Retrying to send request"
                                : $"Internal RudderStack Server error CurrentAttempt:{_backoff.CurrentAttempt} Retrying to send request",
                            new string[,]
                            {
                                { "batch id", batch.MessageId }, { "statusCode", statusCode.ToString() },
                                { "duration (ms)", watch.ElapsedMilliseconds.ToString() }
                            });
                    }
                    else
                    {
                        //HTTP status codes smaller than 500 or greater than 600 except for 429 are either Client errors or a correct status
                        //This means it should not retry
                        break;
                    }
                }

                var hasBackoffReachedMax = _backoff.HasReachedMax;
                if (hasBackoffReachedMax || statusCode != (int)HttpStatusCode.OK)
                {
                    var message =
                        $"Has Backoff reached max: {hasBackoffReachedMax} with number of Attempts:{_backoff.CurrentAttempt},\n Status Code: {statusCode}\n, response message: {responseStr}";
                    Fail(batch, new APIException(statusCode.ToString(), message), watch.ElapsedMilliseconds);
                    if (_backoff.HasReachedMax)
                    {
                        _backoff.Reset();
                    }
                }
            }
            catch (System.Exception e)
            {
                watch.Stop();
                Fail(batch, e, watch.ElapsedMilliseconds);
            }
        }

        private void Fail(Batch batch, System.Exception e, long duration)
        {
            foreach (BaseAction action in batch.batch)
            {
                _client.Statistics.IncrementFailed();
                _client.RaiseFailure(action, e);
            }

            Logger.Info("RudderStack request failed.",
                new string[,]
                {
                    { "batch id", batch.MessageId }, { "reason", e.Message }, { "duration (ms)", duration.ToString() }
                });
        }

        private void Succeed(Batch batch, long duration)
        {
            foreach (BaseAction action in batch.batch)
            {
                _client.Statistics.IncrementSucceeded();
                _client.RaiseSuccess(action);
            }

            Logger.Info("RudderStack request successful.",
                new string[,] { { "batch id", batch.MessageId }, { "duration (ms)", duration.ToString() } });
        }

        private static string BasicAuthHeader(string user, string pass)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(user + ":" + pass));
        }
    }
}