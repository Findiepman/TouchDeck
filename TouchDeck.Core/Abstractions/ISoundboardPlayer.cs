namespace TouchDeck.Core.Abstractions;

/// <summary>What a second press should do while a clip is still playing.</summary>
public enum SoundboardPolicy
{
    /// <summary>Stop whatever is playing and start the new clip. The soundboard default.</summary>
    Cutoff,

    /// <summary>Let the clips stack.</summary>
    Overlap,

    /// <summary>Ignore the press until the current clip finishes.</summary>
    Ignore,
}

/// <summary>One press of a soundboard button.</summary>
/// <param name="Category">The QuoteDeck category to draw from.</param>
/// <param name="Id">A specific clip, or null to draw from the shuffle bag.</param>
/// <param name="Devices">
/// Friendly name substrings of the output devices, matched case insensitively. Empty means
/// the default device. More than one plays the clip to all of them at once, which is how a
/// clip reaches a virtual cable and the user's headphones together.
/// </param>
/// <param name="Volume">Playback gain from 0 to 1.</param>
/// <param name="Policy">What to do if something is already playing.</param>
public sealed record SoundboardRequest(
    string Category,
    string? Id,
    IReadOnlyList<string> Devices,
    double Volume,
    SoundboardPolicy Policy);

/// <summary>Why a press did not make a noise.</summary>
public enum SoundboardOutcome
{
    /// <summary>A clip started playing.</summary>
    Played,

    /// <summary>A clip was already playing and the policy says to ignore the press.</summary>
    Ignored,

    /// <summary>There is no manifest yet. The user needs to run <c>quotedeck build</c>.</summary>
    NoManifest,

    /// <summary>The manifest has no such category.</summary>
    NoCategory,

    /// <summary>The category exists but holds nothing playable.</summary>
    NothingPlayable,

    /// <summary>The named clip is not in that category.</summary>
    NoSuchClip,

    /// <summary>The clip is in the manifest but its file is gone.</summary>
    MissingFile,

    /// <summary>Windows would not open the output device.</summary>
    DeviceUnavailable,
}

/// <summary>What came of a press.</summary>
/// <param name="Outcome">Whether it played, and if not, why not.</param>
/// <param name="Detail">A sentence naming what is wrong, for the log and the user.</param>
public sealed record SoundboardResult(SoundboardOutcome Outcome, string Detail = "")
{
    /// <summary>Whether a noise was made.</summary>
    public bool Played => Outcome == SoundboardOutcome.Played;

    /// <summary>Whether the press was deliberately dropped rather than broken.</summary>
    public bool Quiet => Outcome is SoundboardOutcome.Played or SoundboardOutcome.Ignored;
}

/// <summary>
/// Plays clips a QuoteDeck build has already rendered. Nothing is synthesised here: a press
/// reads a small manifest, picks a clip and opens a file, and that is the whole path.
/// </summary>
public interface ISoundboardPlayer
{
    /// <summary>Plays one clip. Returns rather than throws when there is nothing to play.</summary>
    /// <param name="request">Which clip, where to, and how loud.</param>
    SoundboardResult Play(SoundboardRequest request);

    /// <summary>Stops everything currently playing.</summary>
    void StopAll();
}
