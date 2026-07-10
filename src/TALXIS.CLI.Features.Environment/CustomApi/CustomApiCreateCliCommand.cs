using System.ComponentModel;
using System.Text.Json;
using DotMake.CommandLine;
using Microsoft.Extensions.Logging;
using TALXIS.CLI.Core;
using TALXIS.CLI.Core.Contracts.Dataverse;
using TALXIS.CLI.Core.DependencyInjection;
using TALXIS.CLI.Logging;

namespace TALXIS.CLI.Features.Environment.CustomApi;

/// <summary>
/// Creates a Custom API with optional request parameters and response properties.
/// Usage: <c>txc environment customapi create --unique-name &lt;name&gt; --display-name &lt;label&gt; [--request-param name:type[:optional] ...] [--response-property name:type ...] --apply</c>
/// </summary>
[CliIdempotent]
[CliCommand(
    Name = "create",
    Description = "Create a Custom API in the LIVE connected environment, optionally with request parameters and response properties. Requires an active profile. Parameter types: boolean, datetime, decimal, entity, entitycollection, entityreference, float, integer, money, picklist, string, stringarray, guid."
)]
#pragma warning disable TXC003
public class CustomApiCreateCliCommand : StagedCliCommand
{
    protected override ILogger Logger { get; } = TxcLoggerFactory.CreateLogger(nameof(CustomApiCreateCliCommand));

    [CliOption(Name = "--unique-name", Description = "Unique name of the Custom API, including publisher prefix (e.g. udpp_CalculateTotal).", Required = true)]
    public string UniqueName { get; set; } = null!;

    [CliOption(Name = "--display-name", Description = "Display name (label) for the Custom API.", Required = true)]
    public string DisplayName { get; set; } = null!;

    [CliOption(Name = "--description", Description = "Description of what the Custom API does. Defaults to the display name (Dataverse requires a non-empty description).", Required = false)]
    public string? Description { get; set; }

    [CliOption(Name = "--binding-type", Description = "Binding: 'global' (default), 'entity', or 'entitycollection'.", Required = false)]
    [DefaultValue("global")]
    public string BindingType { get; set; } = "global";

    [CliOption(Name = "--bound-entity", Description = "Logical name of the bound entity. Required when --binding-type is 'entity' or 'entitycollection'.", Required = false)]
    public string? BoundEntity { get; set; }

    [CliOption(Name = "--function", Description = "Register as an OData function (GET, no side effects) instead of an action (POST).", Required = false)]
    [DefaultValue(false)]
    public bool IsFunction { get; set; }

    [CliOption(Name = "--private", Description = "Mark the Custom API as private (hidden from metadata consumers).", Required = false)]
    [DefaultValue(false)]
    public bool IsPrivate { get; set; }

    [CliOption(Name = "--execute-privilege", Description = "Name of the privilege required to execute the Custom API.", Required = false)]
    public string? ExecutePrivilege { get; set; }

    [CliOption(Name = "--processing-step-type", Description = "Allowed custom processing steps: 'none' (default), 'async', or 'sync-and-async'.", Required = false)]
    [DefaultValue("none")]
    public string ProcessingStepType { get; set; } = "none";

    [CliOption(Name = "--request-param", Description = "Request parameter as name:type[:optional] (e.g. Quantity:integer, Comment:string:optional). Repeatable.", Required = false)]
    public string[]? RequestParams { get; set; }

    [CliOption(Name = "--response-property", Description = "Response property as name:type (e.g. Total:money). Repeatable.", Required = false)]
    public string[]? ResponseProperties { get; set; }

