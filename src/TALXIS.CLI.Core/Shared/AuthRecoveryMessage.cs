namespace TALXIS.CLI.Core;

/// <summary>
/// Builds the recovery hint shown when a cached MSAL token has expired or
/// is missing. The text differs between CLI and MCP entry points (issue
/// #73 P4): in a terminal, telling the user to run a <c>txc</c> command is
/// fine; inside an MCP host the agent can't open a browser on its own and
/// needs an explicit instruction it can pass through to the human.
///
/// <para>
/// The MCP host signals itself by setting <c>TXC_ENTRY_POINT=mcp</c> on
/// every subprocess it spawns. We read it directly here rather than
/// plumbing a context object — the helper is meant to be cheap to call
/// from every auth call site (in particular, the runtime token service
/// which has no DI of its own).
/// </para>
/// </summary>
public static class AuthRecoveryMessage
{
    private const string EntryPointEnvVar = "TXC_ENTRY_POINT";
    private const string McpEntryPoint = "mcp";

    /// <summary>
    /// Returns the recovery sentence appropriate for the current entry
    /// point. <paramref name="reason"/> is the prefix that explains
    /// <i>why</i> recovery is needed (e.g. <c>"No cached sign-in found for
    /// credential 'X'."</c>); the helper appends the actionable next step.
    /// </summary>
    /// <param name="reason">Cause sentence to prepend, e.g. "Cached token expired or is missing consent."</param>
    /// <param name="profileName">Profile to mention in the suggested command, or null.</param>
    public static string Build(string reason, string? profileName = null)
    {
        if (string.IsNullOrWhiteSpace(reason))
            reason = "Interactive authentication is required.";
        reason = reason.TrimEnd('.', ' ') + ".";

        var profileFragment = string.IsNullOrWhiteSpace(profileName)
            ? string.Empty
            : $" --profile {profileName}";

        return IsMcpEntryPoint()
            ? $"{reason} The MCP host cannot perform interactive browser sign-in. "
              + $"Ask the user to open a terminal and run: txc config auth login{profileFragment}. "
              + "After it succeeds, retry this operation."
            : $"{reason} Run 'txc config auth login{profileFragment}' and retry.";
    }

    /// <summary>
    /// True when the current process was spawned by the MCP server. Pure
    /// env-var read so it works from any layer.
    /// </summary>
    public static bool IsMcpEntryPoint()
    {
        var value = Environment.GetEnvironmentVariable(EntryPointEnvVar);
        return string.Equals(value, McpEntryPoint, StringComparison.OrdinalIgnoreCase);
    }
}
