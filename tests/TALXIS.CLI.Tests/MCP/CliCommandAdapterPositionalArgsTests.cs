using System.Text.Json;
using TALXIS.CLI.MCP;
using Xunit;
using Xunit.Abstractions;

namespace TALXIS.CLI.Tests.MCP;

/// <summary>
/// Repro tests for issue #73 P1+P2: MCP wrapper drops positional arguments
/// when the JSON-input key uses a different case than the C# property
/// name. Documented symptoms:
///
/// - `config_profile_validate` with `{ "name": "udpp26-txc-demo-thenetw.org" }`
///   → "Unrecognized command or argument 'udpp26-txc-demo-thenetw.org'"
/// - `workspace_validate` with `{ "path": "~/foo/bar" }`
///   → "Unrecognized argument"
///
/// The expected CLI args after translation are
/// <c>["config", "profile", "validate", "&lt;value&gt;"]</c> — i.e. value
/// goes through as a positional. Case-sensitive matching against the C#
/// property name breaks this for the common camelCase shape, and the value
/// silently falls into the options pipeline as <c>--name &lt;value&gt;</c>
/// (and similar for <c>--path</c>), which the CLI then rejects.
/// </summary>
public class CliCommandAdapterPositionalArgsTests
{
    private readonly ITestOutputHelper _output;

    public CliCommandAdapterPositionalArgsTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void BuildCliArgs_ProfileValidate_PascalCaseKey_PutsValueAsPositional()
    {
        var args = new Dictionary<string, JsonElement>
        {
            ["Name"] = JsonSerializer.SerializeToElement("udpp26-txc-demo-thenetw.org"),
        };

        AssertPositionalNotMisroutedAsOption("config_profile_validate", args, "--name");
    }

    [Fact]
    public void BuildCliArgs_ProfileValidate_LowerCaseKey_PutsValueAsPositional()
    {
        var args = new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("udpp26-txc-demo-thenetw.org"),
        };

        AssertPositionalNotMisroutedAsOption("config_profile_validate", args, "--name");
    }

    [Fact]
    public void BuildCliArgs_WorkspaceValidate_LowerCaseKey_PutsValueAsPositional()
    {
        var args = new Dictionary<string, JsonElement>
        {
            ["path"] = JsonSerializer.SerializeToElement("/tmp/some/dir"),
        };

        AssertPositionalNotMisroutedAsOption("workspace_validate", args, "--path");
    }

    [Fact]
    public void BuildCliArgs_WorkspaceValidate_PascalCaseKey_PutsValueAsPositional()
    {
        var args = new Dictionary<string, JsonElement>
        {
            ["Path"] = JsonSerializer.SerializeToElement("/tmp/some/dir"),
        };

        AssertPositionalNotMisroutedAsOption("workspace_validate", args, "--path");
    }

    [Fact]
    public void BuildCliArgs_WorkspaceValidate_UpperCaseKey_StillRoutesAsPositional()
    {
        var args = new Dictionary<string, JsonElement>
        {
            ["PATH"] = JsonSerializer.SerializeToElement("/tmp/some/dir"),
        };

        AssertPositionalNotMisroutedAsOption("workspace_validate", args, "--path");
    }

    [Fact]
    public void BuildCliArgs_PositionalAndOption_Coexist_CaseInsensitively()
    {
        // profile validate accepts both [CliArgument] Name and the inherited
        // ProfiledCliCommand options (--profile). Mixing the two should not
        // drop the positional, regardless of casing.
        var args = new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("my-profile"),
            ["skipLive"] = JsonSerializer.SerializeToElement(true),
        };

        var adapter = new CliCommandAdapter();
        var cliArgs = adapter.BuildCliArgs("config_profile_validate", args);

        _output.WriteLine($"cliArgs = [{string.Join(", ", cliArgs.Select(a => $"'{a}'"))}]");

        // Positional value present, NOT prefixed with --name.
        Assert.Contains("my-profile", cliArgs);
        Assert.DoesNotContain("--name", cliArgs);
        // Option emitted under its declared name (SkipLive has no Attr.Name,
        // so DotMake's auto-generated camelCase form wins).
        Assert.Contains(cliArgs, a => a.StartsWith("--skipLive", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildCliArgs_UnknownInputKey_PassesThroughAsOption()
    {
        // If an MCP client invents a field name we don't recognise, we
        // pass it through verbatim as --key=value rather than silently
        // dropping it. The CLI then surfaces a real "unknown option"
        // error, which is the actionable diagnostic the user wants.
        var args = new Dictionary<string, JsonElement>
        {
            ["totally-made-up"] = JsonSerializer.SerializeToElement("xx"),
        };

        var adapter = new CliCommandAdapter();
        var cliArgs = adapter.BuildCliArgs("workspace_validate", args);

        Assert.Contains("--totally-made-up", cliArgs);
        Assert.Contains("xx", cliArgs);
    }

    private void AssertPositionalNotMisroutedAsOption(
        string toolName,
        Dictionary<string, JsonElement> args,
        string forbiddenOptionFlag)
    {
        var adapter = new CliCommandAdapter();
        var cliArgs = adapter.BuildCliArgs(toolName, args);

        _output.WriteLine($"cliArgs = [{string.Join(", ", cliArgs.Select(a => $"'{a}'"))}]");

        Assert.DoesNotContain(forbiddenOptionFlag, cliArgs);
    }
}