    protected override async Task<int> ExecuteAsync()
    {
        ValidateExecutionMode();

        if (!CustomApiMaps.BindingTypes.TryGetValue(BindingType, out int bindingCode))
        {
            Logger.LogError("Invalid --binding-type '{BindingType}'. Valid values: global, entity, entitycollection.", BindingType);
            return ExitValidationError;
        }

        if (bindingCode != 0 && string.IsNullOrWhiteSpace(BoundEntity))
        {
            Logger.LogError("--bound-entity is required when --binding-type is '{BindingType}'.", BindingType);
            return ExitValidationError;
        }

        if (!CustomApiMaps.ProcessingStepTypes.TryGetValue(ProcessingStepType, out int stepTypeCode))
        {
            Logger.LogError("Invalid --processing-step-type '{StepType}'. Valid values: none, async, sync-and-async.", ProcessingStepType);
            return ExitValidationError;
        }

        if (!TryParseSpecs(RequestParams, out var requestParams) ||
            !TryParseSpecs(ResponseProperties, out var responseProps))
        {
            return ExitValidationError;
        }

        if (Stage)
        {
            if (requestParams.Count > 0 || responseProps.Count > 0)
            {
                Logger.LogError("--request-param and --response-property require --apply; staged creation supports only the Custom API record itself.");
                return ExitValidationError;
            }

            var store = TxcServices.Get<IChangesetStore>();
            store.Add(new StagedOperation
            {
                Category = "data",
                OperationType = "CREATE",
                TargetType = "record",
                TargetDescription = "customapi",
                Details = $"unique name: \"{UniqueName}\"",
                Parameters = new Dictionary<string, object?>
                {
                    ["entity"] = "customapi",
                    ["data"] = JsonSerializer.Serialize(BuildApiAttributes(bindingCode, stepTypeCode)),
                    ["file"] = null
                }
            });
            OutputWriter.WriteLine($"Staged: CREATE customapi '{UniqueName}'");
            return ExitSuccess;
        }

        var service = TxcServices.Get<IDataverseRecordService>();
        var apiAttributes = ToJsonElement(BuildApiAttributes(bindingCode, stepTypeCode));
        var apiId = await service.CreateAsync(Profile, "customapi", apiAttributes, CancellationToken.None).ConfigureAwait(false);

        foreach (var (name, typeCode, optional) in requestParams)
        {
            var attributes = ToJsonElement(BuildChildAttributes(apiId, name, typeCode, isOptional: optional));
            await service.CreateAsync(Profile, "customapirequestparameter", attributes, CancellationToken.None).ConfigureAwait(false);
        }

        foreach (var (name, typeCode, _) in responseProps)
        {
            var attributes = ToJsonElement(BuildChildAttributes(apiId, name, typeCode, isOptional: null));
            await service.CreateAsync(Profile, "customapiresponseproperty", attributes, CancellationToken.None).ConfigureAwait(false);
        }

        OutputFormatter.WriteResult(
            "succeeded",
            $"Created Custom API '{UniqueName}' with {requestParams.Count} request parameter(s) and {responseProps.Count} response property(ies).",
            apiId.ToString());
        return ExitSuccess;
    }

    private bool TryParseSpecs(string[]? specs, out List<(string Name, int TypeCode, bool Optional)> parsed)
    {
        parsed = [];
        foreach (var spec in specs ?? [])
        {
            var result = CustomApiMaps.ParseParameterSpec(spec, out var error);
            if (result is null)
            {
                Logger.LogError("{Error}", error);
                return false;
            }
            parsed.Add(result.Value);
        }
        return true;
    }

    private Dictionary<string, object?> BuildApiAttributes(int bindingCode, int stepTypeCode)
    {
        var attributes = new Dictionary<string, object?>
        {
            ["uniquename"] = UniqueName,
            ["name"] = DisplayName,
            ["displayname"] = DisplayName,
            // Dataverse's RequiredFieldValidator rejects a NULL description on customapi.
            ["description"] = string.IsNullOrWhiteSpace(Description) ? DisplayName : Description,
            ["bindingtype"] = bindingCode,
            ["isfunction"] = IsFunction,
            ["isprivate"] = IsPrivate,
            ["allowedcustomprocessingsteptype"] = stepTypeCode,
        };
        if (bindingCode != 0) attributes["boundentitylogicalname"] = BoundEntity;
        if (!string.IsNullOrWhiteSpace(ExecutePrivilege)) attributes["executeprivilegename"] = ExecutePrivilege;
        return attributes;
    }

    private static Dictionary<string, object?> BuildChildAttributes(Guid apiId, string name, int typeCode, bool? isOptional)
    {
        var attributes = new Dictionary<string, object?>
        {
            ["uniquename"] = name,
            ["name"] = name,
            ["displayname"] = name,
            ["description"] = name,
            ["type"] = typeCode,
            ["customapiid"] = new Dictionary<string, object?> { ["Id"] = apiId, ["LogicalName"] = "customapi" },
        };
        if (isOptional is not null) attributes["isoptional"] = isOptional;
        return attributes;
    }

    private static JsonElement ToJsonElement(Dictionary<string, object?> attributes) =>
        JsonSerializer.SerializeToElement(attributes);
}
