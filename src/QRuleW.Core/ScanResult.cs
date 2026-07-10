namespace QRuleW.Core;

/// <summary>
/// The decoded payload of a QR scan plus the URL (if any) that the "Open" button should launch.
/// Mirrors the macOS <c>ScanResult</c> / <c>openableURL</c> logic.
/// </summary>
public sealed class ScanResult
{
    /// <summary>Schemes we are willing to hand to the shell's default handler.</summary>
    private static readonly HashSet<string> OpenableSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "http", "https", "mailto", "tel", "sms",
    };

    public ScanResult(string payload)
    {
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        OpenableUrl = DeriveOpenableUrl(payload);
    }

    /// <summary>The raw decoded text, exactly as read from the QR code.</summary>
    public string Payload { get; }

    /// <summary>The absolute URI to open, or <c>null</c> when the payload is not a launchable link.</summary>
    public Uri? OpenableUrl { get; }

    public bool CanOpen => OpenableUrl is not null;

    /// <summary>
    /// Returns a launchable absolute URI for <paramref name="payload"/>, or null.
    /// Accepts allow-listed schemes directly; promotes bare "www.host" (no spaces) to https.
    /// </summary>
    public static Uri? DeriveOpenableUrl(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return null;

        var trimmed = payload.Trim();

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && uri.Scheme is { Length: > 0 }
            && OpenableSchemes.Contains(uri.Scheme))
        {
            return uri;
        }

        if (trimmed.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            && !trimmed.Any(char.IsWhiteSpace)
            && Uri.TryCreate("https://" + trimmed, UriKind.Absolute, out var promoted))
        {
            return promoted;
        }

        return null;
    }
}
