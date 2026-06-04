using TALXIS.CLI.Core;
using Xunit;

namespace TALXIS.CLI.Tests.Shared;

/// <summary>
/// Tests for <see cref="AuthRecoveryMessage"/>. The helper
/// branches on the <c>TXC_ENTRY_POINT</c> env var so we manipulate it
/// inside each test with try/finally to keep test isolation clean. xUnit
/// uses a shared test process, so leaking the variable would corrupt
/// unrelated tests.
/// </summary>
[Collection("AuthRecoveryMessageTests")]
public class AuthRecoveryMessageTests
{
    private const string EntryPointVar = "TXC_ENTRY_POINT";

    [Fact]
    public void Build_CliEntryPoint_AdvisesRunningTxcCommand()
    {
        using (new EntryPointScope(null))
        {
            var message = AuthRecoveryMessage.Build("Cached token expired.", "dev");

            Assert.Contains("Run 'txc config auth login --profile dev'", message);
            Assert.DoesNotContain("MCP host", message);
        }
    }

    [Fact]
    public void Build_McpEntryPoint_InstructsToAskTheUser()
    {
        using (new EntryPointScope("mcp"))
        {
            var message = AuthRecoveryMessage.Build("Cached token expired.", "dev");

            Assert.Contains("MCP host", message);
            Assert.Contains("Ask the user", message);
            Assert.Contains("txc config auth login --profile dev", message);
        }
    }

    [Fact]
    public void Build_McpEntryPoint_OmitsProfileSegmentWhenNullOrBlank()
    {
        using (new EntryPointScope("mcp"))
        {
            var message = AuthRecoveryMessage.Build("Cached token expired.", null);
            Assert.Contains("txc config auth login.", message);
            Assert.DoesNotContain("--profile", message);
        }
    }

    [Fact]
    public void Build_AppendsPeriodIfReasonHasNone()
    {
        using (new EntryPointScope(null))
        {
            var message = AuthRecoveryMessage.Build("Need to log in", null);
            Assert.StartsWith("Need to log in.", message);
        }
    }

    [Fact]
    public void Build_NullOrWhitespaceReason_FallsBackToGenericMessage()
    {
        using (new EntryPointScope(null))
        {
            var message = AuthRecoveryMessage.Build("   ", null);
            Assert.StartsWith("Interactive authentication is required.", message);
        }
    }

    [Fact]
    public void IsMcpEntryPoint_RespectsEnvVar_CaseInsensitively()
    {
        using (new EntryPointScope("MCP"))
            Assert.True(AuthRecoveryMessage.IsMcpEntryPoint());

        using (new EntryPointScope("cli"))
            Assert.False(AuthRecoveryMessage.IsMcpEntryPoint());

        using (new EntryPointScope(null))
            Assert.False(AuthRecoveryMessage.IsMcpEntryPoint());
    }

    /// <summary>
    /// Sets the <c>TXC_ENTRY_POINT</c> env var inside a scope and restores
    /// the previous value on dispose. Necessary because <see cref="System.Environment"/>
    /// is process-wide and xUnit shares the process across tests.
    /// </summary>
    private sealed class EntryPointScope : IDisposable
    {
        private readonly string? _previous;

        public EntryPointScope(string? value)
        {
            _previous = System.Environment.GetEnvironmentVariable(EntryPointVar);
            System.Environment.SetEnvironmentVariable(EntryPointVar, value);
        }

        public void Dispose()
        {
            System.Environment.SetEnvironmentVariable(EntryPointVar, _previous);
        }
    }
}
