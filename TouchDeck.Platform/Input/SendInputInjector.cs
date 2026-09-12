using Serilog;
using TouchDeck.Core.Abstractions;
using TouchDeck.Core.Input;
using TouchDeck.Platform.Native;

namespace TouchDeck.Platform.Input;

/// <summary>
/// Injects keystrokes with <c>SendInput</c>. Everything goes out as a scan code, because
/// virtual key codes are silently ignored by games and anything else reading raw input.
/// </summary>
public sealed class SendInputInjector : IInputInjector
{
    /// <summary>
    /// Keys that live on the extended part of the keyboard. Without the extended flag these
    /// arrive as their numeric keypad twins, so an arrow key would type a digit.
    /// </summary>
    private static readonly IReadOnlySet<VirtualKey> ExtendedKeys = new HashSet<VirtualKey>
    {
        VirtualKey.Left,
        VirtualKey.Up,
        VirtualKey.Right,
        VirtualKey.Down,
        VirtualKey.Insert,
        VirtualKey.Delete,
        VirtualKey.Home,
        VirtualKey.End,
        VirtualKey.PageUp,
        VirtualKey.PageDown,
        VirtualKey.RightControl,
        VirtualKey.RightAlt,
        VirtualKey.NumLock,
        VirtualKey.Divide,
        VirtualKey.PrintScreen,
        VirtualKey.Applications,
        VirtualKey.LeftWindows,
        VirtualKey.RightWindows,
        VirtualKey.BrowserBack,
        VirtualKey.BrowserForward,
        VirtualKey.BrowserRefresh,
        VirtualKey.BrowserStop,
        VirtualKey.BrowserSearch,
        VirtualKey.BrowserFavorites,
        VirtualKey.BrowserHome,
        VirtualKey.VolumeMute,
        VirtualKey.VolumeDown,
        VirtualKey.VolumeUp,
        VirtualKey.MediaNextTrack,
        VirtualKey.MediaPreviousTrack,
        VirtualKey.MediaStop,
        VirtualKey.MediaPlayPause,
    };

    private readonly ILogger _logger;
    private readonly Lock _gate = new();
    private readonly HashSet<VirtualKey> _held = new();

    /// <summary>Creates an injector.</summary>
    /// <param name="logger">Where injection failures are recorded.</param>
    public SendInputInjector(ILogger logger)
    {
        _logger = logger.ForContext<SendInputInjector>();
    }

    /// <inheritdoc />
    public void SendCombo(KeyCombo combo)
    {
        if (combo.IsEmpty)
        {
            return;
        }

        var pressed = new List<VirtualKey>(combo.PressOrder.Count);

        try
        {
            var inputs = new List<NativeMethods.Input>(combo.PressOrder.Count * 2);

            foreach (var key in combo.PressOrder)
            {
                inputs.Add(BuildInput(key, down: true));
                pressed.Add(key);
            }

            foreach (var key in combo.ReleaseOrder)
            {
                inputs.Add(BuildInput(key, down: false));
            }

            // One call, so nothing can interleave between press and release.
            if (Send(inputs))
            {
                pressed.Clear();
            }
        }
        finally
        {
            // Anything that got as far as being pressed must come back up, whatever happened.
            ForceRelease(pressed);
        }
    }

    /// <inheritdoc />
    public void HoldCombo(KeyCombo combo)
    {
        if (combo.IsEmpty)
        {
            return;
        }

        var inputs = combo.PressOrder.Select(key => BuildInput(key, down: true)).ToList();
        Send(inputs);

        lock (_gate)
        {
            foreach (var key in combo.PressOrder)
            {
                _held.Add(key);
            }
        }
    }

    /// <inheritdoc />
    public void ReleaseCombo(KeyCombo combo)
    {
        if (combo.IsEmpty)
        {
            return;
        }

        var inputs = combo.ReleaseOrder.Select(key => BuildInput(key, down: false)).ToList();
        Send(inputs);

        lock (_gate)
        {
            foreach (var key in combo.ReleaseOrder)
            {
                _held.Remove(key);
            }
        }
    }

    /// <inheritdoc />
    public void ReleaseAllHeldKeys()
    {
        List<VirtualKey> keys;

        lock (_gate)
        {
            if (_held.Count == 0)
            {
                return;
            }

            keys = _held.ToList();
            _held.Clear();
        }

        _logger.Warning("Releasing {Count} keys that were still held down.", keys.Count);
        ForceRelease(keys);
    }

    private void ForceRelease(IEnumerable<VirtualKey> keys)
    {
        var inputs = keys.Reverse().Select(key => BuildInput(key, down: false)).ToList();
        if (inputs.Count > 0)
        {
            Send(inputs);
        }
    }

    /// <summary>Sends a batch, returning true only when every event was accepted.</summary>
    private bool Send(List<NativeMethods.Input> inputs)
    {
        if (inputs.Count == 0)
        {
            return true;
        }

        var array = inputs.ToArray();
        var sent = NativeMethods.SendInput(
            (uint)array.Length,
            array,
            System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.Input>());

        if (sent != array.Length)
        {
            var error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            _logger.Warning(
                "SendInput accepted {Sent} of {Total} events (win32 error {Error}). " +
                "A more privileged window may be in the foreground.",
                sent,
                array.Length,
                error);
            return false;
        }

        return true;
    }

