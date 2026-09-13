namespace TouchDeck.Core.Configuration;

/// <summary>
/// Where an icon path points and whether it is a format that can be drawn. The rendering
/// layer and the validator both need these rules, and they must agree: a path the validator
/// passes and the renderer then refuses is the worst of both.
/// </summary>
public static class IconFile
{
    /// <summary>Vector format, understood well enough to explain why it does not work yet.</summary>
    public const string SvgExtension = ".svg";

    private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".ico",
    };

    /// <summary>Extensions that can be drawn, in a stable order for messages.</summary>
    public static IReadOnlyList<string> SupportedExtensions { get; } =
        Supported.OrderBy(e => e, StringComparer.Ordinal).ToArray();

    /// <summary>True when the path names an svg.</summary>
    /// <param name="path">A file name or path.</param>
    public static bool IsSvg(string path) =>
        SvgExtension.Equals(Path.GetExtension(path), StringComparison.OrdinalIgnoreCase);

    /// <summary>True when the path names a format that can be drawn.</summary>
    /// <param name="path">A file name or path.</param>
    public static bool IsSupported(string path) => Supported.Contains(Path.GetExtension(path));

    /// <summary>
    /// Turns a configured icon value into an absolute path. Relative paths are looked up in
    /// the icons folder, which is the one rule to learn; environment variables are expanded,
    /// so <c>%LOCALAPPDATA%\Discord\app.ico</c> works. Returns null when the value could
    /// never be a path.
    /// </summary>
    /// <param name="iconsDirectory">Root for relative paths.</param>
    /// <param name="value">The configured path.</param>
    public static string? Resolve(string iconsDirectory, string value)
    {
        var text = value.Trim();
        if (text.Length == 0)
        {
            return null;
        }

        try
        {
            text = Environment.ExpandEnvironmentVariables(text);
            return Path.GetFullPath(Path.IsPathRooted(text) ? text : Path.Combine(iconsDirectory, text));
        }
        catch (Exception e) when (e is ArgumentException or PathTooLongException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// True when the value contains a <c>{{...}}</c> placeholder, which means its real path
    /// is not known until the deck is running and nothing about it can be checked up front.
    /// </summary>
    /// <param name="value">The configured path.</param>
    public static bool IsInterpolated(string value) => value.Contains("{{", StringComparison.Ordinal);
}
