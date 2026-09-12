namespace TouchDeck.Core.Configuration;

/// <summary>Fixed limits that come from how touchscreens work rather than from taste.</summary>
public static class DeckLimits
{
    /// <summary>
    /// Smallest comfortable touch target, in device independent units. A grid that computes
    /// cells below this is reported, because fingers are not mice.
    /// </summary>
    public const double MinimumTouchTargetDip = 64;
}
