namespace TouchDeck.Core.Configuration;

/// <summary>How badly a validation message affects the deck.</summary>
public enum ValidationSeverity
{
    /// <summary>The deck runs, but something will not behave as written.</summary>
    Warning,

    /// <summary>The file could not be used at all. The previous good config keeps running.</summary>
    Error,
}

/// <summary>A single problem found in a config file, addressed well enough for the user to fix it.</summary>
/// <param name="Severity">Whether the deck can still honour the file.</param>
/// <param name="File">File the problem is in, relative to the config root where possible.</param>
/// <param name="Path">JSON path inside that file, for example <c>pages[0].buttons[2].action</c>.</param>
/// <param name="Message">What is wrong, in plain words.</param>
public sealed record ValidationMessage(
    ValidationSeverity Severity,
    string File,
    string Path,
    string Message)
{
    /// <summary>Creates an error.</summary>
    public static ValidationMessage Error(string file, string path, string message) =>
        new(ValidationSeverity.Error, file, path, message);

    /// <summary>Creates a warning.</summary>
    public static ValidationMessage Warning(string file, string path, string message) =>
        new(ValidationSeverity.Warning, file, path, message);

    /// <summary>Renders the message as one line, suitable for the panel overlay and the log.</summary>
    public override string ToString() =>
        string.IsNullOrEmpty(Path) ? $"{File}: {Message}" : $"{File} ({Path}): {Message}";
}
