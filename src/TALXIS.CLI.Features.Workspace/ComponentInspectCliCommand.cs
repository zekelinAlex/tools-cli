using DotMake.CommandLine;
using Microsoft.Extensions.Logging;
using TALXIS.CLI.Core;
using TALXIS.CLI.Core.Resolution;
using TALXIS.CLI.Logging;
using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Serialization.Xml;
using MetadataWorkspace = TALXIS.Platform.Metadata.Serialization.Xml.Workspace;

namespace TALXIS.CLI.Features.Workspace;

/// <summary>
/// CLI command that drills into the internal structure of a single workspace component.
/// Usage: <c>txc workspace component inspect --type Form --id {guid}</c>
/// </summary>
[CliReadOnly]
[CliCommand(
    Name = "inspect",
    Description = "Inspects the internal structure of a single component in the local workspace (no environment connection). " +
        "Forms: tab > section > control tree with data field bindings. Entities: attribute list with types and required levels. " +
        "Identify forms by GUID or display name, entities by logical name.")]
public class ComponentInspectCliCommand : TxcLeafCommand
{
    protected override ILogger Logger { get; } = TxcLoggerFactory.CreateLogger(nameof(ComponentInspectCliCommand));

    private const int MaxSuggestions = 20;

    [CliArgument(Description = "Path inside the solution workspace (defaults to current directory).")]
    public string Path { get; set; } = ".";

    [CliOption(Name = "--type", Description = "Component type to inspect: Form or Entity (aliases: SystemForm, Table).")]
    public required string Type { get; set; }

    [CliOption(Name = "--id", Description = "Component identifier - form GUID (with or without braces) or form display name for forms, entity logical name for entities.")]
    public required string Id { get; set; }

    [CliOption(Name = "--depth", Required = false, Description = "Maximum form tree depth: 1 = tabs only, 2 = tabs and sections, 3 or omitted = full tree including controls. Ignored for entities.")]
    public int? Depth { get; set; }

    [CliOption(Name = "--entity", Required = false, Description = "Entity logical name to disambiguate forms when --id is a display name shared by multiple entities. Ignored for entities.")]
    public string? Entity { get; set; }

    protected override Task<int> ExecuteAsync()
    {
        var fullPath = System.IO.Path.GetFullPath(Path);
        var workspaceRoot = SolutionProjectResolver.FindWorkspaceRoot(fullPath);
        if (workspaceRoot is null)
        {
            Logger.LogError("Could not find workspace root (Other/Solution.xml) from: {Path}.", fullPath);
            return Task.FromResult(ExitValidationError);
        }

        var workspace = new XmlWorkspaceReader().Load(workspaceRoot);

        return Type.Trim().ToLowerInvariant() switch
        {
            "entity" or "table" => Task.FromResult(InspectEntity(workspace)),
            "form" or "systemform" => Task.FromResult(InspectForm(workspace)),
            _ => Task.FromResult(UnsupportedType()),
        };
    }

    private int UnsupportedType()
    {
        Logger.LogError("Unsupported component type '{Type}'. Supported types: Form, Entity.", Type);
        return ExitValidationError;
    }

    private int InspectEntity(MetadataWorkspace workspace)
    {
        var entity = workspace.Entities.FirstOrDefault(e =>
            string.Equals(e.LogicalName, Id, StringComparison.OrdinalIgnoreCase));

        if (entity is null)
        {
            Logger.LogError("Entity '{Id}' not found in workspace. Available entities: {Entities}.",
                Id, Summarize(workspace.Entities.Select(e => e.LogicalName ?? "?")));
            return ExitValidationError;
        }

        var result = ComponentInspectHelpers.BuildEntityResult(entity);
        OutputFormatter.WriteData(result, ComponentInspectHelpers.RenderEntityText);
        return ExitSuccess;
    }

    private int InspectForm(MetadataWorkspace workspace)
    {
        var normalizedId = ComponentInspectHelpers.NormalizeGuid(Id);

        var candidates = Entity is null
            ? workspace.Forms
            : workspace.Forms.Where(f => string.Equals(f.EntityLogicalName, Entity, StringComparison.OrdinalIgnoreCase)).ToList();

        var matches = normalizedId is not null
            ? candidates.Where(f => ComponentInspectHelpers.NormalizeGuid(f.FormId) == normalizedId).ToList()
            : candidates.Where(f =>
                string.Equals(ComponentInspectHelpers.PickLabel(f.DisplayName), Id, StringComparison.OrdinalIgnoreCase)).ToList();

        if (matches.Count == 0)
        {
            Logger.LogError("Form '{Id}' not found in workspace. Available forms: {Forms}.",
                Id, Summarize(candidates.Select(DescribeForm)));
            return ExitValidationError;
        }

        if (matches.Count > 1)
        {
            Logger.LogError("Form name '{Id}' is ambiguous ({Count} matches): {Forms}. Use the form GUID or narrow with --entity.",
                Id, matches.Count, Summarize(matches.Select(DescribeForm)));
            return ExitValidationError;
        }

        var result = ComponentInspectHelpers.BuildFormResult(matches[0], Depth);
        OutputFormatter.WriteData(result, ComponentInspectHelpers.RenderFormText);
        return ExitSuccess;
    }

    private static string DescribeForm(FormMetadata form)
        => $"{form.FormId} ({ComponentInspectHelpers.PickLabel(form.DisplayName) ?? "unnamed"}, {form.EntityLogicalName})";

    private static string Summarize(IEnumerable<string> items)
    {
        var list = items.ToList();
        var shown = string.Join(", ", list.Take(MaxSuggestions));
        return list.Count > MaxSuggestions ? $"{shown} ... and {list.Count - MaxSuggestions} more" : shown;
    }
}
