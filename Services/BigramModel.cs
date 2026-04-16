using System;
using System.Collections.Generic;

namespace KeyboardSwitch.Services;

/// <summary>
/// Character-bigram log-probability model for a single language.
/// Trained from a small embedded word list (~200 top frequency words).
/// Score(word) = average log-probability per bigram with Laplace smoothing.
/// </summary>
public sealed class BigramModel
{
    private readonly Dictionary<int, double> _logProbs = new();
    private readonly double _floorLogProb;
    private readonly int _alphabetSize;

    private static int Key(char a, char b) => (a << 16) | b;

    public BigramModel(IEnumerable<string> corpus, int alphabetSize)
    {
        _alphabetSize = Math.Max(alphabetSize, 1);

        var counts = new Dictionary<int, int>();
        long total = 0;
        foreach (var word in corpus)
        {
            if (string.IsNullOrEmpty(word) || word.Length < 2) continue;
            var lower = word.ToLowerInvariant();
            for (int i = 0; i < lower.Length - 1; i++)
            {
                int k = Key(lower[i], lower[i + 1]);
                counts.TryGetValue(k, out int c);
                counts[k] = c + 1;
                total++;
            }
        }

        // Laplace smoothing: P(bigram) = (count + 1) / (total + V^2)
        // where V is alphabet size → V^2 is the bigram space.
        double denom = total + (double)_alphabetSize * _alphabetSize;
        foreach (var (k, c) in counts)
            _logProbs[k] = Math.Log((c + 1.0) / denom);

        _floorLogProb = Math.Log(1.0 / denom);
    }

    /// <summary>
    /// Return the average log-probability per bigram of the given word.
    /// Higher value ⇒ word is more likely under this language.
    /// </summary>
    public double Score(string word)
    {
        if (word.Length < 2) return _floorLogProb;
        var lower = word.ToLowerInvariant();
        double sum = 0;
        int n = 0;
        for (int i = 0; i < lower.Length - 1; i++)
        {
            int k = Key(lower[i], lower[i + 1]);
            sum += _logProbs.TryGetValue(k, out var lp) ? lp : _floorLogProb;
            n++;
        }
        return n == 0 ? _floorLogProb : sum / n;
    }
}
