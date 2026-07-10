using System.Text.Json;
using DotMake.CommandLine;
using Microsoft.Extensions.Logging;
using TALXIS.CLI.Core;
using TALXIS.CLI.Core.Abstractions;
using TALXIS.CLI.Core.Contracts.Dataverse;
using TALXIS.CLI.Core.DependencyInjection;
using TALXIS.CLI.Logging;

namespace TALXIS.CLI.Features.Environment.CustomApi;

/// <summary>
/// Generates an OpenAPI 3.0 specification for Custom APIs in the connected environment.
/// Usage: <c>txc environment customapi generate-openapi [--unique-name &lt;name&gt;] [--output &lt;file&gt;]</c>
/// </summary>
[CliReadOnly]
[CliCommand(
    Name = "generate-openapi",
    Description = "Generate an OpenAPI 3.0 spec (JSON) describing Custom APIs in the LIVE connected environment, including request parameters and response properties. Requires an active profile. Use --unique-name for a single API, --output to write to a file instead of stdout."
)]
public class CustomApiGenerateOpenApiCliCommand : ProfiledCliCommand
{
    protected override ILogger Logger { get; } = TxcLoggerFactory.CreateLogger(nameof(CustomApiGenerateOpenApiCliCommand));

    [CliOption(Name = "--unique-name", Description = "Generate the spec for a single Custom API by unique name. Omit to include all.", Required = false)]
    public string? UniqueName { get; set; }

    [CliOption(Name = "--output", Description = "Path of the file to write the spec to. Omit to print to stdout.", Required = false)]
    public string? Output { get; set; }

    [CliOption(Name = "--title", Description = "OpenAPI document title.", Required = false)]
    public string? Title { get; set; }

    [CliOption(Name = "--spec-version", Description = "OpenAPI document version string (info.version).", Required = false)]
    public string? SpecVersion { get; set; }

    protected override async Task<int> ExecuteAsync()
    {
        var query = TxcServices.Get<IDataverseQueryService>();
        var ct = CancellationToken.None;

        string? apiFilter = UniqueName is not null ? $"uniquename eq '{UniqueName.Replace("'", "''")}'" : null;
        var apiResult = await query.QueryODataAsync(
            Profile, "customapis",
            "customapiid,uniquename,name,description,bindingtype,boundentitylogicalname,isfunction",
            apiFilter, "uniquename", null, false, ct).ConfigureAwait(false);

        if (apiResult.Records.Count == 0)
        {
            Logger.LogError(UniqueName is not null
                ? $"Custom API '{UniqueName}' was not found in the environment."
                : "No Custom APIs found in the environment.");
            return ExitValidationError;
        }

        var requestParams = await query.QueryODataAsync(
            Profile, "customapirequestparameters",
            "uniquename,name,type,isoptional,_customapiid_value",
            null, "uniquename", null, false, ct).ConfigureAwait(false);

        var responseProps = await query.QueryODataAsync(
            Profile, "customapiresponseproperties",
            "uniquename,name,type,_customapiid_value",
            null, "uniquename", null, false, ct).ConfigureAwait(false);

        var definitions = BuildDefinitions(apiResult.Records, requestParams.Records, responseProps.Records);

        var document = CustomApiOpenApiBuilder.Build(
            definitions,
            Title ?? "Dataverse Custom APIs",
            SpecVersion ?? "1.0.0",
            await TryResolveEnvironmentUrlAsync(ct).ConfigureAwait(false));

        // Serialize via JsonNode so dictionary keys (parameter names, paths) keep their exact casing.
        string json = JsonSerializer.SerializeToNode(document)!.ToJsonString(TxcOutputJsonOptions.Default);

        if (Output is not null)
        {
            var fullPath = Path.GetFullPath(Output);
            await File.WriteAllTextAsync(fullPath, json, ct).ConfigureAwait(false);
            OutputFormatter.WriteResult("succeeded", $"OpenAPI spec with {definitions.Count} Custom API(s) written to {fullPath}.");
        }
        else
        {
            OutputFormatter.WriteRaw(json);
        }

        return ExitSuccess;
    }

    internal static List<CustomApiDefinition> BuildDefinitions(
        IReadOnlyList<JsonElement> apis,
        IReadOnlyList<JsonElement> requestParams,
        IReadOnlyList<JsonElement> responseProps)
    {
        var paramsByApi = requestParams.ToLookup(p => GetString(p, "_customapiid_value"));
        var propsByApi = responseProps.ToLookup(p => GetString(p, "_customapiid_value"));

        return apis.Select(api =>
        {
            string? id = GetString(api, "customapiid");
            return new CustomApiDefinition(
                GetString(api, "uniquename") ?? "",
                GetString(api, "name"),
                GetString(api, "description"),
                GetInt(api, "bindingtype"),
                GetString(api, "boundentitylogicalname"),
                GetBool(api, "isfunction"),
                paramsByApi[id].Select(ToParameter).OrderBy(p => p.UniqueName, StringComparer.OrdinalIgnoreCase).ToList(),
                propsByApi[id].Select(ToParameter).OrderBy(p => p.UniqueName, StringComparer.OrdinalIgnoreCase).ToList());
        }).ToList();
    }

    private static CustomApiParameter ToParameter(JsonElement e) => new(
        GetString(e, "uniquename") ?? "",
        GetString(e, "name"),
        GetInt(e, "type"),
        GetBool(e, "isoptional"));

    private async Task<string?> TryResolveEnvironmentUrlAsync(CancellationToken ct)
    {
        try
        {
            var resolver = TxcServices.Get<IConfigurationResolver>();
            var context = await resolver.ResolveAsync(Profile, ct).ConfigureAwait(false);
            return context.Connection.EnvironmentUrl;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? GetString(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static int GetInt(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : 0;

    private static bool GetBool(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.True;
}
