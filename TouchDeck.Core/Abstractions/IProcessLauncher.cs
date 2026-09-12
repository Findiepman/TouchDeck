namespace TouchDeck.Core.Abstractions;

/// <summary>What to start, and how.</summary>
/// <param name="Path">Executable, document or shell verb target.</param>
/// <param name="Arguments">Command line arguments, or null.</param>
/// <param name="WorkingDirectory">Working directory, or null for the executable's own folder.</param>
/// <param name="SingleInstance">Do nothing when the program is already running.</param>
/// <param name="FocusIfRunning">Bring the running copy forward instead of starting another.</param>
public sealed record LaunchRequest(
    string Path,
    string? Arguments = null,
    string? WorkingDirectory = null,
    bool SingleInstance = false,
    bool FocusIfRunning = false);

/// <summary>Starts external programs on behalf of an action.</summary>
public interface IProcessLauncher
{
    /// <summary>Starts a process. Throws when the target cannot be started.</summary>
    /// <param name="request">What to start.</param>
    void Launch(LaunchRequest request);
}
