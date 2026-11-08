using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Revora.Web;
using Revora.Persistence.PostgreSql;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
string? customConfiguration = CustomConfiguration.FindPath(args);
if (customConfiguration is not null) builder.Configuration.AddJsonFile(customConfiguration, optional: false, reloadOnChange: false);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'");
builder.Logging.AddFilter("Microsoft.Extensions.Hosting.Internal.Host", LogLevel.Critical);
builder.Logging.AddFilter("Microsoft.AspNetCore.DataProtection", LogLevel.Error);
builder.Logging.AddFilter("Microsoft.AspNetCore.StaticFiles", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Warning);
builder.Services.Configure<WebOptions>(builder.Configuration.GetSection(WebOptions.SectionName));
builder.Services.Configure<FormOptions>(settings => { settings.ValueLengthLimit = 256; settings.ValueCountLimit = 32; settings.MultipartBodyLengthLimit = 16 * 1024; });
builder.WebHost.ConfigureKestrel(settings => settings.Limits.MaxRequestBodySize = 16 * 1024);
WebOptions configuredIdentityOptions = builder.Configuration.GetSection(WebOptions.SectionName).Get<WebOptions>() ?? new WebOptions();
builder.Services.AddSingleton(configuredIdentityOptions);
string dataProtectionPath = builder.Configuration[$"{WebOptions.SectionName}:DataProtectionKeysPath"] ?? throw new InvalidOperationException("A private data-protection key directory is required.");
string instanceRoot = Directory.GetParent(builder.Environment.ContentRootPath)?.FullName ?? throw new InvalidOperationException("The instance root could not be resolved.");
string resolvedDataProtectionPath = Path.GetFullPath(dataProtectionPath, builder.Environment.ContentRootPath);
if (!resolvedDataProtectionPath.StartsWith(instanceRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal) || resolvedDataProtectionPath.StartsWith(builder.Environment.ContentRootPath + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new InvalidOperationException("Data-protection keys must remain in the private instance data tree outside immutable application files.");
Directory.CreateDirectory(resolvedDataProtectionPath);
if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(resolvedDataProtectionPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(resolvedDataProtectionPath)).SetApplicationName("Revora.Web");
IdentityTokenService identityTokens;
if (!string.IsNullOrWhiteSpace(configuredIdentityOptions.SecurityHmacKeyPath))
{
    string resolvedHmacPath = Path.GetFullPath(configuredIdentityOptions.SecurityHmacKeyPath, builder.Environment.ContentRootPath);
    if (!resolvedHmacPath.StartsWith(instanceRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal) || resolvedHmacPath.StartsWith(builder.Environment.ContentRootPath + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new InvalidOperationException("The identity HMAC key must remain in the private instance data tree.");
    byte[] identityHmacKey = File.ReadAllBytes(resolvedHmacPath);
    if (identityHmacKey.Length < 32) throw new InvalidOperationException("The identity HMAC key must contain at least 256 bits.");
    identityTokens = new IdentityTokenService(identityHmacKey);
    System.Security.Cryptography.CryptographicOperations.ZeroMemory(identityHmacKey);
}
else identityTokens = new IdentityTokenService(string.IsNullOrWhiteSpace(configuredIdentityOptions.SecurityHmacKeyBase64) ? Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)) : configuredIdentityOptions.SecurityHmacKeyBase64);
builder.Services.AddSingleton(_ => new WebAccountStore(configuredIdentityOptions.DatabaseConnectionString));
builder.Services.AddHttpClient<GameIdentityRegistrationClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Revora.Web/1.0");
});
builder.Services.AddSingleton(identityTokens);
builder.Services.AddSingleton<RecoveryDisplayCache>();
builder.Services.AddSingleton<FormProofService>();
builder.Services.AddHostedService<IdentityRetentionService>();
builder.Services.AddSingleton<IAdministrativeAuditSink, InMemoryAdministrativeAuditSink>();
builder.Services.AddSingleton<WebRuntimeState>();
builder.Services.AddSingleton<GameServerStatusRegistry>();
if (configuredIdentityOptions.InternalGameIntegrationEnabled)
    builder.Services.AddSingleton(_ => new IdentityHandoffSigner(WebOptions.LoadIdentityKey(configuredIdentityOptions)));
