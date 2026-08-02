using System.Reflection;
using System.Runtime.InteropServices;
using HsWin.App.Keyboard;
using HsWin.Core.Hotkeys;
using HsWin.Core.Keyboard;

namespace HsWin.App.Tests;

public sealed class NativeKeyboardEventServiceTests
{
    [Theory]
    [InlineData(KeyboardKeyRules.VkControl)]
    [InlineData(KeyboardKeyRules.VkLeftControl)]
    [InlineData(KeyboardKeyRules.VkRightControl)]
    public void TrackedControlStateCoversGenericAndSideSpecificKeys(uint virtualKey)
    {
        Assert.True(
            NativeKeyboardEventService.IsTrackedModifierDown(
                virtualKey,
                HotkeyModifiers.Control));
    }

    [Fact]
    public void TrackedModifierStateDoesNotReportUnrelatedModifier()
    {
        Assert.False(
            NativeKeyboardEventService.IsTrackedModifierDown(
                KeyboardKeyRules.VkLeftShift,
                HotkeyModifiers.Control));
    }

    [Fact]
    public void TrackedModifierStateRejectsNonModifierKeys()
    {
        Assert.False(
            NativeKeyboardEventService.IsTrackedModifierDown(
                (uint)'A',
                HotkeyModifiers.Control | HotkeyModifiers.Shift));
    }

    [Fact]
    public void GetAsyncKeyStatePInvokeIsCentralized()
    {
        var declarations = typeof(NativeKeyboardEventService).Assembly
            .GetTypes()
            .SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            .Select(method => new
            {
                Method = method,
                Import = method.GetCustomAttribute<DllImportAttribute>()
            })
            .Where(item => item.Import is not null
                && string.Equals(
                    item.Import.EntryPoint ?? item.Method.Name,
                    "GetAsyncKeyState",
                    StringComparison.Ordinal))
            .ToArray();

        var declaration = Assert.Single(declarations);
        Assert.Contains(
            nameof(NativeKeyStateReader),
            declaration.Method.DeclaringType?.FullName,
            StringComparison.Ordinal);
    }
}
