using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Revora.Admin;
using Revora.Auth.SimpleIdentity;

internal class Program
{
	private static async Task Main(string[] args)
	{
		string dataRoot = Environment.GetEnvironmentVariable("REVORA_ADMIN_DATA") ?? @"C:\Revora\Admin\data";
        Directory.CreateDirectory(dataRoot);
		Directory.CreateDirectory(Path.Combine(dataRoot, "keys"));
		WebApplicationBuilder webApplicationBuilder = WebApplication.CreateBuilder(args);
		string listen = Environment.GetEnvironmentVariable("REVORA_ADMIN_URL") ?? "http://127.0.0.1:5081";
        if (!Uri.TryCreate(listen, UriKind.Absolute, out var uri) || uri.Scheme != "http" || uri.Host != "127.0.0.1" || uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.UserInfo.Length != 0)
            throw new InvalidOperationException("Admin must listen on loopback HTTP.");
        webApplicationBuilder.WebHost.UseUrls(listen);
		webApplicationBuilder.Services.AddDataProtection().SetApplicationName("Revora.WKAdmin").PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataRoot, "keys")));
		webApplicationBuilder.Services.AddSingleton<RevoraPasswordHasher>();
				webApplicationBuilder.Services.AddSingleton<LiveGameStore>();
		webApplicationBuilder.Services.AddSingleton((IServiceProvider sp) => new AdminSecurityStore(dataRoot, sp.GetRequiredService<IDataProtectionProvider>()));
		webApplicationBuilder.Services.AddAntiforgery(delegate(AntiforgeryOptions o)
		{
			o.Cookie.Name = "__Host-RevoraAdminCsrf";
			o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
			o.Cookie.SameSite = SameSiteMode.Strict;
		});
		webApplicationBuilder.Services.AddAuthentication("Cookies").AddCookie(delegate(CookieAuthenticationOptions o)
		{
			o.Cookie.Name = "__Host-RevoraAdmin";
			o.Cookie.HttpOnly = true;
			o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
			o.Cookie.SameSite = SameSiteMode.Strict;
			o.LoginPath = "/login";
			o.ExpireTimeSpan = TimeSpan.FromMinutes(30.0);
			o.SlidingExpiration = false;
			o.Events.OnValidatePrincipal = async delegate(CookieValidatePrincipalContext c)
			{
				AdminSecurityStore requiredService2 = c.HttpContext.RequestServices.GetRequiredService<AdminSecurityStore>();
				string? text = c.Principal?.FindFirstValue("stamp");
				if (!requiredService2.Exists || text != requiredService2.Load().SecurityStamp.ToString())
				{
					c.RejectPrincipal();
					await c.HttpContext.SignOutAsync();
				}
			};
		});
		webApplicationBuilder.Services.AddAuthorization();
		webApplicationBuilder.Services.AddRateLimiter(delegate(RateLimiterOptions o)
		{
			o.AddFixedWindowLimiter("login", delegate(FixedWindowRateLimiterOptions x)
			{
				x.PermitLimit = 5;
				x.Window = TimeSpan.FromMinutes(1.0);
				x.QueueLimit = 0;
			});
		});
		webApplicationBuilder.Services.Configure(delegate(ForwardedHeadersOptions o)
		{
			o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
			o.KnownIPNetworks.Clear();
            o.KnownProxies.Add(IPAddress.Loopback);
			o.ForwardLimit = 1;
		});
		WebApplication webApplication = webApplicationBuilder.Build();
		AdminSecurityStore requiredService = webApplication.Services.GetRequiredService<AdminSecurityStore>();
		if (!requiredService.Exists)
		{
			throw new InvalidOperationException("Existing administrator credentials are required.");
		}

        // Validate the pinned live database and schema before reporting ready.
        var liveStore = webApplication.Services.GetRequiredService<LiveGameStore>();
        var initial = await liveStore.ReadAsync(default);
        if (args.Contains("--verify-database", StringComparer.Ordinal))
        {
            Console.WriteLine($"Verified revora6711_3d: {initial.Accounts.Count} accounts, {initial.Characters.Count} characters.");
            return;
        }
        webApplication.UseExceptionHandler(handler => handler.Run(async c =>
        {
            c.Response.StatusCode = 503;
            await c.Response.WriteAsync("Administration is temporarily unavailable.");
        }));
        webApplication.UseForwardedHeaders();
		webApplication.Use(async delegate(HttpContext c, Func<Task> n)
		{
			c.Response.Headers["X-Robots-Tag"] = "noindex,nofollow";
			c.Response.Headers.CacheControl = "no-store";
			c.Response.Headers.XContentTypeOptions = "nosniff";
			c.Response.Headers.XFrameOptions = "DENY";
			c.Response.Headers.ContentSecurityPolicy = "default-src 'none';style-src 'unsafe-inline';form-action 'self';frame-ancestors 'none'";
			await n();
		});
		webApplication.UseRateLimiter();
		webApplication.UseAuthentication();
		webApplication.UseAuthorization();
		webApplication.UseAntiforgery();
		webApplication.MapGet("/login", (HttpContext c, IAntiforgery csrf) => Html(Page("Sign in", "<h1>Administrator sign in</h1><form method=post><input type=hidden name=__RequestVerificationToken value='" + E(csrf.GetAndStoreTokens(c).RequestToken ?? "") + "'><label>Username<input name=username required autocomplete=username></label><label>Password<input type=password name=password required autocomplete=current-password></label><label>Authenticator code<input name=code inputmode=numeric pattern='[0-9]{6}' required autocomplete=one-time-code></label><button>Sign in</button></form>")));
		webApplication.MapPost("/login", async Task<IResult> (HttpContext c, IAntiforgery csrf, AdminSecurityStore store, RevoraPasswordHasher hasher) =>
		{
			await csrf.ValidateRequestAsync(c);
			IFormCollection formCollection = await c.Request.ReadFormAsync();
			AdminCredential adminCredential = store.Load();
			if (!string.Equals(formCollection["username"], adminCredential.Username, StringComparison.Ordinal) || !hasher.Verify(formCollection["password"].ToString(), adminCredential.PasswordHash) || !store.VerifyTotp(adminCredential, formCollection["code"].ToString()))
			{
				return Results.Content(Page("Rejected", "<h1>Sign in rejected</h1><a href=/login>Try again</a>"), "text/html; charset=utf-8", null, 401);
			}
			ClaimsIdentity identity = new ClaimsIdentity(new Claim[2]
			{
				new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name", adminCredential.Username),
				new Claim("stamp", adminCredential.SecurityStamp.ToString())
			}, "Cookies");
			await c.SignInAsync(new ClaimsPrincipal(identity));
			return Results.Redirect("/");
		}).RequireRateLimiting("login");

        webApplication.MapGet("/", async (HttpContext c, IAntiforgery csrf, LiveGameStore store) =>
        {
            var snapshot = await store.ReadAsync(c.RequestAborted);
            string accounts = string.Join("", snapshot.Accounts.Select(x => $"<tr><td>{E(x.Username)}</td><td>{x.CharacterId}</td><td>{x.State}</td></tr>"));
            string characters = string.Join("", snapshot.Characters.Select(x => $"<tr><td>{x.Uid}</td><td>{E(x.Name)}</td><td>{x.Level}</td><td>{x.Profession}</td><td>{x.Map} ({x.X}, {x.Y})</td><td>{(x.Online ? "Online" : "Offline")}</td></tr>"));
            string body = $"<div class=shell><aside><div class=brand><span>R</span><div><b>REVORA</b><small>ADMINISTRATION</small></div></div><nav><a href='#accounts'>Registered users</a><a href='#characters'>Characters</a></nav><div class=operator><b>{E(c.User.Identity?.Name ?? "")}</b><form method=post action=/logout><input type=hidden name=__RequestVerificationToken value='{E(csrf.GetAndStoreTokens(c).RequestToken ?? "")}'><button>Sign out</button></form></div></aside><main><header><div><p class=eyebrow>LIVE GAME DATABASE</p><h1>Revora administration</h1><p>PostgreSQL · revora6711_3d · 127.0.0.1:25432</p></div></header><section class=metrics><article><span>REGISTERED USERS</span><b>{snapshot.Accounts.Count}</b></article><article><span>CHARACTERS</span><b>{snapshot.Characters.Count}</b></article><article><span>ONLINE</span><b>{snapshot.Characters.Count(x => x.Online)}</b></article></section><p>Character editing is temporarily unavailable while safe coordination with the game server is being added.</p><section id=accounts class=panel><h2>Registered users</h2><div class=scroll><table><thead><tr><th>Username</th><th>Character UID</th><th>Account state</th></tr></thead><tbody>{accounts}</tbody></table></div></section><section id=characters class=panel><h2>Characters</h2><div class=scroll><table><thead><tr><th>UID</th><th>Name</th><th>Level</th><th>Class</th><th>Location</th><th>State</th></tr></thead><tbody>{characters}</tbody></table></div></section></main></div>";
            return Html(Page("Dashboard", body, true));
        }).RequireAuthorization();
        webApplication.MapPost("/characters/{uid}", () => Results.StatusCode(409)).RequireAuthorization();
		webApplication.MapPost("/logout", async (HttpContext c, IAntiforgery csrf) =>
		{
			await csrf.ValidateRequestAsync(c);
			await c.SignOutAsync();
			return Results.Redirect("/login");
		}).RequireAuthorization();
		webApplication.Lifetime.ApplicationStarted.Register(delegate
		{
			Console.WriteLine("[Revora.Admin] READY");
		});
		await webApplication.RunAsync();
		static string E(string value)
		{
			return HtmlEncoder.Default.Encode(value);
		}
		static IResult Html(string value)
		{
			return Results.Content(value, "text/html; charset=utf-8");
		}
		static string Page(string title, string body, bool dashboard = false)
		{
			return "<!doctype html><html><head><meta charset=utf-8><meta name=viewport content='width=device-width'><meta name=robots content='noindex,nofollow'><title>" + E(title) + " &middot; Revora</title><style>:root{color-scheme:dark;--bg:#080b10;--side:#0c1119;--card:#111923;--card2:#151f2c;--line:#243141;--gold:#d8ad61;--text:#f3f5f7;--muted:#8e9aaa;--green:#55d58b}*{box-sizing:border-box}html{scroll-behavior:smooth}body{margin:0;background:var(--bg);color:var(--text);font:14px/1.5 Inter,Segoe UI,system-ui,sans-serif}body:before{content:'';position:fixed;inset:0;pointer-events:none;background:radial-gradient(circle at 75% -10%,#23364d66,transparent 40rem)}.shell{position:relative;display:grid;grid-template-columns:250px 1fr;min-height:100vh}.shell>aside{position:sticky;top:0;height:100vh;padding:28px 20px;border-right:1px solid var(--line);background:#0b1017e8;display:flex;flex-direction:column}.brand{display:flex;align-items:center;gap:12px;padding:0 8px 28px}.brand>span{display:grid;place-items:center;width:42px;height:42px;border:1px solid #795f33;border-radius:12px;background:linear-gradient(145deg,#332819,#17130e);color:var(--gold);font:700 24px Georgia}.brand b{display:block;color:var(--gold);letter-spacing:.17em}.brand small{font-size:9px;letter-spacing:.13em}.shell nav{display:grid;gap:6px}.shell nav a{padding:11px 13px;border-radius:9px;color:var(--muted);text-decoration:none;font-weight:600}.shell nav a:hover,.shell nav a.active{color:#fff;background:#ffffff0b}.shell nav a.active{box-shadow:inset 2px 0 var(--gold)}.operator{margin-top:auto;padding:16px 8px 0;border-top:1px solid var(--line);display:grid;gap:5px}.operator b{font-size:13px}.operator form{margin-top:8px}.shell>main{width:min(1280px,100%);padding:42px clamp(28px,5vw,70px);margin:0}.shell header{display:flex;justify-content:space-between;align-items:flex-start;margin-bottom:32px}.shell h1{margin:2px 0 5px;font:600 clamp(25px,3vw,38px) Georgia;color:#fff}.shell h2{margin:2px 0;font:600 23px Georgia;color:#fff}.shell p{margin:0;color:var(--muted)}.eyebrow{color:var(--gold)!important;font-size:10px;font-weight:800;letter-spacing:.16em}.health{padding:8px 12px;border:1px solid #244735;border-radius:999px;background:#10251a;color:#85e8ad;font-size:12px}.metrics{display:grid;grid-template-columns:repeat(4,1fr);gap:14px;margin-bottom:26px}.metrics article{padding:19px;border:1px solid var(--line);border-radius:13px;background:linear-gradient(145deg,var(--card2),var(--card))}.metrics span{display:block;color:var(--muted);font-size:10px;font-weight:800;letter-spacing:.1em}.metrics b{display:block;margin:7px 0 2px;color:#fff;font:600 30px Georgia}.metrics small{font-size:11px}.panel{margin-bottom:24px;border:1px solid var(--line);border-radius:14px;background:var(--card);overflow:hidden}.section-title{display:flex;justify-content:space-between;align-items:center;padding:20px 22px;border-bottom:1px solid var(--line)}.section-title>span{color:var(--muted);font-size:12px}.scroll{overflow:auto}table{width:100%;border-collapse:collapse}th{padding:11px 16px;background:#0d141d;color:#778495;font-size:10px;letter-spacing:.08em;text-transform:uppercase;text-align:left}td{padding:14px 16px;border-top:1px solid #202c3a;color:#cbd2da}td strong{display:block;color:#fff}td small{display:block;color:#748090}.status{display:inline-block;padding:4px 8px;border-radius:999px;font-size:11px;font-weight:700}.status.online{background:#40251b;color:#ffb28f}.status.offline{background:#153526;color:#7ee1a7}.edit{display:inline-block;padding:6px 10px;border:1px solid #4d402b;border-radius:7px;color:var(--gold);text-decoration:none}.shell form{display:grid;gap:12px}.quiet{min-height:34px;background:#171f29;color:#b5bec9;border:1px solid var(--line)}label{display:grid;gap:6px;color:#ccd3db}input,button{min-height:44px;padding:10px 13px;border:1px solid #35465c;border-radius:8px;background:#0a1017;color:#fff;font:inherit}button{background:linear-gradient(135deg,#e2b969,#a97d36);color:#111;font-weight:750}body>main{width:min(560px,calc(100% - 32px));margin:70px auto}body>main h1{font:600 34px Georgia;color:var(--gold)}body>main form{display:grid;gap:15px}.online{color:#ffb28f}.offline{color:#7ee1a7}@media(max-width:900px){.shell{grid-template-columns:1fr}.shell>aside{position:relative;height:auto}.shell nav{display:flex;flex-wrap:wrap}.operator{margin-top:20px}.metrics{grid-template-columns:repeat(2,1fr)}}@media(max-width:560px){.shell>main{padding:25px 16px}.metrics{grid-template-columns:1fr}.health{display:none}}</style></head><body>" + (dashboard ? body : ("<main class=standalone>" + body + "</main>")) + "</body></html>";
		}
	}
}
