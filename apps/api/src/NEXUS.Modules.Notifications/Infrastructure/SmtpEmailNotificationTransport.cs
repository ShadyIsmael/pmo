using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using MailKit;
using MailKit.Security;
using MimeKit;
using NEXUS.Modules.Notifications.Application;
using MailKitSmtpClient = MailKit.Net.Smtp.SmtpClient;
using SmtpCommandException = MailKit.Net.Smtp.SmtpCommandException;
using SmtpProtocolException = MailKit.Net.Smtp.SmtpProtocolException;
using MailAddress = System.Net.Mail.MailAddress;

namespace NEXUS.Modules.Notifications.Infrastructure;

/// <summary>
/// Provider-neutral SMTP transport. DNS is resolved once and the socket is connected to one of
/// those validated addresses directly; TLS still validates the certificate against the configured
/// SMTP hostname. STARTTLS is mandatory when selected and TLS validation is never bypassed.
/// </summary>
public sealed class SmtpEmailNotificationTransport : ISmtpEmailNotificationTransport
{
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(30);
    private readonly ISmtpDestinationPolicy _destinationPolicy;

    public SmtpEmailNotificationTransport(ISmtpDestinationPolicy destinationPolicy) =>
        _destinationPolicy = destinationPolicy;

    public async Task<SmtpTransportResult> TestConnectionAsync(
        SmtpTransportProfile profile,
        Guid? tenantId,
        Guid? organizationId,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateProfile(profile);
        if (validation is not null)
            return validation;

        using var timeout = CreateTimeout(cancellationToken);
        try
        {
            await using var session = await ConnectAndAuthenticateAsync(profile, tenantId, organizationId, timeout.Token);
            return SmtpTransportResult.Success();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SmtpTransportResult.Failure("Timeout", "The SMTP server did not respond in time.", isTransient: true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return MapFailure(exception);
        }
    }

    public async Task<SmtpTransportResult> SendAsync(
        SmtpTransportProfile profile,
        Guid? tenantId,
        Guid? organizationId,
        string recipientAddress,
        SmtpEmailMessage message,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateProfile(profile);
        if (validation is not null)
            return validation;

        if (!TryCreateMailbox(recipientAddress, out var recipient))
            return SmtpTransportResult.Failure("InvalidRecipient", "The email recipient address is invalid.");

        if (message is null || string.IsNullOrWhiteSpace(message.Subject)
            || message.Subject.Length > SmtpEmailMessage.MaximumSubjectLength
            || message.Subject.Contains('\r') || message.Subject.Contains('\n')
            || message.PlainTextBody is null
            || message.PlainTextBody.Length > SmtpEmailMessage.MaximumBodyLength
            || (message.HtmlBody?.Length ?? 0) > SmtpEmailMessage.MaximumBodyLength)
            return SmtpTransportResult.Failure("InvalidMessage", "The email subject or body is invalid or too large.");

        var from = CreateMailbox(profile.FromAddress, profile.FromName);
        var email = new MimeMessage();
        email.From.Add(from);
        email.To.Add(recipient);
        email.Subject = message.Subject;
        if (TryCreateMailbox(profile.ReplyToAddress, out var replyTo))
            email.ReplyTo.Add(replyTo);

        var body = new BodyBuilder
        {
            TextBody = message.PlainTextBody,
            HtmlBody = string.IsNullOrWhiteSpace(message.HtmlBody)
                ? RenderPlainTextHtml(message.PlainTextBody)
                : message.HtmlBody,
        };
        email.Body = body.ToMessageBody();

        using var timeout = CreateTimeout(cancellationToken);
        try
        {
            await using var session = await ConnectAndAuthenticateAsync(profile, tenantId, organizationId, timeout.Token);
            await session.Client.SendAsync(email, timeout.Token);
            return SmtpTransportResult.Success();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SmtpTransportResult.Failure("Timeout", "The SMTP server did not respond in time.", isTransient: true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return MapFailure(exception);
        }
    }

    private async Task<SmtpSession> ConnectAndAuthenticateAsync(
        SmtpTransportProfile profile,
        Guid? tenantId,
        Guid? organizationId,
        CancellationToken cancellationToken)
    {
        var host = SmtpDestinationPolicy.NormalizeHost(profile.Host);
        if (host is null)
            throw new SmtpTransportException("InvalidConfiguration", "The SMTP host is invalid.");

        IPAddress[] addresses;
        try
        {
            addresses = IPAddress.TryParse(host, out var literal)
                ? [literal]
                : await Dns.GetHostAddressesAsync(host, cancellationToken);
        }
        catch (SocketException)
        {
            throw new SmtpTransportException("DnsResolutionFailed", "The SMTP host could not be resolved.", isTransient: true);
        }

        if (addresses.Length == 0)
            throw new SmtpTransportException("DnsResolutionFailed", "The SMTP host could not be resolved.", isTransient: true);

        // Reject the entire DNS answer set if any address is outside the scoped policy. This avoids
        // selecting an apparently safe address from a mixed answer that also contains a private target.
        foreach (var address in addresses)
        {
            if (!_destinationPolicy.IsDestinationAllowed(
                    tenantId,
                    organizationId,
                    host,
                    address,
                    profile.AuthenticationMode))
                throw new SmtpTransportException("DestinationNotAllowed", "The SMTP destination is not allowed for this scope.");
        }

        Exception? lastConnectionFailure = null;
        foreach (var address in addresses)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TcpClient? socket = null;
            MailKitSmtpClient? client = null;
            try
            {
                socket = new TcpClient(address.AddressFamily);
                await socket.ConnectAsync(address, profile.Port, cancellationToken);

                client = CreateSmtpClient();
                var securityMode = profile.SecurityMode switch
                {
                    SmtpSecurityMode.StartTlsRequired => SecureSocketOptions.StartTls,
                    SmtpSecurityMode.ImplicitTls => SecureSocketOptions.SslOnConnect,
                    _ => throw new SmtpTransportException("InvalidConfiguration", "The SMTP TLS mode is invalid."),
                };

                await client.ConnectAsync(socket.GetStream(), host, profile.Port, securityMode, cancellationToken);
                if (!client.IsSecure)
                    throw new SmtpTransportException("TlsRequired", "The SMTP connection did not establish TLS.");

                if (profile.AuthenticationMode == SmtpAuthenticationMode.UsernamePassword)
                    await client.AuthenticateAsync(
                        System.Text.Encoding.UTF8,
                        new NetworkCredential(profile.UserName!, profile.Password!),
                        cancellationToken);
                else if (profile.AuthenticationMode != SmtpAuthenticationMode.TrustedRelay)
                    throw new SmtpTransportException("AuthenticationModeUnavailable", "The selected SMTP authentication mode is unavailable.");

                return new SmtpSession(client, socket);
            }
            catch (OperationCanceledException)
            {
                client?.Dispose();
                socket?.Dispose();
                throw;
            }
            catch (Exception exception) when (socket is null || !socket.Connected || exception is SocketException or IOException)
            {
                client?.Dispose();
                socket?.Dispose();
                lastConnectionFailure = exception;
            }
            catch
            {
                client?.Dispose();
                socket?.Dispose();
                throw;
            }
        }

        if (lastConnectionFailure is not null)
            throw new SmtpTransportException(
                "ConnectionFailed",
                "The SMTP server could not be reached.",
                isTransient: true,
                innerException: lastConnectionFailure);
        throw new SmtpTransportException("ConnectionFailed", "The SMTP server could not be reached.", isTransient: true);
    }

    private static MailKitSmtpClient CreateSmtpClient() => new()
    {
        CheckCertificateRevocation = true,
        SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
        ServerCertificateValidationCallback = (_, _, _, errors) => errors == SslPolicyErrors.None,
    };

    private static SmtpTransportResult? ValidateProfile(SmtpTransportProfile profile)
    {
        if (profile is null)
            return SmtpTransportResult.Failure("InvalidConfiguration", "The SMTP profile is missing.");

        if (SmtpDestinationPolicy.NormalizeHost(profile.Host) is null
            || profile.Port is < 1 or > 65535
            || profile.SecurityMode is not (SmtpSecurityMode.StartTlsRequired or SmtpSecurityMode.ImplicitTls)
            || !TryCreateMailbox(profile.FromAddress, out _)
            || profile.FromName is null
            || profile.FromName.Length > 200
            || profile.FromName.Contains('\r') || profile.FromName.Contains('\n')
            || (!string.IsNullOrWhiteSpace(profile.ReplyToAddress) && !TryCreateMailbox(profile.ReplyToAddress, out _)))
            return SmtpTransportResult.Failure("InvalidConfiguration", "The SMTP connection or sender settings are invalid.");

        if (profile.AuthenticationMode == SmtpAuthenticationMode.UsernamePassword
            && (string.IsNullOrWhiteSpace(profile.UserName) || string.IsNullOrEmpty(profile.Password)
                || profile.UserName.Length > 512 || profile.UserName.Contains('\r') || profile.UserName.Contains('\n')))
            return SmtpTransportResult.Failure("InvalidCredentials", "SMTP username and password are required.");

        if (profile.AuthenticationMode == SmtpAuthenticationMode.TrustedRelay
            && (!string.IsNullOrWhiteSpace(profile.UserName) || !string.IsNullOrEmpty(profile.Password)))
            return SmtpTransportResult.Failure("InvalidConfiguration", "Trusted Relay cannot include SMTP credentials.");

        if (profile.AuthenticationMode == SmtpAuthenticationMode.OAuth2)
            return SmtpTransportResult.Failure("OAuth2Unavailable", "OAuth2/XOAUTH2 SMTP authentication is not currently supported.");

        if (profile.AuthenticationMode is not SmtpAuthenticationMode.UsernamePassword
            and not SmtpAuthenticationMode.TrustedRelay)
            return SmtpTransportResult.Failure("InvalidConfiguration", "The SMTP authentication mode is invalid.");

        return null;
    }

    private static bool TryCreateMailbox(string? value, out MailboxAddress mailbox)
    {
        mailbox = null!;
        if (string.IsNullOrWhiteSpace(value) || value.Length > 320 || value.Contains('\r') || value.Contains('\n'))
            return false;

        try
        {
            var parsed = new MailAddress(value);
            mailbox = new MailboxAddress(parsed.DisplayName, parsed.Address);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static MailboxAddress CreateMailbox(string address, string displayName)
    {
        var parsed = new MailAddress(address);
        return new MailboxAddress(displayName, parsed.Address);
    }

    private static string RenderPlainTextHtml(string plainText)
    {
        var encoded = WebUtility.HtmlEncode(plainText);
        var isRtl = plainText.Any(character => character is >= '\u0600' and <= '\u06ff');
        var language = isRtl ? "ar" : "en";
        var direction = isRtl ? "rtl" : "ltr";
        var body = encoded.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n')
            .Replace("\n", "<br />", StringComparison.Ordinal);
        return $"<!doctype html><html lang=\"{language}\" dir=\"{direction}\"><head><meta charset=\"utf-8\"></head><body style=\"font-family:Arial,Helvetica,sans-serif;line-height:1.65\">{body}</body></html>";
    }

    private static CancellationTokenSource CreateTimeout(CancellationToken cancellationToken)
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(OperationTimeout);
        return timeout;
    }

    private static SmtpTransportResult MapFailure(Exception exception) => exception switch
    {
        SmtpTransportException transportException => SmtpTransportResult.Failure(
            transportException.Code,
            transportException.SafeSummary,
            transportException.IsTransient),
        MailKit.Security.AuthenticationException => SmtpTransportResult.Failure(
            "AuthenticationFailed", "The SMTP server rejected the configured credentials."),
        System.Security.Authentication.AuthenticationException => SmtpTransportResult.Failure(
            "TlsValidationFailed", "The SMTP server's TLS certificate could not be validated."),
        SmtpCommandException commandException => MapCommandFailure(commandException),
        SmtpProtocolException => SmtpTransportResult.Failure(
            "ProtocolFailure", "The SMTP server did not complete the required protocol exchange."),
        SocketException or IOException or TimeoutException => SmtpTransportResult.Failure(
            "ProviderUnavailable", "The SMTP server is temporarily unavailable.", isTransient: true),
        NotSupportedException => SmtpTransportResult.Failure(
            "TlsRequired", "The SMTP server did not offer the selected secure connection mode."),
        _ => SmtpTransportResult.Failure(
            "ProviderError", "The SMTP operation could not be completed.", isTransient: true),
    };

    private static SmtpTransportResult MapCommandFailure(SmtpCommandException exception)
    {
        var statusCode = (int)exception.StatusCode;
        var transient = statusCode is >= 400 and < 500;
        return SmtpTransportResult.Failure(
            transient ? "ProviderUnavailable" : "ProviderRejected",
            transient ? "The SMTP server could not accept the operation yet." : "The SMTP server rejected the operation.",
            isTransient: transient);
    }

    private sealed class SmtpSession(MailKitSmtpClient client, TcpClient socket) : IAsyncDisposable
    {
        public MailKitSmtpClient Client { get; } = client;

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (Client.IsConnected)
                    await Client.DisconnectAsync(true, CancellationToken.None);
            }
            catch (Exception)
            {
                // Closing a completed or failed session is best-effort; never return server text.
            }
            finally
            {
                Client.Dispose();
                socket.Dispose();
            }
        }
    }

    private sealed class SmtpTransportException(
        string code,
        string safeSummary,
        bool isTransient = false,
        Exception? innerException = null) : Exception(safeSummary, innerException)
    {
        public string Code { get; } = code;
        public string SafeSummary { get; } = safeSummary;
        public bool IsTransient { get; } = isTransient;
    }
}
