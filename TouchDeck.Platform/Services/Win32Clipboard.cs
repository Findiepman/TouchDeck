using System.Runtime.InteropServices;
using Serilog;
using TouchDeck.Core.Abstractions;
using TouchDeck.Platform.Native;

namespace TouchDeck.Platform.Services;

/// <summary>
/// The clipboard, straight from Win32. The clipboard is single threaded apartment only and
/// is frequently held open by other applications, so every call runs on its own apartment
/// thread and retries for a moment before giving up.
/// </summary>
public sealed class Win32Clipboard : IClipboard
{
    private const int Attempts = 10;
    private const int RetryDelayMs = 25;

    private readonly ILogger _logger;

    /// <summary>Creates a clipboard service.</summary>
    /// <param name="logger">Where clipboard failures are recorded.</param>
    public Win32Clipboard(ILogger logger)
    {
        _logger = logger.ForContext<Win32Clipboard>();
    }

    /// <inheritdoc />
    public string? GetText() => OnApartmentThread(() =>
    {
        if (!NativeMethods.IsClipboardFormatAvailable(NativeMethods.CfUnicodeText))
        {
            return null;
        }

        if (!Open())
        {
            return null;
        }

        try
        {
            var handle = NativeMethods.GetClipboardData(NativeMethods.CfUnicodeText);

            if (handle == 0)
            {
                return null;
            }

            var pointer = NativeMethods.GlobalLock(handle);

            if (pointer == 0)
            {
                return null;
            }

            try
            {
                return Marshal.PtrToStringUni(pointer);
            }
            finally
            {
                NativeMethods.GlobalUnlock(handle);
            }
        }
        finally
        {
            NativeMethods.CloseClipboard();
        }
    });

    /// <inheritdoc />
    public void SetText(string text) => OnApartmentThread<object?>(() =>
    {
        if (!Open())
        {
            return null;
        }

        nint memory = 0;

        try
        {
            NativeMethods.EmptyClipboard();

            var bytes = (nuint)((text.Length + 1) * 2);
            memory = NativeMethods.GlobalAlloc(NativeMethods.GlobalMovable, bytes);

            if (memory == 0)
            {
                return null;
            }

            var pointer = NativeMethods.GlobalLock(memory);

            if (pointer == 0)
            {
                return null;
            }

            try
            {
                Marshal.Copy(text.ToCharArray(), 0, pointer, text.Length);
                Marshal.WriteInt16(pointer, text.Length * 2, 0);
            }
            finally
            {
                NativeMethods.GlobalUnlock(memory);
            }

            // Ownership passes to the clipboard, so the handle must not be freed afterwards.
            if (NativeMethods.SetClipboardData(NativeMethods.CfUnicodeText, memory) != 0)
            {
                memory = 0;
            }

            return null;
        }
        finally
        {
            NativeMethods.CloseClipboard();

            if (memory != 0)
            {
                NativeMethods.GlobalFree(memory);
            }
        }
    });

    /// <inheritdoc />
    public void Clear() => OnApartmentThread<object?>(() =>
    {
        if (!Open())
        {
            return null;
        }

        try
        {
            NativeMethods.EmptyClipboard();
            return null;
        }
        finally
        {
            NativeMethods.CloseClipboard();
        }
    });

    /// <summary>Opens the clipboard, waiting out whoever else has it.</summary>
    private bool Open()
    {
        for (var attempt = 1; attempt <= Attempts; attempt++)
        {
            if (NativeMethods.OpenClipboard(0))
            {
                return true;
            }

            Thread.Sleep(RetryDelayMs);
        }

        _logger.Warning("Another application is holding the clipboard open.");
        return false;
    }

    /// <summary>Runs work on a fresh single threaded apartment, which the clipboard requires.</summary>
    private T? OnApartmentThread<T>(Func<T?> work)
    {
        T? result = default;
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                result = work();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(3));

        if (failure is not null)
        {
            _logger.Warning(failure, "A clipboard operation failed.");
        }

        return result;
    }
}
