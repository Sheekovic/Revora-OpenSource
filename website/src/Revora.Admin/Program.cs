using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Revora.Admin;
using Revora.Application.Authentication;
using Revora.Persistence.PostgreSql;
using Npgsql;

string configPath = ReadConfigPath(args);
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile(configPath, optional: false, reloadOnChange: false);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(settings => settings.TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'");
builder.Logging.AddFilter("Microsoft.AspNetCore.DataProtection", LogLevel.Error);
builder.Logging.AddFilter("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware", LogLevel.Critical);
builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Warning);

AdminOptions options = builder.Configuration.GetSection("RevoraAdmin").Get<AdminOptions>() ?? throw new InvalidDataException("RevoraAdmin configuration is required.");
options.Validate();
AdminSecrets secrets = AdminOptions.LoadSecrets(options.SecretFile);
if (secrets.DatabaseMode != "LocalPeer") throw new InvalidDataException("Revora.Admin requires the reviewed local peer database mode.");

string keyPath = Path.GetFullPath(options.DataProtectionKeysPath, builder.Environment.ContentRootPath);
Directory.CreateDirectory(keyPath);
builder.WebHost.UseUrls(options.Url);
builder.Services.AddDataProtection().SetApplicationName("Revora.Admin").PersistKeysToFileSystem(new DirectoryInfo(keyPath));
var database = new NpgsqlConnectionStringBuilder
{
    Host = "/var/run/postgresql",
    Database = "revora",
    Username = "revora-admin",
    Pooling = true,
    MaxPoolSize = 10,
};
var adminStore = new AdminDataStore(database.ConnectionString);
builder.Services.AddSingleton(adminStore);
builder.Services.AddSingleton<AdaptivePasswordHasher>();
builder.Services.AddSingleton<TotpService>();
builder.Services.AddAntiforgery(settings =>
{
    settings.SuppressXFrameOptionsHeader = true;
    settings.Cookie.Name = "__Host-RevoraAdmin-Csrf";
    settings.Cookie.HttpOnly = true;
    settings.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    settings.Cookie.SameSite = SameSiteMode.Strict;
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(settings =>
{
    settings.Cookie.Name = "__Host-RevoraAdmin";
    settings.Cookie.HttpOnly = true;
    settings.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    settings.Cookie.SameSite = SameSiteMode.Strict;
    settings.ExpireTimeSpan = TimeSpan.FromMinutes(15);
    settings.SlidingExpiration = true;
    settings.LoginPath = "/login";
    settings.AccessDeniedPath = "/denied";
    settings.Events.OnValidatePrincipal = async context =>
    {
        string? sessionValue = context.Principal?.FindFirstValue("session_id");
        string? adminValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (context.Principal?.FindFirstValue("access") == "setup" && Guid.TryParse(adminValue, out _))
        {
            return;
        }

        if (!Guid.TryParse(sessionValue, out Guid sessionId) || !Guid.TryParse(adminValue, out Guid adminId) ||
            !await context.HttpContext.RequestServices.GetRequiredService<AdminDataStore>().ValidateSessionAsync(sessionId, adminId, context.HttpContext.RequestAborted))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync();
        }
    };
});
builder.Services.AddAuthorizationBuilder().AddPolicy("Administrator", policy => policy.RequireClaim("access", "full"));
builder.Services.AddRateLimiter(settings => settings.AddFixedWindowLimiter("admin-login", limiter =>
{
    limiter.PermitLimit = 5;
    limiter.Window = TimeSpan.FromMinutes(1);
    limiter.QueueLimit = 0;
    limiter.AutoReplenishment = true;
}));
builder.Services.Configure<RateLimiterOptions>(settings => settings.AddFixedWindowLimiter("launcher-login", limiter =>
{
    limiter.PermitLimit = 5;
    limiter.Window = TimeSpan.FromMinutes(1);
    limiter.QueueLimit = 0;
    limiter.AutoReplenishment = true;
}));
builder.Services.Configure<ForwardedHeadersOptions>(settings =>
{
    settings.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    settings.KnownProxies.Add(IPAddress.Loopback);
    settings.ForwardLimit = 1;
});

if (args.Contains("--bootstrap", StringComparer.Ordinal))
{
    string username = (await Console.In.ReadLineAsync())?.Trim() ?? string.Empty;
    string password = await Console.In.ReadLineAsync() ?? string.Empty;
    if (username.Length is < 3 or > 64) throw new InvalidDataException("Bootstrap username is invalid.");
    string hash = new AdaptivePasswordHasher().Hash(password);
    try
    {
        await adminStore.CreateBootstrapAdministratorAsync(username, hash, CancellationToken.None);
        Console.WriteLine("Revora.Admin bootstrap identity created; first login requires password replacement and MFA enrollment.");
    }
    finally
    {
        password = string.Empty;
    }

    await adminStore.DisposeAsync();
    return;
}

WebApplication app = builder.Build();
app.UseForwardedHeaders();
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.WriteAsync(Shell("Request failed", "<h1>Request failed</h1><p>The operation could not be completed safely.</p>"));
}));
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
    context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'self'; img-src 'self'; form-action 'self'; base-uri 'none'; frame-ancestors 'none'";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
    context.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/admin.css", () => Results.Text(AdminStyles(), "text/css; charset=utf-8"));
