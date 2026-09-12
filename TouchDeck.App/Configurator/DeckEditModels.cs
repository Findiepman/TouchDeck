using System.Collections.ObjectModel;
using TouchDeck.Core.Actions;
using TouchDeck.Core.Configuration;

namespace TouchDeck.App.Configurator;

/// <summary>An editable button.</summary>
public sealed class ButtonEditModel : ObservableObject
{
    private readonly ButtonConfig _original;

    private int _col;
    private int _row;
    private int _colSpan;
    private int _rowSpan;
    private string? _label;
    private LabelPosition? _labelPosition;
    private ActionEditModel _action;

    /// <summary>Creates an editable copy of a button.</summary>
    /// <param name="registry">Where action types come from.</param>
    /// <param name="button">The button as configured.</param>
    public ButtonEditModel(ActionRegistry registry, ButtonConfig button)
    {
        _original = button;
        _col = button.Col;
        _row = button.Row;
        _colSpan = button.SpanColumns;
        _rowSpan = button.SpanRows;
        _label = button.Label;
        _labelPosition = button.LabelPosition;

        Style = new StyleEditModel(button.Style);
        _action = new ActionEditModel(registry, button.Action);

        Adopt(Style);
        Adopt(_action);
    }

    /// <summary>Style overrides for this button alone.</summary>
    public StyleEditModel Style { get; }

    /// <summary>Every label position, for the dropdown.</summary>
    public static IReadOnlyList<Core.Configuration.LabelPosition?> LabelPositions => StyleEditModel.LabelPositions;

    public int Col
    {
        get => _col;
        set => Set(ref _col, Math.Max(0, value));
    }

    public int Row
    {
        get => _row;
        set => Set(ref _row, Math.Max(0, value));
    }

    public int ColSpan
    {
        get => _colSpan;
        set => Set(ref _colSpan, Math.Max(1, value));
    }

    public int RowSpan
    {
        get => _rowSpan;
        set => Set(ref _rowSpan, Math.Max(1, value));
    }

    /// <summary>The text drawn on the button.</summary>
    public string? Label
    {
        get => _label;
        set => Set(ref _label, value);
    }

    /// <summary>Where the label sits, or null to follow the theme.</summary>
    public LabelPosition? LabelPosition
    {
        get => _labelPosition;
        set => Set(ref _labelPosition, value);
    }

    /// <summary>What happens when the button is pressed.</summary>
    public ActionEditModel Action
    {
        get => _action;
        private set => Set(ref _action, value);
    }

    /// <summary>What to show in lists when the button has no label.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(_label) ? $"({_col}, {_row})" : _label;

    /// <summary>Builds the config record, keeping the parts the editor does not cover yet.</summary>
    public ButtonConfig ToConfig() => _original with
    {
        Col = _col,
        Row = _row,
        ColSpan = _colSpan == 1 ? null : _colSpan,
        RowSpan = _rowSpan == 1 ? null : _rowSpan,
        Label = string.IsNullOrWhiteSpace(_label) ? null : _label,
        LabelPosition = _labelPosition,
        Style = Style.ToConfig(),
        Action = Action.ToConfig(),
    };
}

/// <summary>An editable page of buttons.</summary>
public sealed class PageEditModel : ObservableObject
{
    private readonly Page _original;

    private string _id;
    private string? _name;
    private bool _isFolder;

    /// <summary>Creates an editable copy of a page.</summary>
    /// <param name="registry">Where action types come from.</param>
    /// <param name="page">The page as configured.</param>
    public PageEditModel(ActionRegistry registry, Page page)
    {
        _original = page;
        _id = page.Id;
        _name = page.Name;
        _isFolder = page.IsFolder;

        foreach (var button in page.Buttons)
        {
            Add(new ButtonEditModel(registry, button));
        }

        Buttons.CollectionChanged += (_, _) => Raise(nameof(Buttons));
    }

    /// <summary>The buttons on this page.</summary>
    public ObservableCollection<ButtonEditModel> Buttons { get; } = new();

    public string Id
    {
        get => _id;
        set => Set(ref _id, value);
    }

