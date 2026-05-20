namespace HsWin.Core.Mouse;

public sealed class NullMouseService : IMouseService
{
    public static NullMouseService Instance { get; } = new();

    private NullMouseService()
    {
    }

    public MouseScreenSnapshot? GetCurrentScreen()
    {
        return null;
    }
}
