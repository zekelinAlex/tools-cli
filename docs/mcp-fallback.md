# MCP vs. direct CLI fallback

When an AI assistant is configured with both the `txc-mcp` server **and**
the `txc` CLI on PATH, it may sometimes invoke `txc` directly in the
terminal instead of going through the MCP tool. This page documents that
behaviour: when it happens, what it changes, and how to tune it.

## When the agent falls back

Direct-CLI execution typically kicks in when:

- The MCP server failed to start (no .NET 10 SDK, `dnx` cache miss
  with no network, port held by another process, etc.).
- The MCP server is running but a specific tool call errored
  transiently and the agent retries via the terminal.
- The tool the agent needs hasn't been injected into the active tool
  set yet (see *Progressive Disclosure* in the
  [MCP server README](../src/TALXIS.CLI.MCP/README.md)) and the agent
  decides to shell out rather than call a `guide_*` tool first.
- The user explicitly tells the agent to "just run it in the
  terminal".

This is supported. The CLI is the source of truth; the MCP server is a
thin wrapper that maps each tool to a `txc` subcommand. Anything you
can do over MCP, you can do at the terminal — the surface area is the
same.

## What changes when falling back

| | MCP tool call | Direct `txc` in the terminal |
|---|---|---|
| Log format | JSON (`TXC_LOG_FORMAT=json` forced by the server) | Human-readable text (the CLI default) |
| Interactivity | Disabled (`TXC_NON_INTERACTIVE=1` forced) | Enabled — browser auth, masked prompts can appear |
| Output channel | `notifications/message` frames + structured tool result | stdout / stderr in the shell |
| Last-chance log redaction | `McpLogForwarder` strips bearer tokens, JWTs, secret query params | CLI's own redaction only — no MCP belt |
| Progress / cancellation | `notifications/progress` + tasks protocol (`tasks/get`, `tasks/result`) | Long-running calls block the terminal until exit |
| Working directory | Set from the client's primary workspace root via `roots/list` | Inherits the agent shell's `cwd` |

The two paths produce equivalent *results*, but they don't produce
equivalent *traces*. If you're correlating an agent's actions across a
session log, MCP-routed calls appear as structured JSON-RPC frames
while fallback calls appear as plain terminal transcript lines.

## Auth implications

The auth contract documented in the
[MCP server README](../src/TALXIS.CLI.MCP/README.md#auth-contract-stdio)
applies to MCP-routed calls only. In the terminal:

- An expired token may trigger an interactive browser login instead
  of failing fast. If the agent's shell is non-interactive (common in
  CI-style harnesses) the call can hang.
- Service-principal env vars (`AZURE_CLIENT_ID`,
  `AZURE_CLIENT_SECRET`, `AZURE_TENANT_ID`, …) are picked up the same
  way as under MCP.

To make terminal fallback behave like MCP, export the same forced
environment variables before the agent runs:

**PowerShell (Windows / macOS / Linux):**

```powershell
$env:TXC_NON_INTERACTIVE = '1'
$env:TXC_LOG_FORMAT = 'json'
```

**bash / zsh (macOS / Linux):**

```sh
export TXC_NON_INTERACTIVE=1
export TXC_LOG_FORMAT=json
```

With those set, an expired credential aborts the call instead of
opening a browser, and stderr is JSON — which most agents parse more
reliably than the default text format.

## Platform notes

The `txc` CLI runs on .NET 10 and behaves the same on Windows, macOS,
and Linux. The differences worth knowing about for fallback are
shell-level, not CLI-level:

- **Windows / PowerShell** — use backtick (`` ` ``) for line
  continuation, not backslash. Single-quote literal arguments so `$`
  isn't expanded; double-quote when you do want expansion.
- **macOS / Linux (bash, zsh)** — standard POSIX quoting. Backslash
  for line continuation.
- **No global install?** Use `dnx TALXIS.CLI -- <args>` anywhere
  `txc <args>` is shown. `dnx` ships with the .NET 10 SDK and works
  identically on all three platforms.

Path quoting is the most common source of cross-platform breakage when
an agent shells out — if the agent emits a command that works on one
OS but not another, that's almost always why.

## When to prefer MCP over direct CLI

MCP is the better path when any of these matter:

- You want one auth setup that the agent reuses across calls without
  re-prompting.
- The operation is long-running (deploys, package imports, bulk data
  loads) — the tasks protocol lets the agent fire-and-forget and poll
  for results.
- You're worried about secrets leaking into logs — `McpLogForwarder`
  provides defence-in-depth on top of the CLI's own redaction.
- The agent needs structured tool results to chain into the next
  step.

Direct CLI is the better path when MCP isn't available, when you're
debugging the CLI itself, or when you want the full text log right in
front of you.