    public string? Name
    {
        get => _name;
        set => Set(ref _name, value);
    }

    /// <summary>Folders are reached through a button rather than by swiping.</summary>
    public bool IsFolder
    {
        get => _isFolder;
        set => Set(ref _isFolder, value);
    }

    /// <summary>What to show in the tree.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(_name) ? _id : _name;

    /// <summary>Adds a button and starts listening to its edits.</summary>
    /// <param name="button">The button to add.</param>
    public void Add(ButtonEditModel button)
    {
        Adopt(button);
        Buttons.Add(button);
    }

    /// <summary>Builds the config record.</summary>
    public Page ToConfig() => _original with
    {
        Id = _id,
        Name = string.IsNullOrWhiteSpace(_name) ? null : _name,
        IsFolder = _isFolder,
        Buttons = Buttons.Select(b => b.ToConfig()).ToArray(),
    };
}

/// <summary>An editable profile.</summary>
public sealed class ProfileEditModel : ObservableObject
{
    private readonly Profile _original;

    private string _id;
    private string? _name;
    private string? _theme;
    private int _columns;
    private int _rows;
    private double? _cellAspect;
    private bool _fill;

    /// <summary>Creates an editable copy of a profile.</summary>
    /// <param name="registry">Where action types come from.</param>
    /// <param name="profile">The profile as configured.</param>
    public ProfileEditModel(ActionRegistry registry, Profile profile)
    {
        _original = profile;
        _id = profile.Id;
        _name = profile.Name;
        _theme = profile.Theme;
        _columns = profile.Grid.Columns;
        _rows = profile.Grid.Rows;
        _cellAspect = profile.Grid.CellAspect;
        _fill = profile.Grid.Fill;

        foreach (var page in profile.Pages)
        {
            Add(new PageEditModel(registry, page));
        }

        Pages.CollectionChanged += (_, _) => Raise(nameof(Pages));
    }

    /// <summary>The pages in this profile.</summary>
    public ObservableCollection<PageEditModel> Pages { get; } = new();

    /// <summary>The file this profile is stored in, empty for one that has never been saved.</summary>
    public string SourceFile => _original.SourceFile;

    public string Id
    {
        get => _id;
        set => Set(ref _id, value);
    }

    public string? Name
    {
        get => _name;
        set
        {
            if (Set(ref _name, value))
            {
                Raise(nameof(DisplayName));
            }
        }
    }

    /// <summary>Name of a theme, or null for the built in defaults.</summary>
    public string? Theme
    {
        get => _theme;
        set => Set(ref _theme, string.IsNullOrWhiteSpace(value) ? null : value);
    }

    public int Columns
    {
        get => _columns;
        set => Set(ref _columns, Math.Clamp(value, 1, 32));
    }

    public int Rows
    {
        get => _rows;
        set => Set(ref _rows, Math.Clamp(value, 1, 32));
    }

    /// <summary>Cell width divided by height, or null for square cells.</summary>
    public double? CellAspect
    {
        get => _cellAspect;
        set => Set(ref _cellAspect, value is > 0 ? value : null);
    }

    /// <summary>Stretch cells to fill the screen instead of keeping their shape.</summary>
    public bool Fill
    {
        get => _fill;
        set => Set(ref _fill, value);
    }

    /// <summary>What to show in the tree.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(_name) ? _id : _name;

    /// <summary>Adds a page and starts listening to its edits.</summary>
    /// <param name="page">The page to add.</param>
    public void Add(PageEditModel page)
    {
        Adopt(page);
        Pages.Add(page);
    }

    /// <summary>Builds the config record.</summary>
    public Profile ToConfig() => _original with
    {
        Id = _id,
        Name = string.IsNullOrWhiteSpace(_name) ? null : _name,
        Theme = _theme,
        Grid = new GridConfig
        {
            Columns = _columns,
            Rows = _rows,
            CellAspect = _cellAspect,
            Fill = _fill,
        },
        Pages = Pages.Select(p => p.ToConfig()).ToArray(),
    };
}
