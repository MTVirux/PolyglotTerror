using System;
using System.Numerics;

namespace PolyglotTerror.Core;

/// <summary>Where a panel of translated names sits relative to the tooltip it follows.</summary>
public static class PanelPlacement
{
    /// <summary>
    /// Beside the tooltip, flipping to the left when there is no room on the right and sliding up
    /// when the panel would otherwise run off the bottom of the screen. A panel whose height is not
    /// known yet keeps the top it asked for.
    /// </summary>
    public static Vector2 Beside(
        Vector2 tooltip,
        float tooltipWidth,
        Vector2 panel,
        float gap,
        float offsetY,
        Vector2 screen)
    {
        var right = tooltip.X + tooltipWidth + gap;
        var x = right + panel.X > screen.X
            ? Math.Max(0f, tooltip.X - panel.X - gap)
            : right;

        var top = tooltip.Y + offsetY;
        var y = panel.Y > 0f
            ? Math.Clamp(top, 0f, Math.Max(0f, screen.Y - panel.Y))
            : top;

        return new Vector2(x, y);
    }
}
