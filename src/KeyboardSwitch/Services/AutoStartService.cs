using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace KeyboardSwitch.Services;

public interface IAutoStartService
{
    bool IsEnabled();
    void SetEnabled(bool enabled);
}

public sealed class RegistryAutoStartService : IAutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "KeyboardSwitch";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        if (key == null) return false;
        var val = key.GetValue(ValueName) as string;
        return !string.IsNullOrEmpty(val);
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (key == null) return;

        if (enabled)
        {
            var exe = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exe)) return;
            // --tray: starts minimized to tray without showing settings window
            key.SetValue(ValueName, $"\"{exe}\" --tray", RegistryValueKind.String);
        }
        else
        {
            if (key.GetValue(ValueName) != null)
                key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
