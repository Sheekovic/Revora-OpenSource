using Revora.Web;
using Microsoft.Extensions.Hosting;
using System.Text.Json;
using System.Security.Cryptography;
using Revora.Persistence.PostgreSql;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;

WebOptions options = new();
List<(string Name, Action Test)> tests =
[
    ("English metadata and direction", () => Contains(PageRenderer.Render("en", LocalizedContent.For("en")[""], options), "<html lang=\"en\" dir=\"ltr\">", "hreflang=\"ar\"", "hreflang=\"x-default\"", "<meta name=\"robots\" content=\"noindex,nofollow\"")),
    ("Structured data and social card are valid", ValidatePublicMetadata),
    ("Arabic metadata and direction", () => Contains(PageRenderer.Render("ar", LocalizedContent.For("ar")[""], options), "<html lang=\"ar\" dir=\"rtl\">", "hreflang=\"en\"", "عالمٌ يستحق")),
    ("Language switch preserves slug", () => Contains(PageRenderer.Render("en", LocalizedContent.For("en")["download"], options), "href=\"/ar/download\"")),
    ("Approved header brand is rendered", () => Contains(PageRenderer.Render("en", LocalizedContent.For("en")[""], options), "src=\"/assets/revora-header-logo.png?v=20260831-2\"", "width=\"2079\" height=\"756\"", "href=\"/favicon.ico\"")),
    ("Homepage registration CTA is live", ValidateHomepageRegistration),
    ("Approved full logo is rendered", () => Contains(PageRenderer.RenderLanguageChoice(options), "src=\"/assets/revora-full-logo.png\"", "width=\"1254\" height=\"1254\"", "href=\"/favicon.ico\"")),
    ("Approved brand asset bytes are preserved", ValidateBrandAssets),
    ("Localized descriptions differ", () => Assert(LocalizedContent.For("en")["about"].Description != LocalizedContent.For("ar")["about"].Description, "Descriptions must be localized.")),
    ("Stable downloads are published without internal terminology", ValidateStableDownloads),
    ("Language choice is crawlable", () => Contains(PageRenderer.RenderLanguageChoice(options), "href=\"/ar/\"", "href=\"/en/\"", "hreflang=\"x-default\"")),
    ("Production defaults fail closed", () => Throws(() => WebOptions.Validate(options, new FakeEnvironment("Production"), ["http://127.0.0.1:5080"]))),
    ("Public binding is rejected", () => Throws(() => WebOptions.Validate(WithRuntime("Development"), new FakeEnvironment("Development"), ["http://0.0.0.0:5080"]))),
    ("Loopback development binding is allowed", () => WebOptions.Validate(WithRuntime("Development"), new FakeEnvironment("Development"), ["http://127.0.0.1:5080"])),
    ("External return URL is rejected", () => Assert(ReturnUrlPolicy.SafeLocalPath("https://evil.example/", "en") == "/en/dashboard", "External return URL was accepted.")),
    ("Scheme-relative return URL is rejected", () => Assert(ReturnUrlPolicy.SafeLocalPath("//evil.example/", "ar") == "/ar/dashboard", "Scheme-relative return URL was accepted.")),
    ("Local return URL is preserved", () => Assert(ReturnUrlPolicy.SafeLocalPath("/en/dashboard/security", "en") == "/en/dashboard/security", "Local return URL was rejected.")),
    ("Authentication form is password-manager compatible", () => Contains(PublicAccountRenderer.Login("en", "csrf", "proof"), "autocomplete=username", "autocomplete=current-password", "forgot-password", "noindex,nofollow")),
    ("Registration advertises no-email immediate activation", () => Contains(PublicAccountRenderer.Register("en", "csrf", "proof"), "active immediately", "No email required", "4–15 English letters or numbers", "8–15 supported characters")),
    ("Authentication routes share approved header", ValidateAccountHeader),
    ("Authentication forms use associated labels", ValidateFormAssociations),
    ("Trust-device checkbox is one associated row", () => Contains(PublicAccountRenderer.Login("en", "csrf", "proof"), "<label class=check-row for=trust-device>", "<input id=trust-device type=checkbox", "Trust this device")),
    ("Honeypot remains submitted but inaccessible", ValidateHoneypot),
    ("Field validation is adjacent and associated", ValidateFieldValidation),
    ("Account pages introduce no email field", ValidateNoEmailField),
    ("Arabic authentication pages retain RTL", () => Contains(PublicAccountRenderer.Login("ar", "csrf", "proof"), "lang='ar' dir='rtl'", "إنشاء حساب", "Revora game logo")),
    ("Anonymous navigation shows registration and login", ValidateAnonymousNavigation),
    ("Authenticated navigation shows encoded username and POST logout", ValidateAuthenticatedNavigation),
    ("Arabic authenticated navigation localizes logout", ValidateArabicAuthenticatedNavigation),
    ("Navigation identity comes only from authenticated claims", ValidateNavigationIdentitySource),
    ("Logout endpoint is POST-only", ValidatePostOnlyLogout),
    ("Dynamic HTML is private and non-cacheable", ValidateDynamicCachePolicy),
    ("Public username policy accepts boundaries", () => { Assert(UsernamePolicy.TryNormalize("Ab12", out string low) && low == "ab12", "Minimum username failed."); Assert(UsernamePolicy.TryNormalize("abcdefghijk1234", out _), "Maximum username failed."); }),
    ("Public username policy rejects unsafe values", () => { foreach (string value in new[] { "abc", "abcdefghijklmnop", "name space", "user_name", "شخص", "Admin!" }) Assert(!UsernamePolicy.TryNormalize(value, out _), $"Unsafe username accepted: {value}"); }),
    ("Legacy password policy accepts boundaries", () => { Assert(PublicPasswordPolicy.IsValid("12345678"), "Minimum password failed."); Assert(PublicPasswordPolicy.IsValid("123456789012345"), "Maximum password failed."); }),
    ("Legacy password policy rejects incompatible values", () => { foreach (string value in new[] { "1234567", "1234567890123456", "pass word", "كلمةمرور12" }) Assert(!PublicPasswordPolicy.IsValid(value), $"Incompatible password accepted: {value}"); }),
    ("Recovery codes carry at least 128 bits and are independent", TestRecoveryCodes),
    ("Familiar-network digests are versioned and prefix scoped", TestNetworkDigests),
    ("Enabled identity configuration requires durable secrets", TestIdentityConfiguration),
    ("WKConquer handoff signature layout is exact", TestIdentityHandoffSignature),
    ("Internal client fingerprint comparison is fixed and canonical", TestInternalFingerprint),
    ("Status heartbeat expires closed", TestStatusExpiry),
    ("Session revocation is ownership scoped", TestSessionRevocation),
    ("Admin pages are private and mutations disabled", () => Contains(AdminPageRenderer.Render("en", "users", options), "noindex,nofollow", "Protected administration", "Mutations disabled", "disabled aria-disabled=\"true\"")),
    ("Arabic Admin direction is RTL", () => Contains(AdminPageRenderer.Render("ar", "audit", options), "lang=\"ar\" dir=\"rtl\"", "سجل التدقيق")),
    ("Audit summaries reject secret terms", TestAuditRedaction),
    ("Safe patch manifest is accepted", TestSafeManifest),
    ("Patch traversal is rejected", () => Assert(!PatchManifestValidator.IsSafeDestination("client/../../escape.exe"), "Traversal destination was accepted.")),
    ("Absolute patch path is rejected", () => Assert(!PatchManifestValidator.IsSafeDestination("/etc/passwd"), "Absolute destination was accepted.")),
    ("Windows patch path is rejected", () => Assert(!PatchManifestValidator.IsSafeDestination("client\\file.dll"), "Backslash destination was accepted.")),
    ("Insecure patch URL is rejected", TestInsecurePatchUrl),
    ("Duplicate destinations are rejected", TestDuplicatePatchPath),
    ("Custom config rejects traversal", () => Throws(() => CustomConfiguration.FindPath(["--config", "../secret.json"]))),
    ("Custom config accepts reviewed relative path", () => Assert(CustomConfiguration.FindPath(["--config", "config/revora-web.json"]) == "config/revora-web.json", "Reviewed config path was rejected.")),
    ("AMP Web JSON is valid", ValidateAmpJson),
    ("AMP Web template is loopback and fail safe", ValidateAmpTemplate)
];

