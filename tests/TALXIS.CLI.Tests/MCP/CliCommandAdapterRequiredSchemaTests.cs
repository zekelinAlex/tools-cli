using System.Text.Json;
using TALXIS.CLI.Features.Config.Profile;
using TALXIS.CLI.Features.Environment.Publisher;
using TALXIS.CLI.Features.Workspace;
using TALXIS.CLI.MCP;
using Xunit;

namespace TALXIS.CLI.Tests.MCP;

public class CliCommandAdapterRequiredSchemaTests
{
    private readonly CliCommandAdapter _adapter = new();

    private static (HashSet<string> required, HashSet<string> properties) ReadSchema(JsonElement schema)
    {
        var required = schema.TryGetProperty("required", out var req) && req.ValueKind == JsonValueKind.Array
            ? req.EnumerateArray().Select(e => e.GetString()!).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        var properties = schema.GetProperty("properties").EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        return (required, properties);
    }

    [Fact]
    public void ProfileValidate_OptionalNamePositional_IsNotRequired()
    {
        // Name is nullable (string?) and defaults to the active profile.
        var (required, properties) = ReadSchema(_adapter.BuildInputSchema(typeof(ProfileValidateCliCommand)));

        Assert.Contains("Name", properties);       // still advertised as an accepted input
        Assert.DoesNotContain("Name", required);    // ...but not demanded
    }

    [Fact]
    public void WorkspaceValidate_PathWithDefault_IsNotRequired()
    {
        // Path is non-nullable but has a field-initializer default of ".".
        var (required, properties) = ReadSchema(_adapter.BuildInputSchema(typeof(WorkspaceValidateCliCommand)));

        Assert.Contains("Path", properties);
        Assert.DoesNotContain("Path", required);
    }

    [Fact]
    public void PublisherGet_NonNullablePositionalWithoutDefault_StaysRequired()
    {
        // Sanity check the fix didn't make everything optional: Publisher's
        // name argument is non-nullable with no default and must remain required.
        var (required, properties) = ReadSchema(_adapter.BuildInputSchema(typeof(PublisherGetCliCommand)));

        Assert.Contains("name", properties);
        Assert.Contains("name", required);
    }
}
