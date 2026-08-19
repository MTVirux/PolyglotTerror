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
        IReadOnlyList<LanguageEntry> order,
        bool hideDuplicates)
    {
        var lines = new List<string>();

        void Add(string? candidate)
        {
            var text = candidate?.Trim();
            if (string.IsNullOrEmpty(text))
                return;

            if (hideDuplicates && lines.Contains(text, StringComparer.Ordinal))
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

    /// <summary>
    /// Returns null when there is nothing to add, which every caller reads as
    /// "leave the game's own text alone".
    /// </summary>
    public static string? Compose(
        string? original,
        IReadOnlyDictionary<GameLanguage, string?> names,
        IReadOnlyList<LanguageEntry> order,
        bool hideDuplicates)
    {
        var lines = BuildLines(original, names, order, hideDuplicates);
        return lines.Count > 1 ? string.Join(Separator, lines) : null;
    }
}
