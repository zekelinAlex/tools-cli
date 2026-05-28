namespace TALXIS.CLI.MCP;

/// <summary>
/// Translates exceptions thrown during guide-tool execution into actionable
/// user-facing messages. Tied to issue #73 P3 — agents seeing "generic
/// invocation failure" had no clue whether the cause was sampling support,
/// an unknown workflow, an empty catalog slice, or something else. This
/// helper turns each common failure mode into a concrete next step the
/// client can take without reading server logs.
/// </summary>
internal static class GuideErrorMessage
{
    public static string Build(string guideName, Exception exception)
    {
        var hint = ResolveHint(exception);
        var baseMessage = $"Guide '{guideName}' failed: {exception.Message}";
        return string.IsNullOrEmpty(hint)
            ? baseMessage
            : $"{baseMessage}\n\nHow to recover: {hint}";
    }

    private static string ResolveHint(Exception exception)
    {
        // Sampling-related failures are the dominant cause: the only path
        // that needs the client LLM is the "guide + query" combo, and many
        // clients (or CI harnesses) don't implement MCP sampling.
        var message = exception.Message ?? string.Empty;
        if (LooksLikeSamplingFailure(exception, message))
        {
            return
                "this guide needed to ask the client's LLM to pick relevant tools, "
                + "but the MCP client did not respond to the sampling request. "
                + "Call the guide again WITHOUT a 'query' parameter to get a "
                + "compact listing of operations, or call 'execute_operation' "
                + "directly with an operation name from tools/list.";
        }

        if (exception is ArgumentException)
        {
            return "check that the 'query', 'top', and 'workflow' parameters match the schema returned by tools/list for this guide.";
        }

        if (exception is OperationCanceledException)
        {
            return "the request was cancelled by the client before sampling completed. Retry without cancelling, or call the guide without a 'query' to skip sampling.";
        }

        return string.Empty;
    }

    private static bool LooksLikeSamplingFailure(Exception exception, string message)
    {
        if (exception is InvalidOperationException)
            return message.Contains("sampling", StringComparison.OrdinalIgnoreCase);
        // Some MCP transports surface sampling rejections as NotSupportedException.
        if (exception is NotSupportedException)
            return true;
        // Generic message-text fallback for SDK-level errors.
        return message.Contains("sampling", StringComparison.OrdinalIgnoreCase)
            && (message.Contains("not supported", StringComparison.OrdinalIgnoreCase)
                || message.Contains("did not respond", StringComparison.OrdinalIgnoreCase)
                || message.Contains("returned no", StringComparison.OrdinalIgnoreCase));
    }
}
