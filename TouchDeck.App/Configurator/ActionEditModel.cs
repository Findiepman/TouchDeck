using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json.Nodes;
using TouchDeck.Core.Actions;
using TouchDeck.Core.Configuration;

namespace TouchDeck.App.Configurator;

/// <summary>
/// One editable parameter of an action. The description and the kind come from the action
/// itself, so this needs no knowledge of any particular action type.
/// </summary>
public sealed class ActionParameterEditModel : ObservableObject
{
    private string? _value;
    private RelayCommand? _browse;

    /// <summary>Creates an editable parameter.</summary>
    /// <param name="parameter">What the action says about it.</param>
    /// <param name="value">The current value, written as text.</param>
    public ActionParameterEditModel(ActionParameter parameter, string? value)
    {
        Parameter = parameter;
        _value = value;
    }

    /// <summary>The metadata the action declared.</summary>
    public ActionParameter Parameter { get; }

    /// <summary>The property name as written in JSON.</summary>
    public string Name => Parameter.Name;

    /// <summary>What the parameter does.</summary>
    public string Description => Parameter.Description;

    /// <summary>Whether the action refuses to run without it.</summary>
    public bool IsRequired => Parameter.Required;

    /// <summary>The allowed values, when there is a fixed set.</summary>
    public IReadOnlyList<string> Choices => Parameter.Choices;

    /// <summary>Text shown in the box when the parameter is left empty.</summary>
    public string Placeholder => Parameter.Default is { Length: > 0 } value
        ? $"default: {value}"
        : Parameter.Required ? "required" : "optional";

    /// <summary>What kind of input control to show.</summary>
    public ActionParameterKind Kind => Parameter.Kind;

    /// <summary>True for the kinds that get a browse button.</summary>
    public bool IsPath => Kind is ActionParameterKind.FilePath or ActionParameterKind.FolderPath;

    /// <summary>True when this parameter is offered as a dropdown.</summary>
    public bool IsChoice => Kind == ActionParameterKind.Choice;

    /// <summary>True when this parameter is a tick box.</summary>
    public bool IsBoolean => Kind == ActionParameterKind.Boolean;

    /// <summary>True when this parameter is recorded by pressing the keys.</summary>
    public bool IsKeys => Kind == ActionParameterKind.Keys;

    /// <summary>True for everything that is edited as free text.</summary>
    public bool IsText => !IsChoice && !IsBoolean && !IsKeys;

    /// <summary>Opens a file or folder picker for path parameters.</summary>
    public RelayCommand BrowseCommand => _browse ??= new RelayCommand(_ => Browse());

    /// <summary>The value as text. Empty means the parameter is left out entirely.</summary>
    public string? Value
    {
        get => _value;
        set => Set(ref _value, string.IsNullOrWhiteSpace(value) ? null : value);
    }

    /// <summary>The value as a tick box, for boolean parameters.</summary>
    public bool BooleanValue
    {
        get => bool.TryParse(_value, out var parsed) && parsed;
        set => Value = value ? "true" : "false";
    }

    private void Browse()
    {
        if (Kind == ActionParameterKind.FolderPath)
        {
            var folders = new Microsoft.Win32.OpenFolderDialog { Title = $"Choose a folder for {Name}" };
            if (folders.ShowDialog() == true)
            {
                Value = folders.FolderName;
            }

            return;
        }

        var files = new Microsoft.Win32.OpenFileDialog
        {
            Title = $"Choose a file for {Name}",
            Filter = "Programs|*.exe;*.com;*.bat;*.cmd;*.lnk|All files|*.*",
            CheckFileExists = true,
        };

        if (files.ShowDialog() == true)
        {
            Value = files.FileName;
        }
    }

    /// <summary>Writes the value into a JSON object using the right JSON type.</summary>
    /// <param name="target">The object being built.</param>
    public void WriteTo(JsonObject target)
    {
        if (string.IsNullOrWhiteSpace(_value))
        {
            return;
        }

        target[Name] = Kind switch
        {
            ActionParameterKind.Number when double.TryParse(_value, NumberStyles.Any, CultureInfo.InvariantCulture, out var number)
                => JsonValue.Create(number),
            ActionParameterKind.Boolean when bool.TryParse(_value, out var flag)
                => JsonValue.Create(flag),
            _ => JsonValue.Create(_value),
        };
    }
}

/// <summary>
/// An editable action: a type picked from the registry, plus the parameters that type
/// declares. Parameters the action did not declare are kept as they were, so nothing a
/// person wrote by hand is thrown away.
/// </summary>
public sealed class ActionEditModel : ObservableObject
{
    private readonly ActionRegistry _registry;
    private readonly JsonObject _unrecognised = new();

