namespace TouchDeck.App.Rendering;

/// <summary>
/// Decides when the panel is allowed to rebuild itself.
///
/// A button fires on the way down, so a button that changes the page changes it while the
/// finger is still on the glass. Rebuilding there and then destroys the button that is in
/// the middle of a gesture: the touch that started on it has nowhere left to end, and what
/// is left of it lands on whatever the new page put in the same square, which presses that.
///
/// So the rebuild waits. Nothing is torn down until every finger is off, which is a few tens
/// of milliseconds later and looks instant, and the gesture ends on the button it started on.
/// </summary>
public sealed class SurfaceGate
{
    private int _held;
    private bool _owed;

    /// <summary>True while at least one button is down.</summary>
    public bool Held => _held > 0;

    /// <summary>True when a rebuild has been asked for and is waiting for the last finger.</summary>
    public bool Owed => _owed;

    /// <summary>Records that a button went down.</summary>
    public void Down() => _held++;

    /// <summary>Records that a button came up.</summary>
    /// <returns>True when that was the last one and a rebuild is now due.</returns>
    public bool Up()
    {
        _held = Math.Max(0, _held - 1);

        if (_held > 0 || !_owed)
        {
            return false;
        }

        _owed = false;
        return true;
    }

    /// <summary>Records that what the panel should show has changed.</summary>
    /// <returns>True when it may be rebuilt now, false when it has to wait.</returns>
    public bool Changed()
    {
        if (_held == 0)
        {
            _owed = false;
            return true;
        }

        _owed = true;
        return false;
    }

    /// <summary>
    /// Gives up waiting, for the case where a press is somehow never reported as finished.
    /// A panel that never redraws again would be a far worse fault than the one this avoids.
    /// </summary>
    /// <returns>True when a rebuild was still owed.</returns>
    public bool GiveUp()
    {
        _held = 0;

        if (!_owed)
        {
            return false;
        }

        _owed = false;
        return true;
    }
}
