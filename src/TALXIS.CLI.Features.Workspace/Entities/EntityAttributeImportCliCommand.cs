using DotMake.CommandLine;
using Microsoft.Extensions.Logging;
using TALXIS.CLI.Core;
using TALXIS.CLI.Logging;
using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.CLI.Features.Workspace.Entities;

/// <summary>
/// Applies a rendered pp-entity-attribute scaffold to the solution in one in-process
/// transaction: option set options, attribute import into Entity.xml, money support
/// attributes, lookup relationship files, attribute sorting, and nil-tag normalization.
/// Replaces the template's PowerShell post-action scripts.
/// </summary>
[CliIdempotent]
[CliCommand(
    Description = "Import a rendered attribute scaffold into an entity (used by the pp-entity-attribute template)",
    Name = "attribute-import")]
public class EntityAttributeImportCliCommand : TxcLeafCommand
{
    protected override ILogger Logger { get; } = TxcLoggerFactory.CreateLogger(nameof(EntityAttributeImportCliCommand));

    [CliOption(Name = "--solution-root", Description = "Folder containing the unpacked solution files (Other/, Entities/, OptionSets/)", Required = true)]
    public string SolutionRoot { get; set; } = null!;

    [CliOption(Name = "--entity", Description = "Schema name of the entity that receives the attribute (e.g. udpp_warehouseitem)", Required = true)]
    public string EntitySchemaName { get; set; } = null!;

    [CliOption(Name = "--attribute-file", Description = "Path to the rendered <attribute> XML file", Required = true)]
    public string AttributeFile { get; set; } = null!;

    [CliOption(Name = "--options", Description = "Choice options: comma-separated labels or Label:Value pairs (e.g. Active:100000000,Inactive)", Required = false)]
    public string? Options { get; set; }

    [CliOption(Name = "--global-optionset-file", Description = "Path to the rendered global option set file; options are written there instead of the attribute", Required = false)]
    public string? GlobalOptionSetFile { get; set; }

    [CliOption(Name = "--global-optionset-name", Description = "Schema name of the global option set to register as a RootComponent (type 9)", Required = false)]
    public string? GlobalOptionSetName { get; set; }

    [CliOption(Name = "--money-base-file", Description = "Path to the rendered money base attribute file", Required = false)]
    public string? MoneyBaseFile { get; set; }

    [CliOption(Name = "--currency-file", Description = "Path to the rendered transactioncurrencyid attribute file", Required = false)]
    public string? CurrencyFile { get; set; }

    [CliOption(Name = "--exchange-rate-file", Description = "Path to the rendered exchangerate attribute file", Required = false)]
    public string? ExchangeRateFile { get; set; }

    [CliOption(Name = "--relationship-file", Description = "Path to the rendered lookup EntityRelationship XML file", Required = false)]
    public string? RelationshipFile { get; set; }

    [CliOption(Name = "--relationship-name", Description = "Name of the lookup relationship", Required = false)]
    public string? RelationshipName { get; set; }

    [CliOption(Name = "--referenced-entity", Description = "Logical name of the entity the lookup points to (e.g. account)", Required = false)]
    public string? ReferencedEntity { get; set; }

    protected override Task<int> ExecuteAsync()
    {
        var request = new EntityAttributeScaffoldRequest
        {
            SolutionRootPath = Path.GetFullPath(SolutionRoot),
            EntitySchemaName = EntitySchemaName,
            AttributeFilePath = Path.GetFullPath(AttributeFile),
            OptionSetOptions = Options,
            GlobalOptionSetFilePath = FullPathOrNull(GlobalOptionSetFile),
            GlobalOptionSetSchemaName = GlobalOptionSetName,
            MoneyBaseAttributeFilePath = FullPathOrNull(MoneyBaseFile),
            CurrencyAttributeFilePath = FullPathOrNull(CurrencyFile),
            ExchangeRateAttributeFilePath = FullPathOrNull(ExchangeRateFile),
            LookupRelationshipFilePath = FullPathOrNull(RelationshipFile),
            LookupRelationshipName = RelationshipName,
            ReferencedEntityName = ReferencedEntity,
        };

        var result = EntityAttributeScaffold.Apply(request);
        foreach (var warning in result.Warnings)
        {
            Logger.LogWarning("{Warning}", warning);
        }

        OutputFormatter.WriteResult("succeeded", $"Attribute scaffold applied to '{EntitySchemaName}'");
        return Task.FromResult(ExitSuccess);
    }

    private static string? FullPathOrNull(string? path) => path == null ? null : Path.GetFullPath(path);
}
