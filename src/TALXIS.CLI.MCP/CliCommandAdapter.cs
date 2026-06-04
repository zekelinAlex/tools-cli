using System.Reflection;
using System.Text.Json;

namespace TALXIS.CLI.MCP
{
    public class CliCommandAdapter
    {
        public List<string> BuildCliArgs(string toolName, IReadOnlyDictionary<string, JsonElement>? arguments)
        {
            // Split tool name into CLI command parts
            var cliArgs = ParseToolName(toolName);
            var commandType = new McpToolRegistry().FindCommandTypeByToolName(toolName);
            var positionalArgs = new List<string>();
            var optionArgs = new List<string>();

            if (arguments != null && commandType != null)
            {
                var positionalProps = GetPositionalProperties(commandType);
                var optionProps = GetOptionProperties(commandType);
                var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Handle CliArgument (positional) in declaration order.
                foreach (var prop in positionalProps)
                {
                    var propName = prop.Attr?.Name ?? prop.Prop.Name;
                    if (!TryGetCaseInsensitive(arguments, propName, out var matchedKey, out var value))
                        continue;
                    if (value.ValueKind == JsonValueKind.Null)
                        continue;

                    AddArgumentValues(positionalArgs, value, null);
                    consumed.Add(matchedKey);
                }

                // Handle CliOption (named).
                foreach (var entry in arguments)
                {
                    if (consumed.Contains(entry.Key))
                        continue;
                    if (entry.Value.ValueKind == JsonValueKind.Null)
                        continue;

                    var optionName = ResolveOptionName(entry.Key, optionProps);
                    AddArgumentValues(optionArgs, entry.Value, $"--{optionName}=");
                }
            }
            else if (arguments != null)
            {
                // Fallback: treat all as options
                foreach (var entry in arguments)
                {
                    if (entry.Value.ValueKind != JsonValueKind.Null)
                    {
                        AddArgumentValues(optionArgs, entry.Value, $"--{entry.Key}=");
                    }
                }
            }

            cliArgs.AddRange(positionalArgs);
            cliArgs.AddRange(optionArgs);
            return cliArgs;
        }

        private static bool TryGetCaseInsensitive(
            IReadOnlyDictionary<string, JsonElement> arguments,
            string targetKey,
            out string matchedKey,
            out JsonElement value)
        {
            foreach (var pair in arguments)
            {
                if (string.Equals(pair.Key, targetKey, StringComparison.OrdinalIgnoreCase))
                {
                    matchedKey = pair.Key;
                    value = pair.Value;
                    return true;
                }
            }
            matchedKey = string.Empty;
            value = default;
            return false;
        }

        private static string ResolveOptionName(
            string inputKey,
            IReadOnlyList<(PropertyInfo Prop, DotMake.CommandLine.CliOptionAttribute? Attr)> optionProps)
        {
            // Try to find a matching declared option. We accept three
            // representations interchangeably so MCP clients don't have to
            // mirror the C# casing exactly:
            //   - the explicit [CliOption(Name = "--xxx")] (stripped of "--")
            //   - the C# property name as-is (PascalCase)
            //   - the camelCase form DotMake auto-generates from PascalCase
            // The first hit wins, case-insensitive.
            foreach (var opt in optionProps)
            {
                var explicitName = opt.Attr?.Name?.TrimStart('-');
                var propName = opt.Prop.Name;
                var camelName = ToCamelCase(propName);

                if ((explicitName is not null && string.Equals(explicitName, inputKey, StringComparison.OrdinalIgnoreCase))
                    || string.Equals(propName, inputKey, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(camelName, inputKey, StringComparison.OrdinalIgnoreCase))
                {
                    return explicitName ?? camelName;
                }
            }
            // Unknown key — pass through. The CLI will produce a real error
            // rather than us silently dropping a typo'd flag.
            return inputKey;
        }

        private static string ToCamelCase(string name)
            => string.IsNullOrEmpty(name)
               ? name
               : char.ToLowerInvariant(name[0]) + name[1..];

        // Helper: Split tool name by underscores
        private static List<string> ParseToolName(string toolName)
        {
            return toolName.Split('_', StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        // Helper: Get positional (CliArgument) properties
        private static List<(PropertyInfo Prop, DotMake.CommandLine.CliArgumentAttribute? Attr)> GetPositionalProperties(Type commandType)
        {
            return commandType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => (Prop: p, Attr: p.GetCustomAttribute(typeof(DotMake.CommandLine.CliArgumentAttribute)) as DotMake.CommandLine.CliArgumentAttribute))
                .Where(x => x.Attr != null)
                .ToList();
        }

        // Helper: Get option (CliOption) properties
        private static List<(PropertyInfo Prop, DotMake.CommandLine.CliOptionAttribute? Attr)> GetOptionProperties(Type commandType)
        {
            return commandType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => (Prop: p, Attr: p.GetCustomAttribute(typeof(DotMake.CommandLine.CliOptionAttribute)) as DotMake.CommandLine.CliOptionAttribute))
                .Where(x => x.Attr != null || x.Prop != null)
                .ToList();
        }

        // Helper: Add argument values to target list, handling arrays and single values
        // For array options (e.g., List<string>), emit repeated --Option value pairs (not --Option=value)
        // For single values, emit --Option value or --Option=value as appropriate
        private static void AddArgumentValues(List<string> target, JsonElement value, string? prefix)
        {
            if (value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in value.EnumerateArray())
                {
                    AddSingleArgumentValue(target, item, prefix);
                }
            }
            else
            {
                AddSingleArgumentValue(target, value, prefix);
            }
        }

