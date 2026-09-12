namespace TouchDeck.Core.Configuration;

/// <summary>
/// Every path the app reads or writes. Defaults to <c>%APPDATA%\TouchDeck</c> and can be
/// redirected with the <c>TOUCHDECK_CONFIG_DIR</c> environment variable or a command line
/// switch, which is what the tests use.
/// </summary>
public sealed class ConfigPaths
{
    /// <summary>Environment variable that redirects the config root.</summary>
    public const string RootEnvironmentVariable = "TOUCHDECK_CONFIG_DIR";

    /// <summary>Creates paths rooted at <paramref name="root"/>.</summary>
    public ConfigPaths(string root)
    {
        Root = Path.GetFullPath(root);
    }

    /// <summary>The config root directory.</summary>
    public string Root { get; }

    /// <summary>Global settings file.</summary>
    public string ConfigFile => Path.Combine(Root, "config.json");

    /// <summary>Named themes file.</summary>
    public string ThemesFile => Path.Combine(Root, "themes.json");

    /// <summary>Directory holding one json file per profile.</summary>
    public string ProfilesDirectory => Path.Combine(Root, "profiles");

    /// <summary>Directory holding user supplied icons.</summary>
    public string IconsDirectory => Path.Combine(Root, "icons");

    /// <summary>Directory holding rolling log files.</summary>
    public string LogsDirectory => Path.Combine(Root, "logs");

    /// <summary>Generated JSON Schema, referenced by the config files.</summary>
    public string SchemaFile => Path.Combine(Root, "touchdeck.schema.json");

    /// <summary>Resolves the config root from the environment, falling back to <c>%APPDATA%\TouchDeck</c>.</summary>
    public static ConfigPaths FromEnvironment()
    {
        var overridden = Environment.GetEnvironmentVariable(RootEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overridden))
        {
            return new ConfigPaths(overridden);
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return new ConfigPaths(Path.Combine(appData, "TouchDeck"));
    }

    /// <summary>Creates the directory structure if it does not exist.</summary>
    public void EnsureDirectories()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(ProfilesDirectory);
        Directory.CreateDirectory(IconsDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }

    /// <summary>Turns an absolute path into one relative to the config root, for readable messages.</summary>
    public string Describe(string path)
    {
        var full = Path.GetFullPath(path);
        return full.StartsWith(Root, StringComparison.OrdinalIgnoreCase)
            ? full[Root.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : full;
    }
}