int failures = 0;
foreach ((string name, Action test) in tests)
{
    try { test(); Console.WriteLine($"PASS {name}"); }
    catch (Exception exception) { failures++; Console.Error.WriteLine($"FAIL {name}: {exception.Message}"); }
}
Console.WriteLine($"Revora.WebTests: {tests.Count - failures}/{tests.Count} passed.");
return failures == 0 ? 0 : 1;

static WebOptions WithRuntime(string runtime) => new() { RuntimeMode = runtime };
static void ValidateHomepageRegistration()
{
    string page = PageRenderer.Render("en", LocalizedContent.For("en")[""], new WebOptions());
    Contains(page, "class=\"button primary\" href=\"/register\"", "Create Account");
    NotContains(page, "Registration coming soon", "registration unavailable");
}
static void ValidateStableDownloads()
{
    string english = PageRenderer.Render("en", LocalizedContent.For("en")["download"], new WebOptions());
    string arabic = PageRenderer.Render("ar", LocalizedContent.For("ar")["download"], new WebOptions());
    foreach (string page in new[] { english, arabic })
    {
        Contains(page, "/downloads/7630-original.7z",
            "0fa5b083d73a43b60bbaf5f1b52a5358bc83a4acf987e4d16a678bbafbdda0c8",
            "7630", "Windows");
        NotContains(page, "Revora-Launcher-1.0.8.zip", "Revora-Full-Client-5165-1.0.7.zip",
            "owner test", "manual-playable", "releaseId", "test channel");
    }
    Contains(english, "Original baseline", "Download Client 7630", "does not yet claim launcher or server interoperability");
    Contains(arabic, "خط أساس أصلي", "تنزيل عميل 7630");
}
static void ValidateAccountHeader()
{
    foreach (string page in new[]
    {
        PublicAccountRenderer.Register("en", "csrf", "proof"), PublicAccountRenderer.Login("en", "csrf", "proof"),
        PublicAccountRenderer.Forgot("en", "csrf", "proof"), PublicAccountRenderer.Reset("en", "csrf", "proof"),
        PublicAccountRenderer.Security("en", "csrf", "proof"),
        PublicAccountRenderer.Dashboard("en", new AccountDashboard("player", "active", DateTimeOffset.UtcNow, 10, 0, []), "csrf")
    })
        Contains(page, "<header class=\"site-header\">", "href=\"/\" aria-label=\"Revora home\"", "src=\"/assets/revora-header-logo.png?v=20260831-2\"", "width=\"2079\" height=\"756\"", "alt=\"Revora game logo\"");
}
static void ValidateFormAssociations()
{
    string login = PublicAccountRenderer.Login("en", "csrf", "proof");
    string register = PublicAccountRenderer.Register("en", "csrf", "proof");
    Contains(login, "<label for=login-username>Username</label><input id=login-username", "<label for=login-password>Password</label><input id=login-password");
    Contains(register, "<label for=register-username>Username</label><input id=register-username", "<label for=register-password>Password</label><input id=register-password", "<label for=register-confirm>Confirm password</label><input id=register-confirm");
}
static void ValidateHoneypot()
{
    string page = PublicAccountRenderer.Register("en", "csrf", "proof");
    Contains(page, "class=bot-field aria-hidden=true", "name=website type=text tabindex=-1 autocomplete=off aria-hidden=true");
    string css = File.ReadAllText("src/Revora.Web/wwwroot/assets/account.css");
    Contains(css, ".bot-field{position:fixed!important", "inset-inline-start:-10000px!important", "pointer-events:none!important");
}
static void ValidateFieldValidation()
{
    string page = PublicAccountRenderer.Register("en", "csrf", "proof", "Use a valid username.", "username");
    Contains(page, "id=register-username", "aria-describedby='register-username-error' aria-invalid=true", "class=field-error id=register-username-error role=alert>Use a valid username.");
}
static void ValidateNoEmailField()
{
    foreach (string page in new[] { PublicAccountRenderer.Register("en", "csrf", "proof"), PublicAccountRenderer.Login("en", "csrf", "proof") })
        NotContains(page, "type=email", "name=email", "email verification");
}
static void ValidateAnonymousNavigation()
{
    string page = PublicAccountRenderer.Generic("en", "Notice", "Message", NavigationState.Anonymous);
    Contains(page, "class=\"nav-account\" href=\"/register\"", ">Create Account</a>", "class=\"nav-login\" href=\"/login\"", ">Log in</a>");
    NotContains(page, "class=\"nav-logout\"", "action=\"/logout\"");
}
static void ValidateAuthenticatedNavigation()
{
    string page = PublicAccountRenderer.Generic("en", "Notice", "Message", NavigationState.Authenticated("Player<&", "csrf<&"));
    Contains(page, "class=\"nav-account\" href=\"/account\"", ">Player&lt;&amp;</a>", "class=\"nav-logout\" method=\"post\" action=\"/logout\"", "value=\"csrf&lt;&amp;\"", ">Log out</button>");
    NotContains(page, "href=\"/register\"", "href=\"/login\"");
}
static void ValidateArabicAuthenticatedNavigation()
{
    string page = PublicAccountRenderer.Generic("ar", "Notice", "Message", NavigationState.Authenticated("Player7", "csrf"));
    Contains(page, "lang='ar' dir='rtl'", ">Player7</a>", ">تسجيل الخروج</button>");
    NotContains(page, ">إنشاء حساب</a>", ">تسجيل الدخول</a>");
}
static void ValidateNavigationIdentitySource()
{
    DefaultHttpContext anonymous = new();
    anonymous.Request.QueryString = new QueryString("?username=forged");
    anonymous.Request.Headers.Cookie = "username=forged";
    Assert(!NavigationState.FromValidatedPrincipal(anonymous, new FakeAntiforgery()).IsAuthenticated, "Unvalidated request values created an authenticated navigation state.");

    DefaultHttpContext authenticated = new();
    authenticated.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "ClaimUser")], "validated-test"));
    NavigationState state = NavigationState.FromValidatedPrincipal(authenticated, new FakeAntiforgery());
    Assert(state.Username == "ClaimUser" && state.AntiforgeryToken == "test-antiforgery", "Validated claims were not used for navigation.");
}
static void ValidatePostOnlyLogout()
{
    string source = File.ReadAllText("src/Revora.Web/PublicAccountEndpoints.cs");
    Contains(source, "app.MapPost(\"/logout\", Logout).RequireAuthorization()", "app.MapPost(\"/{culture:regex(^(ar|en)$)}/logout\", Logout).RequireAuthorization()");
    NotContains(source, "MapGet(\"/logout\"", "MapGet(\"/{culture:regex(^(ar|en)$)}/logout\"");
}
static void ValidateDynamicCachePolicy()
{
    string source = File.ReadAllText("src/Revora.Web/Program.cs");
    Contains(source, ".WithHeader(\"Cache-Control\", \"no-store,private\")");
}
static void TestRecoveryCodes()
{
    using var tokens = new IdentityTokenService(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
    IReadOnlyList<RecoveryCode> codes = tokens.NewRecoveryCodes();
    Assert(codes.Count == 10 && codes.Select(code => code.Plaintext).Distinct().Count() == 10, "Recovery codes were not independent.");
    Assert(codes.All(code => code.Plaintext.Replace("-", string.Empty, StringComparison.Ordinal).Length == 32 && code.Digest.Length == 32), "Recovery-code entropy or digest length is invalid.");
}
static void TestNetworkDigests()
{
    using var tokens = new IdentityTokenService(Enumerable.Repeat((byte)0x42, 32).ToArray());
    byte[] ipv4A = tokens.NetworkDigest(System.Net.IPAddress.Parse("198.51.100.24"));
    byte[] ipv4Same = tokens.NetworkDigest(System.Net.IPAddress.Parse("198.51.100.199"));
    byte[] ipv4Different = tokens.NetworkDigest(System.Net.IPAddress.Parse("198.51.101.24"));
    byte[] ipv6A = tokens.NetworkDigest(System.Net.IPAddress.Parse("2001:db8:1234:5600::1"));
    byte[] ipv6Same = tokens.NetworkDigest(System.Net.IPAddress.Parse("2001:db8:1234:56ff::2"));
    byte[] ipv6Different = tokens.NetworkDigest(System.Net.IPAddress.Parse("2001:db8:1234:5700::1"));
    Assert(ipv4A.SequenceEqual(ipv4Same) && !ipv4A.SequenceEqual(ipv4Different), "IPv4 network prefixes were not isolated.");
    Assert(ipv6A.SequenceEqual(ipv6Same) && !ipv6A.SequenceEqual(ipv6Different), "IPv6 network prefixes were not isolated.");
    Assert(!ipv4A.SequenceEqual(ipv6A), "Address families shared a network digest.");
}
static void TestIdentityConfiguration()
{
    WebOptions enabled = new() { RuntimeMode = "Development", RegistrationEnabled = true, RecoveryEnabled = true, DatabaseConnectionString = "Host=/run/postgresql;Database=revora", SecurityHmacKeyBase64 = Convert.ToBase64String(new byte[32]) };
    WebOptions.Validate(enabled, new FakeEnvironment("Development"), ["http://127.0.0.1:5080"]);
    Throws(() => WebOptions.Validate(new WebOptions { RuntimeMode = "Development", RegistrationEnabled = true }, new FakeEnvironment("Development"), ["http://127.0.0.1:5080"]));
    Throws(() => WebOptions.Validate(enabled, new FakeEnvironment("Production"), ["http://127.0.0.1:5080"]));
    string key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    WebOptions integration = new() { RuntimeMode = "Development", InternalGameIntegrationEnabled = true,
        IdentityServerId = 7, IdentityVerificationKeyBase64 = key,
        IdentityClientCertificateSha256 = new string('A', 64) };
    WebOptions.Validate(integration, new FakeEnvironment("Development"), ["http://127.0.0.1:5080"]);
    WebOptions invalidIntegration = new() { RuntimeMode = "Development", InternalGameIntegrationEnabled = true,
        IdentityServerId = 0, IdentityVerificationKeyBase64 = key,
        IdentityClientCertificateSha256 = new string('A', 64) };
    Throws(() => WebOptions.Validate(invalidIntegration, new FakeEnvironment("Development"), ["http://127.0.0.1:5080"]));
}

static void TestIdentityHandoffSignature()
{
    byte[] key = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
    byte[] nonce = Enumerable.Range(32, 32).Select(i => (byte)i).ToArray();
    const string address = "::ffff:203.0.113.9";
    byte[] actual = IdentityHandoffSigner.ComputeSignature(key, 0x10203040, 1000001, 2000001,
        7, address, 1_800_000_000, nonce);
    using MemoryStream stream = new();
    using (BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, true))
    {
        writer.Write(0x10203040u); writer.Write(1000001u); writer.Write(2000001u); writer.Write(7u);
        writer.Write(1_800_000_000L); byte[] bytes = System.Text.Encoding.UTF8.GetBytes(address);
        writer.Write(bytes.Length); writer.Write(bytes); writer.Write(nonce.Length); writer.Write(nonce);
    }
    byte[] expected = HMACSHA256.HashData(key, stream.ToArray());
    Assert(CryptographicOperations.FixedTimeEquals(actual, expected), "Handoff signature differs from the server BinaryWriter layout.");
}

