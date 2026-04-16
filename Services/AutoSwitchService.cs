using System;
using System.Runtime.InteropServices;
using KeyboardSwitch.Interop;

namespace KeyboardSwitch.Services;

public interface IAutoSwitchService
{
    /// <summary>Backspace word-length times, switch layout, re-type the swapped word.</summary>
    void FixWord(string typedWord, string correctedWord, DetectedLanguage correctLang);
}

public sealed class AutoSwitchService : IAutoSwitchService
{
    private readonly ILayoutService _layoutService;

    public AutoSwitchService(ILayoutService layoutService)
    {
        _layoutService = layoutService;
    }

    public void FixWord(string typedWord, string correctedWord, DetectedLanguage correctLang)
    {
        if (string.IsNullOrEmpty(typedWord) || string.IsNullOrEmpty(correctedWord)) return;

        // 1. Erase the wrongly-typed word.
        SendBackspaces(typedWord.Length);

        // 2. Switch the layout of the foreground window.
        _layoutService.ActivateLayout(correctLang);

        // Small sleep to let the target process receive the WM_INPUTLANGCHANGE before typing.
        System.Threading.Thread.Sleep(30);

        // 3. Type each character via Unicode input (no dependency on physical keys / scancodes).
        SendUnicodeString(correctedWord);
    }

    private static void SendBackspaces(int count)
    {
        var inputs = new NativeMethods.INPUT[count * 2];
        for (int i = 0; i < count; i++)
        {
            inputs[2 * i] = KeyDown(NativeMethods.VK_BACK);
            inputs[2 * i + 1] = KeyUp(NativeMethods.VK_BACK);
        }
        Send(inputs);
    }

    private static void SendUnicodeString(string text)
    {
        var inputs = new NativeMethods.INPUT[text.Length * 2];
        for (int i = 0; i < text.Length; i++)
        {
            ushort ch = text[i];
            inputs[2 * i] = UnicodeDown(ch);
            inputs[2 * i + 1] = UnicodeUp(ch);
        }
        Send(inputs);
    }

    private static NativeMethods.INPUT KeyDown(ushort vk) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        u = new NativeMethods.InputUnion
        {
            ki = new NativeMethods.KEYBDINPUT { wVk = vk, wScan = 0, dwFlags = 0, time = 0, dwExtraInfo = UIntPtr.Zero }
        }
    };

    private static NativeMethods.INPUT KeyUp(ushort vk) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        u = new NativeMethods.InputUnion
        {
            ki = new NativeMethods.KEYBDINPUT { wVk = vk, wScan = 0, dwFlags = NativeMethods.KEYEVENTF_KEYUP, time = 0, dwExtraInfo = UIntPtr.Zero }
        }
    };

    private static NativeMethods.INPUT UnicodeDown(ushort ch) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        u = new NativeMethods.InputUnion
        {
            ki = new NativeMethods.KEYBDINPUT { wVk = 0, wScan = ch, dwFlags = NativeMethods.KEYEVENTF_UNICODE, time = 0, dwExtraInfo = UIntPtr.Zero }
        }
    };

    private static NativeMethods.INPUT UnicodeUp(ushort ch) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        u = new NativeMethods.InputUnion
        {
            ki = new NativeMethods.KEYBDINPUT { wVk = 0, wScan = ch, dwFlags = NativeMethods.KEYEVENTF_UNICODE | NativeMethods.KEYEVENTF_KEYUP, time = 0, dwExtraInfo = UIntPtr.Zero }
        }
    };

    private static void Send(NativeMethods.INPUT[] inputs)
    {
        if (inputs.Length == 0) return;
        uint sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
        if (sent != inputs.Length)
            System.Diagnostics.Debug.WriteLine($"SendInput partial: {sent}/{inputs.Length}");
    }
}
