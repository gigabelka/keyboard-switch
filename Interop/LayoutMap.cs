using System.Collections.Generic;
using System.Text;

namespace KeyboardSwitch.Interop;

/// <summary>
/// Static table mapping characters between the standard Russian (ЙЦУКЕН) and
/// US English (QWERTY) keyboard layouts, i.e. same physical key → char in other layout.
/// </summary>
internal static class LayoutMap
{
    // ЙЦУКЕНГ... on QWERTY keys (row-by-row, left to right).
    // Pairs: <en,ru> covering both cases and adjacent punctuation keys.
    private static readonly (char En, char Ru)[] Pairs = new (char, char)[]
    {
        // number row right: - =  (no Cyrillic equivalents, keep as-is)
        // top letter row
        ('q','й'), ('w','ц'), ('e','у'), ('r','к'), ('t','е'), ('y','н'),
        ('u','г'), ('i','ш'), ('o','щ'), ('p','з'), ('[','х'), (']','ъ'),
        // home row
        ('a','ф'), ('s','ы'), ('d','в'), ('f','а'), ('g','п'), ('h','р'),
        ('j','о'), ('k','л'), ('l','д'), (';','ж'), ('\'','э'),
        // bottom row
        ('z','я'), ('x','ч'), ('c','с'), ('v','м'), ('b','и'), ('n','т'),
        ('m','ь'), (',','б'), ('.','ю'), ('/','.'),
        // '`' ↔ 'ё' (tilde key)
        ('`','ё'),
    };

    private static readonly Dictionary<char, char> EnToRu;
    private static readonly Dictionary<char, char> RuToEn;

    static LayoutMap()
    {
        EnToRu = new Dictionary<char, char>(Pairs.Length * 2);
        RuToEn = new Dictionary<char, char>(Pairs.Length * 2);
        foreach (var (en, ru) in Pairs)
        {
            EnToRu[en] = ru;
            RuToEn[ru] = en;
            // Uppercase
            char enU = char.ToUpperInvariant(en);
            char ruU = char.ToUpperInvariant(ru);
            if (enU != en) EnToRu[enU] = ruU;
            if (ruU != ru) RuToEn[ruU] = enU;
        }
    }

    public static bool IsCyrillicLetter(char c) =>
        (c >= 'а' && c <= 'я') || (c >= 'А' && c <= 'Я') || c == 'ё' || c == 'Ё';

    public static bool IsLatinLetter(char c) =>
        (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

    public static bool IsWordChar(char c) =>
        IsLatinLetter(c) || IsCyrillicLetter(c) || c == '\'' || c == '-';

    public static string SwapEnToRu(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            sb.Append(EnToRu.TryGetValue(c, out var mapped) ? mapped : c);
        }
        return sb.ToString();
    }

    public static string SwapRuToEn(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            sb.Append(RuToEn.TryGetValue(c, out var mapped) ? mapped : c);
        }
        return sb.ToString();
    }
}
