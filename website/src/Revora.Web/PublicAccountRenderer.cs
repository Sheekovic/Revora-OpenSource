using System.Net;
using Revora.Persistence.PostgreSql;

namespace Revora.Web;

public static class PublicAccountRenderer
{
    private static readonly string[] Questions =
    [
        "What was the name of a childhood game you loved?",
        "What was the name of your first fictional hero?",
        "What private nickname did your family use for you?",
        "What was the first book you remember choosing yourself?",
        "What was the name of a memorable childhood place?",
        "What was the name of your first game character?",
        "What private word do you associate with your first school?",
        "What was the name of a favorite childhood toy?"
    ];

    public static string Register(string culture, string csrf, string formProof, string? error = null, string? errorField = null, NavigationState? navigation = null)
    {
        bool ar = culture == "ar";
        string body = Card(culture, "register", ar ? "إنشاء حساب" : "Create Account", ar ? "حسابك يصبح نشطًا فورًا، ولا تحتاج إلى بريد إلكتروني." : "Your account is active immediately. No email required.") +
            $"<form class=account-form method=post action=/{culture}/register autocomplete=on>{Hidden(csrf, formProof)}" +
            Field("register-username", "username", ar ? "اسم المستخدم" : "Username", "text", "username", "4", "15", "[A-Za-z0-9]+", ar ? "من 4 إلى 15 حرفًا إنجليزيًا أو رقمًا." : "4–15 English letters or numbers.", errorField == "username" ? error : null) +
            Field("register-password", "password", ar ? "كلمة المرور" : "Password", "password", "new-password", "8", "15", null, null, errorField == "password" ? error : null) +
            Field("register-confirm", "confirmPassword", ar ? "تأكيد كلمة المرور" : "Confirm password", "password", "new-password", "8", "15", null, null, errorField == "confirmPassword" ? error : null) +
            $"<p class=compatibility-note>{(ar ? "استخدم من 8 إلى 15 محرفًا مدعومًا لتعمل كلمة المرور في الموقع وعميل اللعبة." : "Use 8–15 supported characters so your password works on both the website and game client.")}</p>" +
            Honeypot("register-website") + FormError(errorField == "form" ? error : null) +
            $"<button class='button primary account-primary' type=submit>{(ar ? "إنشاء حساب" : "Create Account")}</button></form>" +
            $"<p class=account-secondary>{(ar ? "لديك حساب بالفعل؟" : "Already have an account?")} <a href=/{culture}/login>{(ar ? "تسجيل الدخول" : "Log in")}</a></p></section>";
        return Shell(culture, ar ? "إنشاء حساب" : "Create Account", "register", body, navigation);
    }

    public static string Login(string culture, string csrf, string formProof, string? error = null, NavigationState? navigation = null)
    {
        bool ar = culture == "ar";
        string body = Card(culture, "login", ar ? "تسجيل الدخول" : "Log in", ar ? "استخدم بيانات الحساب نفسها في الموقع وعميل ريفورا." : "Use the same credentials on the website and in the Revora client.") +
            $"<form class=account-form method=post action=/{culture}/login>{Hidden(csrf, formProof)}" +
            Field("login-username", "username", ar ? "اسم المستخدم" : "Username", "text", "username", "4", "15", null, null, null) +
            Field("login-password", "password", ar ? "كلمة المرور" : "Password", "password", "current-password", "8", "15", null, null, error) +
            $"<label class=check-row for=trust-device><input id=trust-device type=checkbox name=trustDevice value=true><span>{(ar ? "الوثوق بهذا الجهاز" : "Trust this device")}</span></label>" + Honeypot("login-website") +
            $"<button class='button primary account-primary' type=submit>{(ar ? "تسجيل الدخول" : "Log in")}</button></form>" +
            $"<div class=account-links><a href=/{culture}/forgot-password>{(ar ? "هل نسيت كلمة المرور؟" : "Forgot password?")}</a><a href=/{culture}/register>{(ar ? "إنشاء حساب" : "Create an account")}</a></div></section>";
        return Shell(culture, ar ? "تسجيل الدخول" : "Log in", "login", body, navigation);
    }

    public static string RecoveryCodes(string culture, string csrf, IReadOnlyList<string> codes, NavigationState? navigation = null)
    {
        string body = Card(culture, "account", "Save recovery codes", "Your account is active now. Each code works once. Store them offline; Revora cannot show them again.") +
            "<pre class=recovery-codes>" + WebUtility.HtmlEncode(string.Join('\n', codes)) + $"</pre><a class='button quiet account-wide-action' href=/{culture}/account/security/recovery-codes.txt>Download text copy</a>" +
            $"<form class=account-form method=post action=/{culture}/account/security/recovery-codes/acknowledge><input type=hidden name=__RequestVerificationToken value='{E(csrf)}'><label class=check-row for=recovery-ack><input id=recovery-ack type=checkbox name=acknowledged value=true required><span>I stored these codes safely.</span></label><button class='button primary account-primary' type=submit>Continue to account</button></form></section>";
        return Shell(culture, "Save recovery codes", "account", body, navigation);
    }

