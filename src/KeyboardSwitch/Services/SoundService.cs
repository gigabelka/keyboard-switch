using System;
using System.Diagnostics;
using System.IO;
using System.Media;

namespace KeyboardSwitch.Services;

public interface ISoundService
{
    void PlayAlert();
    void Reload();
}

public sealed class SoundService : ISoundService
{
    private readonly ISettingsService _settings;
    private SoundPlayer? _player;
    private readonly object _sync = new();
    private readonly Stopwatch _throttle = Stopwatch.StartNew();
    private const int MinIntervalMs = 800;

    public SoundService(ISettingsService settings)
    {
        _settings = settings;
        Reload();
    }

    public void Reload()
    {
        lock (_sync)
        {
            _player?.Dispose();
            _player = null;

            var path = ResolveSoundPath();
            if (path != null && File.Exists(path))
            {
                try
                {
                    _player = new SoundPlayer(path);
                    _player.LoadAsync();
                }
                catch
                {
                    _player = null;
                }
            }
        }
    }

    public void PlayAlert()
    {
        if (!_settings.Current.PlaySound) return;

        if (_throttle.ElapsedMilliseconds < MinIntervalMs) return;
        _throttle.Restart();

        lock (_sync)
        {
            try
            {
                if (_player != null)
                {
                    _player.Play();
                }
                else
                {
                    SystemSounds.Exclamation.Play();
                }
            }
            catch
            {
                SystemSounds.Exclamation.Play();
            }
        }
    }

    private string? ResolveSoundPath()
    {
        var custom = _settings.Current.CustomSoundPath;
        if (!string.IsNullOrWhiteSpace(custom) && File.Exists(custom))
            return custom;

        var exeDir = AppContext.BaseDirectory;
        var bundled = Path.Combine(exeDir, "Resources", "alert.wav");
        if (File.Exists(bundled)) return bundled;

        var legacy = Path.Combine(exeDir, "alert.wav");
        if (File.Exists(legacy)) return legacy;

        return null;
    }
}
