using DotMake.CommandLine;
using TALXIS.CLI.Features.Workspace.Controls;
using TALXIS.CLI.Features.Workspace.Entities;

namespace TALXIS.CLI.Features.Workspace;

[CliCommand(
    Description = "Implement software in your local computer workspace (Git repository)",
    Alias = "ws",
    Children = new[]
    {
        typeof(ComponentCliCommand),
        typeof(ControlCliCommand),
        typeof(EntityCliCommand),
        typeof(ProjectCliCommand),
        typeof(WorkspaceExplainCliCommand),
        typeof(WorkspaceValidateCliCommand)
    },
    ShortFormAutoGenerate = CliNameAutoGenerate.None)]
public class WorkspaceCliCommand
{
    public void Run(CliContext context)
    {
        context.ShowHelp();
    }
}
