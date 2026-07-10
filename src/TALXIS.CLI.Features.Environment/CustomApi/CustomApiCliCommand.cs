using DotMake.CommandLine;

namespace TALXIS.CLI.Features.Environment.CustomApi;

/// <summary>
/// Parent command for Custom API operations.
/// Usage: <c>txc environment customapi [list|create|generate-openapi]</c>
/// </summary>
[CliCommand(
    Name = "customapi",
    Description = "Custom API discovery, creation, and OpenAPI generation for the live environment.",
    Children = new[] { typeof(CustomApiListCliCommand), typeof(CustomApiCreateCliCommand), typeof(CustomApiGenerateOpenApiCliCommand) }
)]
public class CustomApiCliCommand
{
    public void Run(CliContext context)
    {
        context.ShowHelp();
    }
}
