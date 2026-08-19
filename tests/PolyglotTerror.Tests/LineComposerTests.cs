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
    public void Compose_appends_enabled_languages_in_configured_order()
    {
        var result = LineComposer.Compose("Fire IV", Names(ja: "ファイジャ", de: "Feuga"), JaThenDe, hideDuplicates: true);

        Assert.Equal("Fire IV\nファイジャ\nFeuga", result);
    }

    [Fact]
    public void Compose_skips_languages_that_are_disabled()
    {
        var order = new LanguageEntry[] { new(GameLanguage.Japanese, false), new(GameLanguage.German, true) };

        var result = LineComposer.Compose("Fire IV", Names(ja: "ファイジャ", de: "Feuga"), order, hideDuplicates: true);

        Assert.Equal("Fire IV\nFeuga", result);
    }

    [Fact]
    public void Compose_drops_a_language_matching_a_line_already_shown()
    {
        var result = LineComposer.Compose("Copper Ingot", Names(ja: "銅インゴット", de: "Copper Ingot"), JaThenDe, hideDuplicates: true);

        Assert.Equal("Copper Ingot\n銅インゴット", result);
    }

    [Fact]
    public void Compose_keeps_duplicates_when_the_option_is_off()
    {
        var result = LineComposer.Compose("Copper Ingot", Names(ja: "銅インゴット", de: "Copper Ingot"), JaThenDe, hideDuplicates: false);

        Assert.Equal("Copper Ingot\n銅インゴット\nCopper Ingot", result);
    }

    [Fact]
    public void Compose_drops_missing_and_blank_names()
    {
        var result = LineComposer.Compose("Fire IV", Names(ja: null, de: "   "), JaThenDe, hideDuplicates: true);

        Assert.Null(result);
    }

    [Fact]
    public void Compose_returns_null_when_nothing_would_be_added()
    {
        var result = LineComposer.Compose("Fire IV", Names(), JaThenDe, hideDuplicates: true);

        Assert.Null(result);
    }

    [Fact]
    public void Compose_works_without_an_original_line()
    {
        var result = LineComposer.Compose(null, Names(ja: "ファイジャ", de: "Feuga"), JaThenDe, hideDuplicates: true);

        Assert.Equal("ファイジャ\nFeuga", result);
    }

    [Fact]
    public void Compose_trims_each_line()
    {
        var result = LineComposer.Compose("  Fire IV  ", Names(ja: " ファイジャ "), JaThenDe, hideDuplicates: true);

        Assert.Equal("Fire IV\nファイジャ", result);
    }

    [Fact]
    public void ComposeStandalone_keeps_a_single_enabled_language()
    {
        var order = new LanguageEntry[] { new(GameLanguage.Japanese, true), new(GameLanguage.German, false) };

        var result = LineComposer.ComposeStandalone(Names(ja: "ファイジャ", de: "Feuga"), order, hideDuplicates: true);

        Assert.Equal("ファイジャ", result);
    }

    [Fact]
    public void ComposeStandalone_joins_every_enabled_language()
    {
        var result = LineComposer.ComposeStandalone(Names(ja: "ファイジャ", de: "Feuga"), JaThenDe, hideDuplicates: true);

        Assert.Equal("ファイジャ\nFeuga", result);
    }

    [Fact]
    public void ComposeStandalone_returns_null_when_no_name_resolves()
    {
        var result = LineComposer.ComposeStandalone(Names(), JaThenDe, hideDuplicates: true);

        Assert.Null(result);
    }

    [Fact]
    public void Primary_is_the_first_enabled_language()
    {
        var order = new LanguageEntry[]
        {
            new(GameLanguage.English, false),
            new(GameLanguage.German, true),
            new(GameLanguage.Japanese, true),
        };

        Assert.Equal(GameLanguage.German, LineComposer.Primary(order));
    }

    [Fact]
    public void Primary_is_null_when_every_language_is_disabled()
    {
        var order = new LanguageEntry[] { new(GameLanguage.English, false) };

        Assert.Null(LineComposer.Primary(order));
    }
}
