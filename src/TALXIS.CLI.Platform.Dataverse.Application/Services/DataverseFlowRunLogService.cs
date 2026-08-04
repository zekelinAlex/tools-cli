using System.Text.Json;
using TALXIS.CLI.Core.Contracts.Dataverse;

namespace TALXIS.CLI.Platform.Dataverse.Application.Services;

/// <summary>
/// Reads cloud flow run history from the Dataverse <c>flowrun</c> virtual
/// table. The flow is resolved from the <c>workflow</c> table (category 5)
/// by name or workflowid, then runs are queried by the workflowid attribute.
/// Everything runs under the ordinary Dataverse token — no flow-service
/// audience is required (which this CLI's client id could not acquire anyway).
/// </summary>
public sealed class DataverseFlowRunLogService : IFlowRunLogService
{
    private const string RunColumns = "name,status,triggertype,starttime,endtime,duration,errorcode,errormessage";

    private readonly IDataverseQueryService _query;

    public DataverseFlowRunLogService(IDataverseQueryService query)
    {
        _query = query ?? throw new ArgumentNullException(nameof(query));
    }

    public async Task<IReadOnlyList<FlowRunRecord>> ListRunsAsync(
        string? profileName, string flow, int top, string? status, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flow);

        var workflowId = await ResolveWorkflowIdAsync(profileName, flow, ct).ConfigureAwait(false);

        var filter = $"workflowid eq {workflowId:D}";
        if (!string.IsNullOrWhiteSpace(status))
            filter += $" and status eq '{status.Replace("'", "''")}'";

        var result = await _query.QueryODataAsync(
            profileName,
            "flowruns",
            select: RunColumns,
            filter: filter,
            orderBy: "starttime desc",
            top: top,
            includeAnnotations: false,
            ct).ConfigureAwait(false);

        return result.Records.Select(ParseRun).ToList();
    }

    public async Task<FlowRunRecord?> GetRunAsync(
        string? profileName, string flow, string runId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flow);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);

        var workflowId = await ResolveWorkflowIdAsync(profileName, flow, ct).ConfigureAwait(false);

        var result = await _query.QueryODataAsync(
            profileName,
            "flowruns",
            select: RunColumns,
            filter: $"workflowid eq {workflowId:D} and name eq '{runId.Replace("'", "''")}'",
            orderBy: null,
            top: 1,
            includeAnnotations: false,
            ct).ConfigureAwait(false);

        return result.Records.Count == 0 ? null : ParseRun(result.Records[0]);
    }

    private async Task<Guid> ResolveWorkflowIdAsync(string? profileName, string flow, CancellationToken ct)
    {
        var condition = Guid.TryParse(flow, out var workflowId)
            ? $"workflowid = '{workflowId:D}'"
            : $"name = '{flow.Replace("'", "''")}'";
        var sql = $"SELECT workflowid, name FROM workflow WHERE {condition} AND category = 5";

        var result = await _query.QuerySqlAsync(profileName, sql, top: 5, includeAnnotations: false, ct).ConfigureAwait(false);
        if (result.Records.Count == 0)
        {
            throw new InvalidOperationException(
                $"No cloud flow matching '{flow}' was found in the environment. Pass the flow's name or workflowid (workflow table, category 5).");
        }

        if (result.Records.Count > 1)
        {
            throw new InvalidOperationException(
                $"Multiple cloud flows are named '{flow}'. Pass the workflowid GUID instead.");
        }

        var resolved = ReadString(result.Records[0], "workflowid");
        if (!Guid.TryParse(resolved, out var id))
            throw new InvalidOperationException($"Cloud flow '{flow}' returned an unreadable workflowid.");

        return id;
    }

    private static FlowRunRecord ParseRun(JsonElement run)
        => new(
            RunId: ReadString(run, "name") ?? string.Empty,
            Status: ReadString(run, "status"),
            TriggerType: ReadString(run, "triggertype"),
            StartTimeUtc: ReadUtc(run, "starttime"),
            EndTimeUtc: ReadUtc(run, "endtime"),
            DurationMs: ReadInt(run, "duration"),
            ErrorCode: ReadString(run, "errorcode"),
            ErrorMessage: ReadString(run, "errormessage"));

    private static string? ReadString(JsonElement element, string property)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty(property, out var value)
           && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static DateTime? ReadUtc(JsonElement element, string property)
        => ReadString(element, property) is { } raw
           && DateTimeOffset.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed.UtcDateTime
            : null;

    private static int? ReadInt(JsonElement element, string property)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty(property, out var value)
           && value.ValueKind == JsonValueKind.Number
           && value.TryGetInt32(out var parsed)
            ? parsed
            : null;
}
