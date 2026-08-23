using System;
using System.Collections.Generic;

namespace PolyglotTerror.Core;

public static class LineComposer
{
    public const string Separator = "\n";

    public static GameLanguage? Primary(IReadOnlyList<LanguageEntry> order)
    {
        foreach (var entry in order)
        {
            if (entry.Enabled)
                return entry.Language;
        }

        return null;
    }

    public static IReadOnlyList<string> BuildLines(
        string? original,
        IReadOnlyDictionary<GameLanguage, string?> names,
        IReadOnlyList<LanguageEntry> order)
    {
        var lines = new List<string>();

        void Add(string? candidate)
        {
            var text = candidate?.Trim();
            if (string.IsNullOrEmpty(text))
                return;

            if (Contains(lines, text))
                return;

            lines.Add(text);
        }

        Add(original);

        foreach (var entry in order)
        {
            if (!entry.Enabled)
                continue;

            Add(names.TryGetValue(entry.Language, out var name) ? name : null);
        }

        return lines;
    }

    private static bool Contains(List<string> lines, string text)
    {
        foreach (var line in lines)
        {
            if (string.Equals(line, text, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Composes without an original line to keep, so a single enabled language is still worth
    /// showing. Returns null only when no name resolved at all.
    /// </summary>
    public static string? ComposeStandalone(
        IReadOnlyDictionary<GameLanguage, string?> names,
        IReadOnlyList<LanguageEntry> order)
    {
        var lines = BuildLines(null, names, order);
        return lines.Count > 0 ? string.Join(Separator, lines) : null;
    }

    /// <summary>
    /// Returns null when there is nothing to add, which every caller reads as
    /// "leave the game's own text alone".
    /// </summary>
    public static string? Compose(
        string? original,
        IReadOnlyDictionary<GameLanguage, string?> names,
        IReadOnlyList<LanguageEntry> order)
    {
        var lines = BuildLines(original, names, order);
        return lines.Count > 1 ? string.Join(Separator, lines) : null;
    }
}
