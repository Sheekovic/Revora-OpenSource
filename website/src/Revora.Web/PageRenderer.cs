using System.Net;
using System.Text;
using System.Text.Json;

namespace Revora.Web;

public static class PageRenderer
{
    public static string Render(string culture, LocalizedPage page, WebOptions options, string? notice = null, NavigationState? navigation = null)
    {
        bool arabic = culture == "ar";
        string direction = arabic ? "rtl" : "ltr";
        string alternateCulture = LocalizedContent.OtherLanguage(culture);
        string canonical = $"{options.PublicBaseUrl}/{culture}/{page.Slug}".TrimEnd('/');
        canonical = page.Slug.Length == 0 ? $"{options.PublicBaseUrl}/{culture}/" : canonical;
        string alternate = page.Slug.Length == 0
            ? $"{options.PublicBaseUrl}/{alternateCulture}/"
            : $"{options.PublicBaseUrl}/{alternateCulture}/{page.Slug}";
        string robots = options.IndexingEnabled && page.Kind == "public" ? "index,follow" : "noindex,nofollow";
        string title = Html(page.Title);
        string description = Html(page.Description);
        string statusTitle = page.Slug == "download" ? (arabic ? "خدمة التنزيل" : "Download service") : (arabic ? "حالة العالم" : "Realm state");
        string statusText = page.Slug == "download" ? (arabic ? "متاحة — ملفات موقّعة عبر HTTPS" : "Available — signed files over HTTPS") : (arabic ? "قيد التطوير — غير متاح للاعبين" : "In development — not available to players");
        string jsonLd = BuildStructuredData(culture, page, options, canonical);
        string socialImage = $"{options.PublicBaseUrl}/assets/revora-guardian.png";

        StringBuilder body = new();
        body.Append("<!doctype html><html lang=\"").Append(culture).Append("\" dir=\"").Append(direction).Append("\"><head>")
            .Append("<meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">")
            .Append("<title>").Append(title).Append("</title><meta name=\"description\" content=\"").Append(description).Append("\">")
            .Append("<meta name=\"robots\" content=\"").Append(robots).Append("\">")
            .Append("<link rel=\"canonical\" href=\"").Append(canonical).Append("\">")
            .Append("<link rel=\"alternate\" hreflang=\"").Append(alternateCulture).Append("\" href=\"").Append(alternate).Append("\">")
            .Append("<link rel=\"alternate\" hreflang=\"").Append(culture).Append("\" href=\"").Append(canonical).Append("\">")
            .Append("<link rel=\"alternate\" hreflang=\"x-default\" href=\"").Append(options.PublicBaseUrl).Append("/\">")
            .Append("<meta property=\"og:type\" content=\"website\"><meta property=\"og:site_name\" content=\"Revora\">")
            .Append("<meta property=\"og:title\" content=\"").Append(title).Append("\"><meta property=\"og:description\" content=\"").Append(description).Append("\">")
            .Append("<meta property=\"og:url\" content=\"").Append(canonical).Append("\"><meta property=\"og:locale\" content=\"").Append(arabic ? "ar_AR" : "en_US").Append("\">")
            .Append("<meta property=\"og:image\" content=\"").Append(socialImage).Append("\"><meta property=\"og:image:alt\" content=\"Revora guardian\">")
            .Append("<meta name=\"twitter:card\" content=\"summary_large_image\"><meta name=\"twitter:image\" content=\"").Append(socialImage).Append("\"><link rel=\"icon\" href=\"/favicon.ico\" sizes=\"any\">")
            .Append("<link rel=\"stylesheet\" href=\"/assets/site.css?v=20260831-2\"><link rel=\"stylesheet\" href=\"/assets/brand.css?v=20260831-2\"><link rel=\"stylesheet\" href=\"/assets/downloads.css?v=20260831-2\"><script type=\"application/ld+json\">").Append(jsonLd).Append("</script></head><body>")
            .Append("<a class=\"skip\" href=\"#main\">").Append(arabic ? "انتقل إلى المحتوى" : "Skip to content").Append("</a>")
            .Append(RenderSharedHeader(culture, page.Slug, navigation));

        if (!string.IsNullOrWhiteSpace(notice))
        {
            body.Append("<div class=\"notice\" role=\"status\">").Append(Html(notice)).Append("</div>");
        }

        body.Append("<main id=\"main\"><section class=\"hero\"><div class=\"hero-art\" aria-hidden=\"true\"></div><div class=\"hero-copy\">")
            .Append("<span class=\"eyebrow\">").Append(arabic ? "مشروع مستقل • قيد التطوير" : "Independent project • In development").Append("</span>")
            .Append("<h1>").Append(Html(page.Heading)).Append("</h1><p class=\"lead\">").Append(Html(page.Lead)).Append("</p>")
            .Append("<div class=\"actions\"><a class=\"button primary\" href=\"").Append(arabic ? "/ar/register" : "/register").Append("\">").Append(arabic ? "إنشاء حساب" : "Create Account").Append("</a>")
            .Append("<a class=\"button quiet\" href=\"/").Append(culture).Append("/download\">").Append(arabic ? "مركز التنزيل" : "Download center").Append("</a></div></div></section>")
            .Append("<section class=\"content-grid\"><article class=\"prose panel\"><p>").Append(Html(page.Body)).Append("</p>").Append(RenderSpecial(culture, page.Slug)).Append("</article>")
            .Append("<aside class=\"status-card panel\"><span class=\"status-dot\"></span><div><strong>").Append(statusTitle).Append("</strong><p>")
            .Append(statusText).Append("</p></div></aside></section>")
            .Append(RenderHighlights(culture)).Append("</main>").Append(RenderSharedFooter(culture)).Append("</body></html>");
        return body.ToString();
    }

    public static string RenderLanguageChoice(WebOptions options)
    {
        string robots = options.IndexingEnabled ? "index,follow" : "noindex,nofollow";
        return $"<!doctype html><html lang=\"en\" dir=\"ltr\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>Revora — Choose language | اختر اللغة</title><meta name=\"robots\" content=\"{robots}\"><link rel=\"canonical\" href=\"{options.PublicBaseUrl}/\"><link rel=\"alternate\" hreflang=\"ar\" href=\"{options.PublicBaseUrl}/ar/\"><link rel=\"alternate\" hreflang=\"en\" href=\"{options.PublicBaseUrl}/en/\"><link rel=\"alternate\" hreflang=\"x-default\" href=\"{options.PublicBaseUrl}/\"><link rel=\"icon\" href=\"/favicon.ico\" sizes=\"any\"><link rel=\"stylesheet\" href=\"/assets/site.css\"><link rel=\"stylesheet\" href=\"/assets/brand.css\"></head><body><main class=\"language-gate\"><img class=\"full-brand-logo\" src=\"/assets/revora-full-logo.png\" width=\"1254\" height=\"1254\" alt=\"Revora\"><p>An original realm is taking shape. اختر لغتك لمتابعة رحلة ريفورا.</p><div class=\"actions\"><a class=\"button primary\" href=\"/en/\">Continue in English</a><a class=\"button quiet\" href=\"/ar/\" lang=\"ar\" dir=\"rtl\">المتابعة بالعربية</a></div></main></body></html>";
    }

    internal static string RenderSharedHeader(string culture, string slug, NavigationState? navigation = null)
    {
        navigation ??= NavigationState.Anonymous;
        bool ar = culture == "ar";
        string other = LocalizedContent.OtherLanguage(culture);
        string alternatePath = slug.Length == 0 ? $"/{other}/" : $"/{other}/{slug}";
        (string Slug, string En, string Ar)[] links = [("", "Home", "الرئيسية"), ("about", "About", "عن ريفورا"), ("news", "News", "الأخبار"), ("status", "Status", "الحالة"), ("features", "Features", "المزايا"), ("roadmap", "Roadmap", "الخارطة"), ("rankings", "Rankings", "التصنيفات"), ("download", "Download", "التنزيل")];
        StringBuilder nav = new("<header class=\"site-header\"><a class=\"brand\" href=\"/\" aria-label=\"Revora home\">");
        nav.Append("<img class=\"header-brand-logo\" src=\"/assets/revora-header-logo.png?v=20260831-2\" width=\"2079\" height=\"756\" alt=\"Revora game logo\"></a><nav aria-label=\"")
            .Append(ar ? "التنقل الرئيسي" : "Primary navigation").Append("\">");
        foreach (var link in links)
        {
            string href = link.Slug.Length == 0 ? $"/{culture}/" : $"/{culture}/{link.Slug}";
            nav.Append("<a href=\"").Append(href).Append("\"");
            if (link.Slug == slug) nav.Append(" aria-current=\"page\"");
            nav.Append('>').Append(ar ? link.Ar : link.En).Append("</a>");
        }
        if (navigation.IsAuthenticated)
        {
            nav.Append("<a class=\"nav-account\" href=\"/account\"");
            if (slug == "account") nav.Append(" aria-current=\"page\"");
            nav.Append('>').Append(Html(navigation.Username!)).Append("</a><form class=\"nav-logout\" method=\"post\" action=\"/logout\">")
                .Append("<input type=\"hidden\" name=\"__RequestVerificationToken\" value=\"").Append(Html(navigation.AntiforgeryToken!)).Append("\">")
                .Append("<button class=\"nav-login\" type=\"submit\">").Append(ar ? "تسجيل الخروج" : "Log out").Append("</button></form>");
        }
        else
        {
            nav.Append("<a class=\"nav-account\" href=\"").Append(ar ? "/ar/register" : "/register").Append("\"");
            if (slug == "register") nav.Append(" aria-current=\"page\"");
            nav.Append('>').Append(ar ? "إنشاء حساب" : "Create Account").Append("</a>")
                .Append("<a class=\"nav-login\" href=\"").Append(ar ? "/ar/login" : "/login").Append("\"");
            if (slug == "login") nav.Append(" aria-current=\"page\"");
            nav.Append('>').Append(ar ? "تسجيل الدخول" : "Log in").Append("</a>");
        }
        nav.Append("</nav><a class=\"language\" href=\"").Append(alternatePath).Append("\" hreflang=\"").Append(other).Append("\">")
            .Append(ar ? "English" : "العربية").Append("</a></header>");
        return nav.ToString();
    }

    private static string RenderSpecial(string culture, string slug)
    {
        bool ar = culture == "ar";
        return slug switch
        {
            "" => $"<div class=\"milestones\"><div><span>01</span><strong>{(ar ? "الدخول الموثق" : "Verified entry")}</strong></div><div><span>02</span><strong>{(ar ? "عالم متزامن" : "Synchronized world")}</strong></div><div><span>03</span><strong>{(ar ? "مجتمع يستمر" : "Community that lasts")}</strong></div></div>",
            "download" => RenderDownloads(ar),
            "rankings" => $"<div class=\"empty-state\"><span>◇</span><strong>{(ar ? "لا توجد شخصيات بعد" : "No characters yet")}</strong><p>{(ar ? "ستظهر البيانات الموثقة هنا بعد إطلاق العالم." : "Verified data will appear here after the realm is live.")}</p></div>",
            "status" => $"<dl class=\"health-list\"><div><dt>{(ar ? "خادم الحساب" : "Account service")}</dt><dd>{(ar ? "متاح" : "Available")}</dd></div><div><dt>{(ar ? "خادم اللعبة" : "Game service")}</dt><dd>{(ar ? "متاح" : "Available")}</dd></div><div><dt>{(ar ? "التسجيل" : "Registration")}</dt><dd>{(ar ? "مفتوح" : "Open")}</dd></div></dl>",
            "news" => $"<a class=\"news-card\" href=\"/{culture}/news/development-foundation\"><time datetime=\"2026-08-28\">28 Aug 2026</time><strong>{(ar ? "الأساس قبل الانطلاق" : "The foundation before the frontier")}</strong><span>{(ar ? "سجل هندسي واقعي" : "A factual engineering note")}</span></a>",
            _ => string.Empty
        };
    }

    private static string RenderDownloads(bool ar)
    {
        const string fullClientUrl = "/downloads/7630-original.7z";
        const string fullClientHash = "0fa5b083d73a43b60bbaf5f1b52a5358bc83a4acf987e4d16a678bbafbdda0c8";
        if (!ar)
        {
            return $"""
                <section class="download-options" aria-label="Revora downloads">
                  <article class="download-card recommended"><span class="download-tag">Original baseline</span><h2>Conquer Client 7630</h2><p>Complete preserved original patch-7630 client. This baseline is provided for the Revora 7630 migration and does not yet claim launcher or server interoperability.</p><dl><div><dt>Client build</dt><dd>7630</dd></div><div><dt>Archive</dt><dd>7z</dd></div><div><dt>Download size</dt><dd>3.28 GiB</dd></div><div><dt>Extracted size</dt><dd>6.88 GiB</dd></div><div><dt>Updated</dt><dd>31 August 2026</dd></div><div><dt>System</dt><dd>Windows</dd></div></dl><a class="button primary" href="{fullClientUrl}" download>Download Client 7630</a><p class="checksum"><strong>SHA-256</strong><code>{fullClientHash}</code></p></article>
                </section>
                <section class="download-instructions"><article><h2>Baseline archive</h2><ol><li>Download the complete 7z archive.</li><li>Verify its SHA-256 against the value above.</li><li>Extract all contents into a new writable folder.</li><li>Keep this original baseline unchanged for reproducible compatibility work.</li></ol></article><article><h2>Launcher status</h2><p>The previous launcher path has been retired. A patch-7630-compatible Revora launcher and authenticated handoff flow are still under migration and validation.</p></article></section>
                <aside class="download-warning"><strong>Use safely</strong><ul><li>Do not run directly from inside the archive.</li><li>Do not install under Program Files; administrator rights are not required.</li><li>This is the original client baseline, not a claim that Revora gameplay is ready.</li><li>Do not download loose Conquer.exe files from third-party sources.</li></ul></aside>
                """;
        }
        return $"""
            <section class="download-options" aria-label="تنزيلات ريفورا">
              <article class="download-card recommended"><span class="download-tag">خط أساس أصلي</span><h2>عميل Conquer 7630</h2><p>عميل الإصدار 7630 الأصلي الكامل والمحفوظ. يتوفر هذا الخط الأساسي لنقل ريفورا إلى 7630 ولا يمثل بعد ادعاءً بتوافق اللانشر أو الخادم.</p><dl><div><dt>إصدار العميل</dt><dd>7630</dd></div><div><dt>نوع الأرشيف</dt><dd>7z</dd></div><div><dt>حجم التنزيل</dt><dd>3.28 GiB</dd></div><div><dt>الحجم بعد الفك</dt><dd>6.88 GiB</dd></div><div><dt>آخر تحديث</dt><dd>31 أغسطس 2026</dd></div><div><dt>النظام</dt><dd>Windows</dd></div></dl><a class="button primary" href="{fullClientUrl}" download>تنزيل عميل 7630</a><p class="checksum"><strong>SHA-256</strong><code dir="ltr">{fullClientHash}</code></p></article>
            </section>
            <section class="download-instructions"><article><h2>أرشيف خط الأساس</h2><ol><li>نزّل أرشيف 7z الكامل.</li><li>تحقق من SHA-256 باستخدام القيمة أعلاه.</li><li>فك جميع المحتويات داخل مجلد جديد قابل للكتابة.</li><li>احتفظ بخط الأساس الأصلي دون تعديل لأعمال التوافق القابلة للتكرار.</li></ol></article><article><h2>حالة اللانشر</h2><p>تم إيقاف مسار اللانشر السابق. لا يزال لانشر ريفورا المتوافق مع 7630 ومسار التسليم الموثق قيد النقل والتحقق.</p></article></section>
            <aside class="download-warning"><strong>استخدام آمن</strong><ul><li>لا تشغّل البرنامج من داخل الأرشيف.</li><li>لا تثبّته داخل Program Files؛ لا تحتاج إلى صلاحيات المسؤول.</li><li>هذا هو خط أساس العميل الأصلي وليس ادعاءً بأن لعب ريفورا أصبح جاهزًا.</li><li>لا تحمّل ملفات Conquer.exe منفردة من مصادر خارجية.</li></ul></aside>
            """;
    }

    private static string RenderHighlights(string culture)
    {
        bool ar = culture == "ar";
        return $"<section class=\"highlights\" aria-labelledby=\"highlights-title\"><div class=\"section-heading\"><span>{(ar ? "مبادئ ريفورا" : "Revora principles")}</span><h2 id=\"highlights-title\">{(ar ? "الصدق جزء من التجربة" : "Honesty is part of the experience")}</h2></div><div class=\"card-row\"><article class=\"panel\"><span class=\"rune\">◈</span><h3>{(ar ? "أصل مستقل" : "Original identity")}</h3><p>{(ar ? "عالم وهوية بصرية ونصوص أصلية بالكامل." : "An original world, visual language, and voice.")}</p></article><article class=\"panel\"><span class=\"rune\">⌁</span><h3>{(ar ? "أمان من الأساس" : "Secure foundation")}</h3><p>{(ar ? "حدود واضحة واختبارات قبل كل توسع." : "Explicit boundaries and tests before expansion.")}</p></article><article class=\"panel\"><span class=\"rune\">◇</span><h3>{(ar ? "تقدم موثّق" : "Evidence-led progress")}</h3><p>{(ar ? "لا وعود مزيفة ولا أرقام مختلقة." : "No fake promises and no invented numbers.")}</p></article></div></section>";
    }

    internal static string RenderSharedFooter(string culture)
    {
        bool ar = culture == "ar";
        return $"<footer><div><strong>REVORA</strong><p>{(ar ? "مشروع مستقل غير تابع أو معتمد من مالكي Conquer Online." : "An independent project, not affiliated with or endorsed by the owners of Conquer Online.")}</p></div><div><a href=\"/{culture}/privacy\">{(ar ? "الخصوصية" : "Privacy")}</a><a href=\"/{culture}/terms\">{(ar ? "الشروط" : "Terms")}</a><a href=\"/{culture}/contact\">{(ar ? "الدعم" : "Support")}</a><a href=\"/{culture}/status\">{(ar ? "الحالة" : "Status")}</a></div><small>© 2026 Revora. {(ar ? "جميع الحقوق محفوظة." : "All rights reserved.")}</small></footer>";
    }

    private static string BuildStructuredData(string culture, LocalizedPage page, WebOptions options, string canonical)
    {
        Dictionary<string, object?> website = new()
        {
            ["@type"] = "WebSite",
            ["@id"] = $"{options.PublicBaseUrl}/#website",
            ["name"] = "Revora",
            ["url"] = options.PublicBaseUrl,
            ["inLanguage"] = new[] { "ar", "en" }
        };
        Dictionary<string, object?> organization = new()
        {
            ["@type"] = "Organization",
            ["@id"] = $"{options.PublicBaseUrl}/#organization",
            ["name"] = "Revora",
            ["url"] = options.PublicBaseUrl,
            ["logo"] = $"{options.PublicBaseUrl}/assets/revora-full-logo.png"
        };
        Dictionary<string, object?> webPage = new()
        {
            ["@type"] = "WebPage",
            ["@id"] = $"{canonical}#webpage",
            ["name"] = page.Title,
            ["description"] = page.Description,
            ["url"] = canonical,
            ["inLanguage"] = culture,
            ["isPartOf"] = new Dictionary<string, object?> { ["@id"] = website["@id"] }
        };
        List<object> graph = [website, organization, webPage];
        if (page.Slug.Length > 0)
        {
            graph.Add(new Dictionary<string, object?>
            {
                ["@type"] = "BreadcrumbList",
                ["itemListElement"] = new object[]
                {
                    new Dictionary<string, object?>
                    {
                        ["@type"] = "ListItem", ["position"] = 1, ["name"] = culture == "ar" ? "الرئيسية" : "Home", ["item"] = $"{options.PublicBaseUrl}/{culture}/"
                    },
                    new Dictionary<string, object?>
                    {
                        ["@type"] = "ListItem", ["position"] = 2, ["name"] = page.Title, ["item"] = canonical
                    }
                }
            });
        }

        return JsonSerializer.Serialize(new Dictionary<string, object?> { ["@context"] = "https://schema.org", ["@graph"] = graph });
    }

    private static string Html(string value) => WebUtility.HtmlEncode(value);
}
