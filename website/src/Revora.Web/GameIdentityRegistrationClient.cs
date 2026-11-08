using System.Net.Http.Json;

namespace Revora.Web;

public sealed class GameIdentityRegistrationClient(HttpClient httpClient, WebOptions options)
{
    public async Task<bool> RegisterAsync(string username, string password, CancellationToken cancellationToken)
    {
        if (!options.RegistrationEnabled) return false;
        Uri endpoint = WebOptions.RegistrationEndpoint(options);
        using var response = await httpClient.PostAsJsonAsync(endpoint, new RegistrationRequest(username, password), cancellationToken);
        return response.IsSuccessStatusCode;
    }

    private sealed record RegistrationRequest(string Username, string Password);
}