app.MapGet("/login", (HttpContext context, IAntiforgery antiforgery) => Html(LoginPage(antiforgery.GetAndStoreTokens(context).RequestToken ?? string.Empty, null)));
app.MapPost("/login", LoginAsync).RequireRateLimiting("admin-login");
app.MapGet("/setup", SetupPageAsync).RequireAuthorization();
app.MapPost("/setup", SetupAsync).RequireAuthorization();
app.MapGet("/", DashboardAsync).RequireAuthorization("Administrator");
app.MapGet("/owner-downloads/client-doctor-test-1", () =>
{
    const string path = "/srv/revora-client-repository/owner-test/revora-client-doctor-test-1.zip";
    return Results.File(path, "application/octet-stream", "revora-client-doctor-test-1.zip", enableRangeProcessing: true);
}).RequireAuthorization("Administrator");
app.MapGet("/owner-downloads/client-doctor-test-2", () =>
{
    const string path = "/srv/revora-client-repository/owner-test/revora-client-doctor-test-2.zip";
    return Results.File(path, "application/octet-stream", "revora-client-doctor-test-2.zip", enableRangeProcessing: true);
}).RequireAuthorization("Administrator");
app.MapGet("/owner-downloads/client-doctor-test-2b", () =>
{
    const string path = "/srv/revora-client-repository/owner-test/revora-client-doctor-test-2b.zip";
    return Results.File(path, "application/octet-stream", "revora-client-doctor-test-2b.zip", enableRangeProcessing: true);
}).RequireAuthorization("Administrator");
app.MapGet("/owner-downloads/client-doctor-test-2c", () =>
{
    const string path = "/srv/revora-client-repository/owner-test/revora-client-doctor-test-2c.zip";
    return Results.File(path, "application/octet-stream", "revora-client-doctor-test-2c.zip", enableRangeProcessing: true);
}).RequireAuthorization("Administrator");
app.MapGet("/owner-downloads/client-native-login-test-3", () =>
{
    const string path = "/srv/revora-client-repository/owner-test/revora-client-native-login-test-3.zip";
    return Results.File(path, "application/octet-stream", "revora-client-native-login-test-3.zip", enableRangeProcessing: true);
}).RequireAuthorization("Administrator");
app.MapGet("/owner-downloads/client-native-bootstrap-test-4", () =>
{
    const string path = "/srv/revora-client-repository/owner-test/revora-client-native-bootstrap-test-4.zip";
    return Results.File(path, "application/octet-stream", "revora-client-native-bootstrap-test-4.zip", enableRangeProcessing: true);
}).RequireAuthorization("Administrator");
app.MapGet("/owner-downloads/client-native-bootstrap-test-4b", () =>
{
    const string path = "/srv/revora-client-repository/owner-test/revora-client-native-bootstrap-test-4b.zip";
    return Results.File(path, "application/octet-stream", "revora-client-native-bootstrap-test-4b.zip", enableRangeProcessing: true);
}).RequireAuthorization("Administrator");
app.MapGet("/owner-downloads/client-native-bootstrap-test-4c", () =>
{
    const string path = "/srv/revora-client-repository/owner-test/revora-client-native-bootstrap-test-4c.zip";
    return Results.File(path, "application/octet-stream", "revora-client-native-bootstrap-test-4c.zip", enableRangeProcessing: true);
}).RequireAuthorization("Administrator");
app.MapGet("/owner-downloads/client-native-bootstrap-test-4d", () =>
{
    const string path = "/srv/revora-client-repository/owner-test/revora-client-native-bootstrap-test-4d.zip";
    return Results.File(path, "application/octet-stream", "revora-client-native-bootstrap-test-4d.zip", enableRangeProcessing: true);
}).RequireAuthorization("Administrator");
app.MapGet("/owner-downloads/client-native-bootstrap-test-4d-r1", () =>
{
    const string path = "/srv/revora-client-repository/owner-test/revora-client-native-bootstrap-test-4d-r1.zip";
    return Results.File(path, "application/octet-stream", "revora-client-native-bootstrap-test-4d-r1.zip", enableRangeProcessing: true);
}).RequireAuthorization("Administrator");
app.MapGet("/owner-downloads/client-native-bootstrap-test-4e", () =>
{
    const string path = "/srv/revora-client-repository/owner-test/revora-client-native-bootstrap-test-4e.zip";
    return Results.File(path, "application/octet-stream", "revora-client-native-bootstrap-test-4e.zip", enableRangeProcessing: true);
}).RequireAuthorization("Administrator");
app.MapGet("/owner-downloads/client-native-bootstrap-test-4f", () =>
{
    const string path = "/srv/revora-client-repository/owner-test/revora-client-native-bootstrap-test-4f.zip";
    return Results.File(path, "application/octet-stream", "revora-client-native-bootstrap-test-4f.zip", enableRangeProcessing: true);
}).RequireAuthorization("Administrator");
app.MapGet("/owner-downloads/client-native-bootstrap-test-4g", () =>
{
    const string path = "/srv/revora-client-repository/owner-test/revora-client-native-bootstrap-test-4g.zip";
    return Results.File(path, "application/octet-stream", "revora-client-native-bootstrap-test-4g.zip", enableRangeProcessing: true);
}).RequireAuthorization("Administrator");
app.MapGet("/owner-downloads/client-native-bootstrap-test-4h", () =>
{
    const string path = "/srv/revora-client-repository/owner-test/revora-client-native-bootstrap-test-4h.zip";
    return Results.File(path, "application/octet-stream", "revora-client-native-bootstrap-test-4h.zip", enableRangeProcessing: true);
}).RequireAuthorization("Administrator");
app.MapGet("/owner-downloads/client-native-bootstrap-test-4i", () =>
{
    const string path = "/srv/revora-client-repository/owner-test/revora-client-native-bootstrap-test-4i.zip";
    return Results.File(path, "application/octet-stream", "revora-client-native-bootstrap-test-4i.zip", enableRangeProcessing: true);
}).RequireAuthorization("Administrator");
app.MapGet("/owner-downloads/client-native-bootstrap-test-4j", () =>
{
    const string path = "/srv/revora-client-repository/owner-test/revora-client-native-bootstrap-test-4j.zip";
    return Results.File(path, "application/octet-stream", "revora-client-native-bootstrap-test-4j.zip", enableRangeProcessing: true);
}).RequireAuthorization("Administrator");
app.MapGet("/owner-downloads/manual-playable-baseline-m1", () =>
{
    const string path = "/srv/revora-client-repository/owner-test/revora-manual-playable-baseline-m1.zip";
    return Results.File(path, "application/octet-stream", "revora-manual-playable-baseline-m1.zip", enableRangeProcessing: true);
}).RequireAuthorization("Administrator");
app.MapGet("/owner-downloads/manual-playable-m2-h1", () =>
{
    const string path = "/srv/revora-client-repository/owner-test/revora-manual-playable-m2-h1.zip";
    return Results.File(path, "application/octet-stream", "revora-manual-playable-m2-h1.zip", enableRangeProcessing: true);
}).RequireAuthorization("Administrator");
app.MapGet("/owner-downloads/manual-playable-vps", () =>
{
    const string path = "/srv/revora-client-repository/owner-test/revora-manual-playable-vps.zip";
    return Results.File(path, "application/octet-stream", "revora-manual-playable-vps.zip", enableRangeProcessing: true);
}).RequireAuthorization("Administrator");
app.MapGet("/owner-downloads/character-first-world-entry", () =>
{
    const string path = "/srv/revora-client-repository/owner-test/revora-character-first-world-entry.zip";
    return Results.File(path, "application/octet-stream", "revora-character-first-world-entry.zip", enableRangeProcessing: true);
}).RequireAuthorization("Administrator");
app.MapPost("/launcher-api/v1/session", LauncherSessionAsync).RequireRateLimiting("launcher-login");
app.MapPost("/launcher-api/v1/session/consume", ConsumeLauncherSessionAsync).RequireRateLimiting("launcher-login");
app.MapPost("/accounts/create", CreateAccountAsync).RequireAuthorization("Administrator");
app.MapPost("/accounts/{accountId:guid}/status", ChangeAccountStatusAsync).RequireAuthorization("Administrator");
app.MapPost("/accounts/{accountId:guid}/reset", ResetAccountAsync).RequireAuthorization("Administrator");
app.MapPost("/accounts/{accountId:guid}/ban", BanAccountAsync).RequireAuthorization("Administrator");
app.MapPost("/recoveries/{requestId:guid}/review", ReviewRecoveryAsync).RequireAuthorization("Administrator");
app.MapPost("/logout", LogoutAsync).RequireAuthorization();
app.MapGet("/denied", () => Results.Text("Access denied.", statusCode: StatusCodes.Status403Forbidden));

