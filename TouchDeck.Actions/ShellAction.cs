using TouchDeck.Core.Abstractions;
using TouchDeck.Core.Actions;
using TouchDeck.Core.Variables;

namespace TouchDeck.Actions;

/// <summary>
/// Runs a shell command. It can keep what the command printed in a variable, which other
/// buttons can then show or test.
/// <code>
/// { "type": "shell", "command": "Get-Date -Format t", "shell": "powerShell",
///   "hidden": true, "captureOutputTo": "clock" }
/// </code>
/// </summary>
public sealed class ShellAction : IAction
{
    /// <inheritdoc />
    public string Type => "shell";

    /// <inheritdoc />
    public string Title => "Run a command";

    /// <inheritdoc />
    public string Description => "Runs a shell command, and can keep what it printed in a variable.";

    /// <inheritdoc />
    public IReadOnlyList<ActionParameter> Parameters { get; } = new[]
    {
        ActionParameter.Require("command", ActionParameterKind.MultilineText, "The command line to run."),
        ActionParameter.OneOf("shell", "Which shell runs it.", false, ActionContext.ChoiceList<ShellKind>()),
        ActionParameter.Optional("hidden", ActionParameterKind.Boolean, "Hide the console window.", "true"),
        ActionParameter.Optional("captureOutputTo", ActionParameterKind.Text, "Name of a variable to keep the output in."),
    };

    /// <inheritdoc />
    public async Task ExecuteAsync(ActionContext ctx, CancellationToken ct)
    {
        var into = ctx.Action.GetString("captureOutputTo");

        var output = await ctx.Service<IShellRunner>().RunAsync(
            ctx.RequireString("command"),
            ctx.RequireOneOf("shell", ShellKind.PowerShell),
            ctx.Action.GetBoolean("hidden", true),
            captureOutput: into is { Length: > 0 },
            ct).ConfigureAwait(false);

        if (into is { Length: > 0 })
        {
            ctx.Variables.Set(VariableName.Of(into), output, VariableScope.Session);
        }
    }
}

/// <summary>
/// Accepts a variable written either way. People write <c>var.mode</c> in one place and
/// <c>mode</c> in another, and both should mean the same variable.
/// </summary>
internal static class VariableName
{
    /// <summary>Strips a leading <c>var.</c> so both spellings land on the same name.</summary>
    /// <param name="written">The name as the config wrote it.</param>
    public static string Of(string written) =>
        written.StartsWith("var.", StringComparison.OrdinalIgnoreCase) ? written[4..] : written;
}
