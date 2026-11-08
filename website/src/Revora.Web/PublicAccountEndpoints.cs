using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Revora.Persistence.PostgreSql;

namespace Revora.Web;

public static class PublicAccountEndpoints
{
    public static void MapPublicAccounts(this WebApplication app, WebOptions options)
    {
        app.MapGet("/register", (HttpContext context, IAntiforgery csrf, FormProofService proof) => Private(PublicAccountRenderer.Register("en", Token(context, csrf), proof.Create(), navigation: NavigationState.FromValidatedPrincipal(context, csrf))));
        app.MapGet("/login", (HttpContext context, IAntiforgery csrf, FormProofService proof) => Private(PublicAccountRenderer.Login("en", Token(context, csrf), proof.Create(), navigation: NavigationState.FromValidatedPrincipal(context, csrf))));
        app.MapGet("/forgot-password", (HttpContext context, IAntiforgery csrf, FormProofService proof) => Private(PublicAccountRenderer.Forgot("en", Token(context, csrf), proof.Create(), navigation: NavigationState.FromValidatedPrincipal(context, csrf))));
        app.MapGet("/recover-account", (HttpContext context, IAntiforgery csrf, FormProofService proof) => Private(PublicAccountRenderer.Forgot("en", Token(context, csrf), proof.Create(), navigation: NavigationState.FromValidatedPrincipal(context, csrf))));
        app.MapGet("/reset-password", (HttpContext context, IAntiforgery csrf, FormProofService proof) => context.Request.Cookies.ContainsKey(AccountCookies.Reset) ? Private(PublicAccountRenderer.Reset("en", Token(context, csrf), proof.Create(), navigation: NavigationState.FromValidatedPrincipal(context, csrf))) : Private(PublicAccountRenderer.Generic("en", "Reset unavailable", "Start a new account recovery request.", NavigationState.FromValidatedPrincipal(context, csrf))));
        app.MapGet("/account", () => Results.LocalRedirect("/en/account")).RequireAuthorization();
        app.MapGet("/account/security", () => Results.LocalRedirect("/en/account/security")).RequireAuthorization();
        app.MapPost("/logout", Logout).RequireAuthorization();

        app.MapGet("/{culture:regex(^(ar|en)$)}/register", (HttpContext context, string culture, IAntiforgery csrf, FormProofService proof) => Private(PublicAccountRenderer.Register(culture, Token(context, csrf), proof.Create(), navigation: NavigationState.FromValidatedPrincipal(context, csrf))));
        app.MapPost("/{culture:regex(^(ar|en)$)}/register", Register).RequireRateLimiting("registration");
        app.MapGet("/{culture:regex(^(ar|en)$)}/login", (HttpContext context, string culture, IAntiforgery csrf, FormProofService proof) => Private(PublicAccountRenderer.Login(culture, Token(context, csrf), proof.Create(), navigation: NavigationState.FromValidatedPrincipal(context, csrf))));
        app.MapPost("/{culture:regex(^(ar|en)$)}/login", Login).RequireRateLimiting("authentication");
        app.MapGet("/{culture:regex(^(ar|en)$)}/forgot-password", (HttpContext context, string culture, IAntiforgery csrf, FormProofService proof) => Private(PublicAccountRenderer.Forgot(culture, Token(context, csrf), proof.Create(), navigation: NavigationState.FromValidatedPrincipal(context, csrf))));
        app.MapGet("/{culture:regex(^(ar|en)$)}/recover-account", (HttpContext context, string culture, IAntiforgery csrf, FormProofService proof) => Private(PublicAccountRenderer.Forgot(culture, Token(context, csrf), proof.Create(), navigation: NavigationState.FromValidatedPrincipal(context, csrf))));
        app.MapPost("/{culture:regex(^(ar|en)$)}/recover-account/code", RecoveryCode).RequireRateLimiting("recovery");
        app.MapPost("/{culture:regex(^(ar|en)$)}/recover-account/questions", QuestionRecovery).RequireRateLimiting("recovery");
        app.MapGet("/{culture:regex(^(ar|en)$)}/recover-account/pending", ContinuePendingRecovery).RequireRateLimiting("recovery");
        app.MapGet("/{culture:regex(^(ar|en)$)}/reset-password", (HttpContext context, string culture, IAntiforgery csrf, FormProofService proof) => context.Request.Cookies.ContainsKey(AccountCookies.Reset) ? Private(PublicAccountRenderer.Reset(culture, Token(context, csrf), proof.Create(), navigation: NavigationState.FromValidatedPrincipal(context, csrf))) : Private(PublicAccountRenderer.Generic(culture, "Reset unavailable", "Start a new account recovery request.", NavigationState.FromValidatedPrincipal(context, csrf))));
        app.MapPost("/{culture:regex(^(ar|en)$)}/reset-password", ResetPassword).RequireRateLimiting("recovery");

        RouteGroupBuilder account = app.MapGroup("/{culture:regex(^(ar|en)$)}/account").RequireAuthorization("DashboardUser");
        account.MapGet("", Dashboard);
        account.MapGet("/security", (HttpContext context, string culture, IAntiforgery csrf, FormProofService proof) => Private(PublicAccountRenderer.Security(culture, Token(context, csrf), proof.Create(), navigation: NavigationState.FromValidatedPrincipal(context, csrf))));
        account.MapPost("/security/password", ChangePassword);
        account.MapPost("/security/questions", ConfigureQuestions);
        account.MapPost("/security/recovery-codes/regenerate", RegenerateCodes);
        account.MapPost("/security/sessions/revoke-others", RevokeOtherSessions);
        account.MapGet("/security/recovery-codes", ShowCodes);
        account.MapGet("/security/recovery-codes.txt", DownloadCodes);
        account.MapPost("/security/recovery-codes/acknowledge", AcknowledgeCodes);
        app.MapPost("/{culture:regex(^(ar|en)$)}/logout", Logout).RequireAuthorization();
    }

