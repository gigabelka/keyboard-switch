using System.Collections.Generic;

namespace KeyboardSwitch.Models;

public enum Sensitivity
{
    Low,
    Medium,
    High
}

public sealed class AppSettings
{
    public bool Enabled { get; set; } = true;
    public bool PlaySound { get; set; } = true;
    public bool AutoSwitch { get; set; } = false;
    public bool AutoStart { get; set; } = false;
    public string? CustomSoundPath { get; set; }
    public int MinWordLength { get; set; } = 4;
    public Sensitivity Sensitivity { get; set; } = Sensitivity.Medium;
    public List<string> IgnoredProcesses { get; set; } = new()
    {
        "keepass.exe",
        "keepassxc.exe",
        "1password.exe",
        "bitwarden.exe",
        "lastpass.exe"
    };

    public static AppSettings CreateDefault() => new();
}
