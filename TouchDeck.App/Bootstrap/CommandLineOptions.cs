namespace TouchDeck.App.Bootstrap;

/// <summary>
/// The switches the executable accepts. There are deliberately very few: everything that
/// shapes the deck belongs in the config files, not on a command line.
/// </summary>
/// <param name="ConfigDirectory">Overrides the config root, mostly useful for trying a setup out.</param>
/// <param name="Quit">Asks a running instance to exit, then exits.</param>
/// <param name="Configure">Opens the config center instead of, or alongside, the panel.</param>
public sealed record CommandLineOptions(string? ConfigDirectory, bool Quit, bool Configure)
{
    /// <summary>Parses the arguments the process was started with.</summary>
    /// <param name="args">Arguments, without the executable name.</param>
    public static CommandLineOptions Parse(IReadOnlyList<string> args)
    {
        string? configDirectory = null;
        var quit = false;
        var configure = false;

        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i].TrimStart('-', '/').ToLowerInvariant())
            {
                case "config" when i + 1 < args.Count:
                    configDirectory = args[++i];
                    break;
                case "quit":
                    quit = true;
                    break;
                case "configure":
                case "config-center":
                case "settings":
                    configure = true;
                    break;
            }
        }

        return new CommandLineOptions(configDirectory, quit, configure);
    }
}
