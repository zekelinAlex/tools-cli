namespace TALXIS.CLI.Core.Contracts.Dataverse;

/// <summary>
/// One cloud flow run from the Dataverse <c>flowrun</c> virtual table (the
/// table proxies the flow service's run history). All <see cref="DateTime"/>
/// values are UTC. Per-action details are not exposed by the table — they
/// only exist in the flow API, which requires a preauthorized first-party
/// client this CLI does not have.
/// </summary>
public sealed record FlowRunRecord(
    string RunId,
    string? Status,
    string? TriggerType,
    DateTime? StartTimeUtc,
    DateTime? EndTimeUtc,
    int? DurationMs,
    string? ErrorCode,
    string? ErrorMessage);

/// <summary>
/// Reads cloud flow (Power Automate) run history. Modern flow runs do not
/// live in Dataverse log tables like <c>asyncoperation</c>; they are read
/// from the <c>flowrun</c> virtual table.
/// </summary>
public interface IFlowRunLogService
{
    /// <summary>
    /// Lists recent runs of one cloud flow, newest first.
    /// <paramref name="flow"/> is the flow's name or workflowid GUID.
    /// </summary>
    Task<IReadOnlyList<FlowRunRecord>> ListRunsAsync(
        string? profileName,
        string flow,
        int top,
        string? status,
        CancellationToken ct);

    /// <summary>
    /// Returns one run of the flow, or null when the run id does not exist.
    /// </summary>
    Task<FlowRunRecord?> GetRunAsync(
        string? profileName,
        string flow,
        string runId,
        CancellationToken ct);
}
