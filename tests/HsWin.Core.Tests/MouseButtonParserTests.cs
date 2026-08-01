using HsWin.Core.Mouse;

namespace HsWin.Core.Tests;

public sealed class MouseButtonParserTests
{
    [Theory]
    [InlineData("left", MouseButton.Left)]
    [InlineData("mouse.right", MouseButton.Right)]
    [InlineData("button3", MouseButton.Middle)]
    [InlineData("back", MouseButton.XButton1)]
    [InlineData("xbutton2", MouseButton.XButton2)]
    public void ParseAcceptsMouseButtonAliases(string value, MouseButton expected)
    {
        Assert.Equal(expected, MouseButtonParser.Parse(value));
    }

    [Fact]
    public void ParseRejectsUnsupportedMouseButton()
    {
        var exception = Assert.Throws<ArgumentException>(() => MouseButtonParser.Parse("button6"));

        Assert.Contains("Unsupported mouse button", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(MouseButton.Left, "left")]
    [InlineData(MouseButton.Right, "right")]
    [InlineData(MouseButton.Middle, "middle")]
    [InlineData(MouseButton.XButton1, "back")]
    [InlineData(MouseButton.XButton2, "forward")]
    public void GetDisplayNameUsesScriptFriendlyNames(MouseButton button, string expected)
    {
        Assert.Equal(expected, MouseButtonParser.GetDisplayName(button));
    }
}