if (!await app.Services.GetRequiredService<AdminDataStore>().CanConnectAsync(CancellationToken.None)) throw new InvalidOperationException("Admin database readiness failed.");
app.Lifetime.ApplicationStarted.Register(() => Console.WriteLine("[Revora.Admin] READY"));
app.Lifetime.ApplicationStopped.Register(() => Console.WriteLine("[Revora.Admin] STOPPED"));
await app.RunAsync();

async Task<IResult> LauncherSessionAsync(HttpContext context, AdminDataStore store, AdaptivePasswordHasher hasher)
{
    LauncherLoginRequest? request;
    try { request = await context.Request.ReadFromJsonAsync<LauncherLoginRequest>(context.RequestAborted); }
    catch (JsonException) { return Results.Json(new { error = "authentication_failed" }, statusCode: 400); }
    if (request is null || request.Username is null || request.Password is null || request.ClientBuildId is null ||
        request.Username.Length is < 3 or > 32 || request.Password.Length is < 1 or > 256 ||
        request.ClientBuildId.Length is < 1 or > 128 || request.LauncherSessionId == Guid.Empty)
        return Results.Json(new { error = "authentication_failed" }, statusCode: 400);

    Guid? accountId = await store.AuthenticateGameAccountAsync(request.Username, request.Password, hasher, context.RequestAborted);
    request = request with { Password = string.Empty };
    if (accountId is null) return Results.Json(new { error = "authentication_failed" }, statusCode: 401);
    (byte[] ticket, Guid launchId) = await store.IssueLaunchTicketAsync(accountId.Value, request.LauncherSessionId, request.ClientBuildId, TimeSpan.FromSeconds(60), context.RequestAborted);
    try
    {
        return Results.Json(new { launchTicket = Convert.ToBase64String(ticket), launchId, expiresInSeconds = 60, realm = "Revora" });
    }
    finally { CryptographicOperations.ZeroMemory(ticket); }
}

