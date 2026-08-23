using PolyglotTerror.Core;

namespace PolyglotTerror.Tests;

public class LineComposerTests
{
    private static readonly IReadOnlyList<LanguageEntry> JaThenDe =
    [
        new(GameLanguage.Japanese, true),
        new(GameLanguage.German, true),
        new(GameLanguage.French, false),
    ];

    private static Dictionary<GameLanguage, string?> Names(
        string? en = null, string? ja = null, string? de = null, string? fr = null) => new()
    {
        [GameLanguage.English] = en,
        [GameLanguage.Japanese] = ja,
        [GameLanguage.German] = de,
        [GameLanguage.French] = fr,
    };

    [Fact]
    public void Compose_joins_enabled_languages_in_configured_order()
    {
        var result = LineComposer.Compose(Names(ja: "ファイジャ", de: "Feuga"), JaThenDe);

        Assert.Equal("ファイジャ\nFeuga", result);
    }

    [Fact]
    public void Compose_skips_languages_that_are_disabled()
    {
        var order = new LanguageEntry[] { new(GameLanguage.Japanese, false), new(GameLanguage.German, true) };

        var result = LineComposer.Compose(Names(ja: "ファイジャ", de: "Feuga"), order);

        Assert.Equal("Feuga", result);
    }

    [Fact]
    public void Compose_keeps_a_single_enabled_language()
    {
        var order = new LanguageEntry[] { new(GameLanguage.Japanese, true), new(GameLanguage.German, false) };

        var result = LineComposer.Compose(Names(ja: "ファイジャ", de: "Feuga"), order);

        Assert.Equal("ファイジャ", result);
    }

    [Fact]
    public void Compose_drops_a_language_matching_a_line_already_shown()
    {
        var result = LineComposer.Compose(Names(ja: "Copper Ingot", de: "Copper Ingot"), JaThenDe);

        Assert.Equal("Copper Ingot", result);
    }

    [Fact]
    public void Compose_drops_missing_and_blank_names()
    {
        var result = LineComposer.Compose(Names(ja: null, de: "   "), JaThenDe);

        Assert.Null(result);
    }

    [Fact]
    public void Compose_returns_null_when_no_name_resolves()
    {
        var result = LineComposer.Compose(Names(), JaThenDe);

        Assert.Null(result);
    }

    [Fact]
    public void Compose_trims_each_line()
    {
        var result = LineComposer.Compose(Names(ja: " ファイジャ ", de: " Feuga "), JaThenDe);

        Assert.Equal("ファイジャ\nFeuga", result);
    }
}
