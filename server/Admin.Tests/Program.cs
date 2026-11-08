using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Revora.Admin;
using Revora.Auth.SimpleIdentity;

string root = Path.GetFullPath(args[0]);
string fixture = Path.Combine(root, "obj", "admin-database-diagnostic", "test-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.Combine(fixture, "keys"));
var protection = DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(fixture, "keys")), b => b.SetApplicationName("Revora.WKAdmin"));
var security = new AdminSecurityStore(fixture, protection);
var hasher = new RevoraPasswordHasher();
string password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
byte[] secret = RandomNumberGenerator.GetBytes(20);
security.Bootstrap("synthetic-admin", password, secret, hasher);
Check(hasher.Verify(password, security.Load().PasswordHash), "Argon2 verification");
Check(!hasher.Verify("wrong-password", security.Load().PasswordHash), "Wrong password rejected");
Check(AdminSecurityStore.VerifyCode(System.Text.Encoding.ASCII.GetBytes("12345678901234567890"), "287082", DateTimeOffset.FromUnixTimeSeconds(59)), "RFC TOTP vector");
Check(!security.VerifyTotp(security.Load(), "invalid"), "Invalid TOTP rejected");
var start = new ProcessStartInfo(Path.Combine(root, "obj", "admin-database-diagnostic", "publish", "Revora.Admin.exe"))
{
    UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
};
start.Environment["REVORA_ADMIN_DATA"] = fixture;
start.Environment["REVORA_ADMIN_URL"] = "http://127.0.0.1:5082";
using var process = Process.Start(start) ?? throw new Exception("Test host did not start");
var output = process.StandardOutput.ReadToEndAsync();
var error = process.StandardError.ReadToEndAsync();
try
{
    using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { BaseAddress = new Uri("http://127.0.0.1:5082"), Timeout = TimeSpan.FromSeconds(10) };
    client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
    bool ready = false;
    for (int attempt = 0; attempt < 40 && !process.HasExited; attempt++)
    {
        try { using var response = await client.GetAsync("/login"); if (response.StatusCode == HttpStatusCode.OK) { ready = true; break; } }
        catch (HttpRequestException) { }
        await Task.Delay(250);
    }
    Check(ready, "Isolated admin startup");
    using var anonymous = await client.GetAsync("/");
    Check(anonymous.StatusCode == HttpStatusCode.Redirect, "Dashboard requires authentication");
    using var login = await client.GetAsync("/login");
    string html = await login.Content.ReadAsStringAsync();
    string csrf = Regex.Match(html, "name=__RequestVerificationToken value='([^']+)'").Groups[1].Value;
    string cookie = login.Headers.GetValues("Set-Cookie").First().Split(';')[0];
    Check(login.Headers.GetValues("Set-Cookie").First().Contains("secure", StringComparison.OrdinalIgnoreCase), "Secure CSRF cookie");
    client.DefaultRequestHeaders.Add("Cookie", cookie);
    using var missingCsrf = await client.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string,string>()));
    Check(missingCsrf.StatusCode != HttpStatusCode.Redirect, "Missing CSRF rejected");
    string Code()
    {
        Span<byte> counter = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(counter, DateTimeOffset.UtcNow.ToUnixTimeSeconds()/30);
        byte[] hash = HMACSHA1.HashData(secret, counter);
        int offset = hash[^1] & 15;
        return (((hash[offset] & 127)<<24 | hash[offset+1]<<16 | hash[offset+2]<<8 | hash[offset+3]) % 1000000).ToString("D6");
    }
    using var signin = await client.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string,string> {
        ["username"]="synthetic-admin", ["password"]=password, ["code"]=Code(), ["__RequestVerificationToken"]=csrf }));
    Check(signin.StatusCode == HttpStatusCode.Redirect, "Password and TOTP sign-in");
    string auth = signin.Headers.GetValues("Set-Cookie").First(x=>x.StartsWith("__Host-RevoraAdmin=")).Split(';')[0];
    client.DefaultRequestHeaders.Remove("Cookie");
    client.DefaultRequestHeaders.Add("Cookie", cookie + "; " + auth);
    using var dashboard = await client.GetAsync("/");
    string body = await dashboard.Content.ReadAsStringAsync();
    Check(dashboard.StatusCode == HttpStatusCode.OK && body.Contains("revora6711_3d"), "Authenticated live PostgreSQL dashboard");
    Check(!body.Contains("WKConquer") && !body.Contains("Modern Revora identities"), "Stale database labels removed");
    using var mutation = await client.PostAsync("/characters/1", new FormUrlEncodedContent(new Dictionary<string,string>()));
    Check(mutation.StatusCode == HttpStatusCode.Conflict, "Character mutation disabled");
    Console.WriteLine("All admin integration checks passed; no live account or character data modified.");
}
finally
{
    if (!process.HasExited) process.Kill(true);
    await process.WaitForExitAsync();
    await output; await error;
    CryptographicOperations.ZeroMemory(secret);
}
static void Check(bool success, string name)
{
    if (!success) throw new Exception("FAILED: " + name);
    Console.WriteLine("PASS: " + name);
}