async Task<IResult> ConsumeLauncherSessionAsync(HttpContext context, AdminDataStore store)
{
    if (!Guid.TryParse(context.Request.Query["launcherSessionId"], out Guid launcherSessionId) || launcherSessionId == Guid.Empty ||
        !Guid.TryParse(context.Request.Query["launchId"], out Guid launchId) || launchId == Guid.Empty)
        return Results.Json(new { consumed = false }, statusCode: 400);
    string clientBuildId = context.Request.Query["clientBuildId"].ToString();
    if (clientBuildId.Length is < 1 or > 128 || context.Request.ContentLength != LaunchTicket.Size)
        return Results.Json(new { consumed = false }, statusCode: 400);
    byte[] ticket = new byte[LaunchTicket.Size];
    try
    {
        await context.Request.Body.ReadExactlyAsync(ticket, context.RequestAborted);
        bool consumed = await store.ConsumeLaunchTicketAsync(ticket, launcherSessionId, launchId, clientBuildId, context.RequestAborted);
        return Results.Json(new { consumed }, statusCode: consumed ? 200 : 409);
    }
    finally { CryptographicOperations.ZeroMemory(ticket); }
}

async Task<IResult> LoginAsync(HttpContext context, IAntiforgery antiforgery, AdminDataStore store, AdaptivePasswordHasher hasher, TotpService totp, IDataProtectionProvider protection)
{
    await antiforgery.ValidateRequestAsync(context);
    IFormCollection form = await context.Request.ReadFormAsync(context.RequestAborted);
    string username = form["username"].ToString().Trim();
    string password = form["password"].ToString();
    string code = form["code"].ToString().Trim();
    bool rememberDevice = string.Equals(form["remember"].ToString(), "on", StringComparison.Ordinal);
    AdminIdentity? identity = username.Length <= 64 ? await store.FindAdministratorAsync(username, context.RequestAborted) : null;
    PasswordVerification verified = hasher.Verify(password, identity?.PasswordHash ?? "invalid");
    bool allowed = identity is not null && identity.Status == "active" && (identity.LockedUntil is null || identity.LockedUntil <= DateTimeOffset.UtcNow) && verified != PasswordVerification.Failed;
    if (allowed && identity!.TotpEnabled)
    {
        byte[] protectedSecret = identity.ProtectedTotpSecret ?? [];
        byte[] raw;
        try { raw = protection.CreateProtector("Revora.Admin.Totp.v1").Unprotect(protectedSecret); }
        catch (CryptographicException) { raw = []; }
        try { allowed = totp.Verify(raw, code); }
        finally { CryptographicOperations.ZeroMemory(raw); }
    }

    if (!allowed)
    {
        await store.RecordFailedLoginAsync(identity?.Id, context.RequestAborted);
        return Html(LoginPage(antiforgery.GetAndStoreTokens(context).RequestToken ?? string.Empty, "The supplied credentials could not be accepted."), StatusCodes.Status401Unauthorized);
    }

    if (identity!.MustChangePassword || identity.MustEnrollMfa || !identity.TotpEnabled)
    {
        byte[] rawSecret = totp.GenerateSecret();
        string encoded = Convert.ToBase64String(rawSecret);
        CryptographicOperations.ZeroMemory(rawSecret);
        await SignInAsync(context, identity, null, "setup", encoded, TimeSpan.FromMinutes(10));
        return Results.Redirect("/setup");
    }

    TimeSpan sessionLifetime = rememberDevice ? TimeSpan.FromDays(7) : TimeSpan.FromHours(8);
    TimeSpan cookieLifetime = rememberDevice ? TimeSpan.FromDays(7) : TimeSpan.FromMinutes(15);
    Guid sessionId = await store.CreateSessionAsync(identity.Id, sessionLifetime, context.RequestAborted);
    await store.AppendAuditAsync(identity.Id, "admin.login", "administrator", identity.Id.ToString(), "MFA-authenticated login", context.RequestAborted);
    await SignInAsync(context, identity, sessionId, "full", null, cookieLifetime, rememberDevice);
    return Results.Redirect("/");
}

IResult SetupPageAsync(HttpContext context, IAntiforgery antiforgery, TotpService totp)
{
    if (context.User.FindFirstValue("access") != "setup") return Results.Forbid();
    byte[] secret = Convert.FromBase64String(context.User.FindFirstValue("pending_totp") ?? string.Empty);
    string encoded = totp.EncodeSecret(secret);
    CryptographicOperations.ZeroMemory(secret);
    string token = antiforgery.GetAndStoreTokens(context).RequestToken ?? string.Empty;
    return Html(Shell("Secure enrollment", $"<section class=\"card\"><p class=\"eyebrow\">First-time security setup</p><h1>Secure your administrator account</h1><p>Choose a new password, then add the setup key below to Microsoft Authenticator, Google Authenticator, 1Password, or another TOTP app.</p><ol><li>Open your authenticator and choose <strong>Enter setup key</strong> or <strong>Manual entry</strong>.</li><li>Account name: <strong>Revora Administration</strong></li><li>Key type: <strong>Time based</strong></li></ol><div class=\"setup-key\"><span>Authenticator setup key</span><code>{HtmlEncoder.Default.Encode(encoded)}</code></div><p class=\"hint\">A QR image is not required: manual entry creates the same protected authenticator account. The setup key is shown only during this enrollment session.</p><form method=\"post\" action=\"/setup\"><input type=\"hidden\" name=\"__RequestVerificationToken\" value=\"{token}\"><label>New password<input type=\"password\" name=\"password\" minlength=\"12\" maxlength=\"256\" required autocomplete=\"new-password\"></label><label>Six-digit code from your app<input name=\"code\" inputmode=\"numeric\" pattern=\"[0-9]{{6}}\" maxlength=\"6\" required autocomplete=\"one-time-code\"></label><button type=\"submit\">Complete enrollment</button></form></section>"));
}

