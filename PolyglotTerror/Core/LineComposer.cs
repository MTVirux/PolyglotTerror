using System.Collections.Generic;

namespace PolyglotTerror.Core;

public static class LineComposer
{
    public const string Separator = "\n";

    /// <summary>
    /// One line per enabled language, in list order, skipping blanks and repeats. Returns null when
    /// no name resolved at all, which every caller reads as "leave the game's own text alone".
    /// </summary>
    public static string? Compose(
        IReadOnlyDictionary<GameLanguage, string?> names,
        IReadOnlyList<LanguageEntry> order)
    {
        var lines = new List<string>();

        foreach (var entry in order)
        {
            if (!entry.Enabled)
                continue;

            var text = (names.TryGetValue(entry.Language, out var name) ? name : null)?.Trim();
            if (string.IsNullOrEmpty(text) || lines.Contains(text))
                continue;

            lines.Add(text);
        }

        return lines.Count > 0 ? string.Join(Separator, lines) : null;
    }
}