    private static async Task<IResult> Register(HttpContext context, string culture, IAntiforgery csrf, FormProofService proof, GameIdentityRegistrationClient identity, WebOptions options)
    {
        await csrf.ValidateRequestAsync(context); IFormCollection form = await context.Request.ReadFormAsync(context.RequestAborted);
        string username = form["username"].ToString(); string password = form["password"].ToString();
        if (!options.RegistrationEnabled || !ValidBot(form, proof, options))
            return Private(PublicAccountRenderer.Register(culture, Token(context, csrf), proof.Create(), "The account could not be created. Check every field and try again.", "form", NavigationState.FromValidatedPrincipal(context, csrf)), 400);
        if (!UsernamePolicy.TryNormalize(username, out _))
            return Private(PublicAccountRenderer.Register(culture, Token(context, csrf), proof.Create(), "Use 4–15 English letters or numbers.", "username", NavigationState.FromValidatedPrincipal(context, csrf)), 400);
        if (!PublicPasswordPolicy.IsValid(password))
            return Private(PublicAccountRenderer.Register(culture, Token(context, csrf), proof.Create(), "Use 8–15 supported characters.", "password", NavigationState.FromValidatedPrincipal(context, csrf)), 400);
        if (password != form["confirmPassword"].ToString())
            return Private(PublicAccountRenderer.Register(culture, Token(context, csrf), proof.Create(), "The passwords do not match.", "confirmPassword", NavigationState.FromValidatedPrincipal(context, csrf)), 400);
        bool created;
        try { created = await identity.RegisterAsync(username, password, context.RequestAborted); }
        catch (HttpRequestException) { created = false; }
        catch (TaskCanceledException) when (!context.RequestAborted.IsCancellationRequested) { created = false; }
        if (!created) return Private(PublicAccountRenderer.Register(culture, Token(context, csrf), proof.Create(), "The account could not be created. Check every field and try again.", "form", NavigationState.FromValidatedPrincipal(context, csrf)), 400);
        return Private(PublicAccountRenderer.Generic(culture, "Account created", "Your Revora account is ready. Return to the game and sign in from the native login screen.", NavigationState.FromValidatedPrincipal(context, csrf)));
    }

    private static async Task<IResult> Login(HttpContext context, string culture, IAntiforgery csrf, FormProofService proof, WebAccountStore store, IdentityTokenService tokens, WebOptions options)
    {
        await csrf.ValidateRequestAsync(context); IFormCollection form = await context.Request.ReadFormAsync(context.RequestAborted);
        if (!ValidBot(form, proof, options)) return Private(PublicAccountRenderer.Login(culture, Token(context, csrf), proof.Create(), "Invalid username or password.", NavigationState.FromValidatedPrincipal(context, csrf)), 400);
        WebAccountIdentity? account = await store.AuthenticateAsync(form["username"].ToString(), form["password"].ToString(), context.RequestAborted);
        if (account is null) return Private(PublicAccountRenderer.Login(culture, Token(context, csrf), proof.Create(), "Invalid username or password.", NavigationState.FromValidatedPrincipal(context, csrf)), 400);
        bool trust = form["trustDevice"] == "true";
        await EstablishSession(context, store, tokens, account, trust);
        return Results.LocalRedirect($"/{culture}/account");
    }

