using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Serilog;
using TouchDeck.Core.Abstractions;

namespace TouchDeck.Platform.Audio;

/// <summary>
/// Plays clips that a QuoteDeck build has already rendered.
/// </summary>
/// <remarks>
/// The press path does as little as possible: look up an already-parsed manifest, draw from
/// an in-memory shuffle bag, take the wav bytes from an LRU cache, and hand them to WASAPI.
/// No disk read after the first press of a clip, no parsing, no synthesis, ever.
/// <para>
/// Playing to two devices at once is the point of this class. It is what lets a clip land in
/// Discord through a virtual cable and in the user's own headphones at the same time, and it
/// is done with one independent output per device over its own reader on the same bytes.
/// </para>
/// </remarks>
public sealed class QuoteDeckSoundboard : ISoundboardPlayer, IDisposable
{
    /// <summary>Clips held in memory. Roughly 4MB at 48kHz stereo, which is plenty.</summary>
    private const int CacheCapacity = 24;

    /// <summary>Coalesces the burst of writes a manifest rewrite produces.</summary>
    private static readonly TimeSpan ReloadDebounce = TimeSpan.FromMilliseconds(300);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private readonly ILogger _logger;
    private readonly string _home;
    private readonly object _gate = new();
    private readonly ClipCache _clips = new(CacheCapacity);
    private readonly Dictionary<string, ShuffleBag> _bags = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Voice, byte> _playing = new();
    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly DeviceDirectory _devices;

    private Manifest _manifest = Manifest.Empty;
    private FileSystemWatcher? _watcher;
    private Timer? _debounce;
    private bool _disposed;

    /// <summary>Creates a soundboard reading from the QuoteDeck data directory.</summary>
    /// <param name="logger">Where playback problems are recorded.</param>
    /// <param name="home">
    /// The QuoteDeck directory. Defaults to <c>%APPDATA%\QuoteDeck</c>, which is where the
    /// build tool writes.
    /// </param>
    public QuoteDeckSoundboard(ILogger logger, string? home = null)
    {
        _logger = logger.ForContext<QuoteDeckSoundboard>();
        _home = home ?? DefaultHome();
        _devices = new DeviceDirectory(_enumerator, _logger);
        Reload();
        StartWatching();
    }

    /// <summary>Where QuoteDeck keeps its files, honouring the same override the CLI uses.</summary>
    public static string DefaultHome()
    {
        var overridden = Environment.GetEnvironmentVariable("QUOTEDECK_HOME");
        return string.IsNullOrWhiteSpace(overridden)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QuoteDeck")
            : overridden;
    }

    private string ManifestPath => Path.Combine(_home, "manifest.json");

    private string StateDirectory => Path.Combine(_home, "state");

