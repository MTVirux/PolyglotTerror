using PolyglotTerror.Core;

namespace PolyglotTerror.Tests;

public class CastActionSourceTests
{
    [Fact]
    public void Ordinary_casts_come_from_the_action_sheet()
    {
        Assert.Equal(CastNameSource.Action, CastActionSources.FromActionType(1));
    }

    [Fact]
    public void Mounting_comes_from_the_mount_sheet()
    {
        Assert.Equal(CastNameSource.Mount, CastActionSources.FromActionType(13));
    }

    [Fact]
    public void Interacting_comes_from_the_event_action_sheet()
    {
        Assert.Equal(CastNameSource.EventAction, CastActionSources.FromActionType(4));
    }

    [Theory]
    [InlineData((byte)8, CastNameSource.Companion)]
    [InlineData((byte)20, CastNameSource.Ornament)]
    [InlineData((byte)2, CastNameSource.Item)]
    [InlineData((byte)3, CastNameSource.EventItem)]
    [InlineData((byte)5, CastNameSource.GeneralAction)]
    public void Each_action_type_maps_to_its_own_sheet(byte actionType, CastNameSource expected)
    {
        Assert.Equal(expected, CastActionSources.FromActionType(actionType));
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)10)]
    [InlineData((byte)12)]
    [InlineData((byte)200)]
    public void Types_we_cannot_name_resolve_to_nothing(byte actionType)
    {
        Assert.Equal(CastNameSource.None, CastActionSources.FromActionType(actionType));
    }
}
