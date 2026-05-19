using System.Globalization;

namespace HammerspoonWin.Core.Alerts;

public sealed record AlertRequest(string Text, AlertKind Kind, int DurationMs)
{
    public const AlertKind DefaultKind = AlertKind.Success;
    public const int DefaultDurationMs = 2000;

    public static AlertRequest Create(string? text, AlertKind? kind = null, int? durationMs = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Alert text cannot be empty.", nameof(text));
        }

        var normalizedDurationMs = durationMs ?? DefaultDurationMs;
        if (normalizedDurationMs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationMs), "Alert duration cannot be negative.");
        }

        return new AlertRequest(text, kind ?? DefaultKind, normalizedDurationMs);
    }

    public static AlertKind ParseKind(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DefaultKind;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "normal" or "none" or "plain" or "info" => AlertKind.Normal,
            "success" or "ok" or "done" => AlertKind.Success,
            "error" or "failure" or "failed" or "fail" => AlertKind.Error,
            _ => throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"Unknown alert type '{value}'. Use normal, success, or error."),
                nameof(value))
        };
    }
}