async Task<IResult> SetupAsync(HttpContext context, IAntiforgery antiforgery, AdminDataStore store, AdaptivePasswordHasher hasher, TotpService totp, IDataProtectionProvider protection)
{
    if (context.User.FindFirstValue("access") != "setup") return Results.Forbid();
    await antiforgery.ValidateRequestAsync(context);
    IFormCollection form = await context.Request.ReadFormAsync(context.RequestAborted);
    string password = form["password"].ToString();
    string code = form["code"].ToString().Trim();
    byte[] secret = Convert.FromBase64String(context.User.FindFirstValue("pending_totp") ?? string.Empty);
    if (!totp.Verify(secret, code))
    {
        CryptographicOperations.ZeroMemory(secret);
        return Results.Redirect("/setup");
    }

    Guid adminId = Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    byte[] protectedSecret = protection.CreateProtector("Revora.Admin.Totp.v1").Protect(secret);
    CryptographicOperations.ZeroMemory(secret);
    await store.CompleteEnrollmentAsync(adminId, hasher.Hash(password), protectedSecret, context.RequestAborted);
    await context.SignOutAsync();
    return Results.Redirect("/login");
}

async Task<IResult> DashboardAsync(HttpContext context, AdminDataStore store, IAntiforgery antiforgery)
{
    string query = context.Request.Query["q"].ToString();
    IReadOnlyList<GameAccountSummary> accounts = query.Length is > 0 and <= 32 ? await store.SearchAccountsAsync(query, context.RequestAborted) : [];
    IReadOnlyList<AuditSummary> audit = await store.RecentAuditAsync(context.RequestAborted);
    IReadOnlyList<PendingRecoverySummary> recoveries = await store.PendingRecoveriesAsync(context.RequestAborted);
    string status = await store.GetOperationalStatusAsync(context.RequestAborted);
    string token = antiforgery.GetAndStoreTokens(context).RequestToken ?? string.Empty;
    string rows = string.Join("", accounts.Select(item => $"<tr><td>{HtmlEncoder.Default.Encode(item.Username)}</td><td>{item.Status}</td><td>{item.CreatedAt:O}</td><td><form method=\"post\" action=\"/accounts/{item.Id}/status\"><input type=\"hidden\" name=\"__RequestVerificationToken\" value=\"{token}\"><input type=\"hidden\" name=\"reason\" value=\"Administrative security action\"><button name=\"status\" value=\"disabled\">Disable</button><button name=\"status\" value=\"active\">Unlock</button></form></td></tr>"));
    string auditRows = string.Join("", audit.Select(item => $"<tr><td>{item.OccurredAt:O}</td><td>{HtmlEncoder.Default.Encode(item.Action)}</td><td>{HtmlEncoder.Default.Encode(item.TargetType)}</td><td>{HtmlEncoder.Default.Encode(item.TargetId)}</td></tr>"));
    string recoveryRows = string.Join("", recoveries.Select(item => $"<tr><td>{item.RequestedAt:O}</td><td>{HtmlEncoder.Default.Encode(item.SupportCategory)}</td><td><form method=\"post\" action=\"/recoveries/{item.RequestId}/review\"><input type=\"hidden\" name=\"__RequestVerificationToken\" value=\"{token}\"><button name=\"decision\" value=\"approve\">Approve</button><button name=\"decision\" value=\"reject\">Reject</button></form></td></tr>"));
    string downloads = "<section class=\"download-card\"><h2>Current owner test</h2>" +
        "<p><strong>Character Creation and First World Entry</strong><br>Development package for genuine 104/1001 and same-session starter-map entry. A recovery relaunch is not the production onboarding flow. No proprietary executable is included. Requires Microsoft .NET Desktop Runtime 10 x64.<br>3,063,083 bytes · SHA-256 3775079fa10d6e159fb343ce0d8f5ca26419396849e91e14ad612ae98a0261a6</p>" +
        "<a class=\"button-link\" href=\"/owner-downloads/character-first-world-entry\">Download Character + First World Entry</a>" +
        "<p><strong>One-time prerequisite:</strong> Microsoft .NET 10 Desktop Runtime for Windows x64 from <a href=\"https://dotnet.microsoft.com/en-us/download/dotnet/10.0\" rel=\"noreferrer\">Microsoft's official .NET download page</a>.</p>" +
        "<details><summary>Archived manual baseline evidence</summary><a class=\"button-link secondary\" href=\"/owner-downloads/manual-playable-vps\">Archived VPS character-screen baseline</a> <a class=\"button-link secondary\" href=\"/owner-downloads/manual-playable-m2-h1\">Archived M2-H1</a> <a class=\"button-link secondary\" href=\"/owner-downloads/manual-playable-baseline-m1\">Archived M1</a></details>" +
        "<p><strong>Revora Client Doctor — Test Build 2c</strong><br>WPF thread-affinity fix, bounded UI progress, handled failures, and crash evidence.<br>146,317,883 bytes · SHA-256 d551396ca5db8299fd9b2499aa6af14c0ab518779d50d526ca9e39f7b558b54e</p>" +
        "<a class=\"button-link\" href=\"/owner-downloads/client-doctor-test-2c\">Download Client Doctor Test Build 2c</a>" +
        "<details><summary>Archived evidence packages (superseded)</summary><p>Preserved for forensic comparison; do not use for current testing.</p><a class=\"button-link secondary\" href=\"/owner-downloads/client-native-bootstrap-test-4j\">Archived Test 4j</a> <a class=\"button-link secondary\" href=\"/owner-downloads/client-native-bootstrap-test-4i\">Archived Test 4i</a> <a class=\"button-link secondary\" href=\"/owner-downloads/client-native-bootstrap-test-4h\">Archived Test 4h</a> <a class=\"button-link secondary\" href=\"/owner-downloads/client-native-bootstrap-test-4g\">Archived Test 4g</a> <a class=\"button-link secondary\" href=\"/owner-downloads/client-native-bootstrap-test-4f\">Archived Test 4f</a> <a class=\"button-link secondary\" href=\"/owner-downloads/client-native-bootstrap-test-4e\">Archived Test 4e</a> <a class=\"button-link secondary\" href=\"/owner-downloads/client-native-bootstrap-test-4d-r1\">Archived Test 4d Revision 1</a> <a class=\"button-link secondary\" href=\"/owner-downloads/client-native-bootstrap-test-4d\">Archived original Test 4d</a> <a class=\"button-link secondary\" href=\"/owner-downloads/client-native-bootstrap-test-4c\">Archived Test 4c</a> <a class=\"button-link secondary\" href=\"/owner-downloads/client-native-bootstrap-test-4b\">Archived Test 4b</a> <a class=\"button-link secondary\" href=\"/owner-downloads/client-native-bootstrap-test-4\">Archived Test 4</a> <a class=\"button-link secondary\" href=\"/owner-downloads/client-native-login-test-3\">Archived Test 3</a> <a class=\"button-link secondary\" href=\"/owner-downloads/client-doctor-test-2b\">Archived Doctor 2b</a> <a class=\"button-link secondary\" href=\"/owner-downloads/client-doctor-test-2\">Archived Doctor 2</a> <a class=\"button-link secondary\" href=\"/owner-downloads/client-doctor-test-1\">Archived Doctor 1</a></details></section>";
    downloads += $"<section><h2>Pending account recovery</h2><p>Only sanitized support categories are shown. Administrators never receive answers, codes, hashes, or new passwords.</p><table><thead><tr><th>Requested</th><th>Support</th><th>Decision</th></tr></thead><tbody>{recoveryRows}</tbody></table></section>";
    return Html(Shell("Revora Administration", $"<h1>Revora Administration</h1><p>System: {HtmlEncoder.Default.Encode(status)}</p><p>Game server: Development instance not publicly enabled. Online sessions are reported from durable session state.</p>{downloads}<h2>Create game account</h2><form method=\"post\" action=\"/accounts/create\"><input type=\"hidden\" name=\"__RequestVerificationToken\" value=\"{token}\"><label>Username<input name=\"username\" minlength=\"3\" maxlength=\"32\" required></label><label>Temporary password<input type=\"password\" name=\"password\" minlength=\"12\" maxlength=\"256\" required></label><button>Create with forced reset</button></form><form method=\"get\"><label>Game-account search<input name=\"q\" maxlength=\"32\" value=\"{HtmlEncoder.Default.Encode(query)}\"></label><button>Search</button></form><table><caption>Matching game accounts</caption><thead><tr><th>Username</th><th>Status</th><th>Created</th><th>Guarded actions</th></tr></thead><tbody>{rows}</tbody></table><table><caption>Administrative audit</caption><thead><tr><th>Time</th><th>Action</th><th>Target</th><th>Identifier</th></tr></thead><tbody>{auditRows}</tbody></table><p>Character, inventory, currency, item, SQL, shell, and online-state mutation controls are intentionally unavailable.</p><form method=\"post\" action=\"/logout\"><input type=\"hidden\" name=\"__RequestVerificationToken\" value=\"{token}\"><button>Sign out</button></form>"));
}

