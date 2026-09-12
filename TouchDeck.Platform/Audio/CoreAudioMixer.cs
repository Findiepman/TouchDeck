using NAudio.CoreAudioApi;
using Serilog;
using TouchDeck.Core.Abstractions;

namespace TouchDeck.Platform.Audio;

/// <summary>
/// Volume and mute, through the Windows core audio session manager. Works on the default
/// device, a named device, or one application's own slider in the volume mixer.
/// </summary>
public sealed class CoreAudioMixer : IAudioMixer, IDisposable
{
    private readonly ILogger _logger;
    private readonly MMDeviceEnumerator _devices = new();

    /// <summary>Creates a mixer.</summary>
    /// <param name="logger">Where audio changes are recorded.</param>
    public CoreAudioMixer(ILogger logger)
    {
        _logger = logger.ForContext<CoreAudioMixer>();
    }

    /// <inheritdoc />
    public void Apply(AudioTarget target, string? name, AudioOperation operation, double value)
    {
        if (target == AudioTarget.Process)
        {
            ApplyToProcess(name, operation, value);
            return;
        }

        using var device = Device(target, name);
        var volume = device.AudioEndpointVolume;

        switch (operation)
        {
            case AudioOperation.Mute:
                volume.Mute = true;
                break;
            case AudioOperation.Unmute:
                volume.Mute = false;
                break;
            case AudioOperation.ToggleMute:
                volume.Mute = !volume.Mute;
                break;
            case AudioOperation.Set:
                volume.MasterVolumeLevelScalar = (float)Clamp(value);
                break;
            case AudioOperation.Adjust:
                volume.MasterVolumeLevelScalar = (float)Clamp(volume.MasterVolumeLevelScalar + value);
                break;
        }

        _logger.Debug(
            "Audio {Operation} on {Device}, now {Level:P0}{Muted}",
            operation,
            device.FriendlyName,
            volume.MasterVolumeLevelScalar,
            volume.Mute ? " and muted" : string.Empty);
    }

    /// <inheritdoc />
    public double GetVolume(AudioTarget target, string? name)
    {
        if (target == AudioTarget.Process)
        {
            return WithSession(name, session => session.SimpleAudioVolume.Volume, 0);
        }

        using var device = Device(target, name);
        return device.AudioEndpointVolume.MasterVolumeLevelScalar;
    }

    /// <inheritdoc />
    public bool IsMuted(AudioTarget target, string? name)
    {
        if (target == AudioTarget.Process)
        {
            return WithSession(name, session => session.SimpleAudioVolume.Mute, false);
        }

        using var device = Device(target, name);
        return device.AudioEndpointVolume.Mute;
    }

    /// <inheritdoc />
    public void Dispose() => _devices.Dispose();

    private void ApplyToProcess(string? name, AudioOperation operation, double value)
    {
        var touched = ForEachSession(name, session =>
        {
            var volume = session.SimpleAudioVolume;

            switch (operation)
            {
                case AudioOperation.Mute:
                    volume.Mute = true;
                    break;
                case AudioOperation.Unmute:
                    volume.Mute = false;
                    break;
                case AudioOperation.ToggleMute:
                    volume.Mute = !volume.Mute;
                    break;
                case AudioOperation.Set:
                    volume.Volume = (float)Clamp(value);
                    break;
                case AudioOperation.Adjust:
                    volume.Volume = (float)Clamp(volume.Volume + value);
                    break;
            }
        });

        if (touched == 0)
        {
            throw new InvalidOperationException(
                $"Nothing called \"{name}\" is playing audio right now, so there is no slider to move.");
        }
    }

    /// <summary>Runs over every audio session belonging to a named process.</summary>
    private int ForEachSession(string? name, Action<AudioSessionControl> work)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Changing an application's volume needs its name.", nameof(name));
        }

        var wanted = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
        var touched = 0;

        using var device = _devices.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        var sessions = device.AudioSessionManager.Sessions;

        for (var i = 0; i < sessions.Count; i++)
        {
            var session = sessions[i];

            if (!Owns(session, wanted))
            {
                continue;
            }

            work(session);
            touched++;
        }

        return touched;
    }

    private T WithSession<T>(string? name, Func<AudioSessionControl, T> read, T fallback)
    {
        var result = fallback;
        ForEachSession(name, session => result = read(session));
        return result;
    }

    private static bool Owns(AudioSessionControl session, string processName)
    {
        try
        {
            using var process = global::System.Diagnostics.Process.GetProcessById((int)session.GetProcessID);
            return string.Equals(process.ProcessName, processName, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            // The session outlived its process, which happens for a moment after it closes.
            return false;
        }
    }

    private MMDevice Device(AudioTarget target, string? name)
    {
        if (target != AudioTarget.Device || string.IsNullOrWhiteSpace(name))
        {
            return _devices.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }

        foreach (var device in _devices.EnumerateAudioEndPoints(DataFlow.All, DeviceState.Active))
        {
            if (device.FriendlyName.Contains(name, StringComparison.OrdinalIgnoreCase))
            {
                return device;
            }

            device.Dispose();
        }

        throw new InvalidOperationException($"No audio device is called \"{name}\".");
    }

    private static double Clamp(double value) => Math.Clamp(value, 0, 1);
}
