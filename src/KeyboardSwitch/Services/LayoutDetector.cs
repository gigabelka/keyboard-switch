using System;
using KeyboardSwitch.Interop;
using KeyboardSwitch.Models;

namespace KeyboardSwitch.Services;

public readonly record struct DetectionResult(
    bool IsWrongLayout,
    DetectedLanguage TypedIn,
    DetectedLanguage ProbableLanguage,
    string SwappedWord,
    double ScoreCurrent,
    double ScoreOther);

public interface ILayoutDetector
{
    DetectionResult Analyze(string word, DetectedLanguage currentLayout);
}

public sealed class TrigramLayoutDetector : ILayoutDetector
{
    private readonly BigramModel _en;
    private readonly BigramModel _ru;
    private readonly ISettingsService _settings;

    public TrigramLayoutDetector(ISettingsService settings)
    {
        _settings = settings;

        _en = new BigramModel(
            LanguageCorpora.English.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            LanguageCorpora.EnglishAlphabet);

        _ru = new BigramModel(
            LanguageCorpora.Russian.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            LanguageCorpora.RussianAlphabet);
    }

    public DetectionResult Analyze(string word, DetectedLanguage currentLayout)
    {
        if (string.IsNullOrEmpty(word) || word.Length < _settings.Current.MinWordLength)
            return new DetectionResult(false, currentLayout, currentLayout, string.Empty, 0, 0);

        if (currentLayout == DetectedLanguage.Unknown)
            return new DetectionResult(false, currentLayout, currentLayout, string.Empty, 0, 0);

        // Sanity check: the word must be mostly of the current layout's alphabet, otherwise it's
        // probably a mixed/unicode string we can't reason about (emoji, IME, numbers-only).
        if (!MostlyMatchesAlphabet(word, currentLayout))
            return new DetectionResult(false, currentLayout, currentLayout, string.Empty, 0, 0);

        string swapped;
        double scoreCurrent, scoreOther;
        DetectedLanguage probable;

        if (currentLayout == DetectedLanguage.English)
        {
            scoreCurrent = _en.Score(word);
            swapped = LayoutMap.SwapEnToRu(word);
            scoreOther = _ru.Score(swapped);
            probable = DetectedLanguage.Russian;
        }
        else // Russian
        {
            scoreCurrent = _ru.Score(word);
            swapped = LayoutMap.SwapRuToEn(word);
            scoreOther = _en.Score(swapped);
            probable = DetectedLanguage.English;
        }

        double threshold = _settings.Current.Sensitivity switch
        {
            Sensitivity.Low => 2.0,
            Sensitivity.Medium => 1.0,
            Sensitivity.High => 0.5,
            _ => 1.0
        };

        bool wrong = (scoreOther - scoreCurrent) > threshold;
        return new DetectionResult(wrong, currentLayout, probable, swapped, scoreCurrent, scoreOther);
    }

    private static bool MostlyMatchesAlphabet(string w, DetectedLanguage lang)
    {
        int match = 0;
        int total = 0;
        foreach (var c in w)
        {
            if (!char.IsLetter(c)) continue;
            total++;
            bool isCyr = LayoutMap.IsCyrillicLetter(c);
            bool isLat = LayoutMap.IsLatinLetter(c);
            if (lang == DetectedLanguage.English && isLat) match++;
            else if (lang == DetectedLanguage.Russian && isCyr) match++;
        }
        return total > 0 && match >= (total * 0.8);
    }
}