    public static string Dashboard(string culture, AccountDashboard account, string csrf, NavigationState? navigation = null)
    {
        string characters = account.Characters.Count == 0 ? "<p class=empty-copy>No characters yet.</p>" : "<ul class=character-list>" + string.Join(string.Empty, account.Characters.Select(character => $"<li><span>UID {character.LegacyUid}</span><strong>{E(character.Name)}</strong></li>")) + "</ul>";
        string body = Card(culture, "account", account.Username, "Your Revora account and genuine game identity share the same secure AccountId.") +
            $"<dl class=account-facts><div><dt>Status</dt><dd>{E(account.Status)}</dd></div><div><dt>Created</dt><dd>{account.CreatedAt:yyyy-MM-dd}</dd></div><div><dt>Recovery codes remaining</dt><dd>{account.RecoveryCodesRemaining}</dd></div><div><dt>Security questions</dt><dd>{account.SecurityQuestionCount}/3</dd></div></dl><h2>Characters</h2>{characters}<a class='button quiet account-wide-action' href=/{culture}/account/security>Account security</a><form class=account-form method=post action=/{culture}/account/security/sessions/revoke-others><input type=hidden name=__RequestVerificationToken value='{E(csrf)}'><button class='button quiet account-wide-action' type=submit>Sign out other devices</button></form><form class=account-form method=post action=/logout><input type=hidden name=__RequestVerificationToken value='{E(csrf)}'><button class='button quiet account-wide-action' type=submit>Log out</button></form></section>";
        return Shell(culture, "Account", "account", body, navigation);
    }

    public static string Security(string culture, string csrf, string formProof, string? message = null, NavigationState? navigation = null)
    {
        string body = Card(culture, "account", "Account security", "Recovery codes are the strongest recovery method. Security questions are a weaker supporting factor only.", message) +
            $"<div class=account-section><h2>Change password</h2><form class=account-form method=post action=/{culture}/account/security/password>{Hidden(csrf, formProof)}" +
            Field("current-password", "currentPassword", "Current password", "password", "current-password", null, "15", null, null, null) +
            Field("new-password", "newPassword", "New password", "password", "new-password", "8", "15", null, "8–15 supported characters.", null) +
            Field("confirm-new-password", "confirmPassword", "Confirm new password", "password", "new-password", "8", "15", null, null, null) +
            "<button class='button primary account-primary' type=submit>Change password</button></form></div>" +
            $"<div class=account-section><h2>Security questions <small>(optional)</small></h2><form class=account-form method=post action=/{culture}/account/security/questions>{Hidden(csrf, formProof)}{QuestionFields()}<button class='button primary account-primary' type=submit>Save three questions</button></form></div>" +
            $"<form class=account-form method=post action=/{culture}/account/security/recovery-codes/regenerate><input type=hidden name=__RequestVerificationToken value='{E(csrf)}'><button class='button quiet account-wide-action' type=submit>Regenerate recovery codes</button></form></section>";
        return Shell(culture, "Account security", "account", body, navigation);
    }

    public static string Forgot(string culture, string csrf, string formProof, string? message = null, NavigationState? navigation = null)
    {
        string body = Card(culture, "recovery", "Recover account", "Responses are generic and never reveal whether an account exists.", message) +
            $"<div class=account-section><h2>Use a recovery code</h2><form class=account-form method=post action=/{culture}/recover-account/code>{Hidden(csrf, formProof)}" +
            Field("recovery-username", "username", "Username", "text", "username", "4", "15", null, null, null) + Field("recovery-code", "recoveryCode", "Recovery code", "text", "one-time-code", null, "35", null, null, null) +
            "<button class='button primary account-primary' type=submit>Continue</button></form></div>" +
            $"<div class=account-section><h2>Questions with supporting evidence</h2><p class=section-copy>Questions cannot reset an account by themselves.</p><form class=account-form method=post action=/{culture}/recover-account/questions>{Hidden(csrf, formProof)}" +
            Field("question-username", "username", "Username", "text", "username", "4", "15", null, null, null) + AnswerFields() + "<button class='button quiet account-wide-action' type=submit>Submit recovery request</button></form></div></section>";
        return Shell(culture, "Recover account", "recovery", body, navigation);
    }