async Task<IResult> CreateAccountAsync(HttpContext context, IAntiforgery antiforgery, AdminDataStore store, AdaptivePasswordHasher hasher)
{
    if (!RecentlyAuthenticated(context.User)) return Results.Forbid();
    await antiforgery.ValidateRequestAsync(context);
    IFormCollection form = await context.Request.ReadFormAsync(context.RequestAborted);
    string username = form["username"].ToString().Trim();
    string password = form["password"].ToString();
    if (username.Length is < 3 or > 32 || !username.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '_' or '-')) return Results.BadRequest();
    Guid actor = Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    await store.CreateGameAccountAsync(actor, username, hasher.Hash(password), context.RequestAborted);
    return Results.Redirect($"/?q={Uri.EscapeDataString(username)}");
}

async Task<IResult> ReviewRecoveryAsync(Guid requestId, HttpContext context, IAntiforgery antiforgery, AdminDataStore store)
{
    if (!RecentlyAuthenticated(context.User)) return Results.Forbid();
    await antiforgery.ValidateRequestAsync(context);
    IFormCollection form = await context.Request.ReadFormAsync(context.RequestAborted);
    string decision = form["decision"].ToString();
    if (decision is not ("approve" or "reject")) return Results.BadRequest();
    Guid actor = Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    await store.ReviewRecoveryAsync(requestId, actor, decision == "approve", context.RequestAborted);
    return Results.Redirect("/");
}