builder.Services.AddHostedService<WebConsoleService>();
builder.Services.AddAntiforgery(settings =>
{
    settings.Cookie.Name = "__Host-Revora.Csrf";
    settings.Cookie.HttpOnly = true;
    settings.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    settings.Cookie.SameSite = SameSiteMode.Strict;
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(settings =>
{
    settings.Cookie.Name = "__Host-Revora.Session";
    settings.Cookie.HttpOnly = true;
    settings.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    settings.Cookie.SameSite = SameSiteMode.Lax;
    settings.LoginPath = "/en/login";
    settings.AccessDeniedPath = "/en/access-denied";
    settings.SlidingExpiration = false;
    settings.ExpireTimeSpan = TimeSpan.FromHours(8);
    settings.Events.OnValidatePrincipal = async context =>
    {
        string? raw = context.Principal?.FindFirst("session_token")?.Value;
        string? id = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        string? stamp = context.Principal?.FindFirst("security_stamp")?.Value;
        if (raw is null || !Guid.TryParse(id, out Guid accountId) || !Guid.TryParse(stamp, out Guid securityStamp)) { context.RejectPrincipal(); return; }
        WebAccountStore store = context.HttpContext.RequestServices.GetRequiredService<WebAccountStore>();
        IdentityTokenService tokens = context.HttpContext.RequestServices.GetRequiredService<IdentityTokenService>();
        if (!await store.ValidateSessionAsync(tokens.Digest(raw), accountId, securityStamp, context.HttpContext.RequestAborted)) context.RejectPrincipal();
    };
});
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("DashboardUser", policy => policy.RequireAuthenticatedUser())
    .AddPolicy("RevoraAdministrator", policy => policy.RequireAuthenticatedUser().RequireRole("Administrator"));
builder.Services.AddRateLimiter(settings =>
{
    settings.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    settings.AddPolicy("authentication", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    settings.AddPolicy("registration", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromHours(1), QueueLimit = 0, AutoReplenishment = true }));
    settings.AddPolicy("recovery", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 6, Window = TimeSpan.FromMinutes(15), QueueLimit = 0, AutoReplenishment = true }));
});
builder.Services.Configure<ForwardedHeadersOptions>(settings =>
{
    settings.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    settings.KnownProxies.Add(IPAddress.Loopback);
    settings.ForwardLimit = 1;
});

WebApplication app = builder.Build();
WebOptions options = app.Services.GetRequiredService<IOptions<WebOptions>>().Value;
WebRuntimeState runtimeState = app.Services.GetRequiredService<WebRuntimeState>();
string configuredUrls = builder.Configuration["Urls"] ?? builder.Configuration["ASPNETCORE_URLS"] ?? string.Empty;
string[] urls = configuredUrls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
if (urls.Length == 0) throw new InvalidOperationException("An explicit loopback URL is required.");
WebOptions.Validate(options, app.Environment, urls);

app.Use((context, next) =>
{
    context.Items[InternalGameClientFilter.DirectPeerLoopbackKey] =
        context.Connection.RemoteIpAddress is IPAddress peer && IPAddress.IsLoopback(peer);
    return next(context);
});
app.UseForwardedHeaders();
app.Use(async (context, next) =>
{
    if (app.Environment.IsProduction() && !context.Request.IsHttps && !context.Request.Path.StartsWithSegments("/health")) { context.Response.StatusCode = StatusCodes.Status400BadRequest; return; }
    await next();
});
app.UseExceptionHandler(error => error.Run(async context =>
{
    Exception? failure = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    bool invalidAntiforgery = failure is AntiforgeryValidationException;
    context.Response.StatusCode = invalidAntiforgery ? StatusCodes.Status400BadRequest : StatusCodes.Status500InternalServerError;
    context.Response.ContentType = "text/html; charset=utf-8";
    context.Response.Headers["X-Robots-Tag"] = "noindex,nofollow";
    string culture = CultureFromPath(context.Request.Path);
    NavigationState navigation = NavigationState.FromValidatedPrincipal(context, context.RequestServices.GetRequiredService<IAntiforgery>());
    string page = invalidAntiforgery
        ? PublicAccountRenderer.Generic(culture, culture == "ar" ? "تم رفض الطلب" : "Request rejected", culture == "ar" ? "حدّث الصفحة وحاول مرة أخرى." : "Refresh the page and try again.", navigation)
        : PageRenderer.Render(culture, LocalizedContent.For(culture)["server-error"], options, navigation: navigation);
    await context.Response.WriteAsync(page);
}));