    /// <inheritdoc />
    public SoundboardResult Play(SoundboardRequest request)
    {
        Manifest manifest;
        lock (_gate)
        {
            manifest = _manifest;
        }

        if (!manifest.Loaded)
        {
            return new SoundboardResult(
                SoundboardOutcome.NoManifest,
                $"No QuoteDeck manifest at {ManifestPath}. Run: quotedeck build");
        }

        if (!manifest.Categories.TryGetValue(request.Category, out var entries))
        {
            return new SoundboardResult(
                SoundboardOutcome.NoCategory,
                $"No category \"{request.Category}\" in the QuoteDeck manifest.");
        }

        var playable = entries.Where(entry => entry.IsPlayable).ToList();
        if (playable.Count == 0)
        {
            return new SoundboardResult(
                SoundboardOutcome.NothingPlayable,
                $"Category \"{request.Category}\" has nothing playable in it.");
        }

        ManifestEntry chosen;
        if (!string.IsNullOrWhiteSpace(request.Id))
        {
            var match = playable.FirstOrDefault(
                entry => string.Equals(entry.Id, request.Id, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                return new SoundboardResult(
                    SoundboardOutcome.NoSuchClip,
                    $"No clip \"{request.Id}\" in category \"{request.Category}\".");
            }

            chosen = match;
        }
        else
        {
            var picked = Draw(request.Category, playable.Select(entry => entry.Id).ToList());
            chosen = playable.First(entry => entry.Id == picked);
        }

        var path = Path.Combine(manifest.AudioRoot, chosen.File!);
        byte[] bytes;
        try
        {
            bytes = _clips.Get(path);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return new SoundboardResult(SoundboardOutcome.MissingFile, $"Missing clip file: {path}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new SoundboardResult(SoundboardOutcome.MissingFile, $"Cannot read {path}: {ex.Message}");
        }

        return Start(bytes, chosen, request);
    }

    /// <inheritdoc />
    public void StopAll()
    {
        foreach (var voice in _playing.Keys)
        {
            voice.Stop();
        }
    }

    private SoundboardResult Start(byte[] bytes, ManifestEntry clip, SoundboardRequest request)
    {
        if (request.Policy == SoundboardPolicy.Ignore && !_playing.IsEmpty)
        {
            return new SoundboardResult(SoundboardOutcome.Ignored, $"{clip.Id} skipped, still playing");
        }

        if (request.Policy == SoundboardPolicy.Cutoff)
        {
            StopAll();
        }

        var targets = ResolveDevices(request.Devices);
        var volume = (float)Math.Clamp(request.Volume, 0d, 1d);
        var started = 0;
        string? lastFailure = null;

        foreach (var device in targets)
        {
            try
            {
                // One output per device, each with its own reader over the same bytes. They
                // cannot share a reader: two outputs pulling from one stream would each get
                // half the samples.
                var voice = Voice.Open(bytes, device, volume);
                _playing.TryAdd(voice, 0);
                voice.Finished += () =>
                {
                    _playing.TryRemove(voice, out _);
                    voice.Dispose();
                };
                voice.Play();
                started++;
            }
            catch (Exception ex) when (ex is COMException or InvalidOperationException or ArgumentException)
            {
                lastFailure = $"{device?.FriendlyName ?? "the default device"}: {ex.Message}";
                _logger.Warning("Could not play {Clip} on {Device}", clip.Id, lastFailure);
            }
        }

        if (started == 0)
        {
            return new SoundboardResult(
                SoundboardOutcome.DeviceUnavailable,
                lastFailure ?? "No output device would accept the clip.");
        }

        _logger.Debug("Played {Clip} on {Count} device(s)", clip.Id, started);
        return new SoundboardResult(SoundboardOutcome.Played);
    }

    /// <summary>
    /// Turns friendly-name substrings into devices. An unmatched name warns and falls back to
    /// the default device rather than going silent, because a silent button reads as broken.
    /// </summary>
    /// <remarks>
    /// The devices come from a cache. Reading <c>FriendlyName</c> hits the Windows property
    /// store and costs around 120ms per device, so doing it on the press path put a two
    /// device button at nearly a second. The cache is rebuilt only when Windows says the
    /// endpoints changed.
    /// </remarks>
    private List<MMDevice?> ResolveDevices(IReadOnlyList<string> wanted)
    {
        if (wanted.Count == 0)
        {
            return [null];
        }

        var known = _devices.Entries;
        var resolved = new List<MMDevice?>();
        var claimed = new HashSet<string>(StringComparer.Ordinal);

        foreach (var name in wanted)
        {
            // "default" means whatever Windows is playing through at the moment. It is there
            // so that a button can send one copy to a virtual microphone and one copy to the
            // person pressing it, without naming a headset that may not be the one in use.
            if (IsDefault(name))
            {
                if (!resolved.Contains(null))
                {
                    resolved.Add(null);
                }

                continue;
            }

            var match = known.FirstOrDefault(
                entry => !claimed.Contains(entry.Id)
                    && entry.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                // Saying which, when the device exists but is switched off, saves a long hunt.
                // A virtual cable or a virtual microphone is exactly the sort of thing that
                // sits there disabled, and "no device matching" reads as "you typed it wrong".
                if (_devices.Disabled(name) is { } off)
                {
                    _logger.Warning(
                        "\"{Device}\" is turned off in Windows, so {Name} cannot be played to. " +
                        "Enable it under Sound settings, or in the software that provides it. " +
                        "Falling back to the default device.",
                        off,
                        name);
                }
                else
                {
                    _logger.Warning(
                        "No output device matching {Name}; falling back to the default device", name);
                }

                if (!resolved.Contains(null))
                {
                    resolved.Add(null);
                }

                continue;
            }

            claimed.Add(match.Id);
            resolved.Add(match.Device);
        }

        return resolved;
    }

    /// <summary>Whether a name in the device list means the default playback device.</summary>
    /// <param name="name">One entry from the configured device list.</param>
    public static bool IsDefault(string name) =>
        name.Trim().Equals("default", StringComparison.OrdinalIgnoreCase);

    private string Draw(string category, IReadOnlyList<string> ids)
    {
        lock (_gate)
        {
            if (!_bags.TryGetValue(category, out var bag))
            {
                bag = ShuffleBag.Load(Path.Combine(StateDirectory, category + ".json"), _logger);
                _bags[category] = bag;
            }

            return bag.Draw(ids);
        }
    }

    // ------------------------------------------------------------------ the manifest

    private void Reload()
    {
        var loaded = Manifest.Read(ManifestPath, _logger);
        lock (_gate)
        {
            _manifest = loaded;
            // Clip filenames carry a content hash, so a rebuild changes the name and the old
            // cache entries simply stop being asked for. Bags are keyed by id, which survives.
            _clips.Clear();
        }

        if (loaded.Loaded)
        {
            _logger.Information(
                "Loaded QuoteDeck manifest: {Categories} categories, {Clips} clips",
                loaded.Categories.Count,
                loaded.Categories.Sum(pair => pair.Value.Count));
        }
    }

    private void StartWatching()
    {
        try
        {
            Directory.CreateDirectory(_home);
            _watcher = new FileSystemWatcher(_home, "manifest.json")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };
            _watcher.Changed += OnManifestTouched;
            _watcher.Created += OnManifestTouched;
            _watcher.Renamed += OnManifestTouched;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            _logger.Warning(ex, "Not watching {Path}; restart TouchDeck after a rebuild", ManifestPath);
        }
    }

    private void OnManifestTouched(object sender, FileSystemEventArgs e)
    {
        // The build writes the manifest by moving a temp file into place, which can raise
        // several events. Wait for them to stop before reading.
        _debounce ??= new Timer(_ => Reload(), null, Timeout.Infinite, Timeout.Infinite);
        _debounce.Change(ReloadDebounce, Timeout.InfiniteTimeSpan);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _watcher?.Dispose();
        _debounce?.Dispose();
        StopAll();
        foreach (var voice in _playing.Keys)
        {
            voice.Dispose();
        }

        _devices.Dispose();
        _enumerator.Dispose();
    }

    // ------------------------------------------------------------------ output devices

    /// <summary>
    /// The active output devices and their friendly names, cached.
    /// </summary>
    /// <remarks>
    /// Enumerating endpoints is cheap, around 4ms. Reading each one's <c>FriendlyName</c> is
    /// not: it goes to the Windows property store and costs roughly 120ms per device, which
    /// on this machine is 840ms of pure latency on every press of a button that names its
    /// devices. So the names are read once and kept until Windows says the endpoints changed.
    /// </remarks>
    private sealed class DeviceDirectory : IMMNotificationClient, IDisposable
    {
        private readonly MMDeviceEnumerator _enumerator;
        private readonly ILogger _logger;
        private readonly object _gate = new();
        private readonly List<MMDevice> _owned = [];
        private List<DeviceEntry>? _entries;
        private List<string>? _disabled;
        private bool _registered;

        public DeviceDirectory(MMDeviceEnumerator enumerator, ILogger logger)
        {
            _enumerator = enumerator;
            _logger = logger;
            try
            {
                _enumerator.RegisterEndpointNotificationCallback(this);
                _registered = true;
            }
            catch (Exception ex) when (ex is COMException or NotSupportedException)
            {
                // Without notifications the cache would go stale, so refresh it every time.
                _logger.Warning(ex, "Not watching for audio device changes");
            }
        }

        /// <summary>Every active render device, with its name already read.</summary>
        public IReadOnlyList<DeviceEntry> Entries
        {
            get
            {
                lock (_gate)
                {
                    return _entries ??= Build();
                }
            }
        }

        /// <summary>
        /// The full name of a render device that matches but has been switched off, or null
        /// when no such device exists.
        /// </summary>
        /// <remarks>
        /// Only the deliberately disabled ones. The unplugged and the not present ones are
        /// every monitor and headset the machine has ever had, which on this one is dozens,
        /// and each name read costs about 120ms; asking about all of them turned a failed
        /// lookup into a seconds long pause before the clip fell back to the default device.
        /// The answer is kept for the same reason and thrown away when the endpoints change.
        /// </remarks>
        /// <param name="name">The substring that matched no active device.</param>
        public string? Disabled(string name)
        {
            List<string> off;

            lock (_gate)
            {
                off = _disabled ??= Off();
            }

            return off.FirstOrDefault(entry => entry.Contains(name, StringComparison.OrdinalIgnoreCase));
        }

        private List<string> Off()
        {
            var names = new List<string>();

            try
            {
                foreach (var device in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Disabled))
                {
                    using (device)
                    {
                        names.Add(device.FriendlyName);
                    }
                }
            }
            catch (COMException ex)
            {
                // Only ever used to make a message clearer, so it is never worth an error.
                _logger.Debug(ex, "Could not list the switched off output devices");
            }

            return names;
        }

        private List<DeviceEntry> Build()
        {
            var entries = new List<DeviceEntry>();
            try
            {
                foreach (var device in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                {
                    _owned.Add(device);
                    entries.Add(new DeviceEntry(device.ID, device.FriendlyName, device));
                }
            }
            catch (COMException ex)
            {
                _logger.Warning(ex, "Could not enumerate audio devices; using the default one");
            }

            _logger.Debug("Cached {Count} output devices", entries.Count);
            return entries;
        }

        private void Invalidate()
        {
            lock (_gate)
            {
                // The old MMDevices stay alive: a clip may still be playing through one, and
                // they are released together when the soundboard shuts down.
                _entries = null;
                _disabled = null;
            }

            if (!_registered)
            {
                return;
            }

            _logger.Debug("Audio devices changed; the directory will be rebuilt");
        }

        public void OnDeviceStateChanged(string deviceId, DeviceState newState) => Invalidate();

        public void OnDeviceAdded(string pwstrDeviceId) => Invalidate();

        public void OnDeviceRemoved(string deviceId) => Invalidate();

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId) => Invalidate();

        /// <summary>Fires constantly and never changes a friendly name, so it is ignored.</summary>
        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
        {
        }

        public void Dispose()
        {
            if (_registered)
            {
                try
                {
                    _enumerator.UnregisterEndpointNotificationCallback(this);
                }
                catch (Exception ex) when (ex is COMException or NotSupportedException)
                {
                    // Shutting down anyway.
                }

                _registered = false;
            }

            lock (_gate)
            {
                foreach (var device in _owned)
                {
                    try
                    {
                        device.Dispose();
                    }
                    catch (COMException)
                    {
                        // Already gone.
                    }
                }

                _owned.Clear();
                _entries = null;
                _disabled = null;
            }
        }
    }

