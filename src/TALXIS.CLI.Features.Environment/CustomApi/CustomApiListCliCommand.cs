using System.Text.Json;
using DotMake.CommandLine;
using Microsoft.Extensions.Logging;
using TALXIS.CLI.Core;
using TALXIS.CLI.Core.Contracts.Dataverse;
using TALXIS.CLI.Core.DependencyInjection;
using TALXIS.CLI.Logging;

namespace TALXIS.CLI.Features.Environment.CustomApi;

/// <summary>
/// Summary row for a Custom API in the connected environment.
/// </summary>
public sealed record CustomApiSummaryRecord(
    string UniqueName,
    string? DisplayName,
    string BindingType,
    string? BoundEntity,
    bool IsFunction,
    bool IsPrivate,
    Guid Id);

/// <summary>
/// Lists Custom APIs registered in the connected Dataverse environment.
/// Usage: <c>txc environment customapi list [--search &lt;term&gt;]</c>
/// </summary>
[CliReadOnly]
[CliCommand(
    Name = "list",
    Description = "Lists Custom APIs registered in the LIVE connected environment. Requires an active profile. Use --search to filter by unique name or display name."
)]
public class CustomApiListCliCommand : ProfiledCliCommand
{
    protected override ILogger Logger { get; } = TxcLoggerFactory.CreateLogger(nameof(CustomApiListCliCommand));

    [CliOption(Name = "--search", Description = "Filter Custom APIs by unique name or display name (case-insensitive substring).", Required = false)]
    public string? Search { get; set; }

    protected override async Task<int> ExecuteAsync()
    {
        var query = TxcServices.Get<IDataverseQueryService>();
        var result = await query.QueryODataAsync(
            Profile,
            "customapis",
            "customapiid,uniquename,name,bindingtype,boundentitylogicalname,isfunction,isprivate",
            null,
            "uniquename",
            null,
            false,
            CancellationToken.None).ConfigureAwait(false);

        var rows = result.Records.Select(ToSummary)
            .Where(r => MatchesSearch(r, Search))
            .ToList();

        OutputFormatter.WriteList(rows, PrintTable);
        return ExitSuccess;
    }

    internal static CustomApiSummaryRecord ToSummary(JsonElement record) => new(
        GetString(record, "uniquename") ?? "",
        GetString(record, "name"),
        CustomApiMaps.BindingTypeName(GetInt(record, "bindingtype")),
        GetString(record, "boundentitylogicalname"),
        GetBool(record, "isfunction"),
        GetBool(record, "isprivate"),
        record.TryGetProperty("customapiid", out var id) && id.TryGetGuid(out var guid) ? guid : Guid.Empty);

    internal static bool MatchesSearch(CustomApiSummaryRecord row, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return true;
        return row.UniqueName.Contains(search, StringComparison.OrdinalIgnoreCase)
            || (row.DisplayName?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static string? GetString(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static int GetInt(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : 0;

    private static bool GetBool(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.True;

    // Text-renderer callback invoked by OutputFormatter.WriteList — OutputWriter usage is intentional.
#pragma warning disable TXC003
    private static void PrintTable(IReadOnlyList<CustomApiSummaryRecord> rows)
    {
        if (rows.Count == 0)
        {
            OutputWriter.WriteLine("No Custom APIs found.");
            return;
        }

        int uniqueWidth = Math.Clamp(rows.Max(r => r.UniqueName.Length), 11, 48);
        int displayWidth = Math.Clamp(rows.Max(r => (r.DisplayName ?? "").Length), 12, 40);
        int bindingWidth = Math.Clamp(rows.Max(r => r.BindingType.Length), 7, 16);
        int boundWidth = Math.Clamp(rows.Max(r => (r.BoundEntity ?? "").Length), 12, 32);

        string header =
            $"{"Unique Name".PadRight(uniqueWidth)} | " +
            $"{"Display Name".PadRight(displayWidth)} | " +
            $"{"Binding".PadRight(bindingWidth)} | " +
            $"{"Bound Entity".PadRight(boundWidth)} | " +
            $"{"Function".PadRight(8)} | Private";
        OutputWriter.WriteLine(header);
        OutputWriter.WriteLine(new string('-', header.Length));

        foreach (var r in rows)
        {
            OutputWriter.WriteLine(
                $"{Truncate(r.UniqueName, uniqueWidth).PadRight(uniqueWidth)} | " +
                $"{Truncate(r.DisplayName ?? "", displayWidth).PadRight(displayWidth)} | " +
                $"{r.BindingType.PadRight(bindingWidth)} | " +
                $"{Truncate(r.BoundEntity ?? "", boundWidth).PadRight(boundWidth)} | " +
                $"{(r.IsFunction ? "true" : "false").PadRight(8)} | {(r.IsPrivate ? "true" : "false")}");
        }
    }
#pragma warning restore TXC003

    private static string Truncate(string value, int maxWidth) =>
        value.Length > maxWidth ? value[..(maxWidth - 1)] + "." : value;
}
