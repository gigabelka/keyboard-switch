using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using KeyboardSwitch.Models;
using KeyboardSwitch.Services;

namespace KeyboardSwitch.ViewModels;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly ISettingsService _settings;
    private readonly IAutoStartService _autoStart;
    private readonly ISoundService _sound;

    public SettingsViewModel(ISettingsService settings, IAutoStartService autoStart, ISoundService sound)
    {
        _settings = settings;
        _autoStart = autoStart;
        _sound = sound;

        var s = settings.Current;
        _enabled = s.Enabled;
        _playSound = s.PlaySound;
        _autoSwitchOn = s.AutoSwitch;
        _autoStartOn = _autoStart.IsEnabled();
        _customSoundPath = s.CustomSoundPath ?? string.Empty;
        _minWordLength = s.MinWordLength;
        _sensitivity = s.Sensitivity;
        _ignoredProcesses = new ObservableCollection<string>(s.IgnoredProcesses);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

    private bool _enabled;
    public bool Enabled
    {
        get => _enabled;
        set { if (_enabled != value) { _enabled = value; OnChanged(); PersistSoft(); } }
    }

    private bool _playSound;
    public bool PlaySound
    {
        get => _playSound;
        set { if (_playSound != value) { _playSound = value; OnChanged(); PersistSoft(); } }
    }

    private bool _autoSwitchOn;
    public bool AutoSwitchOn
    {
        get => _autoSwitchOn;
        set { if (_autoSwitchOn != value) { _autoSwitchOn = value; OnChanged(); PersistSoft(); } }
    }

    private bool _autoStartOn;
    public bool AutoStartOn
    {
        get => _autoStartOn;
        set
        {
            if (_autoStartOn != value)
            {
                _autoStartOn = value;
                OnChanged();
                _autoStart.SetEnabled(value);
                _settings.Current.AutoStart = value;
                _settings.Save();
            }
        }
    }

    private string _customSoundPath;
    public string CustomSoundPath
    {
        get => _customSoundPath;
        set
        {
            if (_customSoundPath != value)
            {
                _customSoundPath = value;
                OnChanged();
                _settings.Current.CustomSoundPath = string.IsNullOrWhiteSpace(value) ? null : value;
                _settings.Save();
                _sound.Reload();
            }
        }
    }

    private int _minWordLength;
    public int MinWordLength
    {
        get => _minWordLength;
        set
        {
            var clamped = Math.Max(2, Math.Min(12, value));
            if (_minWordLength != clamped)
            {
                _minWordLength = clamped;
                OnChanged();
                PersistSoft();
            }
        }
    }

    private Sensitivity _sensitivity;
    public Sensitivity Sensitivity
    {
        get => _sensitivity;
        set { if (_sensitivity != value) { _sensitivity = value; OnChanged(); PersistSoft(); } }
    }

    private readonly ObservableCollection<string> _ignoredProcesses;
    public ObservableCollection<string> IgnoredProcesses => _ignoredProcesses;

    private string _newIgnoredProcess = string.Empty;
    public string NewIgnoredProcess
    {
        get => _newIgnoredProcess;
        set { if (_newIgnoredProcess != value) { _newIgnoredProcess = value; OnChanged(); } }
    }

    public void AddIgnoredProcess()
    {
        var name = _newIgnoredProcess.Trim();
        if (string.IsNullOrEmpty(name)) return;
        if (!_ignoredProcesses.Any(p => string.Equals(p, name, StringComparison.OrdinalIgnoreCase)))
        {
            _ignoredProcesses.Add(name);
            _settings.Current.IgnoredProcesses = _ignoredProcesses.ToList();
            _settings.Save();
        }
        NewIgnoredProcess = string.Empty;
    }

    public void RemoveIgnoredProcess(string name)
    {
        if (_ignoredProcesses.Remove(name))
        {
            _settings.Current.IgnoredProcesses = _ignoredProcesses.ToList();
            _settings.Save();
        }
    }

    public void TestSound() => _sound.PlayAlert();

    private void PersistSoft()
    {
        var s = _settings.Current;
        s.Enabled = _enabled;
        s.PlaySound = _playSound;
        s.AutoSwitch = _autoSwitchOn;
        s.MinWordLength = _minWordLength;
        s.Sensitivity = _sensitivity;
        _settings.Save();
        _settings.NotifyChanged();
    }

    public Sensitivity[] SensitivityOptions { get; } = new[]
    {
        Sensitivity.Low, Sensitivity.Medium, Sensitivity.High
    };
}