    /// <summary>One output device, with its name read once.</summary>
    private sealed record DeviceEntry(string Id, string Name, MMDevice Device);

    // ------------------------------------------------------------------ one playing clip

    /// <summary>One clip on its way to one device.</summary>
    private sealed class Voice : IDisposable
    {
        private readonly IWavePlayer _output;
        private readonly WaveStream _reader;
        private int _disposed;

        private Voice(IWavePlayer output, WaveStream reader)
        {
            _output = output;
            _reader = reader;
        }

        public event Action? Finished;

        public static Voice Open(byte[] bytes, MMDevice? device, float volume)
        {
            var reader = new WaveFileReader(new MemoryStream(bytes, writable: false));
            var sampled = new SampleToWaveProvider(
                new VolumeSampleProvider(reader.ToSampleProvider()) { Volume = volume });

            // Shared mode, so this never takes a device away from Discord or a game.
            IWavePlayer output = device is null
                ? new WasapiOut(AudioClientShareMode.Shared, 60)
                : new WasapiOut(device, AudioClientShareMode.Shared, useEventSync: true, 60);

            var voice = new Voice(output, reader);
            output.PlaybackStopped += (_, _) => voice.Finished?.Invoke();
            output.Init(sampled);
            return voice;
        }

        public void Play() => _output.Play();