    public static string Reset(string culture, string csrf, string formProof, string? error = null, NavigationState? navigation = null)
    {
        string body = Card(culture, "recovery", "Choose a new password", "The authorization is short-lived and works once.") +
            $"<form class=account-form method=post action=/{culture}/reset-password>{Hidden(csrf, formProof)}" +
            Field("reset-password", "password", "New password", "password", "new-password", "8", "15", null, "8–15 supported characters.", error) +
            Field("reset-confirm", "confirmPassword", "Confirm password", "password", "new-password", "8", "15", null, null, null) +
            "<button class='button primary account-primary' type=submit>Reset password</button></form></section>";
        return Shell(culture, "Reset password", "recovery", body, navigation);
    }

    public static string Generic(string culture, string heading, string message, NavigationState? navigation = null)
    {
        string body = Card(culture, "recovery", heading, message) + $"<a class='button primary account-wide-action' href=/{culture}/>Return home</a></section>";
        return Shell(culture, heading, "recovery", body, navigation);
    }

    private static string Field(string id, string name, string label, string type, string autocomplete, string? min, string? max, string? pattern, string? help, string? error)
    {
        string described = error is not null ? $" aria-describedby='{id}-error' aria-invalid=true" : help is not null ? $" aria-describedby='{id}-help'" : string.Empty;
        return $"<div class=form-field><label for={id}>{E(label)}</label><input id={id} type={type} name={name}{(min is null ? "" : $" minlength={min}")}{(max is null ? "" : $" maxlength={max}")}{(pattern is null ? "" : $" pattern='{pattern}'")} required autocomplete={autocomplete}{described}>" +
            (help is null ? string.Empty : $"<small id={id}-help>{E(help)}</small>") + (error is null ? string.Empty : $"<p class=field-error id={id}-error role=alert>{E(error)}</p>") + "</div>";
    }

    private static string Honeypot(string id) => $"<div class=bot-field aria-hidden=true><label for={id}>Website</label><input id={id} name=website type=text tabindex=-1 autocomplete=off aria-hidden=true></div>";
    private static string FormError(string? error) => error is null ? string.Empty : $"<p class=form-alert role=alert>{E(error)}</p>";
    private static string Hidden(string csrf, string proof) => $"<input type=hidden name=__RequestVerificationToken value='{E(csrf)}'><input type=hidden name=formProof value='{E(proof)}'>";
    private static string QuestionFields() => string.Join("", Enumerable.Range(0, 3).Select(index => $"<div class=form-field><label for=question-{index + 1}>Question {index + 1}</label><select id=question-{index + 1} name=question{index + 1}>{string.Join("", Questions.Select((question, id) => $"<option value={id + 1}>{E(question)}</option>"))}</select></div><div class=form-field><label for=answer-{index + 1}>Answer</label><input id=answer-{index + 1} name=answer{index + 1} minlength=2 maxlength=100 required autocomplete=off></div>"));
    private static string AnswerFields() => string.Join("", Enumerable.Range(0, 3).Select(index => $"<div class=form-field><label for=recovery-question-{index + 1}>Question {index + 1} number</label><input id=recovery-question-{index + 1} type=number min=1 max=8 name=question{index + 1} required inputmode=numeric></div><div class=form-field><label for=recovery-answer-{index + 1}>Answer</label><input id=recovery-answer-{index + 1} name=answer{index + 1} maxlength=100 required autocomplete=off></div>"));

    private static string Card(string culture, string kind, string heading, string intro, string? message = null) =>
        $"<section class='account-card panel account-card-{kind}'><div class=account-emblem><img src=/assets/revora-full-logo.png width=1254 height=1254 alt='Revora emblem'></div><div class=account-heading><span class=eyebrow>{(culture == "ar" ? "بوابة ريفورا الآمنة" : "Secure Revora portal")}</span><h1>{E(heading)}</h1><p>{E(intro)}</p></div>{(message is null ? string.Empty : $"<div class=form-alert role=status>{E(message)}</div>")}";

    private static string Shell(string culture, string title, string active, string body, NavigationState? navigation) =>
        $"<!doctype html><html lang='{culture}' dir='{(culture == "ar" ? "rtl" : "ltr")}'><head><meta charset=utf-8><meta name=viewport content='width=device-width,initial-scale=1'><title>{E(title)} — Revora</title><meta name=robots content='noindex,nofollow'><link rel=icon href=/favicon.ico sizes=any><link rel=stylesheet href=/assets/site.css><link rel=stylesheet href=/assets/brand.css><link rel=stylesheet href=/assets/account.css></head><body class=account-page><a class=skip href=#main>{(culture == "ar" ? "انتقل إلى المحتوى" : "Skip to content")}</a>{PageRenderer.RenderSharedHeader(culture, active, navigation)}<main id=main class=account-main>{body}</main>{PageRenderer.RenderSharedFooter(culture)}</body></html>";

    private static string E(string value) => WebUtility.HtmlEncode(value);
}
