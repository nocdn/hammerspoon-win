namespace HsWin.Core.Alerts;

public sealed class NullAlertPresenter : IAlertPresenter
{
    public static NullAlertPresenter Instance { get; } = new();

    private NullAlertPresenter()
    {
    }

    public void Show(AlertRequest request)
    {
    }
}
