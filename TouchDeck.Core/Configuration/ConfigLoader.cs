using System.Text.Json;

namespace TouchDeck.Core.Configuration;

/// <summary>
/// Reads the config directory into a <see cref="DeckConfiguration"/>. It never throws on bad
/// input: anything unreadable becomes a <see cref="ValidationMessage"/> and the rest still loads.
/// </summary>
public sealed class ConfigLoader
{
    private readonly ConfigPaths _paths;
    private readonly IReadOnlyCollection<string> _knownActionTypes;

    /// <summary>Creates a loader for <paramref name="paths"/>.</summary>
    /// <param name="paths">Where the config files live.</param>
    /// <param name="knownActionTypes">
    /// Action types the registry can execute. Anything else produces a warning. Pass an empty
    /// collection to skip the check.
    /// </param>
    public ConfigLoader(ConfigPaths paths, IReadOnlyCollection<string>? knownActionTypes = null)
    {
        _paths = paths;
        _knownActionTypes = knownActionTypes ?? Array.Empty<string>();
    }

    /// <summary>Loads everything under the config root.</summary>
    public DeckConfiguration Load()
    {
        var messages = new List<ValidationMessage>();

        var app = LoadFile<AppConfig>(_paths.ConfigFile, messages, required: true) ?? new AppConfig();
        var themeFile = LoadFile<ThemeFile>(_paths.ThemesFile, messages, required: false) ?? new ThemeFile();
        var profiles = LoadProfiles(messages);

        var configuration = new DeckConfiguration
        {
            App = app,
            Themes = themeFile.Themes,
            Profiles = profiles,
            Messages = messages,
        };

        messages.AddRange(new ConfigValidator(_paths, _knownActionTypes).Validate(configuration));

        return configuration with { Messages = messages.ToArray() };
    }

    private IReadOnlyList<Profile> LoadProfiles(List<ValidationMessage> messages)
    {
        var profiles = new List<Profile>();

        if (!Directory.Exists(_paths.ProfilesDirectory))
        {
            messages.Add(ValidationMessage.Error(
                _paths.Describe(_paths.ProfilesDirectory),
                "",
                "The profiles folder does not exist, so there is nothing to show."));
            return profiles;
        }

        var files = Directory.GetFiles(_paths.ProfilesDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var profile = LoadFile<Profile>(file, messages, required: true);
            if (profile is null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(profile.Id))
            {
                profile = profile with { Id = Path.GetFileNameWithoutExtension(file) };
            }

            profiles.Add(ProfileNormaliser.Normalise(profile with { SourceFile = file }));
        }

        if (profiles.Count == 0)
        {
            messages.Add(ValidationMessage.Error(
                _paths.Describe(_paths.ProfilesDirectory),
                "",
                "No usable profile was found."));
        }

        return profiles;
    }

    private T? LoadFile<T>(string path, List<ValidationMessage> messages, bool required)
        where T : class
    {
        var described = _paths.Describe(path);

        if (!File.Exists(path))
        {
            if (required)
            {
                messages.Add(ValidationMessage.Error(described, "", "File not found. Built in defaults are in use."));
            }

            return null;
        }

        try
        {
            var json = ReadWithRetry(path);
            var value = JsonSerializer.Deserialize<T>(json, ConfigJson.Options);
            if (value is null)
            {
                messages.Add(ValidationMessage.Error(described, "", "File is empty or contains only null."));
            }

            return value;
        }
        catch (JsonException ex)
        {
            messages.Add(ValidationMessage.Error(
                described,
                ex.Path ?? "",
                ex.LineNumber is { } line
                    ? $"{CleanJsonMessage(ex.Message)} (line {line + 1})"
                    : CleanJsonMessage(ex.Message)));
            return null;
        }
        catch (IOException ex)
        {
            messages.Add(ValidationMessage.Error(described, "", $"Could not read the file: {ex.Message}"));
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            messages.Add(ValidationMessage.Error(described, "", $"Could not read the file: {ex.Message}"));
            return null;
        }
    }

    /// <summary>
    /// Reads a file, retrying briefly. An editor saving the file holds a lock for a few
    /// milliseconds, and hot reload fires exactly then.
    /// </summary>
    private static string ReadWithRetry(string path)
    {
        const int attempts = 5;
        const int delayMs = 40;

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch (IOException) when (attempt < attempts)
            {
                Thread.Sleep(delayMs);
            }
        }
    }

    /// <summary>Trims the file position suffix System.Text.Json appends, since it is reported separately.</summary>
    private static string CleanJsonMessage(string message)
    {
        var marker = message.IndexOf(" Path: ", StringComparison.Ordinal);
        return marker < 0 ? message : message[..marker];
    }
}
