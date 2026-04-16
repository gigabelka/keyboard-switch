using System;
using System.IO;
using System.Text.Json;
using KeyboardSwitch.Models;

namespace KeyboardSwitch.Services;

public interface ISettingsService
{
    AppSettings Current { get; }
    void Save();
    event EventHandler? Changed;
    void NotifyChanged();
}

public sealed class JsonSettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _path;

    public AppSettings Current { get; private set; }

    public event EventHandler? Changed;

    public JsonSettingsService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KeyboardSwitch");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "settings.json");

        Current = Load();
    }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOpts);
                if (loaded != null) return loaded;
            }
        }
        catch
        {
            // Corrupt settings → fall back to defaults.
        }
        return AppSettings.CreateDefault();
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(Current, JsonOpts);
        File.WriteAllText(_path, json);
    }

    public void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
