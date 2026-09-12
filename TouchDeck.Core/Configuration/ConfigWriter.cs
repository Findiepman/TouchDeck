using System.Text.Json;

namespace TouchDeck.Core.Configuration;

/// <summary>
/// Writes config files back out. Only files that actually changed are rewritten, because a
/// rewrite loses the comments in that file, and the previous version is kept alongside it
/// as a <c>.bak</c> so nothing is ever lost outright.
/// </summary>
public sealed class ConfigWriter
{
    private readonly ConfigPaths _paths;

    /// <summary>Creates a writer for a config root.</summary>
    /// <param name="paths">Where the config files live.</param>
    public ConfigWriter(ConfigPaths paths)
    {
        _paths = paths;
    }

    /// <summary>Extension given to the copy of the previous version of a file.</summary>
    public const string BackupExtension = ".bak";

    /// <summary>Writes global settings, if they differ from what is on disk.</summary>
    /// <param name="config">The settings to write.</param>
    /// <returns>True when the file was rewritten.</returns>
    public bool SaveAppConfig(AppConfig config) => WriteIfChanged(_paths.ConfigFile, Serialise(config));

    /// <summary>Writes the theme file, if it differs from what is on disk.</summary>
    /// <param name="themes">The themes to write, keyed by name.</param>
    /// <returns>True when the file was rewritten.</returns>
    public bool SaveThemes(IReadOnlyDictionary<string, Theme> themes) =>
        WriteIfChanged(_paths.ThemesFile, Serialise(new ThemeFile { Themes = themes }));

    /// <summary>Writes one profile, if it differs from what is on disk.</summary>
    /// <param name="profile">The profile to write.</param>
    /// <returns>True when the file was rewritten.</returns>
    public bool SaveProfile(Profile profile)
    {
        var path = FileFor(profile);
        return WriteIfChanged(path, Serialise(profile));
    }

    /// <summary>Deletes a profile's file, keeping a backup copy.</summary>
    /// <param name="profile">The profile to remove.</param>
    public void DeleteProfile(Profile profile)
    {
        var path = FileFor(profile);
        if (!File.Exists(path))
        {
            return;
        }

        File.Copy(path, path + BackupExtension, overwrite: true);
        File.Delete(path);
    }

    /// <summary>The file a profile is stored in, which is its id when it has never been saved.</summary>
    /// <param name="profile">The profile to locate.</param>
    public string FileFor(Profile profile) =>
        string.IsNullOrWhiteSpace(profile.SourceFile)
            ? Path.Combine(_paths.ProfilesDirectory, $"{Sanitise(profile.Id)}.json")
            : profile.SourceFile;

    /// <summary>Turns an id into something safe to use as a file name.</summary>
    /// <param name="id">The profile id.</param>
    public static string Sanitise(string id)
    {
        var cleaned = new string(id
            .Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c)
            .ToArray())
            .Trim();

        return cleaned.Length == 0 ? "profile" : cleaned;
    }

    private static string Serialise<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, ConfigJson.Options);
        return json + Environment.NewLine;
    }

    private bool WriteIfChanged(string path, string contents)
    {
        if (File.Exists(path) && File.ReadAllText(path) == contents)
        {
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? _paths.Root);

        if (File.Exists(path))
        {
            File.Copy(path, path + BackupExtension, overwrite: true);
        }

        File.WriteAllText(path, contents);
        return true;
    }
}