async Task<IResult> ChangeAccountStatusAsync(Guid accountId, HttpContext context, IAntiforgery antiforgery, AdminDataStore store)
{
    if (!RecentlyAuthenticated(context.User)) return Results.Forbid();
    await antiforgery.ValidateRequestAsync(context);
    IFormCollection form = await context.Request.ReadFormAsync(context.RequestAborted);
    string reason = form["reason"].ToString().Trim();
    if (reason.Length is < 3 or > 1000) return Results.BadRequest();
    Guid actor = Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    if (form["status"] == "disabled") await store.DisableGameAccountAsync(actor, accountId, reason, context.RequestAborted);
    else if (form["status"] == "active") await store.UnlockGameAccountAsync(actor, accountId, reason, context.RequestAborted);
    else return Results.BadRequest();
    return Results.Redirect("/");
}

async Task<IResult> ResetAccountAsync(Guid accountId, HttpContext context, IAntiforgery antiforgery, AdminDataStore store, AdaptivePasswordHasher hasher)
{
    if (!RecentlyAuthenticated(context.User)) return Results.Forbid();
    await antiforgery.ValidateRequestAsync(context);
    IFormCollection form = await context.Request.ReadFormAsync(context.RequestAborted);
    string reason = form["reason"].ToString().Trim();
    if (reason.Length is < 3 or > 1000) return Results.BadRequest();
    await store.ForcePasswordResetAsync(Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!), accountId, hasher.Hash(form["password"].ToString()), reason, context.RequestAborted);
    return Results.Redirect("/");
}

async Task<IResult> BanAccountAsync(Guid accountId, HttpContext context, IAntiforgery antiforgery, AdminDataStore store)
{
    if (!RecentlyAuthenticated(context.User)) return Results.Forbid();
    await antiforgery.ValidateRequestAsync(context);
    IFormCollection form = await context.Request.ReadFormAsync(context.RequestAborted);
    string reason = form["reason"].ToString().Trim();
    if (reason.Length is < 3 or > 1000) return Results.BadRequest();
    DateTimeOffset? expiry = DateTimeOffset.TryParse(form["expires"], out DateTimeOffset value) && value > DateTimeOffset.UtcNow ? value : null;
    await store.BanGameAccountAsync(Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!), accountId, reason, expiry, context.RequestAborted);
    return Results.Redirect("/");
}

async Task<IResult> LogoutAsync(HttpContext context, IAntiforgery antiforgery, AdminDataStore store)
{
    await antiforgery.ValidateRequestAsync(context);
    if (Guid.TryParse(context.User.FindFirstValue("session_id"), out Guid sessionId)) await store.RevokeSessionAsync(sessionId, context.RequestAborted);
    await context.SignOutAsync();
    return Results.Redirect("/login");
}

static async Task SignInAsync(HttpContext context, AdminIdentity identity, Guid? sessionId, string access, string? pendingTotp, TimeSpan lifetime, bool persistent = false)
{
    var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, identity.Id.ToString()), new(ClaimTypes.Name, identity.Username), new("access", access), new("auth_time", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)) };
    if (sessionId is not null) claims.Add(new Claim("session_id", sessionId.Value.ToString()));
    if (pendingTotp is not null) claims.Add(new Claim("pending_totp", pendingTotp));
    await context.SignInAsync(new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)), new AuthenticationProperties { IsPersistent = persistent, ExpiresUtc = DateTimeOffset.UtcNow.Add(lifetime), AllowRefresh = access == "full" && !persistent });
}

