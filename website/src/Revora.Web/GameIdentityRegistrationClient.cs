using System.Net.Http.Json;

namespace Revora.Web;

public sealed class GameIdentityRegistrationClient(HttpClient httpClient)
{
    private static readonly Uri Endpoint = new("https://auth.playrevora.com/api/v1/identity/register", UriKind.Absolute);

    public async Task<bool> RegisterAsync(string username, string password, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(Endpoint, new RegistrationRequest(username, password), cancellationToken);
        return response.IsSuccessStatusCode;
    }

    private sealed record RegistrationRequest(string Username, string Password);
}
