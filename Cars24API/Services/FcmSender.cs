using Cars24API.Models;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;

namespace Cars24API.Services;

/// <summary>Thrown when an FCM token is invalid or unregistered.</summary>
public class InvalidRegistrationException : Exception
{
    public InvalidRegistrationException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Thin adapter over the Firebase Admin SDK. Credentials are loaded from
/// configuration only; no service-account material is ever embedded in code.
/// </summary>
public class FcmSender : IFcmSender
{
    private readonly ILogger<FcmSender> _logger;
    private readonly string? _projectId;
    private readonly string? _credentialsPath;
    private readonly object _lock = new();
    private FirebaseApp? _app;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_projectId);

    public FcmSender(IConfiguration config, ILogger<FcmSender> logger)
    {
        _logger = logger;
        _projectId = config["Firebase:ProjectId"];
        _credentialsPath = config["Firebase:CredentialsPath"];
    }

    private FirebaseApp GetApp()
    {
        if (_app is not null) return _app;
        lock (_lock)
        {
            if (_app is not null) return _app;

            var options = new AppOptions { ProjectId = _projectId };
            if (!string.IsNullOrWhiteSpace(_credentialsPath))
            {
                options.Credential = GoogleCredential.FromFile(_credentialsPath);
            }
            // Otherwise Firebase falls back to Application Default Credentials
            // (e.g. the GOOGLE_APPLICATION_CREDENTIALS environment variable).

            _app = FirebaseApp.Create(options);
            return _app;
        }
    }

    public async Task<bool> SendAsync(string registrationToken, NotificationPayload payload)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("FCM is not configured; skipping {Type} notification.", payload.Type);
            return false;
        }

        try
        {
            var message = new Message
            {
                Token = registrationToken,
                Webpush = new WebpushConfig
                {
                    Data = new Dictionary<string, string>
                    {
                        ["type"] = payload.Type,
                        ["title"] = payload.Title,
                        ["body"] = payload.Body,
                        ["vehicleId"] = payload.VehicleId ?? string.Empty,
                        ["actionUrl"] = payload.ActionUrl ?? string.Empty
                    },
                    FcmOptions = new WebpushFcmOptions
                    {
                        Link = string.IsNullOrWhiteSpace(payload.ActionUrl) ? null : payload.ActionUrl
                    }
                }
            };

            var messageId = await FirebaseMessaging.GetMessaging(GetApp()).SendAsync(message);
            return !string.IsNullOrWhiteSpace(messageId);
        }
        catch (FirebaseMessagingException ex) when (
            ex.MessagingErrorCode == MessagingErrorCode.Unregistered ||
            ex.MessagingErrorCode == MessagingErrorCode.InvalidArgument)
        {
            _logger.LogWarning("FCM token invalid/unregistered ({Code}).", ex.MessagingErrorCode);
            throw new InvalidRegistrationException(ex.Message, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FCM send failed for {Type} notification.", payload.Type);
            return false;
        }
    }
}
