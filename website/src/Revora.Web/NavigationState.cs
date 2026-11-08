using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;

namespace Revora.Web;

public sealed record NavigationState
{
    private NavigationState(string? username, string? antiforgeryToken)
    {
        Username = username;
        AntiforgeryToken = antiforgeryToken;
    }

    public static NavigationState Anonymous { get; } = new(null, null);
    public string? Username { get; }
    public string? AntiforgeryToken { get; }
    public bool IsAuthenticated => Username is not null;

    public static NavigationState Authenticated(string username, string antiforgeryToken)
    {
        if (string.IsNullOrWhiteSpace(username)) throw new ArgumentException("An authenticated username is required.", nameof(username));
        if (string.IsNullOrWhiteSpace(antiforgeryToken)) throw new ArgumentException("An antiforgery token is required.", nameof(antiforgeryToken));
        return new NavigationState(username, antiforgeryToken);
    }

    public static NavigationState FromValidatedPrincipal(HttpContext context, IAntiforgery antiforgery)
    {
        if (context.User.Identity?.IsAuthenticated != true) return Anonymous;
        string username = context.User.FindFirstValue(ClaimTypes.Name) ?? throw new InvalidOperationException("The authenticated username claim is missing.");
        if (string.IsNullOrWhiteSpace(username)) throw new InvalidOperationException("The authenticated username claim is empty.");
        string token = antiforgery.GetAndStoreTokens(context).RequestToken ?? throw new InvalidOperationException("An antiforgery token could not be created.");
        return Authenticated(username, token);
    }
}
