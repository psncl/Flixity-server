using System.Globalization;
using System.Text;

namespace server.Features.Movies;

/// <summary>Decides whether a movie found by OMDb is the one that was asked for.</summary>
public static class MovieMatch
{
    // Letters that Unicode doesn't see as a plain letter plus an accent
    private static readonly Dictionary<char, string> PlainLetters = new()
    {
        ['ø'] = "o", ['ł'] = "l", ['đ'] = "d", ['ð'] = "d", ['ħ'] = "h", ['ı'] = "i",
        ['æ'] = "ae", ['œ'] = "oe", ['ß'] = "ss", ['þ'] = "th"
    };

    public static bool IsMatch(string requestedTitle, int requestedYear, string foundTitle, int foundYear) =>
        Math.Abs((long)requestedYear - foundYear) <= 1 && TitlesMatch(requestedTitle, foundTitle);

    private static bool TitlesMatch(string requested, string found)
    {
        var (first, second) = (Simplify(requested), Simplify(found));

        // A title made of punctuation only simplifies to nothing, and would match any other such title.
        return first.Length == 0 || second.Length == 0 ? requested == found : first == second;
    }

    /// <summary>"Amélie!" becomes "amelie".</summary>
    private static string Simplify(string title)
    {
        var simplified = new StringBuilder();
        var afterLatinLetter = false;

        // FormD splits é into e and an accent.
        foreach (var character in title.Normalize(NormalizationForm.FormD))
        {
            if (char.IsLetterOrDigit(character))
            {
                var lower = char.ToLowerInvariant(character);
                afterLatinLetter = IsLatin(lower);
                simplified.Append(PlainLetters.GetValueOrDefault(lower, lower.ToString()));
            }
            // In other scripts, marks can make a different letter, such as か and が.
            else if (IsMark(character) && !afterLatinLetter)
            {
                simplified.Append(character);
            }
        }

        return simplified.ToString();
    }

    private static bool IsLatin(char letter) => letter is >= 'a' and <= 'z' or >= 'ß' and <= 'ɏ';

    private static bool IsMark(char character) => CharUnicodeInfo.GetUnicodeCategory(character)
        is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark;
}
