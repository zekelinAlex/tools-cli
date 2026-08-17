using DotMake.CommandLine;

namespace TALXIS.CLI.Features.Workspace.Entities;

/// <summary>
/// Entity operations on the local workspace, executed in-process through the
/// platform metadata library instead of template post-action scripts.
/// </summary>
[CliCommand(
    Description = "Modify entities in your local workspace",
    Name = "entity",
    Children = new[]
    {
        typeof(EntityAttributeImportCliCommand),
    },
    ShortFormAutoGenerate = CliNameAutoGenerate.None)]
public class EntityCliCommand
{
    public void Run(CliContext context)
    {
        context.ShowHelp();
    }
}
