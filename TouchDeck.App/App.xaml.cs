using System.Windows;
using Serilog;
using TouchDeck.App.Bootstrap;
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
    private readonly LoggingSetup _logging = new();

    private SingleInstanceGuard? _instance;
    private ConfigService? _configService;
    private SendInputInjector? _injector;
    private DeckViewModel? _viewModel;
    private DeckWindow? _window;
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

        var paths = options.ConfigDirectory is { Length: > 0 } directory
            ? new ConfigPaths(directory)
            : ConfigPaths.FromEnvironment();

        var created = StarterConfig.EnsureExists(paths);

        _logger = _logging.Start(paths, LoggingSetup.Peek(paths));
        StyleTranslator.Logger = _logger;

        _logger.Information("TouchDeck starting. Config root {Root}", paths.Root);
        foreach (var file in created)
        {
            _logger.Information("Created starter file {Path}", file);
        }

        InstallGlobalExceptionHandlers();

        var registry = ActionRegistry.Scan(_logger, typeof(TouchDeck.Actions.HotkeyAction).Assembly);
        var loader = new ConfigLoader(paths, registry.KnownTypes);

        _configService = new ConfigService(paths, loader, _logger);
        var configuration = _configService.Load();

        _instance = SingleInstanceGuard.Acquire(configuration.App.Behaviour.SingleInstance);
        if (!_instance.IsPrimary)
        {
            _logger.Information("Another TouchDeck is already running, so this one is exiting.");
            Shutdown();
            return;
        }

        _instance.ListenForQuitRequest(() => Dispatcher.BeginInvoke(() => Shutdown()));

        _injector = new SendInputInjector(_logger);
        var services = new ServiceRegistry()
            .Add<IInputInjector>(_injector)
            .Add<IProcessLauncher>(new ShellProcessLauncher(_logger));

        var dispatcher = new ActionDispatcher(registry, services, _logger);
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
            _logger.Error(args.Exception, "Unhandled exception on the UI thread.");
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
