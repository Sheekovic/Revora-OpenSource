using System.Collections.Concurrent;
using System.Net;

namespace Revora.Web;

public sealed record AdministrativeAuditEvent(
    string ActorId,
    string Action,
    string TargetType,
    string TargetId,
    DateTimeOffset Timestamp,
    string Reason,
    string CorrelationId,
    string SafeBeforeSummary,
    string SafeAfterSummary);

public interface IAdministrativeAuditSink
{
    void Append(AdministrativeAuditEvent auditEvent);
    IReadOnlyCollection<AdministrativeAuditEvent> Recent(int maximum);
}

public sealed class InMemoryAdministrativeAuditSink : IAdministrativeAuditSink
{
    private const int Capacity = 256;
    private readonly ConcurrentQueue<AdministrativeAuditEvent> events = new();
    public void Append(AdministrativeAuditEvent auditEvent)
    {
        Validate(auditEvent);
        events.Enqueue(auditEvent);
        while (events.Count > Capacity) events.TryDequeue(out _);
    }
    public IReadOnlyCollection<AdministrativeAuditEvent> Recent(int maximum) => events.Reverse().Take(Math.Clamp(maximum, 0, Capacity)).ToArray();

    public static void Validate(AdministrativeAuditEvent auditEvent)
    {
        if (string.IsNullOrWhiteSpace(auditEvent.ActorId) || string.IsNullOrWhiteSpace(auditEvent.Action) || string.IsNullOrWhiteSpace(auditEvent.TargetType) || string.IsNullOrWhiteSpace(auditEvent.TargetId) || string.IsNullOrWhiteSpace(auditEvent.Reason) || string.IsNullOrWhiteSpace(auditEvent.CorrelationId)) throw new ArgumentException("Administrative audit fields are required.");
        string combined = $"{auditEvent.SafeBeforeSummary} {auditEvent.SafeAfterSummary}";
        string[] forbidden = ["password", "token", "connectionstring", "private key", "secret"];
        if (forbidden.Any(x => combined.Contains(x, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Sensitive material is forbidden in administrative audit summaries.");
    }
}

public static class AdminPageRenderer
{
    public static string Render(string culture, string section, WebOptions options)
    {
        bool ar = culture == "ar";
        (string Slug, string English, string Arabic)[] sections =
        [
            ("", "Overview", "نظرة عامة"), ("users", "User search", "بحث المستخدمين"), ("game-accounts", "Game accounts", "حسابات اللعبة"),
            ("characters", "Character search", "بحث الشخصيات"), ("moderation", "Bans & suspensions", "الحظر والتعليق"), ("news", "News", "الأخبار"),
            ("downloads", "Downloads", "التنزيلات"), ("patches", "Patch manifests", "بيانات التحديث"), ("maintenance", "Maintenance", "الصيانة"),
            ("status", "Server status", "حالة الخادم"), ("audit", "Audit log", "سجل التدقيق"), ("roles", "Roles", "الأدوار")
        ];
        string nav = string.Join("", sections.Select(x => $"<a{(x.Slug == section ? " aria-current=\"page\"" : "")} href=\"/{culture}/admin{(x.Slug.Length == 0 ? "" : $"/{x.Slug}")}\">{(ar ? x.Arabic : x.English)}</a>"));
        string title = sections.FirstOrDefault(x => x.Slug == section) is var current && current != default ? (ar ? current.Arabic : current.English) : (ar ? "الإدارة" : "Administration");
        string unavailable = ar ? "الإجراءات التي تغير البيانات معطّلة حتى يعمل التخزين الدائم وتُراجع قواعد التزامن والتدقيق." : "Data-changing actions are disabled until durable persistence, concurrency rules, and auditing are operational.";
        return $"<!doctype html><html lang=\"{culture}\" dir=\"{(ar ? "rtl" : "ltr")}\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>{WebUtility.HtmlEncode(title)} — Revora Admin</title><meta name=\"robots\" content=\"noindex,nofollow\"><link rel=\"stylesheet\" href=\"/assets/site.css\"><link rel=\"stylesheet\" href=\"/assets/account.css\"><link rel=\"stylesheet\" href=\"/assets/brand.css\"><link rel=\"icon\" href=\"/favicon.ico\" sizes=\"any\"></head><body><div class=\"dashboard-layout\"><aside class=\"dashboard-nav panel\"><a class=\"brand\" href=\"/{culture}/\"><img class=\"sidebar-brand-logo\" src=\"/assets/revora-header-logo.png\" width=\"2079\" height=\"756\" alt=\"Revora\"><span class=\"admin-label\">ADMIN</span></a><nav>{nav}</nav></aside><main class=\"dashboard-main\"><span class=\"eyebrow\">{(ar ? "منطقة إدارية محمية" : "Protected administration")}</span><h1>{WebUtility.HtmlEncode(title)}</h1><div class=\"form-alert\" role=\"status\">{unavailable}</div><section class=\"panel\" style=\"padding:1.5rem;margin-top:1rem\"><h2>{(ar ? "حالة الوحدة" : "Module state")}</h2><p>{(ar ? "الواجهة جاهزة للربط بمستودعات مراجعة، ولا تتوفر بيانات حقيقية بعد." : "The interface is ready for reviewed repositories; no live data is available yet.")}</p><button class=\"button quiet\" type=\"button\" disabled aria-disabled=\"true\">{(ar ? "الإجراءات معطّلة" : "Mutations disabled")}</button></section></main></div></body></html>";
    }
}