        public void Stop()
        {
            try
            {
                _output.Stop();
            }
            catch (Exception ex) when (ex is COMException or InvalidOperationException or ObjectDisposedException)
            {
                // Already gone. Stopping something that has stopped is not an error.
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            try
            {
                _output.Dispose();
                _reader.Dispose();
            }
            catch (Exception ex) when (ex is COMException or ObjectDisposedException)
            {
                // Nothing useful to do while tearing down a voice.
            }
        }
    }

    // ------------------------------------------------------------------ the clip cache

    /// <summary>
    /// The wav bytes of recently played clips, so the first press after a cold boot is the
    /// only one that touches the disk.
    /// </summary>
    private sealed class ClipCache(int capacity)
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, byte[]> _bytes = new(StringComparer.OrdinalIgnoreCase);
        private readonly LinkedList<string> _order = new();

        public byte[] Get(string path)
        {
            lock (_gate)
            {
                if (_bytes.TryGetValue(path, out var cached))
                {
                    _order.Remove(path);
                    _order.AddFirst(path);
                    return cached;
                }
            }

            var loaded = File.ReadAllBytes(path);

            lock (_gate)
            {
                _bytes[path] = loaded;
                _order.Remove(path);
                _order.AddFirst(path);
                while (_order.Count > capacity && _order.Last is { } oldest)
                {
                    _bytes.Remove(oldest.Value);
                    _order.RemoveLast();
                }
            }

            return loaded;
        }

