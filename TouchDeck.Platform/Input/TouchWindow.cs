using System.Runtime.InteropServices;
using Serilog;
using TouchDeck.Platform.Native;

namespace TouchDeck.Platform.Input;

/// <summary>What a contact is doing.</summary>
public enum TouchPhase
{
    /// <summary>A finger landed.</summary>
    Down,

    /// <summary>A finger that was already down moved.</summary>
    Move,

    /// <summary>A finger lifted.</summary>
    Up,
}

/// <summary>One finger, at one moment, in physical pixels relative to the window's client area.</summary>
/// <param name="Id">Identifies this finger for as long as it stays down.</param>
/// <param name="X">Pixels from the left edge of the client area.</param>
/// <param name="Y">Pixels from the top edge of the client area.</param>
/// <param name="Phase">What it is doing.</param>
public readonly record struct TouchContact(int Id, double X, double Y, TouchPhase Phase);

/// <summary>
/// Takes touch input from Windows directly, instead of letting it be turned into mouse
/// clicks.
/// </summary>
/// <remarks>
/// A window that has not asked for touch gets mouse emulation instead: Windows moves the
/// mouse pointer to wherever the finger landed and synthesises a click there. On a deck that
/// is a real problem rather than a cosmetic one. The panel is on a second screen, so every
/// tap teleports the pointer off the screen the user is looking at, and a game that steers
/// the camera by mouse movement reads that jump as an enormous flick of the mouse.
///
/// Asking for the contacts stops all of that at the source. Nothing is moved, nothing is
/// synthesised, and the tap arrives as what it is.
/// </remarks>
public static class TouchWindow
{
    /// <summary>The message Windows sends once a window has asked for contacts.</summary>
    public const int TouchMessage = NativeMethods.WmTouch;

    /// <summary>Positions arrive in hundredths of a pixel.</summary>
    private const double PixelFraction = 100.0;

    /// <summary>Asks Windows to send this window touch contacts rather than emulated clicks.</summary>
    /// <param name="window">The window handle.</param>
    /// <param name="logger">Where a refusal is reported.</param>
    /// <returns>True when the window is now taking touch input.</returns>
    public static bool Claim(nint window, ILogger logger)
    {
        if (NativeMethods.RegisterTouchWindow(window, NativeMethods.TwfWantPalm))
        {
            return true;
        }

        // Not fatal: without this the deck still works, it just moves the pointer when
        // touched, which is the thing this was for.
        logger.Warning(
            "Windows would not give this window touch input (error {Error}), so a tap will " +
            "still move the mouse pointer.",
            Marshal.GetLastWin32Error());

        return false;
    }

    /// <summary>Whether a window is taking touch input.</summary>
    /// <param name="window">The window handle.</param>
    public static bool IsClaimed(nint window) => NativeMethods.IsTouchWindow(window, out _);

    /// <summary>Hands touch input back, so the window behaves like any other again.</summary>
    /// <param name="window">The window handle.</param>
    public static void Release(nint window) => NativeMethods.UnregisterTouchWindow(window);

    /// <summary>
    /// Reads the contacts out of a touch message and closes it. Positions come back in the
    /// window's own pixels, so the caller only has to undo the display scaling.
    /// </summary>
    /// <param name="window">The window the message arrived at.</param>
    /// <param name="count">The low word of the message's wParam, which is how many there are.</param>
    /// <param name="input">The message's lParam, which is the handle to read from.</param>
    public static IReadOnlyList<TouchContact> Read(nint window, int count, nint input)
    {
        try
        {
            if (count <= 0)
            {
                return Array.Empty<TouchContact>();
            }

            var raw = new NativeMethods.TouchInput[count];
            var size = Marshal.SizeOf<NativeMethods.TouchInput>();

            if (!NativeMethods.GetTouchInputInfo(input, (uint)count, raw, size))
            {
                return Array.Empty<TouchContact>();
            }

            var contacts = new List<TouchContact>(count);

            foreach (var contact in raw)
            {
                if (Phase(contact.Flags) is not { } phase)
                {
                    continue;
                }

                // ScreenToClient deals in whole pixels, so the position crosses in rounded and
                // the fraction is added back on the other side.
                var whole = new NativeMethods.Point
                {
                    X = (int)Math.Floor(contact.X / PixelFraction),
                    Y = (int)Math.Floor(contact.Y / PixelFraction),
                };

                var fractionX = (contact.X / PixelFraction) - whole.X;
                var fractionY = (contact.Y / PixelFraction) - whole.Y;

                if (!NativeMethods.ScreenToClient(window, ref whole))
                {
                    continue;
                }

                contacts.Add(new TouchContact(contact.Id, whole.X + fractionX, whole.Y + fractionY, phase));
            }

            return contacts;
        }
        finally
        {
            // Once, whatever happened. The handle belongs to the message, and marking the
            // message handled makes closing it ours to do.
            NativeMethods.CloseTouchInputHandle(input);
        }
    }

    private static TouchPhase? Phase(uint flags)
    {
        if ((flags & NativeMethods.TouchEventDown) != 0)
        {
            return TouchPhase.Down;
        }

        if ((flags & NativeMethods.TouchEventUp) != 0)
        {
            return TouchPhase.Up;
        }

        return (flags & NativeMethods.TouchEventMove) != 0 ? TouchPhase.Move : null;
    }
}
