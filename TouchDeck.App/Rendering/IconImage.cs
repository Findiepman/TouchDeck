using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TouchDeck.App.Rendering;

/// <summary>
/// Image work done once, when an icon is brought into the icons folder, rather than every
/// time it is drawn. There are two jobs, and they go together: taking the flat background
/// off a logo, because a png saved on white reads as a white card on a dark deck, and then
/// trimming the empty space around what is left, because an icon drawn inside its own
/// margin is smaller than the size asked for and looks it.
/// </summary>
public static class IconImage
{
    /// <summary>Colour distance at or below which a pixel is definitely background.</summary>
    private const int SolidTolerance = 24;

    /// <summary>
    /// Colour distance beyond which a pixel is definitely foreground. Between the two the
    /// pixel fades out, which is what keeps an anti aliased edge from turning into a
    /// staircase once the background behind it is gone.
    /// </summary>
    private const int EdgeTolerance = 72;

    /// <summary>Below this share of the image removed, there was no background worth taking off.</summary>
    private const double MinimumRemoved = 0.02;

    /// <summary>Above this share, the image is mostly background and removing it would leave nothing.</summary>
    private const double MaximumRemoved = 0.97;

    /// <summary>Share of pixels already transparent that means the image was cut out already.</summary>
    private const double AlreadyCutOut = 0.02;

    /// <summary>
    /// Returns <paramref name="source"/> ready to be used as an icon: flat background taken
    /// off, and the empty space around the subject trimmed away. Returns null when neither
    /// was needed, which is the signal to copy the original across untouched.
    /// </summary>
    /// <param name="source">The image as loaded.</param>
    public static BitmapSource? Prepare(BitmapSource source)
    {
        var width = source.PixelWidth;
        var height = source.PixelHeight;

        if (width < 2 || height < 2)
        {
            return null;
        }

        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var stride = width * 4;
        var pixels = new byte[stride * height];
        converted.CopyPixels(pixels, stride, 0);

        var cut = false;

        if (Removable(pixels, width, height, out var background))
        {
            var share = (double)Erase(pixels, width, height, background) / (width * height);

            // Between the two bounds it was a background. Outside them the guess was wrong,
            // so the pixels are reloaded rather than kept half eaten.
            if (share is >= MinimumRemoved and <= MaximumRemoved)
            {
                cut = true;
            }
            else
            {
                converted.CopyPixels(pixels, stride, 0);
            }
        }

        var bounds = ContentBounds(pixels, width, height);
        if (bounds is null)
        {
            return null;
        }

        var trimmed = bounds.Value != new Int32Rect(0, 0, width, height);

        if (!cut && !trimmed)
        {
            return null;
        }

        var result = BitmapSource.Create(
            width,
            height,
            source.DpiX,
            source.DpiY,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);

        if (trimmed)
        {
            result = new CroppedBitmap(result, bounds.Value);
        }

        result.Freeze();
        return result;
    }

    /// <summary>
    /// The smallest rectangle holding everything visible, or null when nothing is. Anything
    /// barely there is treated as empty, so a stray almost transparent pixel in a corner
    /// cannot defeat the trim.
    /// </summary>
    private static Int32Rect? ContentBounds(byte[] pixels, int width, int height)
    {
        var left = width;
        var top = height;
        var right = -1;
        var bottom = -1;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (pixels[(((y * width) + x) * 4) + 3] <= 8)
                {
                    continue;
                }

                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        }

