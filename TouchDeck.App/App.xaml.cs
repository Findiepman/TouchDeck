using System.Windows;
using Serilog;
using TouchDeck.App.Bootstrap;
using TouchDeck.App.Configurator;
using TouchDeck.App.Rendering;
using TouchDeck.App.ViewModels;
using TouchDeck.App.Views;
using TouchDeck.Core.Abstractions;
using TouchDeck.Core.Actions;
using TouchDeck.Core.Configuration;
using TouchDeck.Platform.Input;
using TouchDeck.Platform.Process;

namespace TouchDeck.App;

/// <summary>
/// Wires the deck together and keeps it alive. Nothing here decides what a button does; it
/// only hands the pieces to each other.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// A failure inside a layout pass repeats on every pass. Logging each one filled a disk
    /// once, so repeats inside this window are counted rather than written.
    /// </summary>
    private static readonly TimeSpan RepeatWindow = TimeSpan.FromSeconds(5);

    /// <summary>How many identical failures are written before the rest are just counted.</summary>
    private const int RepeatsBeforeQuietening = 5;

    private readonly LoggingSetup _logging = new();

    private string _lastFailure = "";
    private DateTime _lastFailureAt = DateTime.MinValue;
    private int _repeatCount;

    private SingleInstanceGuard? _instance;
    private ConfigService? _configService;
    private SendInputInjector? _injector;
    private DeckViewModel? _viewModel;
    private DeckWindow? _window;
    private ConfiguratorWindow? _configurator;
    private ConfigPaths? _paths;
    private ActionRegistry? _registry;
    private ILogger _logger = Serilog.Core.Logger.None;

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var options = CommandLineOptions.Parse(e.Args);

        if (options.Quit)
        {
            SingleInstanceGuard.RequestQuit();
            Shutdown();
            return;
        }

        // A running deck opens the config center itself, so there is only ever one of each.
        if (options.Configure && SingleInstanceGuard.IsDeckRunning())
        {
            SingleInstanceGuard.RequestConfigure();
            Shutdown();
            return;
        }

        _paths = options.ConfigDirectory is { Length: > 0 } directory
            ? new ConfigPaths(directory)
            : ConfigPaths.FromEnvironment();

        var created = StarterConfig.EnsureExists(_paths);

        _logger = _logging.Start(_paths, LoggingSetup.Peek(_paths));
        StyleTranslator.Logger = _logger;

        _logger.Information("TouchDeck starting. Config root {Root}", _paths.Root);
        foreach (var file in created)
        {
            _logger.Information("Created starter file {Path}", file);
        }

        InstallGlobalExceptionHandlers();

        _registry = ActionRegistry.Scan(_logger, typeof(TouchDeck.Actions.HotkeyAction).Assembly);

        // With no deck running, --configure gives just the editor and no panel.
        if (options.Configure)
        {
            ShowConfigurator(exitWhenClosed: true);
            return;
        }

        var loader = new ConfigLoader(_paths, _registry.KnownTypes);
        _configService = new ConfigService(_paths, loader, _logger);
        var configuration = _configService.Load();

        _instance = SingleInstanceGuard.Acquire(configuration.App.Behaviour.SingleInstance);
        if (!_instance.IsPrimary)
        {
            _logger.Information("Another TouchDeck is already running, so this one is exiting.");
            Shutdown();
            return;
        }

        _instance.ListenForQuitRequest(() => Dispatcher.BeginInvoke(() => Shutdown()));
        _instance.ListenForConfigureRequest(() => Dispatcher.BeginInvoke(() => ShowConfigurator(exitWhenClosed: false)));

        _injector = new SendInputInjector(_logger);
        var services = new ServiceRegistry()
            .Add<IInputInjector>(_injector)
            .Add<IProcessLauncher>(new ShellProcessLauncher(_logger));

        var dispatcher = new ActionDispatcher(_registry, services, _logger);
        dispatcher.Failed += (_, failure) => _logger.Warning("Action failed: {Message}", failure.Message);

        _viewModel = new DeckViewModel(dispatcher, _logger);
        _viewModel.Apply(configuration);

        _window = new DeckWindow(_viewModel, _logger);
        _window.Show();

        _configService.Changed += OnConfigurationChanged;
        if (configuration.App.Behaviour.HotReload)
        {
            _configService.StartWatching();
        }
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        _logger.Information("TouchDeck stopping.");

        // Whatever happens, nothing stays held down on the user's keyboard.
        _injector?.ReleaseAllHeldKeys();

        if (_configService is not null)
        {
            _configService.Changed -= OnConfigurationChanged;
            _configService.Dispose();
        }

        _instance?.Dispose();
        _logging.Dispose();

        base.OnExit(e);
    }

    /// <summary>
    /// Opens the config center, or brings an already open one forward. It edits the same
    /// files the deck watches, so saving there updates the panel by itself.
    /// </summary>
    /// <param name="exitWhenClosed">True when the editor is the only reason this process is running.</param>
    private void ShowConfigurator(bool exitWhenClosed)
    {
        if (_paths is null || _registry is null)
        {
            return;
        }

        if (_configurator is { IsLoaded: true })
        {
            _configurator.Activate();
            return;
        }

        try
        {
            _configurator = new ConfiguratorWindow(new ConfiguratorViewModel(_paths, _registry, _logger));
            _configurator.Closed += (_, _) =>
            {
                _configurator = null;
                if (exitWhenClosed)
                {
                    Shutdown();
                }
            };

            _configurator.Show();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "The config center could not be opened.");

            if (exitWhenClosed)
            {
                Shutdown();
            }
        }
    }

    /// <summary>Writes a failure, but stops repeating itself when the same one keeps arriving.</summary>
    /// <param name="exception">What went wrong.</param>
    /// <param name="message">The line to write.</param>
    private void LogThrottled(Exception exception, string message)
    {
        var signature = exception.GetType().FullName + exception.Message;
        var now = DateTime.UtcNow;

        if (signature == _lastFailure && now - _lastFailureAt < RepeatWindow)
        {
            _repeatCount++;

            if (_repeatCount == RepeatsBeforeQuietening)
            {
                _logger.Error("The failure above is repeating. Further repeats are not being written.");
            }

            if (_repeatCount >= RepeatsBeforeQuietening)
            {
                _lastFailureAt = now;
                return;
            }
        }
        else
        {
            if (_repeatCount >= RepeatsBeforeQuietening)
            {
                _logger.Error("That failure repeated {Count} times in total.", _repeatCount);
            }

            _repeatCount = 0;
        }

        _lastFailure = signature;
        _lastFailureAt = now;
        _logger.Error(exception, "{Message}", message);
    }

    private void OnConfigurationChanged(object? sender, DeckConfiguration configuration) =>
        Dispatcher.BeginInvoke(() =>
        {
            _logging.Apply(configuration.App.Logging);
            _viewModel?.Apply(configuration);
        });

    /// <summary>
    /// Every unhandled failure is logged and swallowed. A deck that disappears is worse than
    /// a deck with one button that did not work.
    /// </summary>
    private void InstallGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            LogThrottled(args.Exception, "Unhandled exception on the UI thread.");
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            _logger.Error(args.ExceptionObject as Exception, "Unhandled exception on a background thread.");

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            _logger.Error(args.Exception, "A task failed with nobody watching.");
            args.SetObserved();
        };
    }
}