static string LoginPage(string token, string? error) => Shell("Administrator sign in", $"<section class=\"card login-card\"><p class=\"eyebrow\">Protected operations portal</p><h1>Administrator sign in</h1><p>Use your separate Revora administrator identity. The authenticator code is required only after first-time enrollment.</p>{(error is null ? "" : $"<p class=\"alert\" role=\"alert\">{HtmlEncoder.Default.Encode(error)}</p>")}<form method=\"post\" action=\"/login\"><input type=\"hidden\" name=\"__RequestVerificationToken\" value=\"{token}\"><label>Username<input name=\"username\" maxlength=\"64\" required autocomplete=\"username\"></label><label>Password<input type=\"password\" name=\"password\" maxlength=\"256\" required autocomplete=\"current-password\"></label><label>Authenticator code <small>Leave blank on your first sign-in</small><input name=\"code\" inputmode=\"numeric\" pattern=\"[0-9]{{6}}\" maxlength=\"6\" autocomplete=\"one-time-code\"></label><label class=\"check\"><input type=\"checkbox\" name=\"remember\"> <span>Remember this device for 7 days</span></label><button type=\"submit\">Sign in securely</button></form><p class=\"hint\">A remembered session still ends after seven days, logout, administrator revocation, or browser-cookie removal.</p><p class=\"hint\">Private authorized access only · MFA protected · No public registration</p></section>");
static string Shell(string title, string content) => $"<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><meta name=\"robots\" content=\"noindex,nofollow\"><link rel=\"stylesheet\" href=\"/admin.css\"><title>{HtmlEncoder.Default.Encode(title)} · Revora</title></head><body><header><strong>REVORA</strong><span>Administration</span></header><main>{content}</main><footer>Revora private operations</footer></body></html>";
static string AdminStyles() => """
:root{color-scheme:dark;--gold:#d8aa58;--ink:#070b12;--panel:#111824;--line:#283446;--text:#f3f0e9;--muted:#aab4c2}*{box-sizing:border-box}body{margin:0;min-height:100vh;background:radial-gradient(circle at 50% 0,#1d2d40 0,transparent 38rem),linear-gradient(145deg,#070b12,#0b111b);color:var(--text);font:16px/1.55 system-ui,-apple-system,Segoe UI,sans-serif}header{height:76px;display:flex;align-items:center;gap:18px;padding:0 clamp(22px,5vw,72px);border-bottom:1px solid #ffffff12;background:#080d15cc}header strong{color:var(--gold);font:700 1.5rem Georgia,serif;letter-spacing:.18em}header span{padding-left:18px;border-left:1px solid var(--line);color:var(--muted);letter-spacing:.08em;text-transform:uppercase;font-size:.75rem}main{width:min(1120px,calc(100% - 32px));margin:clamp(42px,8vh,96px) auto}.card{width:min(620px,100%);margin:auto;padding:clamp(26px,5vw,48px);border:1px solid var(--line);border-radius:22px;background:linear-gradient(155deg,#151e2bcc,#0d141fcc);box-shadow:0 30px 90px #0008}.login-card{width:min(480px,100%)}h1{font-family:Georgia,serif;font-size:clamp(2rem,5vw,3rem);line-height:1.05;margin:.25rem 0 1rem}h2{color:var(--gold)}p{color:var(--muted)}.eyebrow{margin:0;color:var(--gold);font-size:.75rem;font-weight:700;letter-spacing:.16em;text-transform:uppercase}form{display:grid;gap:18px;margin-top:28px}label{display:grid;gap:7px;font-weight:650}label.check{display:flex;align-items:center;gap:10px;color:var(--muted);font-weight:500}label.check input{width:18px;min-height:18px;accent-color:var(--gold)}small,.hint{color:#8995a6;font-weight:400}input{width:100%;min-height:48px;padding:12px 14px;border:1px solid #334257;border-radius:10px;background:#080d15;color:var(--text);font:inherit;outline:none}input:focus{border-color:var(--gold);box-shadow:0 0 0 3px #d8aa5825}button,.button-link{display:inline-block;min-height:50px;padding:12px 20px;border:0;border-radius:10px;background:linear-gradient(135deg,#e0b766,#a9782f);color:#111;font:700 1rem inherit;text-decoration:none;cursor:pointer}button:hover,.button-link:hover{filter:brightness(1.08)}.alert{padding:12px 14px;border:1px solid #a94646;border-radius:10px;background:#421d24;color:#ffdadd}.setup-key,.download-card{display:grid;gap:8px;margin:24px 0;padding:16px;border:1px solid #66522d;border-radius:12px;background:#0a1018}.setup-key span{color:var(--gold);font-size:.78rem;text-transform:uppercase;letter-spacing:.1em}.setup-key code,.download-card code{overflow-wrap:anywhere;color:#fff;font-size:.9rem;letter-spacing:.04em}table{width:100%;border-collapse:collapse;margin:24px 0}th,td{text-align:left;padding:10px;border-bottom:1px solid var(--line)}footer{text-align:center;padding:28px;color:#667284;font-size:.8rem}@media(max-width:560px){header{height:66px;padding:0 18px}header strong{font-size:1.15rem}header span{font-size:.65rem}main{margin:28px auto}.card{padding:24px 20px;border-radius:16px}}
""";
static IResult Html(string content, int status = 200) => Results.Content(content, "text/html; charset=utf-8", statusCode: status);
static bool RecentlyAuthenticated(ClaimsPrincipal principal) => long.TryParse(principal.FindFirstValue("auth_time"), out long value) && DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(value) <= TimeSpan.FromMinutes(5);
static string ReadConfigPath(string[] values)
{
    int index = Array.IndexOf(values, "--config");
    if (index < 0 || index + 1 >= values.Length) throw new ArgumentException("--config <path> is required.");
    string path = values[index + 1];
    if (Path.IsPathRooted(path) || path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains("..", StringComparer.Ordinal)) throw new ArgumentException("Configuration path must be relative and normalized.");
    return path;
}

sealed record LauncherLoginRequest(string Username, string Password, Guid LauncherSessionId, string ClientBuildId);
