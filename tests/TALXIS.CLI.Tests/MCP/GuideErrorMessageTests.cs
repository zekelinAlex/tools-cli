using TALXIS.CLI.MCP;
using Xunit;

namespace TALXIS.CLI.Tests.MCP;

/// <summary>
/// Tests for <see cref="GuideErrorMessage"/> — the helper that translates
/// guide-handler exceptions into actionable client-visible text (issue #73
/// P3). The pipeline catch in Program.cs wraps every non-McpException with
/// this message, so the assertions here are effectively the contract
/// agents see when sampling fails or arguments are malformed.
/// </summary>
public class GuideErrorMessageTests
{
    [Fact]
    public void Build_NamesTheGuide()
    {
        var ex = new InvalidOperationException("Something went sideways.");
        var message = GuideErrorMessage.Build("guide_workspace", ex);

        Assert.Contains("guide_workspace", message);
        Assert.Contains("Something went sideways.", message);
    }

    [Fact]
    public void Build_SamplingNotSupported_IncludesWorkaroundHint()
    {
        // Mirrors what GuideHandler.DiscoverToolsAsync throws when the
        // sampling round-trip yields nothing.
        var ex = new InvalidOperationException("Sampling returned no results. Ensure the MCP client supports sampling.");
        var message = GuideErrorMessage.Build("guide", ex);

        Assert.Contains("How to recover", message);
        Assert.Contains("WITHOUT a 'query'", message);
        Assert.Contains("execute_operation", message);
    }

    [Fact]
    public void Build_NotSupportedException_TreatedAsSamplingFailure()
    {
        var ex = new NotSupportedException("Client does not support sampling.");
        var message = GuideErrorMessage.Build("guide_config", ex);

        Assert.Contains("How to recover", message);
        Assert.Contains("execute_operation", message);
    }

    [Fact]
    public void Build_GenericMessageMentioningSampling_TreatedAsSamplingFailure()
    {
        // Some MCP SDKs surface sampling rejection as a generic Exception
        // with a descriptive message rather than a typed one. We still
        // want the recovery hint in that case.
        var ex = new Exception("client did not respond to sampling request within timeout");
        var message = GuideErrorMessage.Build("guide_environment", ex);

        Assert.Contains("How to recover", message);
    }

    [Fact]
    public void Build_ArgumentException_SuggestsSchemaCheck()
    {
        var ex = new ArgumentException("Invalid 'top' value.");
        var message = GuideErrorMessage.Build("guide_data", ex);

        Assert.Contains("schema", message);
        Assert.Contains("tools/list", message);
    }

    [Fact]
    public void Build_OperationCanceled_SuggestsRetryOrCompactListing()
    {
        var ex = new OperationCanceledException("Request cancelled.");
        var message = GuideErrorMessage.Build("guide_deployment", ex);

        Assert.Contains("cancelled", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("without a 'query'", message);
    }

    [Fact]
    public void Build_UnrelatedException_NoHintButStillNamesGuide()
    {
        var ex = new InvalidOperationException("Catalog index is corrupted.");
        var message = GuideErrorMessage.Build("guide_workspace", ex);

        // No recovery hint we know about — keep the message tight but still
        // tell the client which guide failed and the actual error text.
        Assert.Contains("guide_workspace", message);
        Assert.Contains("Catalog index is corrupted.", message);
        Assert.DoesNotContain("How to recover", message);
    }
}
