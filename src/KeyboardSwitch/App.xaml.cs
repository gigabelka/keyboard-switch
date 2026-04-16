using System;
using System.IO;
using System.Windows;
using KeyboardSwitch.Interop;
using KeyboardSwitch.Services;
using KeyboardSwitch.ViewModels;
using KeyboardSwitch.Views;

namespace KeyboardSwitch;

public partial class App : System.Windows.Application
{
    private SingleInstanceGuard? _singleInstance;
    private ISettingsService? _settings;
    private ILayoutService? _layout;
    private ISoundService? _sound;
    private IAutoStartService? _autoStart;
    private IAutoSwitchService? _autoSwitch;
    private ILayoutDetector? _detector;
    private WordBuffer? _buffer;
    private KeyboardHook? _hook;
    private InputMonitor? _monitor;
    private TrayIcon? _tray;
    private SettingsWindow? _settingsWindow;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        _singleInstance = new SingleInstanceGuard();
        _singleInstance.ShowWindowRequested += (_, _) => Dispatcher.Invoke(ShowSettingsWindow);
        if (!_singleInstance.TryAcquire())
        {
            Shutdown();
            return;
        }

        try
        {
            EnsureBundledAlertWav();

            _settings = new JsonSettingsService();
            _sound = new SoundService(_settings);
            _autoStart = new RegistryAutoStartService();
            _layout = new LayoutService();
            _autoSwitch = new AutoSwitchService(_layout);
            _detector = new TrigramLayoutDetector(_settings);
            _buffer = new WordBuffer();
            _hook = new KeyboardHook(Dispatcher);
            _monitor = new InputMonitor(_hook, _buffer, _layout, _detector, _sound, _autoSwitch, _settings);
            _hook.Start();

            _tray = new TrayIcon("KeyboardSwitch");
            _tray.OpenSettingsRequested += (_, _) => ShowSettingsWindow();
            _tray.ExitRequested += (_, _) => Shutdown();
            _tray.EnabledToggled += (_, enabled) =>
            {
                _settings!.Current.Enabled = enabled;
                _settings.Save();
                _settings.NotifyChanged();
            };
            _tray.SetEnabledState(_settings.Current.Enabled);

            // If started without --tray flag and no existing settings on disk, show window on first launch.
            bool startMinimized = Array.Exists(e.Args, a =>
                string.Equals(a, "--tray", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a, "-tray", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a, "/tray", StringComparison.OrdinalIgnoreCase));

            if (!startMinimized && !File.Exists(SettingsFilePath()))
                ShowSettingsWindow();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("Ошибка инициализации: " + ex, "KeyboardSwitch",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private static string SettingsFilePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "KeyboardSwitch", "settings.json");

    private static void EnsureBundledAlertWav()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", "alert.wav");
        try { WavGenerator.EnsureAlertWav(path); }
        catch { /* non-fatal: SoundService will fall back to SystemSounds */ }
    }

    private void ShowSettingsWindow()
    {
        if (_settingsWindow == null)
        {
            var vm = new SettingsViewModel(_settings!, _autoStart!, _sound!);
            _settingsWindow = new SettingsWindow(vm);
        }

        if (!_settingsWindow.IsVisible) _settingsWindow.Show();
        if (_settingsWindow.WindowState == WindowState.Minimized)
            _settingsWindow.WindowState = WindowState.Normal;
        _settingsWindow.Activate();
        _settingsWindow.Topmost = true;
        _settingsWindow.Topmost = false;
        _settingsWindow.Focus();
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        _monitor?.Dispose();
        _hook?.Dispose();
        _tray?.Dispose();
        _singleInstance?.Dispose();
    }
}
