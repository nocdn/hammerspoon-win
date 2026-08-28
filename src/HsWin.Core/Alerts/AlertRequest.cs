using System.Globalization;

namespace HsWin.Core.Alerts;

public sealed record AlertRequest(string Text, AlertKind Kind, int DurationMs)
{
    public const AlertKind DefaultKind = AlertKind.Success;
    public const AlertIcon DefaultIcon = AlertIcon.Auto;
    public const AlertStyle DefaultStyle = AlertStyle.Standard;
    public const int DefaultDurationMs = 2000;

    public AlertIcon Icon { get; init; } = DefaultIcon;

    public AlertStyle Style { get; init; } = DefaultStyle;

    public AlertIcon EffectiveIcon =>
        Style is AlertStyle.Following
            ? AlertIcon.None
            : Icon is AlertIcon.Auto
            ? Kind is AlertKind.Normal ? AlertIcon.None : AlertIcon.Dot
            : Icon;

    public static AlertRequest Create(
        string? text,
        AlertKind? kind = null,
        int? durationMs = null,
        AlertIcon? icon = null,
        AlertStyle? style = null)
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

        return new AlertRequest(text, kind ?? DefaultKind, normalizedDurationMs)
        {
            Icon = icon ?? DefaultIcon,
            Style = style ?? DefaultStyle
        };
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

    public static AlertIcon ParseIcon(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DefaultIcon;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "auto" or "default" => AlertIcon.Auto,
            "none" or "no" or "off" or "false" => AlertIcon.None,
            "dot" or "status" => AlertIcon.Dot,
            "loader" or "loading" or "spinner" or "progress" or "busy" => AlertIcon.Loader,
            _ => throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"Unknown alert icon '{value}'. Use auto, none, dot, or loader."),
                nameof(value))
        };
    }

    public static AlertStyle ParseStyle(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DefaultStyle;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "standard" or "default" or "normal" => AlertStyle.Standard,
            "following" or "follow" or "cursor" or "cursor-following" => AlertStyle.Following,
            _ => throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"Unknown alert style '{value}'. Use standard or following."),
                nameof(value))
        };
    }
}
