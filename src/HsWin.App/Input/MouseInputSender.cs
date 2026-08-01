using System.ComponentModel;
using System.Runtime.InteropServices;
using HsWin.Core.Logging;
using HsWin.Core.Mouse;

namespace HsWin.App.Input;

/// <summary>
/// Sends mouse button clicks with SendInput.
/// </summary>
internal static partial class MouseInputSender
{
    internal const nuint InjectedExtraInfo = 0x48535742;

    private const uint InputMouse = 0;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint MouseEventRightDown = 0x0008;
    private const uint MouseEventRightUp = 0x0010;
    private const uint MouseEventMiddleDown = 0x0020;
    private const uint MouseEventMiddleUp = 0x0040;
    private const uint MouseEventXDown = 0x0080;
    private const uint MouseEventXUp = 0x0100;
    private const uint XButton1 = 0x0001;
    private const uint XButton2 = 0x0002;

    internal static int NativeInputSize => Marshal.SizeOf<Input>();

    internal static void SendClick(MouseButton button, IRuntimeLogger? logger = null)
    {
        var (downFlags, upFlags, mouseData) = GetButtonInput(button);
        Span<Input> inputs = stackalloc Input[2];
        inputs[0] = CreateMouseInput(downFlags, mouseData);
        inputs[1] = CreateMouseInput(upFlags, mouseData);
        SendInputs(inputs, button, logger);
    }

    private static (uint DownFlags, uint UpFlags, uint MouseData) GetButtonInput(MouseButton button)
    {
        return button switch
        {
            MouseButton.Left => (MouseEventLeftDown, MouseEventLeftUp, 0),
            MouseButton.Right => (MouseEventRightDown, MouseEventRightUp, 0),
            MouseButton.Middle => (MouseEventMiddleDown, MouseEventMiddleUp, 0),
            MouseButton.XButton1 => (MouseEventXDown, MouseEventXUp, XButton1),
            MouseButton.XButton2 => (MouseEventXDown, MouseEventXUp, XButton2),
            _ => throw new ArgumentOutOfRangeException(nameof(button), button, "Unsupported mouse button.")
        };
    }

    private static Input CreateMouseInput(uint flags, uint mouseData)
    {
        return new Input
        {
            Type = InputMouse,
            Mouse = new MouseInput
            {
                MouseData = mouseData,
                Flags = flags,
                ExtraInfo = new UIntPtr(InjectedExtraInfo)
            }
        };
    }

    private static unsafe void SendInputs(
        ReadOnlySpan<Input> inputs,
        MouseButton button,
        IRuntimeLogger? logger)
    {
        fixed (Input* inputPointer = inputs)
        {
            var sentCount = User32.SendInput((uint)inputs.Length, inputPointer, NativeInputSize);
            if (sentCount != inputs.Length)
            {
                var error = Marshal.GetLastPInvokeError();
                logger?.Warning(
                    $"SendInput sent {sentCount}/{inputs.Length} mouse events button={MouseButtonParser.GetDisplayName(button)} win32=0x{error:X}.");
                throw new Win32Exception(error, $"Could not send {MouseButtonParser.GetDisplayName(button)} mouse input.");
            }
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    private struct Input
    {
        [FieldOffset(0)]
        public uint Type;

        [FieldOffset(8)]
        public MouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;

        public int Dy;

        public uint MouseData;

        public uint Flags;

        public uint Time;

        public UIntPtr ExtraInfo;
    }

    private static partial class User32
    {
        [LibraryImport("user32.dll", SetLastError = true)]
        public static unsafe partial uint SendInput(uint inputCount, Input* inputs, int inputSize);
    }
}
