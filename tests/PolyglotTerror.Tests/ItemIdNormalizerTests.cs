using PolyglotTerror.Core;

namespace PolyglotTerror.Tests;

public class ItemIdNormalizerTests
{
    [Theory]
    [InlineData(4745u, 4745u)]
    [InlineData(1_004_745u, 4745u)]
    [InlineData(500_123u, 123u)]
    [InlineData(2_000_500u, 2_000_500u)]
    public void ToBaseItemId_strips_quality_offsets(uint input, uint expected)
    {
        Assert.Equal(expected, ItemIdNormalizer.ToBaseItemId(input));
    }

    [Fact]
    public void IsEventItem_is_true_at_and_above_two_million()
    {
        Assert.False(ItemIdNormalizer.IsEventItem(1_004_745u));
        Assert.True(ItemIdNormalizer.IsEventItem(2_000_500u));
    }
}