    private IAction? _action;
    private string _type;
    private string _search = "";
    private bool _isPicking;

    /// <summary>Creates an editable action.</summary>
    /// <param name="registry">Where action types and their parameters come from.</param>
    /// <param name="config">The action as configured, or null for a new one.</param>
    public ActionEditModel(ActionRegistry registry, ActionConfig? config)
    {
        _registry = registry;

        var source = config?.ToObject() ?? new JsonObject();
        _type = config?.Type ?? registry.All.FirstOrDefault()?.Type ?? "";

        LoadParameters(source);
    }

    /// <summary>Every action type that can be chosen, in alphabetical order.</summary>
    public IReadOnlyList<IAction> AvailableActions =>
        _registry.All.OrderBy(a => a.Title, StringComparer.CurrentCultureIgnoreCase).ToArray();

    /// <summary>The action types matching <see cref="Search"/>.</summary>
    public IReadOnlyList<IAction> MatchingActions => AvailableActions
        .Where(a => _search.Length == 0
                    || a.Title.Contains(_search, StringComparison.CurrentCultureIgnoreCase)
                    || a.Type.Contains(_search, StringComparison.OrdinalIgnoreCase)
                    || a.Description.Contains(_search, StringComparison.CurrentCultureIgnoreCase))
        .ToArray();

    /// <summary>What has been typed into the action search box.</summary>
    public string Search
    {
        get => _search;
        set
        {
            _search = value ?? "";
            RaiseQuiet(nameof(Search));
            RaiseQuiet(nameof(MatchingActions));
        }
    }

    /// <summary>True while the list of action types is open.</summary>
    public bool IsPicking
    {
        get => _isPicking;
        set
        {
            if (_isPicking != value)
            {
                _isPicking = value;
                RaiseQuiet(nameof(IsPicking));
                RaiseQuiet(nameof(IsNotPicking));
            }
        }
    }

    /// <summary>True while the chosen action and its parameters are shown.</summary>
    public bool IsNotPicking => !_isPicking;

    /// <summary>The name of the chosen action, for the row you click to change it.</summary>
    public string Title => _action?.Title ?? _type;

    /// <summary>The chosen action type.</summary>
    public string Type
    {
        get => _type;
        set
        {
            if (Set(ref _type, value))
            {
                LoadParameters(BuildObject());
                Raise(nameof(SelectedAction));
                Raise(nameof(Description));
                Raise(nameof(Title));
                Raise(nameof(Parameters));
            }
        }
    }

    /// <summary>The chosen action, for binding the dropdown.</summary>
    public IAction? SelectedAction
    {
        get => _action;
        set
        {
            if (value is not null)
            {
                Type = value.Type;
                IsPicking = false;
            }
        }
    }

    /// <summary>What the chosen action does.</summary>
    public string Description => _action?.Description ?? "This action type is not installed.";

    /// <summary>The parameters of the chosen action.</summary>
    public ObservableCollection<ActionParameterEditModel> Parameters { get; } = new();

    /// <summary>Builds the config record for this action.</summary>
    public ActionConfig ToConfig(string jsonPath = "") => ActionConfig.FromObject(BuildObject(), jsonPath);

    private JsonObject BuildObject()
    {
        var result = new JsonObject { ["type"] = _type };

        foreach (var parameter in Parameters)
        {
            parameter.WriteTo(result);
        }

        foreach (var extra in _unrecognised)
        {
            if (result[extra.Key] is null)
            {
                result[extra.Key] = extra.Value?.DeepClone();
            }
        }

        return result;
    }

    /// <summary>
    /// Rebuilds the parameter list for the current type, carrying over any values that still
    /// apply and stashing the rest so switching type and back does not lose them.
    /// </summary>
    private void LoadParameters(JsonObject source)
    {
        _registry.TryGet(_type, out _action);

        Parameters.Clear();
        _unrecognised.Clear();

        var declared = _action?.Parameters ?? Array.Empty<ActionParameter>();

        foreach (var parameter in declared)
        {
            var existing = source
                .FirstOrDefault(p => string.Equals(p.Key, parameter.Name, StringComparison.OrdinalIgnoreCase))
                .Value;

            var model = new ActionParameterEditModel(parameter, AsText(existing));
            Adopt(model);
            Parameters.Add(model);
        }

        foreach (var property in source)
        {
            if (string.Equals(property.Key, "type", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (declared.Any(p => string.Equals(p.Name, property.Key, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            _unrecognised[property.Key] = property.Value?.DeepClone();
        }
    }

    private static string? AsText(JsonNode? node) => node switch
    {
        null => null,
        JsonValue value when value.TryGetValue<string>(out var text) => text,
        _ => node.ToJsonString().Trim('"'),
    };
}
