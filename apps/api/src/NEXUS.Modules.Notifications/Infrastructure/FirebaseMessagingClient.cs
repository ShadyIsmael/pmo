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

    public bool IsConfigured
    {
        get
        {
            var options = _options.Value;
            return !string.IsNullOrWhiteSpace(options.ProjectId)
                && (!string.IsNullOrWhiteSpace(options.CredentialsJson)
                    || !string.IsNullOrWhiteSpace(options.CredentialsPath)
                    || options.AllowApplicationDefaultCredentials);
        }
    }

    public async Task<string> SendAsync(string token, string title, string body, IReadOnlyDictionary<string, string> data, CancellationToken cancellationToken = default)
    {
        var messaging = GetMessaging();
        if (messaging is null) throw new FirebaseMessagingUnavailableException();

        var message = new Message
        {
#pragma warning disable CS0618 // Existing browser clients register FCM tokens; FID migration requires a coordinated client and storage change.
            Token = token,
#pragma warning restore CS0618
            Notification = new FirebaseAdmin.Messaging.Notification { Title = title, Body = body },
            Data = new Dictionary<string, string>(data),
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
            if (!IsConfigured)
            {
                _logger.LogInformation("Firebase push is disabled because Firebase credentials or project configuration are unavailable.");
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
            catch (Exception)
            {
                // Credential parser errors may contain secret material; log only a safe category.
                _logger.LogWarning("Firebase Admin initialization failed. Push deliveries will be recorded as unavailable.");
                return null;
            }
        }
    }

    private static GoogleCredential CreateCredential(FirebaseOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.CredentialsJson))
            return CredentialFactory.FromJson<ServiceAccountCredential>(options.CredentialsJson).ToGoogleCredential();
        if (!string.IsNullOrWhiteSpace(options.CredentialsPath))
            return CredentialFactory.FromFile<ServiceAccountCredential>(options.CredentialsPath).ToGoogleCredential();
        if (!options.AllowApplicationDefaultCredentials) throw new FirebaseMessagingUnavailableException();
        return GoogleCredential.GetApplicationDefault();
    }
}

public sealed class FirebaseMessagingUnavailableException : Exception
{
    public FirebaseMessagingUnavailableException() : base("Firebase messaging is unavailable.") { }
}
