using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace TouchDeck.Core.Configuration;

/// <summary>
/// An action as written in JSON: a <c>type</c> discriminator plus whatever parameters that
/// type declares. The parameters are kept as raw JSON so that adding an action type never
/// requires touching the config models.
/// </summary>
[JsonConverter(typeof(ActionConfigConverter))]
public sealed record ActionConfig
{
    /// <summary>The <c>type</c> discriminator, for example <c>hotkey</c>.</summary>
    public string Type { get; init; } = "";

    /// <summary>The whole action object as written, including <see cref="Type"/>.</summary>
    public JsonElement Raw { get; init; }

    /// <summary>JSON path of this action within its file, used in validation messages.</summary>
    public string JsonPath { get; init; } = "";

    /// <summary>Looks up a parameter by name, case insensitively.</summary>
    public bool TryGetParameter(string name, out JsonElement value)
    {
        value = default;
        if (Raw.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var property in Raw.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined;
            }
        }

        return false;
    }

    /// <summary>Reads a string parameter, or returns <paramref name="fallback"/>.</summary>
    [return: NotNullIfNotNull(nameof(fallback))]
    public string? GetString(string name, string? fallback = null)
    {
        if (!TryGetParameter(name, out var value))
        {
            return fallback;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? fallback,
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => fallback,
        };
    }

    /// <summary>Reads an integer parameter, or returns <paramref name="fallback"/>.</summary>
    public int GetInt32(string name, int fallback = 0) =>
        TryGetParameter(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var i)
            ? i
            : fallback;

    /// <summary>Reads a numeric parameter, or returns <paramref name="fallback"/>.</summary>
    public double GetDouble(string name, double fallback = 0) =>
        TryGetParameter(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var d)
            ? d
            : fallback;

    /// <summary>Reads a boolean parameter, or returns <paramref name="fallback"/>.</summary>
    public bool GetBoolean(string name, bool fallback = false) =>
        TryGetParameter(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : fallback;

    /// <summary>Deserialises the whole action into a typed parameter record.</summary>
    public T? Deserialize<T>(JsonSerializerOptions options) => Raw.Deserialize<T>(options);

    /// <summary>Returns the action as an editable JSON object.</summary>
    public JsonObject ToObject()
    {
        if (Raw.ValueKind == JsonValueKind.Object
            && JsonNode.Parse(Raw.GetRawText()) is JsonObject parsed)
        {
            return parsed;
        }

        return new JsonObject { ["type"] = Type };
    }

    /// <summary>Builds an action from an edited JSON object.</summary>
    /// <param name="value">The object, which must carry a <c>type</c> property.</param>
    /// <param name="jsonPath">Where the action sits in its file, for error messages.</param>
    public static ActionConfig FromObject(JsonObject value, string jsonPath = "")
    {
        var type = value["type"]?.GetValue<string>() ?? "";

        using var document = JsonDocument.Parse(value.ToJsonString());
        return new ActionConfig
        {
            Type = type,
            Raw = document.RootElement.Clone(),
            JsonPath = jsonPath,
        };
    }

    /// <summary>Describes the action for logs and error messages.</summary>
    public override string ToString() => string.IsNullOrEmpty(JsonPath) ? Type : $"{Type} at {JsonPath}";
}

/// <summary>
/// Captures an action object verbatim and pulls the <c>type</c> discriminator out of it.
/// </summary>
public sealed class ActionConfigConverter : JsonConverter<ActionConfig>
{
    /// <inheritdoc />
    public override ActionConfig Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("An action must be a JSON object with a \"type\" property.");
        }

        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement.Clone();

        string? type = null;
        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, "type", StringComparison.OrdinalIgnoreCase))
            {
                type = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(type))
        {
            throw new JsonException("An action must have a non empty \"type\" property.");
        }

        return new ActionConfig { Type = type, Raw = root };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, ActionConfig value, JsonSerializerOptions options)
    {
        if (value.Raw.ValueKind == JsonValueKind.Undefined)
        {
            writer.WriteStartObject();
            writer.WriteString("type", value.Type);
            writer.WriteEndObject();
            return;
        }

        value.Raw.WriteTo(writer);
    }
}