    private static async Task EstablishSession(HttpContext context, WebAccountStore store, IdentityTokenService tokens, WebAccountIdentity account, bool trust)
    {
        string rawSession = tokens.NewToken(); DateTimeOffset expires = DateTimeOffset.UtcNow.AddHours(8);
        await store.CreateSessionAsync(tokens.Digest(rawSession), account, expires, context.RequestAborted);
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, account.AccountId.ToString()), new Claim(ClaimTypes.Name, account.Username), new Claim(ClaimTypes.Role, "Player"), new Claim("security_stamp", account.SecurityStamp.ToString()), new Claim("session_token", rawSession) };
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)), new AuthenticationProperties { IsPersistent = true, ExpiresUtc = expires, AllowRefresh = false });
        await store.RecordNetworkAsync(account.AccountId, tokens.NetworkDigest(context.Connection.RemoteIpAddress), DateTimeOffset.UtcNow.AddDays(30), context.RequestAborted);
        if (trust) { string rawDevice = tokens.NewToken(); await store.RecordDeviceAsync(account.AccountId, tokens.Digest(rawDevice), DateTimeOffset.UtcNow.AddDays(180), context.RequestAborted); context.Response.Cookies.Append(AccountCookies.Device, rawDevice, AccountCookies.DeviceCookie()); }
    }

    private static async Task<IResult> Dashboard(HttpContext context, string culture, IAntiforgery csrf, WebAccountStore store)
    {
        AccountDashboard? dashboard = await store.GetDashboardAsync(AccountId(context), context.RequestAborted);
        return dashboard is null ? Results.SignOut() : Private(PublicAccountRenderer.Dashboard(culture, dashboard, Token(context, csrf), NavigationState.FromValidatedPrincipal(context, csrf)));
    }

    private static async Task<IResult> ChangePassword(HttpContext context, string culture, IAntiforgery csrf, FormProofService proof, WebAccountStore store)
    {
        await csrf.ValidateRequestAsync(context); IFormCollection form = await context.Request.ReadFormAsync(context.RequestAborted);
        string password = form["newPassword"].ToString(); bool valid = proof.IsMature(form["formProof"], 1) && PublicPasswordPolicy.IsValid(password) && password == form["confirmPassword"].ToString();
        bool changed = valid && await store.ChangePasswordAsync(AccountId(context), form["currentPassword"].ToString(), password, context.RequestAborted);
        if (changed) { await context.SignOutAsync(); context.Response.Cookies.Delete(AccountCookies.Device); return Results.LocalRedirect($"/{culture}/login"); }
        return Private(PublicAccountRenderer.Security(culture, Token(context, csrf), proof.Create(), "The password was not changed.", NavigationState.FromValidatedPrincipal(context, csrf)), 400);
    }

    private static async Task<IResult> ConfigureQuestions(HttpContext context, string culture, IAntiforgery csrf, FormProofService proof, WebAccountStore store)
    {
        await csrf.ValidateRequestAsync(context); IFormCollection form = await context.Request.ReadFormAsync(context.RequestAborted);
        if (!proof.IsMature(form["formProof"], 1)) return Private(PublicAccountRenderer.Security(culture, Token(context, csrf), proof.Create(), "Questions were not saved.", NavigationState.FromValidatedPrincipal(context, csrf)), 400);
        try { await store.ConfigureQuestionsAsync(AccountId(context), Enumerable.Range(1, 3).Select(index => ((short)int.Parse(form[$"question{index}"]!), form[$"answer{index}"].ToString())).ToArray(), context.RequestAborted); }
        catch (Exception exception) when (exception is ArgumentException or FormatException) { return Private(PublicAccountRenderer.Security(culture, Token(context, csrf), proof.Create(), "Questions were not saved.", NavigationState.FromValidatedPrincipal(context, csrf)), 400); }
        return Private(PublicAccountRenderer.Security(culture, Token(context, csrf), proof.Create(), "Security questions saved. They cannot reset the account by themselves.", NavigationState.FromValidatedPrincipal(context, csrf)));
    }

    private static async Task<IResult> RecoveryCode(HttpContext context, string culture, IAntiforgery csrf, FormProofService proof, WebAccountStore store, IdentityTokenService tokens, WebOptions options)
    {
        await csrf.ValidateRequestAsync(context); IFormCollection form = await context.Request.ReadFormAsync(context.RequestAborted);
        if (!options.RecoveryEnabled || !proof.IsMature(form["formProof"], options.MinimumFormSeconds)) return GenericRecovery(context, culture, csrf);
        string canonical = form["recoveryCode"].ToString().Replace("-", string.Empty, StringComparison.Ordinal).ToUpperInvariant(); string rawAuth = tokens.NewToken();
        bool success = canonical.Length == 32 && await store.ConsumeRecoveryCodeAsync(form["username"].ToString(), tokens.Digest(canonical), tokens.Digest(rawAuth), DateTimeOffset.UtcNow.AddMinutes(15), context.RequestAborted);
        if (success) { context.Response.Cookies.Append(AccountCookies.Reset, rawAuth, AccountCookies.ShortLived()); return Results.LocalRedirect($"/{culture}/reset-password"); }
        return GenericRecovery(context, culture, csrf);
    }

    private static async Task<IResult> QuestionRecovery(HttpContext context, string culture, IAntiforgery csrf, FormProofService proof, WebAccountStore store, IdentityTokenService tokens, WebOptions options)
    {
        await csrf.ValidateRequestAsync(context); IFormCollection form = await context.Request.ReadFormAsync(context.RequestAborted);
        if (!options.RecoveryEnabled || !proof.IsMature(form["formProof"], options.MinimumFormSeconds)) return GenericRecovery(context, culture, csrf);
        var answers = new Dictionary<short, string>();
        try { foreach (int index in Enumerable.Range(1, 3)) answers.Add(short.Parse(form[$"question{index}"]!), form[$"answer{index}"].ToString()); } catch (Exception exception) when (exception is FormatException or ArgumentException) { return GenericRecovery(context, culture, csrf); }
        string rawRequest = tokens.NewToken(); byte[]? device = context.Request.Cookies.TryGetValue(AccountCookies.Device, out string? rawDevice) ? tokens.Digest(rawDevice) : null;
        QuestionRecoveryResult result = await store.CreateQuestionRecoveryAsync(form["username"].ToString(), answers, device, tokens.NetworkDigest(context.Connection.RemoteIpAddress), tokens.Digest(rawRequest), context.RequestAborted);
        if (result.Created) context.Response.Cookies.Append(AccountCookies.Pending, rawRequest, new CookieOptions { Secure = true, HttpOnly = true, SameSite = SameSiteMode.Strict, Path = "/", MaxAge = TimeSpan.FromHours(24) });
        return GenericRecovery(context, culture, csrf);
    }

    private static async Task<IResult> ResetPassword(HttpContext context, string culture, IAntiforgery csrf, FormProofService proof, WebAccountStore store, IdentityTokenService tokens)
    {
        await csrf.ValidateRequestAsync(context); IFormCollection form = await context.Request.ReadFormAsync(context.RequestAborted);
        if (!context.Request.Cookies.TryGetValue(AccountCookies.Reset, out string? raw) || !proof.IsMature(form["formProof"], 1)) return GenericRecovery(context, culture, csrf);
        string password = form["password"].ToString(); bool success = PublicPasswordPolicy.IsValid(password) && password == form["confirmPassword"].ToString() && await store.ResetPasswordAsync(tokens.Digest(raw), password, context.RequestAborted);
        context.Response.Cookies.Delete(AccountCookies.Reset); context.Response.Cookies.Delete(AccountCookies.Device);
        return success ? Private(PublicAccountRenderer.Generic(culture, "Password reset", "Your new password works immediately on the website and in the Revora client.", NavigationState.FromValidatedPrincipal(context, csrf))) : GenericRecovery(context, culture, csrf);
    }

    private static async Task<IResult> ContinuePendingRecovery(HttpContext context, string culture, IAntiforgery csrf, WebAccountStore store, IdentityTokenService tokens)
    {
        if (!context.Request.Cookies.TryGetValue(AccountCookies.Pending, out string? pending)) return GenericRecovery(context, culture, csrf);
        string authorization = tokens.NewToken();
        bool success = await store.RedeemPendingRecoveryAsync(tokens.Digest(pending), tokens.Digest(authorization), DateTimeOffset.UtcNow.AddMinutes(15), context.RequestAborted);
        if (!success) return GenericRecovery(context, culture, csrf);
        context.Response.Cookies.Delete(AccountCookies.Pending); context.Response.Cookies.Append(AccountCookies.Reset, authorization, AccountCookies.ShortLived());
        return Results.LocalRedirect($"/{culture}/reset-password");
    }

    private static async Task<IResult> RegenerateCodes(HttpContext context, string culture, IAntiforgery csrf, WebAccountStore store, IdentityTokenService tokens, RecoveryDisplayCache displays)
    {
        await csrf.ValidateRequestAsync(context); IReadOnlyList<RecoveryCode> codes = tokens.NewRecoveryCodes(); await store.RegenerateRecoveryCodesAsync(AccountId(context), codes.Select(static code => code.Digest).ToArray(), context.RequestAborted);
        string handle = displays.Add(codes.Select(static code => code.Plaintext).ToArray()); context.Response.Cookies.Append(AccountCookies.RecoveryDisplay, handle, AccountCookies.ShortLived()); return Results.LocalRedirect($"/{culture}/account/security/recovery-codes");
    }

    private static IResult ShowCodes(HttpContext context, string culture, IAntiforgery csrf, RecoveryDisplayCache displays) => context.Request.Cookies.TryGetValue(AccountCookies.RecoveryDisplay, out string? handle) && displays.Get(handle) is { } codes ? Private(PublicAccountRenderer.RecoveryCodes(culture, Token(context, csrf), codes, NavigationState.FromValidatedPrincipal(context, csrf))) : Private(PublicAccountRenderer.Generic(culture, "Recovery codes unavailable", "Recovery codes are displayed only once.", NavigationState.FromValidatedPrincipal(context, csrf)), 404);
    private static IResult DownloadCodes(HttpContext context, RecoveryDisplayCache displays) => context.Request.Cookies.TryGetValue(AccountCookies.RecoveryDisplay, out string? handle) && displays.Get(handle) is { } codes ? Results.Text("Revora recovery codes\n" + string.Join('\n', codes) + "\n", "text/plain", Encoding.UTF8) : Results.NotFound();
    private static async Task<IResult> AcknowledgeCodes(HttpContext context, string culture, IAntiforgery csrf, RecoveryDisplayCache displays) { await csrf.ValidateRequestAsync(context); if (context.Request.Cookies.TryGetValue(AccountCookies.RecoveryDisplay, out string? handle)) displays.Acknowledge(handle); context.Response.Cookies.Delete(AccountCookies.RecoveryDisplay); return Results.LocalRedirect($"/{culture}/account"); }
    private static async Task<IResult> RevokeOtherSessions(HttpContext context, string culture, IAntiforgery csrf, WebAccountStore store, IdentityTokenService tokens) { await csrf.ValidateRequestAsync(context); string raw = context.User.FindFirst("session_token")?.Value ?? throw new InvalidOperationException("The current session is missing."); await store.RevokeOtherSessionsAsync(AccountId(context), tokens.Digest(raw), context.RequestAborted); return Results.LocalRedirect($"/{culture}/account"); }
    private static async Task<IResult> Logout(HttpContext context, IAntiforgery csrf, WebAccountStore store, IdentityTokenService tokens) { await csrf.ValidateRequestAsync(context); if (context.User.FindFirst("session_token")?.Value is { } raw) await store.RevokeSessionAsync(tokens.Digest(raw), context.RequestAborted); await context.SignOutAsync(); return Results.LocalRedirect("/"); }

    private static bool ValidBot(IFormCollection form, FormProofService proof, WebOptions options) => string.IsNullOrEmpty(form["website"]) && proof.IsMature(form["formProof"], options.MinimumFormSeconds);
    private static Guid AccountId(HttpContext context) => Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("The account identity is missing."));
    private static string Token(HttpContext context, IAntiforgery csrf) => csrf.GetAndStoreTokens(context).RequestToken ?? throw new InvalidOperationException("An antiforgery token could not be created.");
    private static IResult GenericRecovery(HttpContext context, string culture, IAntiforgery csrf) => Private(PublicAccountRenderer.Generic(culture, "Recovery request received", "If the submitted details qualify, a reset will become available. Otherwise an administrator may review the sanitized request.", NavigationState.FromValidatedPrincipal(context, csrf)));
    private static IResult Private(string html, int status = 200) => Results.Content(html, "text/html; charset=utf-8", Encoding.UTF8, status).WithHeader("X-Robots-Tag", "noindex,nofollow").WithHeader("Cache-Control", "no-store,private");
}
