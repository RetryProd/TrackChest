using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ChestCounter;

/// <summary>
/// Languages added after the first 16 (see <see cref="L.ExtraCodes"/>): their texts are not passed to <see cref="L.T"/>
/// but read from <c>Lang\&lt;code&gt;.json</c>, embedded in the exe. Each file maps the English text of an
/// <c>L.T</c> call to its translation. Interpolated texts are written with numbered holes
/// (<c>"Installed ({0})"</c>): the English result is matched against the pattern and the values are put back in the
/// translation, in any order. Keys starting with <c>#</c> are plural forms (<c>#chest.one</c>, <c>#chest.few</c>…) and
/// the ordinal (<c>#ordinal</c>). A missing text falls back to English.
/// </summary>
internal static class LangExtra
{
    private static Dictionary<string, string> _exact = new(StringComparer.Ordinal);
    private static List<(Regex Pattern, string Template)> _patterns = new();
    private static readonly Dictionary<string, string> Cache = new(StringComparer.Ordinal);
    private static readonly Regex Hole = new(@"\{(\d+)\}", RegexOptions.Compiled);

    /// <summary>Loads the texts of an added language (nothing for the first 16).</summary>
    public static void Load(string code)
    {
        var exact = new Dictionary<string, string>(StringComparer.Ordinal);
        var patterns = new List<(Regex, string)>();
        if (Array.IndexOf(L.ExtraCodes, code) >= 0)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"lang-{code}.json");
            if (stream != null)
            {
                var texts = JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? new();
                foreach (var (key, value) in texts)
                {
                    if (string.IsNullOrEmpty(value)) continue;
                    exact[key] = value;
                    if (!key.StartsWith('#') && Hole.IsMatch(key))
                    {
                        patterns.Add((Pattern(key), value));
                    }
                }
            }
        }
        lock (Cache)
        {
            _exact = exact;
            _patterns = patterns;
            Cache.Clear();
        }
    }

    /// <summary>"Installed ({0})" -> ^Installed \((?&lt;h0&gt;.*?)\)$</summary>
    private static Regex Pattern(string key)
    {
        var pattern = "^";
        var last = 0;
        foreach (Match hole in Hole.Matches(key))
        {
            pattern += Regex.Escape(key[last..hole.Index]);
            var name = "h" + hole.Groups[1].Value;
            pattern += pattern.Contains($"(?<{name}>") ? $@"\k<{name}>" : $@"(?<{name}>[\s\S]*?)";
            last = hole.Index + hole.Length;
        }
        return new Regex(pattern + Regex.Escape(key[last..]) + "$", RegexOptions.CultureInvariant);
    }

    /// <summary>Translation of the English text of an <c>L.T</c> call; English when it is missing.</summary>
    public static string T(string en)
    {
        var key = en.Contains('\r') ? en.Replace("\r\n", "\n") : en;
        lock (Cache)
        {
            if (_exact.TryGetValue(key, out var found)) return found;
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var result = en;
            foreach (var (pattern, template) in _patterns)
            {
                var match = pattern.Match(key);
                if (!match.Success) continue;
                result = Hole.Replace(template, h => match.Groups["h" + h.Groups[1].Value] is { Success: true } g ? g.Value : h.Value);
                break;
            }
            if (Cache.Count > 1000) Cache.Clear();
            Cache[key] = result;
            return result;
        }
    }

    /// <summary>Text stored under a special key (#…), null when missing.</summary>
    public static string? Get(string key)
    {
        lock (Cache)
        {
            return _exact.TryGetValue(key, out var value) ? value : null;
        }
    }
}
