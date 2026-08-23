using System.Numerics;
using PolyglotTerror.Core;

namespace PolyglotTerror.Tests;

public class PanelPlacementTests
{
    private static readonly Vector2 Screen = new(1920f, 1080f);

    [Fact]
    public void SitsToTheRightOfTheTooltip()
    {
        var at = PanelPlacement.Beside(new Vector2(400f, 200f), 250f, new Vector2(300f, 400f), 8f, 20f, Screen);

        Assert.Equal(new Vector2(658f, 220f), at);
    }

    [Fact]
    public void FlipsLeftWhenThereIsNoRoomOnTheRight()
    {
        var at = PanelPlacement.Beside(new Vector2(1500f, 200f), 350f, new Vector2(300f, 400f), 8f, 20f, Screen);

        Assert.Equal(1192f, at.X);
    }

    [Fact]
    public void SlidesUpWhenTheTooltipIsNearTheBottomOfTheScreen()
    {
        var at = PanelPlacement.Beside(new Vector2(400f, 900f), 250f, new Vector2(300f, 400f), 8f, 20f, Screen);

        Assert.Equal(680f, at.Y);
    }

    [Fact]
    public void StartsAtTheTopWhenThePanelIsTallerThanTheScreen()
    {
        var at = PanelPlacement.Beside(new Vector2(400f, 900f), 250f, new Vector2(300f, 1400f), 8f, 20f, Screen);

        Assert.Equal(0f, at.Y);
    }

    [Fact]
    public void NeverStartsAboveTheTopOfTheScreen()
    {
        var at = PanelPlacement.Beside(new Vector2(400f, 10f), 250f, new Vector2(300f, 400f), 8f, -100f, Screen);

        Assert.Equal(0f, at.Y);
    }

    [Fact]
    public void KeepsTheUnclampedTopWhileThePanelHeightIsStillUnknown()
    {
        var at = PanelPlacement.Beside(new Vector2(400f, 900f), 250f, new Vector2(300f, 0f), 8f, 20f, Screen);

        Assert.Equal(920f, at.Y);
    }
}
