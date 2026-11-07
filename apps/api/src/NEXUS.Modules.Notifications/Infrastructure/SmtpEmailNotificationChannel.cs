using System.Net;
using System.Net.Mail;
using NEXUS.Modules.Notifications.Application;
using NEXUS.Modules.Notifications.Domain;

namespace NEXUS.Modules.Notifications.Infrastructure;

/// <summary>Resolves a scoped SMTP profile for each canonical notification and sends via SMTP.</summary>
public sealed class SmtpEmailNotificationChannel(
    IEmailNotificationProfileStore profiles,
    ISmtpEmailNotificationTransport transport) : IEmailNotificationSender
{
    public async Task<bool> IsConfiguredAsync(Guid tenantId, Guid? organizationId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || organizationId == Guid.Empty) return false;
        var effective = await profiles.ResolveEffectiveAsync(tenantId, organizationId, cancellationToken);
        return effective?.Profile.Enabled == true && IsSendProfileComplete(effective.Profile);
    }

    public async Task<EmailSendResult> SendAsync(Notification notification, string recipientEmail, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        if (notification.TenantId == Guid.Empty || notification.OrganizationId == Guid.Empty)
            return new(false, EmailFailureKind.Permanent, "InvalidScope", "The notification scope is invalid.");

        if (!MailAddress.TryCreate(recipientEmail, out _))
            return new(false, EmailFailureKind.Permanent, "InvalidRecipient", "The email recipient address is invalid.");

        EmailNotificationProfileRecord? effective;
        try
        {
            effective = await profiles.ResolveEffectiveAsync(notification.TenantId, notification.OrganizationId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new(false, EmailFailureKind.Transient, "EmailProfileUnavailable", "Email delivery configuration is temporarily unavailable.");
        }

        if (effective?.Profile.Enabled != true || !IsSendProfileComplete(effective.Profile))
            return new(false, EmailFailureKind.Permanent, "EmailUnavailable", "The effective email profile is disabled or incomplete.");

        var title = NormalizeHeader(notification.Title);
        var body = notification.Body ?? notification.Title;
        if (title.Length == 0 || body.Length > SmtpEmailMessage.MaximumBodyLength)
            return new(false, EmailFailureKind.Permanent, "InvalidMessage", "The notification content is invalid.");

        var message = new SmtpEmailMessage(title, body, RenderHtml(title, body));
        SmtpTransportResult result;
        try
        {
            result = await transport.SendAsync(
                effective.Profile,
                notification.TenantId,
                notification.OrganizationId,
                recipientEmail,
                message,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new(false, EmailFailureKind.Transient, "ProviderError", "The SMTP server could not complete the request.");
        }

        return result.Succeeded
            ? new(true)
            : new(false,
                result.IsTransient ? EmailFailureKind.Transient : EmailFailureKind.Permanent,
                result.FailureCode ?? "ProviderError",
                result.SafeSummary ?? "The SMTP server could not complete the request.");
    }

    private static bool IsSendProfileComplete(SmtpTransportProfile profile) =>
        !string.IsNullOrWhiteSpace(profile.Host)
        && profile.Port is >= 1 and <= 65535
        && (profile.SecurityMode is SmtpSecurityMode.StartTlsRequired or SmtpSecurityMode.ImplicitTls)
        && (profile.AuthenticationMode == SmtpAuthenticationMode.TrustedRelay
            || profile.AuthenticationMode == SmtpAuthenticationMode.UsernamePassword
                && !string.IsNullOrWhiteSpace(profile.UserName) && !string.IsNullOrEmpty(profile.Password))
        && MailAddress.TryCreate(profile.FromAddress, out _);

    private static string NormalizeHeader(string? value)
    {
        var normalized = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        return normalized.Length <= SmtpEmailMessage.MaximumSubjectLength ? normalized : normalized[..SmtpEmailMessage.MaximumSubjectLength];
    }

    private static string RenderHtml(string title, string body)
    {
        var isArabic = body.Any(character => character is >= '\u0600' and <= '\u06ff');
        var direction = isArabic ? "rtl" : "ltr";
        var language = isArabic ? "ar" : "en";
        var paragraphs = body.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n')
            .Split('\n')
            .Select(line => string.IsNullOrWhiteSpace(line)
                ? "<div style=\"height:10px\"></div>"
                : $"<p style=\"margin:0 0 10px;line-height:1.65;white-space:pre-wrap\">{RenderTextLine(line)}</p>");
        var safeTitle = WebUtility.HtmlEncode(title);
        return $"<!doctype html><html lang=\"{language}\" dir=\"{direction}\"><head><meta charset=\"utf-8\"></head><body style=\"margin:0;background:#f4f6f8;font-family:Arial,Helvetica,sans-serif;color:#1f2937\"><table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"padding:28px 12px;background:#f4f6f8\"><tr><td align=\"center\"><table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"max-width:680px;background:#fff;border:1px solid #e5e7eb;border-radius:12px\"><tr><td style=\"padding:24px 28px 10px;border-bottom:1px solid #e5e7eb\"><h1 style=\"margin:0 0 14px;font-size:20px;line-height:1.4;color:#111827\">{safeTitle}</h1></td></tr><tr><td style=\"padding:22px 28px;font-size:14px;line-height:1.65\">{string.Join(string.Empty, paragraphs)}</td></tr></table><p style=\"max-width:680px;margin:14px auto 0;color:#6b7280;font-size:11px;line-height:1.5\">FOPX PM</p></td></tr></table></body></html>";
    }

    private static string RenderTextLine(string line)
    {
        const string englishMeeting = "Open meeting: ";
        const string arabicMeeting = "فتح الاجتماع: ";
        const string englishInvitation = "Accept or decline here: ";
        const string arabicInvitation = "يمكنك القبول أو الرفض من هنا: ";
        var prefix = line.StartsWith(englishMeeting, StringComparison.Ordinal) ? englishMeeting
            : line.StartsWith(arabicMeeting, StringComparison.Ordinal) ? arabicMeeting
            : line.StartsWith(englishInvitation, StringComparison.Ordinal) ? englishInvitation
            : line.StartsWith(arabicInvitation, StringComparison.Ordinal) ? arabicInvitation
            : null;
        if (prefix is not null)
        {
            var candidate = line[prefix.Length..].Trim();
            if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp && IsLoopback(uri.Host))
                && string.IsNullOrEmpty(uri.UserInfo))
                return $"{WebUtility.HtmlEncode(prefix)}<a href=\"{WebUtility.HtmlEncode(uri.AbsoluteUri)}\" style=\"color:#2563eb\">{WebUtility.HtmlEncode(candidate)}</a>";
        }
        return WebUtility.HtmlEncode(line);
    }

    private static bool IsLoopback(string host) => host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
        || host.Equals("::1", StringComparison.OrdinalIgnoreCase);
}
