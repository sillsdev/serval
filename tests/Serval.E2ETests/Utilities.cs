namespace Serval.E2ETests;

public class Utilities
{
    static async public Task<string> GetAccessTokenFromEnvironment(HttpClient httpClient)
    {
        var access_token = "";
        var client_id = Environment.GetEnvironmentVariable("SERVAL_CLIENT_ID");
        var client_secret = Environment.GetEnvironmentVariable("SERVAL_CLIENT_SECRET");
        if (client_id == null)
        {
            Console.WriteLine(
                "You need an auth0 client_id in the environment variable SERVAL_CLIENT_ID!  Look at README for instructions on getting one."
            );
        }
        else if (client_secret == null)
        {
            Console.WriteLine(
                "You need an auth0 client_secret in the environment variable SERVAL_CLIENT_SECRET!  Look at README for instructions on getting one."
            );
        }
        else
        {
            var authHttpClient = new HttpClient();
            authHttpClient.Timeout = TimeSpan.FromSeconds(3);
            var request = new HttpRequestMessage(HttpMethod.Post, "https://sil-appbuilder.auth0.com/oauth/token");
            request.Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    { "grant_type", "client_credentials" },
                    { "client_id", client_id },
                    { "client_secret", client_secret },
                    { "audience", "https://machine.sil.org" },
                }
            );
            var response = authHttpClient.SendAsync(request).Result;
            if (response.Content is null)
                throw new HttpRequestException("Error getting auth0 Authentication.");
            else
            {
                var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(
                    await response.Content.ReadAsStringAsync()
                );
                access_token = dict?["access_token"] ?? "";
            }
        }
        return access_token;
    }
}