    /// <inheritdoc />
    public void TypeText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var inputs = new List<NativeMethods.Input>(text.Length * 2);

        // Unicode injection sends the character itself, so it works whatever the keyboard
        // layout is and reaches characters no key produces.
        foreach (var unit in text)
        {
            if (unit == '\n')
            {
                inputs.Add(BuildInput(VirtualKey.Enter, down: true));
                inputs.Add(BuildInput(VirtualKey.Enter, down: false));
                continue;
            }

            if (unit == '\r')
            {
                continue;
            }

            inputs.Add(BuildUnicode(unit, down: true));
            inputs.Add(BuildUnicode(unit, down: false));
        }

        Send(inputs);
    }

    /// <inheritdoc />
    public void MouseButton(Core.Abstractions.MouseButton button, PressAction action)
    {
        var (down, up) = button switch
        {
            Core.Abstractions.MouseButton.Right =>
                (NativeMethods.MouseEventRightDown, NativeMethods.MouseEventRightUp),
            Core.Abstractions.MouseButton.Middle =>
                (NativeMethods.MouseEventMiddleDown, NativeMethods.MouseEventMiddleUp),
            _ => (NativeMethods.MouseEventLeftDown, NativeMethods.MouseEventLeftUp),
        };

        var inputs = new List<NativeMethods.Input>(2);

        if (action is PressAction.Click or PressAction.Down)
        {
            inputs.Add(BuildMouse(down, 0, 0, 0));
        }

        if (action is PressAction.Click or PressAction.Up)
        {
            inputs.Add(BuildMouse(up, 0, 0, 0));
        }

        Send(inputs);
    }

    /// <inheritdoc />
    public void MoveMouse(int x, int y, bool relative)
    {
        if (relative)
        {
            Send(new List<NativeMethods.Input> { BuildMouse(NativeMethods.MouseEventMove, x, y, 0) });
            return;
        }

        // Absolute movement is in a 0 to 65535 space across the primary screen.
        var width = Math.Max(1, NativeMethods.GetSystemMetrics(NativeMethods.SmCxScreen));
        var height = Math.Max(1, NativeMethods.GetSystemMetrics(NativeMethods.SmCyScreen));

        Send(new List<NativeMethods.Input>
        {
            BuildMouse(
                NativeMethods.MouseEventMove | NativeMethods.MouseEventAbsolute,
                (int)Math.Round(x * 65535.0 / width),
                (int)Math.Round(y * 65535.0 / height),
                0),
        });
    }

    /// <inheritdoc />
    public void Scroll(int amount, ScrollDirection direction)
    {
        var notches = Math.Max(1, Math.Abs(amount));

        var (flag, sign) = direction switch
        {
            ScrollDirection.Down => (NativeMethods.MouseEventWheel, -1),
            ScrollDirection.Left => (NativeMethods.MouseEventHorizontalWheel, -1),
            ScrollDirection.Right => (NativeMethods.MouseEventHorizontalWheel, 1),
            _ => (NativeMethods.MouseEventWheel, 1),
        };

        var inputs = new List<NativeMethods.Input>(notches);

        for (var i = 0; i < notches; i++)
        {
            inputs.Add(BuildMouse(flag, 0, 0, sign * NativeMethods.WheelDelta));
        }

        Send(inputs);
    }

    private static NativeMethods.Input BuildMouse(uint flags, int dx, int dy, int data) => new()
    {
        Type = NativeMethods.InputMouse,
        Union = new NativeMethods.InputUnion
        {
            Mouse = new NativeMethods.MouseInput
            {
                Dx = dx,
                Dy = dy,
                MouseData = unchecked((uint)data),
                Flags = flags,
                Time = 0,
                ExtraInfo = 0,
            },
        },
    };

    private static NativeMethods.Input BuildUnicode(char unit, bool down) => new()
    {
        Type = NativeMethods.InputKeyboard,
        Union = new NativeMethods.InputUnion
        {
            Keyboard = new NativeMethods.KeyboardInput
            {
                Vk = 0,
                Scan = unit,
                Flags = NativeMethods.KeyEventUnicode | (down ? 0 : NativeMethods.KeyEventKeyUp),
                Time = 0,
                ExtraInfo = 0,
            },
        },
    };

    private static NativeMethods.Input BuildInput(VirtualKey key, bool down)
    {
        var mapped = NativeMethods.MapVirtualKey((uint)key, NativeMethods.MapvkVkToVscEx);
        var scan = (ushort)(mapped & 0xFF);
        var extended = ExtendedKeys.Contains(key) || (mapped & 0xE000) == 0xE000;

        var flags = down ? 0u : NativeMethods.KeyEventKeyUp;
        if (extended)
        {
            flags |= NativeMethods.KeyEventExtendedKey;
        }

        ushort vk = 0;
        if (scan == 0)
        {
            // No scan code exists for this key, so fall back to the virtual key code.
            vk = (ushort)key;
        }
        else
        {
            flags |= NativeMethods.KeyEventScanCode;
        }

        return new NativeMethods.Input
        {
            Type = NativeMethods.InputKeyboard,
            Union = new NativeMethods.InputUnion
            {
                Keyboard = new NativeMethods.KeyboardInput
                {
                    Vk = vk,
                    Scan = scan,
                    Flags = flags,
                    Time = 0,
                    ExtraInfo = 0,
                },
            },
        };
    }
}
