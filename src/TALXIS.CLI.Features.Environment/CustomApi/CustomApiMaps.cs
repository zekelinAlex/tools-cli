namespace TALXIS.CLI.Features.Environment.CustomApi;

/// <summary>
/// Value maps for Custom API metadata: binding types, parameter/property
/// type codes, and their OpenAPI schema equivalents.
/// </summary>
internal static class CustomApiMaps
{
    internal static readonly IReadOnlyDictionary<string, int> BindingTypes =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["global"] = 0,
            ["entity"] = 1,
            ["entitycollection"] = 2,
        };

    internal static readonly IReadOnlyDictionary<string, int> ProcessingStepTypes =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["none"] = 0,
            ["async"] = 1,
            ["sync-and-async"] = 2,
        };

    internal static readonly IReadOnlyDictionary<string, int> ParameterTypes =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["boolean"] = 0,
            ["datetime"] = 1,
            ["decimal"] = 2,
            ["entity"] = 3,
            ["entitycollection"] = 4,
            ["entityreference"] = 5,
            ["float"] = 6,
            ["integer"] = 7,
            ["money"] = 8,
            ["picklist"] = 9,
            ["string"] = 10,
            ["stringarray"] = 11,
            ["guid"] = 12,
        };

    internal static string BindingTypeName(int code) => code switch
    {
        0 => "global",
        1 => "entity",
        2 => "entitycollection",
        _ => code.ToString(),
    };

    internal static string ParameterTypeName(int code) =>
        ParameterTypes.FirstOrDefault(kv => kv.Value == code).Key ?? code.ToString();

    /// <summary>Maps a Custom API type code to an OpenAPI (type, format, items-type) triple.</summary>
    internal static (string Type, string? Format, string? ItemsType) ToOpenApiSchema(int code) => code switch
    {
        0 => ("boolean", null, null),
        1 => ("string", "date-time", null),
        2 => ("number", "decimal", null),
        3 => ("object", null, null),
        4 => ("array", null, "object"),
        5 => ("object", null, null),
        6 => ("number", "float", null),
        7 => ("integer", "int32", null),
        8 => ("number", "decimal", null),
        9 => ("integer", "int32", null),
        10 => ("string", null, null),
        11 => ("array", null, "string"),
        12 => ("string", "uuid", null),
        _ => ("string", null, null),
    };

    /// <summary>
    /// Parses a <c>name:type[:optional]</c> parameter definition (e.g. <c>Quantity:integer</c>,
    /// <c>Comment:string:optional</c>). Returns null with an error message on bad input.
    /// </summary>
    internal static (string Name, int TypeCode, bool Optional)? ParseParameterSpec(string spec, out string? error)
    {
        error = null;
        var parts = spec.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length is < 2 or > 3 || parts[0].Length == 0)
        {
            error = $"Invalid parameter spec '{spec}'. Expected format: name:type[:optional].";
            return null;
        }

        if (!ParameterTypes.TryGetValue(parts[1], out int typeCode))
        {
            error = $"Unknown parameter type '{parts[1]}' in '{spec}'. Valid types: {string.Join(", ", ParameterTypes.Keys)}.";
            return null;
        }

        bool optional = false;
        if (parts.Length == 3)
        {
            if (!parts[2].Equals("optional", StringComparison.OrdinalIgnoreCase))
            {
                error = $"Invalid modifier '{parts[2]}' in '{spec}'. Only 'optional' is allowed.";
                return null;
            }
            optional = true;
        }

        return (parts[0], typeCode, optional);
    }
}
