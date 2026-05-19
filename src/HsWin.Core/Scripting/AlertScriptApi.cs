using HsWin.Core.Alerts;

namespace HsWin.Core.Scripting;

public sealed class AlertScriptApi
{
    private readonly IAlertPresenter _alerts;

    public AlertScriptApi(IAlertPresenter alerts)
    {
        _alerts = alerts;
    }

    public void Show(object? text, object? optionsOrKind = null, object? durationMs = null)
    {
        var request = AlertRequestParser.FromScriptArguments(text, optionsOrKind, durationMs);
        _alerts.Show(request);
    }
}
