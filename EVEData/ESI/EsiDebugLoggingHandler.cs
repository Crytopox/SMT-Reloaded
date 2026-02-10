using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading;

namespace HISA.EVEData
{
    internal sealed class EsiDebugLoggingHandler : DelegatingHandler
    {
        private static long _requestCounter = 0;
        private static readonly HashSet<string> FormFieldsToRedact = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "code",
            "refresh_token",
            "token",
            "client_secret"
        };

        private const int MaxBodyLength = 12000;

        public EsiDebugLoggingHandler(HttpMessageHandler innerHandler) : base(innerHandler)
        {
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            long requestId = Interlocked.Increment(ref _requestCounter);
            Stopwatch timer = Stopwatch.StartNew();

            Debug.WriteLine($"[ESI:{requestId}] --> {request.Method} {request.RequestUri}");
            LogHeaders(requestId, "RequestHeaders", request.Headers);

            if (request.Content != null)
            {
                LogHeaders(requestId, "RequestContentHeaders", request.Content.Headers);
                (HttpContent requestClone, string requestBody) = await CloneAndReadContent(request.Content).ConfigureAwait(false);
                request.Content = requestClone;
                Debug.WriteLine($"[ESI:{requestId}] RequestBody: {Truncate(SanitizeRequestBody(request.Content, requestBody))}");
            }

            HttpResponseMessage response;
            try
            {
                response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                timer.Stop();
                Debug.WriteLine($"[ESI:{requestId}] !! Transport error after {timer.ElapsedMilliseconds} ms: {ex}");
                throw;
            }

            timer.Stop();
            Debug.WriteLine($"[ESI:{requestId}] <-- {(int)response.StatusCode} {response.ReasonPhrase} ({timer.ElapsedMilliseconds} ms)");
            LogHeaders(requestId, "ResponseHeaders", response.Headers);

            if (response.Content != null)
            {
                LogHeaders(requestId, "ResponseContentHeaders", response.Content.Headers);
                (HttpContent responseClone, string responseBody) = await CloneAndReadContent(response.Content).ConfigureAwait(false);
                response.Content = responseClone;
                Debug.WriteLine($"[ESI:{requestId}] ResponseBody: {Truncate(responseBody)}");
            }

            return response;
        }

        private static void LogHeaders(long requestId, string label, global::System.Net.Http.Headers.HttpHeaders headers)
        {
            if (headers == null)
            {
                return;
            }

            foreach (KeyValuePair<string, IEnumerable<string>> header in headers)
            {
                IEnumerable<string> values = header.Value.Select(v => SanitizeHeaderValue(header.Key, v));
                Debug.WriteLine($"[ESI:{requestId}] {label}: {header.Key}={string.Join(", ", values)}");
            }
        }

        private static async Task<(HttpContent Clone, string Body)> CloneAndReadContent(HttpContent content)
        {
            byte[] bytes = await content.ReadAsByteArrayAsync().ConfigureAwait(false);
            string body = DecodeBody(content, bytes);

            ByteArrayContent clone = new ByteArrayContent(bytes);
            foreach (KeyValuePair<string, IEnumerable<string>> header in content.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return (clone, body);
        }

        private static string DecodeBody(HttpContent content, byte[] bytes)
        {
            string charSet = content.Headers?.ContentType?.CharSet;
            if (!string.IsNullOrWhiteSpace(charSet))
            {
                try
                {
                    return Encoding.GetEncoding(charSet).GetString(bytes);
                }
                catch
                {
                    // Fall through to UTF-8.
                }
            }

            return Encoding.UTF8.GetString(bytes);
        }

        private static string SanitizeRequestBody(HttpContent content, string body)
        {
            string mediaType = content.Headers?.ContentType?.MediaType;
            if (!string.Equals(mediaType, "application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
            {
                return body;
            }

            string[] pairs = body.Split('&');
            for (int i = 0; i < pairs.Length; i++)
            {
                string pair = pairs[i];
                int idx = pair.IndexOf('=');
                if (idx < 0)
                {
                    continue;
                }

                string key = Uri.UnescapeDataString(pair.Substring(0, idx));
                if (FormFieldsToRedact.Contains(key))
                {
                    pairs[i] = pair.Substring(0, idx + 1) + "***";
                }
            }

            return string.Join("&", pairs);
        }

        private static string SanitizeHeaderValue(string key, string value)
        {
            if (string.Equals(key, "Authorization", StringComparison.OrdinalIgnoreCase))
            {
                int firstSpace = value.IndexOf(' ');
                if (firstSpace > 0)
                {
                    return value.Substring(0, firstSpace + 1) + "***";
                }

                return "***";
            }

            return value;
        }

        private static string Truncate(string body)
        {
            if (string.IsNullOrEmpty(body))
            {
                return body;
            }

            if (body.Length <= MaxBodyLength)
            {
                return body;
            }

            return body.Substring(0, MaxBodyLength) + $" ... [truncated, total {body.Length} chars]";
        }
    }
}