static void TestInternalFingerprint()
{
    string value = new string('A', 64);
    Assert(InternalGameClientFilter.FixedAsciiEquals(value, value.ToLowerInvariant()), "Canonical fingerprint was rejected.");
    Assert(!InternalGameClientFilter.FixedAsciiEquals(value, new string('B', 64)), "Wrong fingerprint was accepted.");
    Assert(!InternalGameClientFilter.FixedAsciiEquals(value, value[..63]), "Truncated fingerprint was accepted.");
}

static void TestStatusExpiry()
{
    GameServerStatusRegistry registry = new();
    DateTimeOffset now = DateTimeOffset.FromUnixTimeSeconds(2_000_000_000);
    registry.Record(new GameServerStatusRequest(7, "Revora", 12, now.AddHours(-1).ToUnixTimeSeconds(),
        now.ToUnixTimeSeconds(), true));
    Assert(registry.Get(7, now.AddSeconds(45)).Online, "Fresh heartbeat was rejected.");
    GameServerStatusSnapshot stale = registry.Get(7, now.AddSeconds(46));
    Assert(!stale.Online && stale.OnlinePlayers == 0, "Stale heartbeat did not fail closed.");
}

static void Contains(string value, params string[] needles) { foreach (string needle in needles) Assert(value.Contains(needle, StringComparison.Ordinal), $"Missing expected text: {needle}"); }
static void NotContains(string value, params string[] needles) { foreach (string needle in needles) Assert(!value.Contains(needle, StringComparison.OrdinalIgnoreCase), $"Unexpected text: {needle}"); }
static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
static void Throws(Action action) { try { action(); } catch (InvalidOperationException) { return; } throw new InvalidOperationException("Expected validation failure."); }
static void TestSessionRevocation()
{
    InMemoryWebSessionRegistry registry = new();
    DateTimeOffset now = DateTimeOffset.UtcNow;
    registry.Register(new WebSession("a", "user-1", now, now));
    registry.Register(new WebSession("b", "user-2", now, now));
    Assert(!registry.Revoke("user-1", "b"), "A user revoked another user's session.");
    Assert(registry.RevokeAll("user-1") == 1, "Expected one owned session to be revoked.");
    Assert(registry.ForUser("user-2").Count == 1, "Unrelated session was removed.");
}
static void TestAuditRedaction()
{
    AdministrativeAuditEvent unsafeEvent = new("actor", "update", "user", "target", DateTimeOffset.UtcNow, "review", "correlation", "password hash present", "changed");
    try { InMemoryAdministrativeAuditSink.Validate(unsafeEvent); }
    catch (ArgumentException) { return; }
    throw new InvalidOperationException("Sensitive audit summary was accepted.");
}
static PatchManifest ValidManifest(params PatchFile[]? files) => new(1, "beta", "0.0.1-preview.1", "0.0.1", DateTimeOffset.UtcNow, null, "Ed25519", PatchManifestValidator.EmbeddedSigningKeyId, new string('A', 86) + "==", files is { Length: > 0 } ? files : [new PatchFile("docs/example.txt", 10, new string('a', 64), "https://downloads.example.invalid/example.txt")]);
static void TestSafeManifest() => Assert(PatchManifestValidator.Validate(ValidManifest()).Count == 0, "Valid manifest was rejected.");
static void TestInsecurePatchUrl()
{
    PatchManifest manifest = ValidManifest(new PatchFile("docs/example.txt", 10, new string('a', 64), "http://downloads.example.invalid/example.txt"));
    Assert(PatchManifestValidator.Validate(manifest).Any(x => x.Contains("HTTPS", StringComparison.Ordinal)), "Insecure URL was accepted.");
}
static void TestDuplicatePatchPath()
{
    PatchFile file = new("docs/example.txt", 10, new string('a', 64), "https://downloads.example.invalid/example.txt");
    Assert(PatchManifestValidator.Validate(ValidManifest(file, file)).Any(x => x.Contains("Duplicate", StringComparison.Ordinal)), "Duplicate path was accepted.");
}
static void ValidateAmpJson()
{
    foreach (string path in Directory.EnumerateFiles("deploy/amp-web", "*.json", SearchOption.AllDirectories))
    {
        using JsonDocument _ = JsonDocument.Parse(File.ReadAllText(path));
    }
}
static void ValidateAmpTemplate()
{
    string kvp = File.ReadAllText("deploy/amp-web/revoraweb.kvp");
    string config = File.ReadAllText("deploy/amp-web/config/revora-web.json");
    Contains(kvp, "App.ExecutableLinux=app/Revora.Web", "App.ExitString=stop", "Console.AppReadyRegex=^\\[Revora.Web\\] READY$", "Limits.AutoRetryCount=0", "App.UpdateSources=@IncludeJson[revorawebupdates.json]", "Meta.ContainerPolicy=Supported");
    Assert(!kvp.Contains("dotnet run", StringComparison.OrdinalIgnoreCase) && !kvp.Contains("git pull", StringComparison.OrdinalIgnoreCase) && !kvp.Contains("docker run", StringComparison.OrdinalIgnoreCase) && !kvp.Contains("sudo ", StringComparison.OrdinalIgnoreCase), "Template contains a prohibited execution mechanism.");
    Contains(config, "http://127.0.0.1:5080", "\"DataProtectionKeysPath\": \"../data/keys\"", "\"IndexingEnabled\": false", "\"RegistrationEnabled\": false");
}
static void ValidateBrandAssets()
{
    Dictionary<string, string> expected = new(StringComparer.Ordinal)
    {
        ["src/Revora.Web/wwwroot/assets/revora-header-logo.png"] = "cf1f42738905ec20f26f34d437ff650a0de8acfb19a4486b2faa42be0620224e",
        ["src/Revora.Web/wwwroot/assets/revora-full-logo.png"] = "036e4312bf01f8c089f6d88ff272bdb647700ea220173ab4065821b24adc6e00",
        ["src/Revora.Web/wwwroot/favicon.ico"] = "4219af37179b0a288c9e5fbf5973ad78b260fd609ae4754b43f10aa252431682"
    };
    foreach ((string path, string hash) in expected)
    {
        Assert(File.Exists(path), $"Missing approved brand asset: {path}");
        Assert(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))) == hash, $"Approved brand asset changed: {path}");
    }
    Assert(!Directory.EnumerateFiles("src/Revora.Web/wwwroot", "*.zip", SearchOption.AllDirectories).Any(), "A ZIP archive entered the public asset tree.");
}

