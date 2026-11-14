namespace Bloxstrap.Accounts
{
    /// <summary>
    /// Requests Roblox authentication tickets, which let a client start already signed in
    /// as the account the ticket was created for.
    /// </summary>
    internal static class RobloxAuth
    {
        private const string LOG_IDENT = "RobloxAuth";
        private const string CookieName = ".ROBLOSECURITY";
        private const string TicketUrl = "https://auth.roblox.com/v1/authentication-ticket/";

        public static async Task<string?> GetAuthenticationTicketAsync(string cookie, CancellationToken token = default)
        {
            try
            {
                using var firstRequest = CreateRequest(cookie, null);
                using var firstResponse = await App.HttpClient.SendAsync(firstRequest, token);

                if (firstResponse.IsSuccessStatusCode)
                    return ReadTicket(firstResponse);

                if (firstResponse.StatusCode != HttpStatusCode.Forbidden ||
                    !firstResponse.Headers.TryGetValues("x-csrf-token", out var csrfValues))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Ticket request failed with HTTP {(int)firstResponse.StatusCode}");
                    return null;
                }

                using var retryRequest = CreateRequest(cookie, csrfValues.First());
                using var retryResponse = await App.HttpClient.SendAsync(retryRequest, token);

                if (!retryResponse.IsSuccessStatusCode)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Ticket request failed with HTTP {(int)retryResponse.StatusCode}");
                    return null;
                }

                return ReadTicket(retryResponse);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to get an authentication ticket");
                App.Logger.WriteException(LOG_IDENT, ex);
                return null;
            }
        }

        private static HttpRequestMessage CreateRequest(string cookie, string? csrfToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, TicketUrl)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };

            request.Headers.TryAddWithoutValidation("Cookie", $"{CookieName}={cookie}");
            request.Headers.TryAddWithoutValidation("Referer", "https://www.roblox.com/");

            if (csrfToken is not null)
                request.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", csrfToken);

            return request;
        }

        private static string? ReadTicket(HttpResponseMessage response)
        {
            if (response.Headers.TryGetValues("rbx-authentication-ticket", out var values))
                return values.FirstOrDefault();

            return null;
        }
    }
}