using System.Text;

namespace FullModPanel.Core;

/// <summary>
/// Search helpers for the mod list. Text is normalized before comparison. An ASCII
/// query of two or more letters can also match Chinese text by pinyin: whole
/// syllables (<c>haikesi</c>), initials (<c>hks</c>), or a mix (<c>haiks</c>).
/// </summary>
internal static class Pinyin
{
    private static readonly Lazy<string[]> Table = new(() => PinyinTable.Readings.Split('|'));

    /// <summary>Toneless reading of a CJK character, or "" when it has none.</summary>
    public static string Syllable(char ch)
    {
        var index = ch - PinyinTable.FirstCodePoint;
        var table = Table.Value;
        return index >= 0 && index < table.Length ? table[index] : "";
    }

    /// <summary>
    /// Lowercases, applies NFKC, and drops whitespace and zero-width characters so
    /// that spaces in either the query or the text do not matter.
    /// </summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.Normalize(NormalizationForm.FormKC))
        {
            if (char.IsWhiteSpace(ch) || IsZeroWidth(ch))
            {
                continue;
            }

            builder.Append(char.ToLowerInvariant(ch));
        }

        return builder.ToString();
    }

    /// <summary>
    /// A pinyin query is two or more ASCII letters. A single letter would pull in
    /// almost every Chinese title, so it stays a plain substring search.
    /// </summary>
    public static bool IsPinyinQuery(string normalizedQuery)
    {
        if (normalizedQuery.Length < 2)
        {
            return false;
        }

        foreach (var ch in normalizedQuery)
        {
            if (ch is not (>= 'a' and <= 'z'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// True when the normalized query can be read from <paramref name="text"/> one
    /// token at a time. Each Chinese character contributes a non-empty prefix of its
    /// syllable, so a whole syllable or its initial both work. Each ASCII letter or
    /// digit contributes one character. A query may start at any token.
    /// </summary>
    public static bool Matches(string normalizedQuery, string text)
    {
        var tokens = Tokenize(text);
        for (var start = 0; start < tokens.Count; start++)
        {
            if (Consume(tokens, start, normalizedQuery, 0))
            {
                return true;
            }
        }

        return false;
    }

    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>(text.Length);
        foreach (var ch in text.Normalize(NormalizationForm.FormKC))
        {
            if (char.IsWhiteSpace(ch) || IsZeroWidth(ch))
            {
                continue;
            }

            var lower = char.ToLowerInvariant(ch);
            if (IsCjk(lower))
            {
                // An empty token has no reading, so a match cannot cross it.
                tokens.Add(Syllable(lower));
            }
            else if (char.IsAsciiLetterOrDigit(lower))
            {
                tokens.Add(lower.ToString());
            }
        }

        return tokens;
    }

    private static bool Consume(IReadOnlyList<string> tokens, int index, string query, int position)
    {
        if (position == query.Length)
        {
            return true;
        }

        if (index >= tokens.Count)
        {
            return false;
        }

        var reading = tokens[index];
        if (reading.Length == 0)
        {
            return false;
        }

        var max = Math.Min(reading.Length, query.Length - position);
        for (var length = 1; length <= max; length++)
        {
            // Prefixes are nested, so one mismatch rules out every longer prefix too.
            if (reading[length - 1] != query[position + length - 1])
            {
                return false;
            }

            if (Consume(tokens, index + 1, query, position + length))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCjk(char ch) =>
        ch is (>= '\u3400' and <= '\u4DBF') or (>= '\u4E00' and <= '\u9FFF');

    private static bool IsZeroWidth(char ch) =>
        ch is '\u200B' or '\u200C' or '\u200D' or '\uFEFF' or '\u2060';
}