static void ValidatePublicMetadata()
{
    string page = PageRenderer.Render("en", LocalizedContent.For("en")["about"], new WebOptions { IndexingEnabled = true });
    const string prefix = "<script type=\"application/ld+json\">";
    int start = page.IndexOf(prefix, StringComparison.Ordinal);
    int end = page.IndexOf("</script>", start, StringComparison.Ordinal);
    Assert(start >= 0 && end > start, "Structured data block was not rendered.");
    using JsonDocument document = JsonDocument.Parse(page[(start + prefix.Length)..end]);
    Assert(document.RootElement.GetProperty("@context").GetString() == "https://schema.org", "Schema.org context is invalid.");
    Assert(document.RootElement.GetProperty("@graph").EnumerateArray().Any(node => node.GetProperty("@type").GetString() == "BreadcrumbList"), "Breadcrumb structured data is missing.");
    Contains(page, "property=\"og:image\"", "name=\"twitter:image\"", "meta name=\"robots\" content=\"index,follow\"");
}

sealed class FakeEnvironment(string name) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = name;
    public string ApplicationName { get; set; } = "Revora.WebTests";
    public string ContentRootPath { get; set; } = "/tmp";
    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
}

sealed class FakeAntiforgery : IAntiforgery
{
    public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => GetTokens(httpContext);
    public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => new("test-antiforgery", "test-cookie", "__RequestVerificationToken", "X-CSRF-TOKEN");
    public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(true);
    public void SetCookieTokenAndHeader(HttpContext httpContext) { }
    public Task ValidateRequestAsync(HttpContext httpContext) => Task.CompletedTask;
}