        public void Clear()
        {
            lock (_gate)
            {
                _bytes.Clear();
                _order.Clear();
            }
        }
    }

    // ------------------------------------------------------------------ the shuffle bag

    /// <summary>
    /// The same bag <c>quotedeck/shuffle.py</c> and <c>play.ps1</c> implement, over the same
    /// state file: play through a shuffled category, reshuffle when empty, and never let a
    /// reshuffle put the clip that just played first.
    /// </summary>
    private sealed class ShuffleBag(string path, ILogger logger, List<string> remaining, string? last)
    {
        private readonly Random _random = new();
        private List<string> _remaining = remaining;
        private string? _last = last;

        public static ShuffleBag Load(string path, ILogger logger)
        {
            try
            {
                if (File.Exists(path))
                {
                    var state = JsonSerializer.Deserialize<BagState>(File.ReadAllText(path), JsonOptions);
                    if (state is not null)
                    {
                        return new ShuffleBag(path, logger, state.Remaining?.ToList() ?? [], state.Last);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                logger.Debug("Starting a fresh shuffle bag for {Path}: {Message}", path, ex.Message);
            }

            // Corrupt or missing state means a fresh bag, never an error.
            return new ShuffleBag(path, logger, [], null);
        }

        public string Draw(IReadOnlyList<string> ids)
        {
            var known = new HashSet<string>(ids, StringComparer.Ordinal);
            _remaining = _remaining.Where(known.Contains).ToList();
            if (_remaining.Count == 0)
            {
                _remaining = Refill(ids);
            }

            var pick = _remaining[0];
            _remaining.RemoveAt(0);
            _last = pick;
            Save();
            return pick;
        }

        private List<string> Refill(IReadOnlyList<string> ids)
        {
            var bag = ids.ToList();
            for (var i = bag.Count - 1; i > 0; i--)
            {
                var j = _random.Next(i + 1);
                (bag[i], bag[j]) = (bag[j], bag[i]);
            }

            if (bag.Count > 1 && bag[0] == _last)
            {
                var swap = _random.Next(1, bag.Count);
                (bag[0], bag[swap]) = (bag[swap], bag[0]);
            }

            return bag;
        }

        private void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var temp = path + "." + Environment.ProcessId + ".tmp";
                File.WriteAllText(
                    temp,
                    JsonSerializer.Serialize(new BagState(1, _remaining, _last), JsonOptions));
                File.Move(temp, path, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Losing the bag costs randomness, not playback.
                logger.Debug("Could not save the shuffle bag {Path}: {Message}", path, ex.Message);
            }
        }
    }

    private sealed record BagState(
        [property: JsonPropertyName("version")] int Version,
        [property: JsonPropertyName("remaining")] List<string>? Remaining,
        [property: JsonPropertyName("last")] string? Last);

    // ------------------------------------------------------------------ manifest model

    private sealed record ManifestEntry(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("file")] string? File,
        [property: JsonPropertyName("ms")] int? Ms,
        [property: JsonPropertyName("text")] string? Text,
        [property: JsonPropertyName("failed")] bool Failed = false)
    {
        public bool IsPlayable => !Failed && !string.IsNullOrWhiteSpace(File);
    }

    private sealed record ManifestFile(
        [property: JsonPropertyName("version")] int Version,
        [property: JsonPropertyName("audioRoot")] string? AudioRoot,
        [property: JsonPropertyName("categories")] Dictionary<string, List<ManifestEntry>>? Categories);

    private sealed record Manifest(bool Loaded, string AudioRoot, IReadOnlyDictionary<string, List<ManifestEntry>> Categories)
    {
        public static Manifest Empty { get; } =
            new(false, string.Empty, new Dictionary<string, List<ManifestEntry>>());

        public static Manifest Read(string path, ILogger logger)
        {
            try
            {
                if (!System.IO.File.Exists(path))
                {
                    return Empty;
                }

                var body = JsonSerializer.Deserialize<ManifestFile>(
                    System.IO.File.ReadAllText(path), JsonOptions);
                if (body?.Categories is null)
                {
                    return Empty;
                }

                return new Manifest(
                    true,
                    body.AudioRoot ?? Path.Combine(Path.GetDirectoryName(path) ?? ".", "audio"),
                    new Dictionary<string, List<ManifestEntry>>(body.Categories, StringComparer.OrdinalIgnoreCase));
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                // A build in flight can be caught mid-write. The old manifest keeps working.
                logger.Warning("Could not read the QuoteDeck manifest {Path}: {Message}", path, ex.Message);
                return Empty;
            }
        }
    }
}
