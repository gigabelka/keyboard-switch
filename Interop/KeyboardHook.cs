using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace KeyboardSwitch.Interop;

public sealed class KeyboardHookEventArgs : EventArgs
{
    public uint VkCode { get; }
    public uint ScanCode { get; }
    public bool IsDown { get; }
    public bool CtrlDown { get; }
    public bool AltDown { get; }
    public bool WinDown { get; }
    public bool ShiftDown { get; }

    public KeyboardHookEventArgs(uint vk, uint scan, bool isDown, bool ctrl, bool alt, bool win, bool shift)
    {
        VkCode = vk;
        ScanCode = scan;
        IsDown = isDown;
        CtrlDown = ctrl;
        AltDown = alt;
        WinDown = win;
        ShiftDown = shift;
    }
}

/// <summary>
/// Installs a global WH_KEYBOARD_LL hook and raises <see cref="KeyEvent"/> for each key transition.
/// The delegate is held in a field to prevent GC from collecting it while the hook is installed.
/// Hook callback does minimal work and marshals further processing to the UI Dispatcher.
/// </summary>
public sealed class KeyboardHook : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly NativeMethods.LowLevelKeyboardProc _proc;
    private IntPtr _hookHandle = IntPtr.Zero;
    private bool _disposed;

    public event EventHandler<KeyboardHookEventArgs>? KeyEvent;

    public KeyboardHook(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _proc = HookCallback;
    }

    public void Start()
    {
        if (_hookHandle != IntPtr.Zero) return;

        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule!;
        _hookHandle = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL,
            _proc,
            NativeMethods.GetModuleHandle(module.ModuleName),
            0);

        if (_hookHandle == IntPtr.Zero)
            throw new InvalidOperationException("Failed to install global keyboard hook.");
    }

    public void Stop()
    {
        if (_hookHandle == IntPtr.Zero) return;
        NativeMethods.UnhookWindowsHookEx(_hookHandle);
        _hookHandle = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var data = System.Runtime.InteropServices.Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            int msg = wParam.ToInt32();
            bool isDown = msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN;
            bool isUp = msg == NativeMethods.WM_KEYUP || msg == NativeMethods.WM_SYSKEYUP;

            if (isDown || isUp)
            {
                bool ctrl = (NativeMethods.GetKeyState(NativeMethods.VK_CONTROL) & 0x8000) != 0;
                bool alt = (NativeMethods.GetKeyState(NativeMethods.VK_MENU) & 0x8000) != 0;
                bool win = (NativeMethods.GetKeyState(NativeMethods.VK_LWIN) & 0x8000) != 0
                           || (NativeMethods.GetKeyState(NativeMethods.VK_RWIN) & 0x8000) != 0;
                bool shift = (NativeMethods.GetKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0;

                var evt = new KeyboardHookEventArgs(data.vkCode, data.scanCode, isDown, ctrl, alt, win, shift);
                // Process on UI thread, but don't block the hook
                _dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                {
                    try { KeyEvent?.Invoke(this, evt); }
                    catch { /* swallow — never let user code crash the hook */ }
                }));
            }
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        GC.SuppressFinalize(this);
    }

    ~KeyboardHook() => Stop();
}
