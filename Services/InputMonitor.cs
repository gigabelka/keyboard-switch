using System;
using System.Text;
using KeyboardSwitch.Interop;

namespace KeyboardSwitch.Services;

/// <summary>
/// Wires the keyboard hook to the word buffer and detector.
/// Converts VK codes into characters using the foreground window's keyboard layout via ToUnicodeEx.
/// </summary>
public sealed class InputMonitor : IDisposable
{
    private readonly KeyboardHook _hook;
    private readonly WordBuffer _buffer;
    private readonly ILayoutService _layoutService;
    private readonly ILayoutDetector _detector;
    private readonly ISoundService _sound;
    private readonly IAutoSwitchService _autoSwitch;
    private readonly ISettingsService _settings;
    private readonly byte[] _keyState = new byte[256];

    public event EventHandler<DetectionResult>? WrongLayoutDetected;

    public InputMonitor(
        KeyboardHook hook,
        WordBuffer buffer,
        ILayoutService layoutService,
        ILayoutDetector detector,
        ISoundService sound,
        IAutoSwitchService autoSwitch,
        ISettingsService settings)
    {
        _hook = hook;
        _buffer = buffer;
        _layoutService = layoutService;
        _detector = detector;
        _sound = sound;
        _autoSwitch = autoSwitch;
        _settings = settings;

        _hook.KeyEvent += OnKey;
        _buffer.WordCompleted += OnWordCompleted;
    }

    private void OnKey(object? sender, KeyboardHookEventArgs e)
    {
        if (!_settings.Current.Enabled) return;

        if (IsIgnoredProcess()) { _buffer.Reset(); return; }

        var lang = _layoutService.GetActiveLanguage();
        var produced = TranslateToChar(e.VkCode, e.ScanCode);

        _buffer.HandleKey(
            e.VkCode, produced, e.IsDown,
            e.CtrlDown, e.AltDown, e.WinDown,
            lang);
    }

    private bool IsIgnoredProcess()
    {
        var name = _layoutService.GetActiveProcessName();
        if (string.IsNullOrEmpty(name)) return false;
        foreach (var ignored in _settings.Current.IgnoredProcesses)
        {
            if (string.Equals(ignored, name, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private string? TranslateToChar(uint vkCode, uint scanCode)
    {
        // Fetch live keyboard state so ToUnicodeEx knows shift/caps.
        if (!NativeMethods.GetKeyboardState(_keyState)) return null;

        var hkl = _layoutService.GetActiveLayout();
        var sb = new StringBuilder(8);
        int rc = NativeMethods.ToUnicodeEx(vkCode, scanCode, _keyState, sb, sb.Capacity, 0, hkl);
        if (rc <= 0) return null; // dead key (-1) or no translation (0)
        return sb.ToString();
    }

    private void OnWordCompleted(object? sender, WordCompletedEventArgs e)
    {
        if (!_settings.Current.Enabled) return;

        var result = _detector.Analyze(e.Word, e.Language);
        if (!result.IsWrongLayout) return;

        WrongLayoutDetected?.Invoke(this, result);

        if (_settings.Current.PlaySound)
            _sound.PlayAlert();

        if (_settings.Current.AutoSwitch && !string.IsNullOrEmpty(result.SwappedWord))
        {
            // The user has already typed a separator (space/punct) after the word,
            // so we must also erase that separator character + one extra space we'll re-add.
            // To keep the logic simple, we only re-type the word; the separator is already in the document.
            // We therefore erase word.Length + 1 and re-type `swapped + <no separator>`—but we can't
            // easily reproduce the exact separator. Compromise: erase word.Length + 1 chars (word + separator)
            // and re-type swapped + space.
            _autoSwitch.FixWord(e.Word + " ", result.SwappedWord + " ", result.ProbableLanguage);
        }
    }

    public void Dispose()
    {
        _hook.KeyEvent -= OnKey;
        _buffer.WordCompleted -= OnWordCompleted;
    }
}
