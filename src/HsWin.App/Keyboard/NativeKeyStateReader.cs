using System.Runtime.InteropServices;

namespace HsWin.App.Keyboard;

/// <summary>
/// Central Win32 boundary for asynchronous key state. Consumers that need physical modifier
/// truth while synthetic input is active must use <see cref="IKeyboardEventService"/> instead.
/// </summary>
internal static partial class NativeKeyStateReader
{
    private const short KeyPressedMask = unchecked((short)0x8000);

    public static bool IsDown(uint virtualKey) =>
        (GetAsyncState(unchecked((int)virtualKey)) & KeyPressedMask) != 0;

    public static short GetAsyncState(int virtualKey) => User32.GetAsyncKeyState(virtualKey);

    private static partial class User32
    {
        [LibraryImport("user32.dll")]
        public static partial short GetAsyncKeyState(int virtualKey);
    }
}
