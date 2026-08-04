using System.Text.Json;
using TALXIS.CLI.Core.Contracts.Dataverse;
using TALXIS.CLI.Platform.Dataverse.Application.Services;
using Xunit;

namespace TALXIS.CLI.Tests.Config.Providers.PowerPlatform;

public sealed class FlowRunLogServiceTests
{
    private static readonly Guid WorkflowId = Guid.Parse("98b4ebbb-3cbf-4021-b5f8-ab8c4ce0988e");

    private static readonly string WorkflowRow =
        $$"""{"workflowid":"{{WorkflowId}}","name":"tst_hourlyjob"}""";

    private const string RunRow = """
    {
      "name": "08584164478908761312693862248CU10",
      "status": "Failed",
      "triggertype": "Scheduled",
      "starttime": "2026-07-27T13:29:54Z",
      "endtime": "2026-07-27T13:29:55Z",
      "duration": 89,
      "errorcode": "ActionFailed",
      "errormessage": "An action failed."
    }
    """;

    [Fact]
    public async Task ListRunsAsync_ResolvesFlowByName_AndQueriesFlowRuns()
    {
        var query = new FakeQueryService(new[] { WorkflowRow }, new[] { RunRow });
        var sut = new DataverseFlowRunLogService(query);

        var runs = await sut.ListRunsAsync(null, "tst_hourlyjob", top: 10, status: null, CancellationToken.None);

        Assert.Contains("name = 'tst_hourlyjob'", query.LastSql);
        Assert.Contains("category = 5", query.LastSql);
        Assert.Equal("flowruns", query.LastEntity);
        Assert.Equal($"workflowid eq {WorkflowId:D}", query.LastFilter);
        Assert.Equal("starttime desc", query.LastOrderBy);
        Assert.Equal(10, query.LastTop);

        var run = Assert.Single(runs);
        Assert.Equal("08584164478908761312693862248CU10", run.RunId);
        Assert.Equal("Failed", run.Status);
        Assert.Equal("Scheduled", run.TriggerType);
        Assert.Equal(89, run.DurationMs);
        Assert.Equal("ActionFailed", run.ErrorCode);
        Assert.Equal(new DateTime(2026, 7, 27, 13, 29, 54, DateTimeKind.Utc), run.StartTimeUtc);
    }

    [Fact]
    public async Task ListRunsAsync_ResolvesFlowByGuid_AndAppendsStatusFilter()
    {
        var query = new FakeQueryService(new[] { WorkflowRow }, Array.Empty<string>());
        var sut = new DataverseFlowRunLogService(query);

        await sut.ListRunsAsync(null, WorkflowId.ToString(), top: 5, status: "Failed", CancellationToken.None);

        Assert.Contains($"workflowid = '{WorkflowId:D}'", query.LastSql);
        Assert.Equal($"workflowid eq {WorkflowId:D} and status eq 'Failed'", query.LastFilter);
    }

    [Fact]
    public async Task GetRunAsync_FiltersByRunId_AndReturnsSingleRun()
    {
        var query = new FakeQueryService(new[] { WorkflowRow }, new[] { RunRow });
        var sut = new DataverseFlowRunLogService(query);

        var run = await sut.GetRunAsync(null, "tst_hourlyjob", "08584164478908761312693862248CU10", CancellationToken.None);

        Assert.NotNull(run);
        Assert.Contains("name eq '08584164478908761312693862248CU10'", query.LastFilter);
        Assert.Equal(1, query.LastTop);
    }

    [Fact]
    public async Task GetRunAsync_ReturnsNull_WhenRunNotFound()
    {
        var query = new FakeQueryService(new[] { WorkflowRow }, Array.Empty<string>());
        var sut = new DataverseFlowRunLogService(query);

        var run = await sut.GetRunAsync(null, "tst_hourlyjob", "missing", CancellationToken.None);

        Assert.Null(run);
    }

    [Fact]
    public async Task ListRunsAsync_Throws_WhenFlowNotFound()
    {
        var query = new FakeQueryService(Array.Empty<string>(), Array.Empty<string>());
        var sut = new DataverseFlowRunLogService(query);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ListRunsAsync(null, "nope", top: 10, status: null, CancellationToken.None));
        Assert.Contains("No cloud flow matching", ex.Message);
    }

    [Fact]
    public async Task ListRunsAsync_Throws_WhenFlowNameAmbiguous()
    {
        var query = new FakeQueryService(new[] { WorkflowRow, WorkflowRow }, Array.Empty<string>());
        var sut = new DataverseFlowRunLogService(query);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ListRunsAsync(null, "dup", top: 10, status: null, CancellationToken.None));
        Assert.Contains("Multiple cloud flows", ex.Message);
    }

    [Fact]
    public async Task ListRunsAsync_EscapesQuotes_InFlowNameAndStatus()
    {
        var query = new FakeQueryService(new[] { WorkflowRow }, Array.Empty<string>());
        var sut = new DataverseFlowRunLogService(query);

        await sut.ListRunsAsync(null, "o'brien", top: 5, status: "Fail'ed", CancellationToken.None);

        Assert.Contains("name = 'o''brien'", query.LastSql);
        Assert.Contains("status eq 'Fail''ed'", query.LastFilter);
    }

    private sealed class FakeQueryService : IDataverseQueryService
    {
        private readonly IReadOnlyList<JsonElement> _sqlRecords;
        private readonly IReadOnlyList<JsonElement> _odataRecords;

        public string? LastSql { get; private set; }
        public string? LastEntity { get; private set; }
        public string? LastFilter { get; private set; }
        public string? LastOrderBy { get; private set; }
        public int? LastTop { get; private set; }

        public FakeQueryService(IReadOnlyList<string> sqlRows, IReadOnlyList<string> odataRows)
        {
            _sqlRecords = Parse(sqlRows);
            _odataRecords = Parse(odataRows);
        }

        private static IReadOnlyList<JsonElement> Parse(IReadOnlyList<string> rows)
            => rows.Select(r => JsonDocument.Parse(r).RootElement.Clone()).ToList();

        public Task<DataverseQueryResult> QuerySqlAsync(string? profileName, string sql, int? top, bool includeAnnotations, CancellationToken ct)
        {
            LastSql = sql;
            return Task.FromResult(new DataverseQueryResult(_sqlRecords, _sqlRecords.Count));
        }

        public Task<DataverseQueryResult> QueryFetchXmlAsync(string? profileName, string fetchXml, int? top, bool includeAnnotations, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<DataverseQueryResult> QueryODataAsync(string? profileName, string entitySetOrPath, string? select, string? filter, string? orderBy, int? top, bool includeAnnotations, CancellationToken ct)
        {
            LastEntity = entitySetOrPath;
            LastFilter = filter;
            LastOrderBy = orderBy;
            LastTop = top;
            return Task.FromResult(new DataverseQueryResult(_odataRecords, _odataRecords.Count));
        }
    }
}
