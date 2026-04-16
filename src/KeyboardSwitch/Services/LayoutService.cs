using System;
using System.Text;
using KeyboardSwitch.Interop;

namespace KeyboardSwitch.Services;

public enum DetectedLanguage
{
    Unknown,
    English,
    Russian
}

public interface ILayoutService
{
    IntPtr GetActiveLayout();
    DetectedLanguage GetActiveLanguage();
    string GetActiveProcessName();
    IntPtr GetLayoutForLanguage(DetectedLanguage lang);
    void ActivateLayout(DetectedLanguage lang);
}

public sealed class LayoutService : ILayoutService
{
    // LANGID low-word values.
    private const ushort LANG_EN_US = 0x0409;
    private const ushort LANG_RU_RU = 0x0419;

    public IntPtr GetActiveLayout()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return IntPtr.Zero;
        uint threadId = NativeMethods.GetWindowThreadProcessId(hwnd, out _);
        return NativeMethods.GetKeyboardLayout(threadId);
    }

    public DetectedLanguage GetActiveLanguage()
    {
        var hkl = GetActiveLayout();
        if (hkl == IntPtr.Zero) return DetectedLanguage.Unknown;
        ushort langId = (ushort)(hkl.ToInt64() & 0xFFFF);
        ushort primary = (ushort)(langId & 0x3FF);
        return primary switch
        {
            0x09 => DetectedLanguage.English,
            0x19 => DetectedLanguage.Russian,
            _ => DetectedLanguage.Unknown
        };
    }

    public string GetActiveProcessName()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return string.Empty;
        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0) return string.Empty;

        var handle = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (handle == IntPtr.Zero) return string.Empty;
        try
        {
            var sb = new StringBuilder(1024);
            uint size = (uint)sb.Capacity;
            if (!NativeMethods.QueryFullProcessImageName(handle, 0, sb, ref size))
                return string.Empty;
            var full = sb.ToString();
            int slash = full.LastIndexOfAny(new[] { '\\', '/' });
            return slash >= 0 ? full[(slash + 1)..] : full;
        }
        finally
        {
            NativeMethods.CloseHandle(handle);
        }
    }

    public IntPtr GetLayoutForLanguage(DetectedLanguage lang)
    {
        int count = NativeMethods.GetKeyboardLayoutList(0, null);
        if (count <= 0) return IntPtr.Zero;
        var arr = new IntPtr[count];
        NativeMethods.GetKeyboardLayoutList(count, arr);

        ushort target = lang switch
        {
            DetectedLanguage.English => LANG_EN_US,
            DetectedLanguage.Russian => LANG_RU_RU,
            _ => 0
        };
        if (target == 0) return IntPtr.Zero;

        foreach (var hkl in arr)
        {
            ushort langId = (ushort)(hkl.ToInt64() & 0xFFFF);
            ushort primary = (ushort)(langId & 0x3FF);
            ushort targetPrimary = (ushort)(target & 0x3FF);
            if (primary == targetPrimary) return hkl;
        }
        return IntPtr.Zero;
    }

    public void ActivateLayout(DetectedLanguage lang)
    {
        var hkl = GetLayoutForLanguage(lang);
        if (hkl == IntPtr.Zero) return;

        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return;

        NativeMethods.PostMessage(hwnd, (uint)NativeMethods.WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, hkl);
    }
}
