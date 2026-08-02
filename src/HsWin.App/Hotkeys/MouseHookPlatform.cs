using System.Runtime.InteropServices;
using HsWin.App.Keyboard;

namespace HsWin.App.Hotkeys;

internal delegate IntPtr MouseHookProcedure(int code, IntPtr wParam, IntPtr lParam);

internal interface IMouseHookPlatform
{
    IntPtr SetWindowsHookEx(int idHook, MouseHookProcedure hookProcedure, IntPtr moduleHandle, uint threadId);

    bool UnhookWindowsHookEx(IntPtr hookHandle);

    IntPtr CallNextHookEx(IntPtr hookHandle, int code, IntPtr wParam, IntPtr lParam);

    short GetAsyncKeyState(int virtualKey);

    int GetMessage(out NativeMessage message, IntPtr windowHandle, uint messageFilterMin, uint messageFilterMax);

    bool TranslateMessage(ref NativeMessage message);

    IntPtr DispatchMessage(ref NativeMessage message);

    bool PostThreadMessage(uint threadId, int message, IntPtr wParam, IntPtr lParam);

    uint GetCurrentThreadId();

    IntPtr GetModuleHandle(string? moduleName);
}

internal struct NativeMessage
{
    public IntPtr Hwnd;

    public uint Message;

    public IntPtr WParam;

    public IntPtr LParam;

    public uint Time;

    public NativePoint Point;
}

internal struct NativePoint
{
    public int X;

    public int Y;
}

internal sealed partial class Win32MouseHookPlatform : IMouseHookPlatform
{
    public static Win32MouseHookPlatform Instance { get; } = new();

    private Win32MouseHookPlatform()
    {
    }

    public IntPtr SetWindowsHookEx(int idHook, MouseHookProcedure hookProcedure, IntPtr moduleHandle, uint threadId) =>
        User32.SetWindowsHookEx(idHook, hookProcedure, moduleHandle, threadId);

    public bool UnhookWindowsHookEx(IntPtr hookHandle) =>
        User32.UnhookWindowsHookEx(hookHandle);

    public IntPtr CallNextHookEx(IntPtr hookHandle, int code, IntPtr wParam, IntPtr lParam) =>
        User32.CallNextHookEx(hookHandle, code, wParam, lParam);

    public short GetAsyncKeyState(int virtualKey) =>
        NativeKeyStateReader.GetAsyncState(virtualKey);

    public int GetMessage(out NativeMessage message, IntPtr windowHandle, uint messageFilterMin, uint messageFilterMax) =>
        User32.GetMessage(out message, windowHandle, messageFilterMin, messageFilterMax);

    public bool TranslateMessage(ref NativeMessage message) =>
        User32.TranslateMessage(ref message);

    public IntPtr DispatchMessage(ref NativeMessage message) =>
        User32.DispatchMessage(ref message);

    public bool PostThreadMessage(uint threadId, int message, IntPtr wParam, IntPtr lParam) =>
        User32.PostThreadMessage(threadId, message, wParam, lParam);

    public uint GetCurrentThreadId() =>
        Kernel32.GetCurrentThreadId();

    public IntPtr GetModuleHandle(string? moduleName) =>
        Kernel32.GetModuleHandle(moduleName);

    private static partial class User32
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, MouseHookProcedure hookProcedure, IntPtr moduleHandle, uint threadId);

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool UnhookWindowsHookEx(IntPtr hookHandle);

        [LibraryImport("user32.dll")]
        public static partial IntPtr CallNextHookEx(IntPtr hookHandle, int code, IntPtr wParam, IntPtr lParam);

        [LibraryImport("user32.dll", EntryPoint = "GetMessageW", SetLastError = true)]
        public static partial int GetMessage(out NativeMessage message, IntPtr windowHandle, uint messageFilterMin, uint messageFilterMax);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool TranslateMessage(ref NativeMessage message);

        [LibraryImport("user32.dll", EntryPoint = "DispatchMessageW")]
        public static partial IntPtr DispatchMessage(ref NativeMessage message);

        [LibraryImport("user32.dll", EntryPoint = "PostThreadMessageW", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool PostThreadMessage(uint threadId, int message, IntPtr wParam, IntPtr lParam);
    }

    private static partial class Kernel32
    {
        [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        public static partial IntPtr GetModuleHandle(string? moduleName);

        [LibraryImport("kernel32.dll")]
        public static partial uint GetCurrentThreadId();
    }
}