        // Adds a single argument value to the target list, handling prefix logic
        private static void AddSingleArgumentValue(List<string> target, JsonElement value, string? prefix)
        {
            if (prefix != null && prefix.EndsWith("="))
            {
                // e.g., --Param value
                var opt = prefix.TrimEnd('=');
                target.Add(opt);
                target.Add(value.ToString());
            }
            else if (prefix != null)
            {
                // e.g., --flagTrue
                target.Add(prefix + value);
            }
            else
            {
                // Just the value
                target.Add(value.ToString());
            }
        }

        public JsonElement BuildInputSchema(Type commandType)
        {
            var properties = commandType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var required = new List<string>();
            var schemaProperties = new Dictionary<string, object?>();

            // A single throwaway instance lets us read field-initializer defaults
            // (e.g. WorkspaceValidate's Path = ".") so positional args that the CLI
            // treats as optional aren't advertised as required in the MCP schema.
            var defaults = TryCreateInstance(commandType);

            // Add CliArgument (positional) properties
            foreach (var prop in properties)
            {
                var argAttr = prop.GetCustomAttribute(typeof(DotMake.CommandLine.CliArgumentAttribute)) as DotMake.CommandLine.CliArgumentAttribute;
                if (argAttr != null)
                {
                    var type = GetJsonSchemaType(prop.PropertyType, out var itemsSchema);
                    var argName = argAttr.Name ?? prop.Name;
                    var schemaProp = new Dictionary<string, object?>
                    {
                        ["type"] = type,
                        ["description"] = argAttr.Description
                    };
                    if (itemsSchema != null)
                        schemaProp["items"] = itemsSchema;
                    schemaProperties[argName] = schemaProp;

                    if (IsPositionalRequired(prop, argAttr, defaults))
                        required.Add(argName);
                }
            }

            // Add CliOption (named) properties
            // Skip --format: MCP subprocess mode auto-detects JSON (stdout is redirected).
            // Exposing it in the tool schema would confuse LLMs without adding value.
            var hiddenFromMcp = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "--format", "Format" };
            foreach (var prop in properties)
            {
                var optionAttr = prop.GetCustomAttribute(typeof(DotMake.CommandLine.CliOptionAttribute)) as DotMake.CommandLine.CliOptionAttribute;
                if (optionAttr != null)
                {
                    if (hiddenFromMcp.Contains(optionAttr.Name ?? prop.Name))
                        continue;
                    var type = GetJsonSchemaType(prop.PropertyType, out var itemsSchema);
                    var optionName = (optionAttr.Name ?? prop.Name).TrimStart('-');
                    var schemaProp = new Dictionary<string, object?>
                    {
                        ["type"] = type,
                        ["description"] = optionAttr.Description
                    };
                    if (itemsSchema != null)
                        schemaProp["items"] = itemsSchema;
                    schemaProperties[optionName] = schemaProp;
                    if (optionAttr.Required)
                        required.Add(optionName);
                }
            }

            var schema = new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = schemaProperties,
                ["required"] = required
            };
            return JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(schema));
        }

        private static bool IsPositionalRequired(
            PropertyInfo prop,
            DotMake.CommandLine.CliArgumentAttribute argAttr,
            object? defaults)
        {
            // Explicit opt-in always wins.
            if (argAttr.Required)
                return true;
            // Nullable (string?, int?) → optional by nature.
            if (IsNullable(prop))
                return false;
            // A reference-type property carrying a field-initializer default (e.g. ".")
            // is optional — the CLI substitutes the default when the caller omits it.
            if (!prop.PropertyType.IsValueType && defaults != null && prop.GetValue(defaults) is not null)
                return false;
            // Non-nullable, no detectable default → genuinely required.
            return true;
        }

        // Helper: true when the property accepts null (Nullable<T> or a nullable reference type).
        private static bool IsNullable(PropertyInfo prop)
        {
            if (Nullable.GetUnderlyingType(prop.PropertyType) != null)
                return true;
            var info = new NullabilityInfoContext().Create(prop);
            return info.WriteState == NullabilityState.Nullable
                || info.ReadState == NullabilityState.Nullable;
        }

        // Helper: best-effort instantiation just to read default property values.
        // Command ctors only run field initializers (logger, defaults), so this is cheap;
        // if a type can't be constructed we fall back to treating defaults as unknown.
        private static object? TryCreateInstance(Type commandType)
        {
            try
            {
                return Activator.CreateInstance(commandType);
            }
            catch
            {
                return null;
            }
        }

        // Helper: Get JSON schema type for a .NET type
        private static string GetJsonSchemaType(Type type, out object? itemsSchema)
        {
            itemsSchema = null;
            if (type == typeof(bool))
                return "boolean";
            if (type == typeof(string))
                return "string";
            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(type) && type != typeof(string))
            {
                // Try to get the element type
                var elementType = type.IsArray ? type.GetElementType() :
                    (type.IsGenericType ? type.GetGenericArguments().FirstOrDefault() : null);
                itemsSchema = new Dictionary<string, object?> { ["type"] = elementType == typeof(bool) ? "boolean" : "string" };
                return "array";
            }
            return "string";
        }
    }
}
