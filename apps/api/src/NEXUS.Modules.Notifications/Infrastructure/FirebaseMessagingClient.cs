using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NEXUS.Modules.Notifications.Application;

namespace NEXUS.Modules.Notifications.Infrastructure;

public sealed class FirebaseMessagingClient
{
    private readonly IOptions<FirebaseOptions> _options;
    private readonly ILogger<FirebaseMessagingClient> _logger;
    private readonly object _sync = new();
    private FirebaseMessaging? _messaging;
    private bool _initializationAttempted;

    public FirebaseMessagingClient(IOptions<FirebaseOptions> options, ILogger<FirebaseMessagingClient> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<string?> SendAsync(string token, Domain.Notification notification, CancellationToken cancellationToken = default)
    {
        var messaging = GetMessaging();
        if (messaging is null) return null;

        var message = new Message
        {
            Token = token,
            Notification = new FirebaseAdmin.Messaging.Notification { Title = notification.Title, Body = notification.Body ?? string.Empty },
            Data = new Dictionary<string, string>
            {
                ["notificationId"] = notification.Id.ToString(),
                ["type"] = notification.Type,
                ["channel"] = notification.Channel,
                ["referenceType"] = notification.ReferenceType ?? string.Empty,
                ["referenceId"] = notification.ReferenceId?.ToString() ?? string.Empty,
            },
        };

        return await messaging.SendAsync(message, cancellationToken);
    }

    private FirebaseMessaging? GetMessaging()
    {
        if (_messaging is not null || _initializationAttempted) return _messaging;

        lock (_sync)
        {
            if (_messaging is not null || _initializationAttempted) return _messaging;
            _initializationAttempted = true;
            var options = _options.Value;
            if (string.IsNullOrWhiteSpace(options.ProjectId))
            {
                _logger.LogInformation("Firebase push is disabled because Firebase:ProjectId is not configured.");
                return null;
            }

            try
            {
                FirebaseApp app;
                try { app = FirebaseApp.DefaultInstance; }
                catch (InvalidOperationException)
                {
                    app = FirebaseApp.Create(new AppOptions { ProjectId = options.ProjectId, Credential = CreateCredential(options) });
                }
                _messaging = FirebaseMessaging.GetMessaging(app);
                return _messaging;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Firebase push is unavailable; notifications will remain in NEXUS only.");
                return null;
            }
        }
    }

    private static GoogleCredential CreateCredential(FirebaseOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.CredentialsJson)) return GoogleCredential.FromJson(options.CredentialsJson);
        if (!string.IsNullOrWhiteSpace(options.CredentialsPath)) return GoogleCredential.FromFile(options.CredentialsPath);
        if (!options.AllowApplicationDefaultCredentials)
        {
            throw new InvalidOperationException("Firebase Admin credentials are not configured for NEXUS.");
        }
        return GoogleCredential.GetApplicationDefault();
    }
}
