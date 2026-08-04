using DotMake.CommandLine;
using Microsoft.Extensions.Logging;
using TALXIS.CLI.Core;
using TALXIS.CLI.Core.Contracts.Dataverse;
using TALXIS.CLI.Core.DependencyInjection;
using TALXIS.CLI.Logging;

namespace TALXIS.CLI.Features.Environment.Diagnostics;

/// <summary>
/// Reads cloud flow (Power Automate) run history. Modern flow runs live in the
/// flow service, not in <c>asyncoperation</c>; they are read through the
/// Dataverse <c>flowrun</c> virtual table.
/// </summary>
/// <example>
///   txc environment log flow-runs --flow tst_oncontactchange
///   txc env log flow-runs --flow tst_oncontactchange --status Failed --top 5
///   txc env log flow-runs --flow tst_oncontactchange --run-id 08585287554388203450022458732CU00
/// </example>
[CliReadOnly]
[CliCommand(
    Name = "flow-runs",
    Description = "Read cloud flow (Power Automate) run history from the LIVE environment. Requires an active profile."
)]
public class LogFlowRunsCliCommand : ProfiledCliCommand
{
    protected override ILogger Logger { get; } = TxcLoggerFactory.CreateLogger(nameof(LogFlowRunsCliCommand));

    private static readonly string[] KnownStatuses = ["Succeeded", "Failed", "Running", "Cancelled"];

    private const int DefaultTop = 10;

    [CliOption(Name = "--flow", Description = "Cloud flow name or workflowid GUID (workflow table, category 5).", Required = true)]
    public string Flow { get; set; } = null!;

    [CliOption(Name = "--run-id", Description = "Show one run instead of the run list.", Required = false)]
    public string? RunId { get; set; }

    [CliOption(Name = "--status", Description = "Filter runs by status (Succeeded, Failed, Running, Cancelled).", Required = false)]
    public string? Status { get; set; }

    [CliOption(Name = "--top", Description = "Maximum number of runs to return.", Required = false)]
    public int? Top { get; set; }

    protected override async Task<int> ExecuteAsync()
    {
        string? status = null;
        if (!string.IsNullOrWhiteSpace(Status))
        {
            status = KnownStatuses.FirstOrDefault(s => string.Equals(s, Status, StringComparison.OrdinalIgnoreCase));
            if (status is null)
            {
                Logger.LogError(
                    "Invalid --status '{Status}'. Expected one of: Succeeded, Failed, Running, Cancelled.", Status);
                return ExitValidationError;
            }
        }

        var service = TxcServices.Get<IFlowRunLogService>();

        if (!string.IsNullOrWhiteSpace(RunId))
        {
            FlowRunRecord? run = await service.GetRunAsync(Profile, Flow, RunId, CancellationToken.None).ConfigureAwait(false);
            if (run is null)
            {
                Logger.LogError(
                    "Run '{RunId}' was not found on flow '{Flow}'. Use 'txc environment log flow-runs --flow {Flow}' to list recent runs.",
                    RunId, Flow, Flow);
                return ExitValidationError;
            }

            OutputFormatter.WriteData(run, PrintDetail);
            return ExitSuccess;
        }

        IReadOnlyList<FlowRunRecord> runs = await service
            .ListRunsAsync(Profile, Flow, Top ?? DefaultTop, status, CancellationToken.None)
            .ConfigureAwait(false);

        OutputFormatter.WriteList(runs, PrintTable);
        return ExitSuccess;
    }

    // Text-renderer callback invoked by OutputFormatter.WriteList — OutputWriter usage is intentional.
#pragma warning disable TXC003
    private static void PrintTable(IReadOnlyList<FlowRunRecord> runs)
    {
        if (runs.Count == 0)
        {
            OutputWriter.WriteLine("No flow runs found.");
            return;
        }

        int idWidth = Math.Clamp(runs.Max(r => r.RunId.Length), 10, 40);
        string header = $"{"Run Id".PadRight(idWidth)} | {"Status".PadRight(10)} | {"Trigger".PadRight(10)} | {"Started (UTC)".PadRight(20)} | {"Duration".PadRight(10)} | Error";
        OutputWriter.WriteLine(header);
        OutputWriter.WriteLine(new string('-', header.Length));

        foreach (var r in runs)
        {
            string id = r.RunId.Length > idWidth ? r.RunId[..(idWidth - 1)] + "." : r.RunId;
            string error = r.ErrorCode is null ? string.Empty : $"{r.ErrorCode}: {r.ErrorMessage}";
            string duration = r.DurationMs is { } ms ? $"{ms} ms" : string.Empty;
            OutputWriter.WriteLine(
                $"{id.PadRight(idWidth)} | {(r.Status ?? string.Empty).PadRight(10)} | {(r.TriggerType ?? string.Empty).PadRight(10)} | {Format(r.StartTimeUtc).PadRight(20)} | {duration.PadRight(10)} | {error}");
        }
    }

    private static void PrintDetail(FlowRunRecord r)
    {
        const int labelWidth = -10;
        OutputWriter.WriteLine($"{"Run Id:",labelWidth}{r.RunId}");
        OutputWriter.WriteLine($"{"Status:",labelWidth}{r.Status}");
        OutputWriter.WriteLine($"{"Trigger:",labelWidth}{r.TriggerType}");
        OutputWriter.WriteLine($"{"Started:",labelWidth}{Format(r.StartTimeUtc)}");
        OutputWriter.WriteLine($"{"Ended:",labelWidth}{Format(r.EndTimeUtc)}");
        if (r.DurationMs is { } ms)
            OutputWriter.WriteLine($"{"Duration:",labelWidth}{ms} ms");
        if (r.ErrorCode is not null)
            OutputWriter.WriteLine($"{"Error:",labelWidth}{r.ErrorCode}: {r.ErrorMessage}");
    }

    private static string Format(DateTime? utc)
        => utc?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty;
#pragma warning restore TXC003
}
