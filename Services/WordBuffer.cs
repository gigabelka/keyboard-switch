using System;
using System.Text;
using KeyboardSwitch.Interop;

namespace KeyboardSwitch.Services;

/// <summary>
/// Accumulates characters belonging to the current word as the user types.
/// Fires <see cref="WordCompleted"/> when a word-terminating event occurs (space, punctuation, nav key, etc.).
/// Backspace pops the last character. Ctrl/Alt/Win modifiers abort the current word.
/// </summary>
public sealed class WordBuffer
{
    private readonly StringBuilder _buffer = new(64);
    private const int MaxLen = 64;

    /// <summary>Raised when a word is considered "finished" and ready for analysis.</summary>
    public event EventHandler<WordCompletedEventArgs>? WordCompleted;

    public int Length => _buffer.Length;

    public string Current => _buffer.ToString();

    public void Reset() => _buffer.Clear();

    public void HandleKey(uint vkCode, string? producedChar, bool isDown,
        bool ctrl, bool alt, bool win,
        DetectedLanguage currentLang)
    {
        if (!isDown) return;

        // Modifier-combo → never a "word char", and abort current word (shortcut context).
        if (ctrl || alt || win)
        {
            _buffer.Clear();
            return;
        }

        // Backspace: shrink buffer by one char (if any), do not treat as separator.
        if (vkCode == NativeMethods.VK_BACK)
        {
            if (_buffer.Length > 0) _buffer.Length--;
            return;
        }

        // No character produced (F-keys, arrows, modifiers alone, dead keys…).
        if (string.IsNullOrEmpty(producedChar))
        {
            if (IsWordTerminator(vkCode))
                FinishWord(currentLang);
            return;
        }

        char ch = producedChar[0];

        if (LayoutMap.IsWordChar(ch))
        {
            if (_buffer.Length < MaxLen)
                _buffer.Append(ch);
            else
                _buffer.Clear(); // overflow guard
            return;
        }

        // Any other produced character (space, punctuation, …) ends the word.
        FinishWord(currentLang);
    }

    private static bool IsWordTerminator(uint vk) => vk switch
    {
        NativeMethods.VK_SPACE => true,
        NativeMethods.VK_RETURN => true,
        NativeMethods.VK_TAB => true,
        NativeMethods.VK_ESCAPE => true,
        NativeMethods.VK_LEFT => true,
        NativeMethods.VK_RIGHT => true,
        NativeMethods.VK_UP => true,
        NativeMethods.VK_DOWN => true,
        NativeMethods.VK_HOME => true,
        NativeMethods.VK_END => true,
        NativeMethods.VK_PRIOR => true,
        NativeMethods.VK_NEXT => true,
        NativeMethods.VK_DELETE => true,
        NativeMethods.VK_INSERT => true,
        _ => false
    };

    private void FinishWord(DetectedLanguage lang)
    {
        if (_buffer.Length == 0) return;
        var word = _buffer.ToString();
        _buffer.Clear();
        WordCompleted?.Invoke(this, new WordCompletedEventArgs(word, lang));
    }
}

public sealed class WordCompletedEventArgs : EventArgs
{
    public string Word { get; }
    public DetectedLanguage Language { get; }

    public WordCompletedEventArgs(string word, DetectedLanguage lang)
    {
        Word = word;
        Language = lang;
    }
}
