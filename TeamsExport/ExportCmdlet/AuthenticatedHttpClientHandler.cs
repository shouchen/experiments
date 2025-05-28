using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ExportCmdlet
{
    public class AuthenticatedHttpClientHandler : DelegatingHandler
    {
        public AuthenticatedHttpClientHandler(string clientId, string tenantId, string clientSecret)
        {
            this.clientId = clientId;
            this.tenantId = tenantId;
            this.clientSecret = clientSecret;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(accessToken))
            {
                GetAccessToken().GetAwaiter().GetResult();
            }

            request.Headers.Authorization = new AuthenticationHeaderValue(tokenType, accessToken);

            for (var defaultBackoffSeconds = 1; defaultBackoffSeconds <= 4; defaultBackoffSeconds *= 2)
            {
                var response = await base.SendAsync(request, cancellationToken);

                switch(response.StatusCode)
                {
                    case HttpStatusCode.Unauthorized:
                        GetAccessToken().GetAwaiter().GetResult();
                        request.Headers.Authorization = new AuthenticationHeaderValue(tokenType, accessToken);
                        break;

                    case HttpStatusCode.TooManyRequests:
                        var delaySeconds = defaultBackoffSeconds;

                        if (response.Headers.TryGetValues("Retry-After", out var values))
                        {
                            var retryAfterValue = values.FirstOrDefault();

                            if (int.TryParse(retryAfterValue, out var seconds))
                            {
                                delaySeconds = seconds;
                            }
                            else if (DateTimeOffset.TryParse(retryAfterValue, out var retryDate))
                            {
                                delaySeconds = (int)(retryDate - DateTimeOffset.UtcNow).TotalSeconds;
                            }
                        }

                        if (delaySeconds > 0)
                            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                        break;

                    default:
                        return response;
                }
            }

            throw new HttpRequestException("All retry attempts for the HTTP request have been exhausted (Unauthorized or TooManyRequests).");
        }

        private async Task GetAccessToken()
        {
            var body = new Dictionary<string, string>
               {
                   { "client_id", clientId },
                   { "client_secret", clientSecret },
                   { "scope", "https://graph.microsoft.com/.default" },
                   { "grant_type", "client_credentials" }
               };

            var request = new HttpRequestMessage(HttpMethod.Post, $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token")
            {
                Content = new FormUrlEncodedContent(body)
            };

            var client = new HttpClient();
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            tokenType = doc.RootElement.GetProperty("token_type").GetString() ?? string.Empty;
            accessToken = doc.RootElement.GetProperty("access_token").GetString() ?? string.Empty;
        }

        private readonly string clientId, tenantId, clientSecret;
        private string tokenType = string.Empty, accessToken = string.Empty;
    }
}