app.Use(async (context, next) =>
{
    if (app.Environment.IsProduction() && context.Request.IsHttps) context.Response.Headers.StrictTransportSecurity = "max-age=31536000; includeSubDomains";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; img-src 'self'; style-src 'self'; script-src 'self'; object-src 'none'; base-uri 'self'; frame-ancestors 'none'; form-action 'self'";
    await next();
});

app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "public,max-age=604800,immutable" });
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/health/live", () => Results.Json(new { state = "alive", component = "Revora.Web" }));
runtimeState.SetMaintenance(options.MaintenanceMode);
app.MapGet("/health/ready", () => Results.Json(new { state = runtimeState.IsMaintenance ? "degraded" : runtimeState.IsReady ? "ready" : "starting", dependencies = "public-content" }, statusCode: runtimeState.IsReady ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable));
app.MapGet("/", () => Html(PageRenderer.RenderLanguageChoice(options), options.IndexingEnabled));
app.MapGet("/robots.txt", () => Results.Text(options.IndexingEnabled
    ? "User-agent: *\nAllow: /\nDisallow: /ar/login\nDisallow: /en/login\nDisallow: /ar/register\nDisallow: /en/register\nDisallow: /ar/forgot-password\nDisallow: /en/forgot-password\nDisallow: /ar/reset-password\nDisallow: /en/reset-password\nDisallow: /ar/verify-email\nDisallow: /en/verify-email\nDisallow: /ar/dashboard\nDisallow: /en/dashboard\nDisallow: /ar/admin\nDisallow: /en/admin\nSitemap: https://playrevora.com/sitemap.xml\n"
    : "User-agent: *\nDisallow: /\n", "text/plain", Encoding.UTF8));
app.MapGet("/sitemap.xml", () => Results.Text(BuildSitemap(options), "application/xml", Encoding.UTF8));
app.MapGet("/feed.xml", () => Results.Text(BuildFeed(), "application/atom+xml", Encoding.UTF8));
string client7630Archive = Path.Combine(app.Environment.WebRootPath, "downloads", "7630-original.7z");
app.MapGet("/downloads/7630-original.7z", () => File.Exists(client7630Archive)
    ? Results.File(client7630Archive, "application/x-7z-compressed", "7630-original.7z", enableRangeProcessing: true)
    : Results.NotFound());

app.MapGet("/{culture:regex(^(ar|en)$)}/verify-email", (HttpContext context, string culture, IAntiforgery csrf) => PrivateHtml(PublicAccountRenderer.Generic(culture, "No email required", "Revora accounts activate immediately without email verification.", NavigationState.FromValidatedPrincipal(context, csrf))));
app.MapPublicAccounts(options);
if (options.InternalGameIntegrationEnabled) app.MapInternalGameEndpoints(options);

RouteGroupBuilder administration = app.MapGroup("/{culture:regex(^(ar|en)$)}/admin").RequireAuthorization("RevoraAdministrator");
administration.MapGet("", (string culture) => PrivateHtml(AdminPageRenderer.Render(culture, string.Empty, options)));
administration.MapGet("/{section:regex(^(users|game-accounts|characters|moderation|news|downloads|patches|maintenance|status|audit|roles)$)}", (string culture, string section) => PrivateHtml(AdminPageRenderer.Render(culture, section, options)));

app.MapGet("/{culture:regex(^(ar|en)$)}/", (HttpContext context, string culture, IAntiforgery csrf) => Localized(context, culture, string.Empty, options, csrf));
app.MapGet("/{culture:regex(^(ar|en)$)}/{**slug}", (HttpContext context, string culture, string? slug, IAntiforgery csrf) =>
{
    string normalized = (slug ?? string.Empty).Trim('/').ToLowerInvariant();
    IReadOnlyDictionary<string, LocalizedPage> pages = LocalizedContent.For(culture);
    if (!pages.TryGetValue(normalized, out LocalizedPage? page))
    {
        return Html(PageRenderer.Render(culture, pages["not-found"], options, navigation: NavigationState.FromValidatedPrincipal(context, csrf)), false, StatusCodes.Status404NotFound);
    }

    return Html(PageRenderer.Render(culture, page, options, navigation: NavigationState.FromValidatedPrincipal(context, csrf)), options.IndexingEnabled && page.Kind == "public");
});