        return right < 0 ? null : new Int32Rect(left, top, right - left + 1, bottom - top + 1);
    }

    /// <summary>Saves an image as a png, which is the only format that can carry the transparency.</summary>
    /// <param name="image">The image to write.</param>
    /// <param name="path">Where to write it.</param>
    public static void SavePng(BitmapSource image, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));

        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    /// <summary>
    /// Decides whether there is a flat background to remove, and what colour it is. The
    /// border pixels have to agree: a photograph or a gradient does not have a background in
    /// the sense meant here, and guessing one would eat part of the picture.
    /// </summary>
    private static bool Removable(byte[] pixels, int width, int height, out (byte B, byte G, byte R) background)
    {
        background = default;

        var transparent = 0;
        var total = width * height;

        for (var i = 3; i < pixels.Length; i += 4)
        {
            if (pixels[i] < 250)
            {
                transparent++;
            }
        }

        if ((double)transparent / total > AlreadyCutOut)
        {
            return false;
        }

        // The corner is the seed rather than an average, because an average of two colours is
        // usually a third colour that matches neither.
        var seed = At(pixels, width, 0, 0);
        var agreeing = 0;
        var border = 0;

        void Check(int x, int y)
        {
            border++;
            if (Distance(At(pixels, width, x, y), seed) <= SolidTolerance)
            {
                agreeing++;
            }
        }

        for (var x = 0; x < width; x++)
        {
            Check(x, 0);
            Check(x, height - 1);
        }

        for (var y = 1; y < height - 1; y++)
        {
            Check(0, y);
            Check(width - 1, y);
        }

        // Most of the border, but not necessarily all of it: a logo that touches one edge is
        // still a logo on a background.
        if ((double)agreeing / border < 0.8)
        {
            return false;
        }

        background = seed;
        return true;
    }

    /// <summary>
    /// Clears the background by flooding inwards from the edges, so an enclosed area of the
    /// same colour inside the logo is kept. Returns how many pixels were fully cleared.
    /// </summary>
    private static int Erase(byte[] pixels, int width, int height, (byte B, byte G, byte R) background)
    {
        var seen = new bool[width * height];
        var queue = new Queue<int>();

        void Consider(int x, int y)
        {
            var index = (y * width) + x;
            if (seen[index])
            {
                return;
            }

            seen[index] = true;

            var distance = Distance(At(pixels, width, x, y), background);
            if (distance > EdgeTolerance)
            {
                return;
            }

            var offset = index * 4;

            if (distance <= SolidTolerance)
            {
                pixels[offset + 3] = 0;
            }
            else
            {
                // Fades from clear at the solid tolerance to opaque at the edge tolerance.
                var fade = (distance - SolidTolerance) / (double)(EdgeTolerance - SolidTolerance);
                pixels[offset + 3] = (byte)Math.Clamp(fade * 255, 0, 255);
            }

            queue.Enqueue(index);
        }

        for (var x = 0; x < width; x++)
        {
            Consider(x, 0);
            Consider(x, height - 1);
        }

        for (var y = 0; y < height; y++)
        {
            Consider(0, y);
            Consider(width - 1, y);
        }

        while (queue.Count > 0)
        {
            var index = queue.Dequeue();
            var x = index % width;
            var y = index / width;

            if (x > 0)
            {
                Consider(x - 1, y);
            }

            if (x < width - 1)
            {
                Consider(x + 1, y);
            }

            if (y > 0)
            {
                Consider(x, y - 1);
            }

            if (y < height - 1)
            {
                Consider(x, y + 1);
            }
        }

        var cleared = 0;
        for (var i = 3; i < pixels.Length; i += 4)
        {
            if (pixels[i] == 0)
            {
                cleared++;
            }
        }

        return cleared;
    }

    private static (byte B, byte G, byte R) At(byte[] pixels, int width, int x, int y)
    {
        var offset = ((y * width) + x) * 4;
        return (pixels[offset], pixels[offset + 1], pixels[offset + 2]);
    }

    /// <summary>How far apart two colours are, as the largest difference on any channel.</summary>
    private static int Distance((byte B, byte G, byte R) left, (byte B, byte G, byte R) right) =>
        Math.Max(
            Math.Abs(left.B - right.B),
            Math.Max(Math.Abs(left.G - right.G), Math.Abs(left.R - right.R)));
}
