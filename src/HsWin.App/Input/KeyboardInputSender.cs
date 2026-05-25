using System.ComponentModel;
using System.Runtime.InteropServices;
using HsWin.Core.Keyboard;
using HsWin.Core.Logging;

namespace HsWin.App.Input;

/// <summary>
/// Sends keyboard events with SendInput (preferred over legacy keybd_event per Microsoft Learn).
/// </summary>
internal static partial class KeyboardInputSender
{
    internal const nuint InjectedExtraInfo = 0x48535742;

    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventExtendedKey = 0x0001;

    internal static int NativeInputSize => Marshal.SizeOf<Input>();

    internal static void SendKeyDown(uint virtualKey, IRuntimeLogger? logger = null)
    {
        Span<Input> inputs = stackalloc Input[1];
        inputs[0] = CreateKeyboardInput(virtualKey, keyUp: false);
        SendInputs(inputs, virtualKey, logger);
    }

    internal static void SendKeyUp(uint virtualKey, IRuntimeLogger? logger = null)
    {
        Span<Input> inputs = stackalloc Input[1];
        inputs[0] = CreateKeyboardInput(virtualKey, keyUp: true);
        SendInputs(inputs, virtualKey, logger);
    }

    internal static void SendTap(
        uint virtualKey,
        IReadOnlyList<uint>? suppressedModifierVirtualKeys = null,
        IReadOnlyList<uint>? modifierVirtualKeys = null,
        IRuntimeLogger? logger = null)
    {
        Span<Input> inputs = stackalloc Input[24];
        var inputCount = 0;
        if (suppressedModifierVirtualKeys is not null)
        {
            foreach (var modifierVirtualKey in suppressedModifierVirtualKeys)
            {
                inputs[inputCount++] = CreateKeyboardInput(modifierVirtualKey, keyUp: true);
            }
        }

        if (modifierVirtualKeys is not null)
        {
            foreach (var modifierVirtualKey in modifierVirtualKeys)
            {
                inputs[inputCount++] = CreateKeyboardInput(modifierVirtualKey, keyUp: false);
            }
        }

        inputs[inputCount++] = CreateKeyboardInput(virtualKey, keyUp: false);
        inputs[inputCount++] = CreateKeyboardInput(virtualKey, keyUp: true);

        if (modifierVirtualKeys is not null)
        {
            foreach (var modifierVirtualKey in modifierVirtualKeys.Reverse())
            {
                inputs[inputCount++] = CreateKeyboardInput(modifierVirtualKey, keyUp: true);
            }
        }

        if (suppressedModifierVirtualKeys is not null)
        {
            foreach (var modifierVirtualKey in suppressedModifierVirtualKeys.Reverse())
            {
                inputs[inputCount++] = CreateKeyboardInput(modifierVirtualKey, keyUp: false);
            }
        }

        try
        {
            SendInputs(inputs[..inputCount], virtualKey, logger);
        }
        catch
        {
            RestoreAfterFailedTap(virtualKey, suppressedModifierVirtualKeys, modifierVirtualKeys, logger);
            throw;
        }
    }

    private static unsafe void RestoreAfterFailedTap(
        uint virtualKey,
        IReadOnlyList<uint>? suppressedModifierVirtualKeys,
        IReadOnlyList<uint>? modifierVirtualKeys,
        IRuntimeLogger? logger)
    {
        Span<Input> inputs = stackalloc Input[9];
        var inputCount = 0;
        inputs[inputCount++] = CreateKeyboardInput(virtualKey, keyUp: true);

        if (modifierVirtualKeys is not null)
        {
            foreach (var modifierVirtualKey in modifierVirtualKeys.Reverse())
            {
                inputs[inputCount++] = CreateKeyboardInput(modifierVirtualKey, keyUp: true);
            }
        }

        if (suppressedModifierVirtualKeys is not null)
        {
            foreach (var modifierVirtualKey in suppressedModifierVirtualKeys.Reverse())
            {
                inputs[inputCount++] = CreateKeyboardInput(modifierVirtualKey, keyUp: false);
            }
        }

        fixed (Input* inputPointer = inputs[..inputCount])
        {
            var sentCount = User32.SendInput((uint)inputCount, inputPointer, NativeInputSize);
            if (sentCount != inputCount)
            {
                var error = Marshal.GetLastPInvokeError();
                logger?.Warning($"SendInput recovery sent {sentCount}/{inputCount} events for vk=0x{virtualKey:X2} win32=0x{error:X}.");
                return;
            }
        }

        logger?.Warning($"Recovered keyboard state after failed tap for vk=0x{virtualKey:X2}.");
    }

    private static unsafe void SendInputs(ReadOnlySpan<Input> inputs, uint virtualKey, IRuntimeLogger? logger)
    {
        fixed (Input* inputPointer = inputs)
        {
            var sentCount = User32.SendInput((uint)inputs.Length, inputPointer, NativeInputSize);
            if (sentCount != inputs.Length)
            {
                var error = Marshal.GetLastPInvokeError();
                logger?.Warning($"SendInput sent {sentCount}/{inputs.Length} events for vk=0x{virtualKey:X2} win32=0x{error:X}.");
                throw new Win32Exception(error, $"Could not send keyboard input for virtual key 0x{virtualKey:X2}.");
            }
        }
    }

    private static Input CreateKeyboardInput(uint virtualKey, bool keyUp)
    {
        var flags = keyUp ? KeyEventKeyUp : 0u;
        if (KeyboardKeyRules.IsExtendedVirtualKey(virtualKey))
        {
            flags |= KeyEventExtendedKey;
        }

        return new Input
        {
            Type = InputKeyboard,
            Keyboard = new KeyboardInput
            {
                VirtualKey = (ushort)virtualKey,
                Flags = flags,
                ExtraInfo = new UIntPtr(InjectedExtraInfo)
            }
        };
    }

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    private struct Input
    {
        [FieldOffset(0)]
        public uint Type;

        [FieldOffset(8)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;

        public ushort ScanCode;

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
