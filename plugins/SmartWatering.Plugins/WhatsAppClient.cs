using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using SmartWatering.Plugins.Logic;

namespace SmartWatering.Plugins
{
    /// <summary>What happened to one message.</summary>
    public sealed class SendOutcome
    {
        /// <summary>True when Meta accepted the message.</summary>
        public bool Ok { get; set; }

        /// <summary>HTTP status code, or 0 when there was no response.</summary>
        public int StatusCode { get; set; }

        /// <summary>Meta's message ID (wamid...) when accepted.</summary>
        public string MessageId { get; set; }

        /// <summary>One line explaining the failure.</summary>
        public string Error { get; set; }
    }

    /// <summary>
    /// Sends one message to the Meta WhatsApp Cloud API with an HTTPS POST and a Bearer token.
    /// One shared HttpClient with a 15 second timeout; the token is sent per request and never traced.
    /// </summary>
    public sealed class WhatsAppClient
    {
        /// <summary>Timeout for one send.</summary>
        public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

        private static readonly HttpClient Http = CreateClient();

        private static HttpClient CreateClient()
        {
            // .NET Framework 4.6.2 may not offer TLS 1.2 by default; graph.facebook.com requires it.
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            return new HttpClient { Timeout = Timeout };
        }

        /// <summary>Posts one payload.</summary>
        /// <param name="url">From <see cref="WhatsAppPayload.MessagesUrl"/>.</param>
        /// <param name="accessToken">Meta access token (system user token for anything beyond a quick test).</param>
        /// <param name="payloadJson">From <see cref="WhatsAppPayload.Text"/> or <see cref="WhatsAppPayload.Template"/>.</param>
        public SendOutcome Send(string url, string accessToken, string payloadJson)
        {
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, url))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    request.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");

                    using (var response = Http.SendAsync(request).GetAwaiter().GetResult())
                    {
                        string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        int status = (int)response.StatusCode;
                        if (response.IsSuccessStatusCode)
                        {
                            return new SendOutcome { Ok = true, StatusCode = status, MessageId = WhatsAppPayload.TryReadMessageId(body) };
                        }
                        return new SendOutcome { Ok = false, StatusCode = status, Error = "Meta returned " + status + ", " + WhatsAppPayload.DescribeError(body) };
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return new SendOutcome { Ok = false, Error = "timed out after " + (int)Timeout.TotalSeconds + " s" };
            }
            catch (HttpRequestException ex)
            {
                return new SendOutcome { Ok = false, Error = "network error, " + (ex.InnerException?.Message ?? ex.Message) };
            }
        }
    }
}
