using Serilog;

namespace TouchDeck.App.Bootstrap;

/// <summary>
/// Switches off the stylus and touch stack WPF uses by default.
/// </summary>
/// <remarks>
/// WPF does not read touch from the window messages. It talks to the Windows tablet input
/// service, and that service promotes touch to the mouse on its own: it moves the pointer to
/// the contact and synthesises a click there, which is the thing the panel asking Windows for
/// touch was meant to stop and does not, because the two go through different machinery.
/// <para>
/// Nothing is lost by turning it off. The panel routes <c>WM_TOUCH</c> itself, which is where
/// its presses now come from, and a window that has not asked for touch still gets ordinary
/// mouse emulation, so the config center carries on working under a finger as before.
/// </para>
/// <para>
/// This has to happen before the first window exists, because that is when WPF decides
/// whether to start the stack at all.
/// </para>
/// </remarks>
internal static class StylusStack
{
    /// <summary>The switch WPF reads when it decides whether to start its stylus stack.</summary>
    private const string DisableSwitch = "Switch.System.Windows.Input.Stylus.DisableStylusAndTouchSupport";

    /// <summary>Turns the stack off. Call before any window is created.</summary>
    /// <param name="logger">Where the decision is recorded.</param>
    public static void StandDown(ILogger logger)
    {
        AppContext.SetSwitch(DisableSwitch, true);

        logger.Information(
            "WPF's own touch stack is off, so nothing in this process promotes a tap to the mouse.");
    }
}