app.Lifetime.ApplicationStarted.Register(() => { runtimeState.MarkReady(); Console.Out.WriteLine("[Revora.Web] READY"); });
app.Lifetime.ApplicationStopping.Register(() => Console.Out.WriteLine("[Revora.Web] STOPPING"));
app.Lifetime.ApplicationStopped.Register(() => Console.Out.WriteLine("[Revora.Web] STOPPED"));
try
{
    await app.RunAsync();
}
catch (IOException)
{
    Console.Error.WriteLine(JsonSerializer.Serialize(new { timestamp = DateTimeOffset.UtcNow, severity = "Critical", component = "Revora.Web", eventName = "FatalStartupFailure", message = "The web listener could not start." }));
    Environment.ExitCode = 1;
}

static IResult Localized(HttpContext context, string culture, string slug, WebOptions options, IAntiforgery csrf)
{
    LocalizedPage page = LocalizedContent.For(culture)[slug];
    return Html(PageRenderer.Render(culture, page, options, navigation: NavigationState.FromValidatedPrincipal(context, csrf)), options.IndexingEnabled);
}

static IResult Html(string content, bool indexable, int statusCode = StatusCodes.Status200OK) => Results.Content(content, "text/html; charset=utf-8", Encoding.UTF8, statusCode)
    .WithHeader("X-Robots-Tag", indexable ? "index,follow" : "noindex,nofollow")
    .WithHeader("Cache-Control", "no-store,private");

static IResult PrivateHtml(string content, int statusCode = StatusCodes.Status200OK) => Results.Content(content, "text/html; charset=utf-8", Encoding.UTF8, statusCode)
    .WithHeader("X-Robots-Tag", "noindex,nofollow")
    .WithHeader("Cache-Control", "no-store,private");

static string CultureFromPath(PathString path) => path.StartsWithSegments("/ar", StringComparison.OrdinalIgnoreCase) ? "ar" : "en";

static string BuildSitemap(WebOptions options)
{
    string[] slugs = ["", "about", "news", "status", "features", "roadmap", "rankings", "download", "privacy", "terms", "contact"];
    StringBuilder xml = new("<?xml version=\"1.0\" encoding=\"UTF-8\"?><urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\" xmlns:xhtml=\"http://www.w3.org/1999/xhtml\">");
    foreach (string slug in slugs)
    {
        foreach (string culture in new[] { "ar", "en" })
        {
            string suffix = slug.Length == 0 ? $"/{culture}/" : $"/{culture}/{slug}";
            xml.Append("<url><loc>").Append(options.PublicBaseUrl).Append(suffix).Append("</loc><xhtml:link rel=\"alternate\" hreflang=\"ar\" href=\"").Append(options.PublicBaseUrl).Append(slug.Length == 0 ? "/ar/" : $"/ar/{slug}").Append("\"/><xhtml:link rel=\"alternate\" hreflang=\"en\" href=\"").Append(options.PublicBaseUrl).Append(slug.Length == 0 ? "/en/" : $"/en/{slug}").Append("\"/><xhtml:link rel=\"alternate\" hreflang=\"x-default\" href=\"").Append(options.PublicBaseUrl).Append("/\"/></url>");
        }
    }
    return xml.Append("</urlset>").ToString();
}

static string BuildFeed() => "<?xml version=\"1.0\" encoding=\"utf-8\"?><feed xmlns=\"http://www.w3.org/2005/Atom\"><title>Revora development news</title><id>https://playrevora.com/news</id><updated>2026-08-28T00:00:00Z</updated><link href=\"https://playrevora.com/feed.xml\" rel=\"self\"/><entry><title>The foundation before the frontier</title><id>https://playrevora.com/en/news/development-foundation</id><updated>2026-08-28T00:00:00Z</updated><link href=\"https://playrevora.com/en/news/development-foundation\"/><summary>A factual record of the current Revora engineering foundation.</summary></entry></feed>";

internal static class ResultExtensions
{
    public static IResult WithHeader(this IResult result, string name, string value) => new HeaderResult(result, name, value);

    private sealed class HeaderResult(IResult inner, string name, string value) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.Headers[name] = value;
            await inner.ExecuteAsync(httpContext);
        }
    }
}
