using System.Text.RegularExpressions;

namespace Noopad.Services;

public class SearchReplaceService : ISearchReplaceService
{
    public IEnumerable<(int start, int length)> FindAll(string text, string pattern, bool matchCase, bool wholeWord, bool regex)
    {
        if (string.IsNullOrEmpty(pattern)) yield break;
        var regexPattern = regex ? pattern : Regex.Escape(pattern);
        if (wholeWord) regexPattern = $@"\b{regexPattern}\b";
        var options = matchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
        Regex rx;
        try { rx = new Regex(regexPattern, options); }
        catch { yield break; }
        foreach (Match m in rx.Matches(text))
            yield return (m.Index, m.Length);
    }

    public string ReplaceAll(string text, string pattern, string replacement, bool matchCase, bool wholeWord, bool regex)
    {
        if (string.IsNullOrEmpty(pattern)) return text;
        var regexPattern = regex ? pattern : Regex.Escape(pattern);
        if (wholeWord) regexPattern = $@"\b{regexPattern}\b";
        var options = matchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
        try
        {
            var rx = new Regex(regexPattern, options);
            replacement = DecodeReplacementEscapes(replacement);
            return rx.Replace(text, regex ? replacement : replacement.Replace("$", "$$"));
        }
        catch { return text; }
    }

    public static string DecodeReplacementEscapes(string replacement)
    {
        if (string.IsNullOrEmpty(replacement)) return replacement;

        return replacement
            .Replace("\\r\\n", "\r\n", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\r", "\r", StringComparison.Ordinal)
            .Replace("\\t", "\t", StringComparison.Ordinal);
    }
}
