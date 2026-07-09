using System.Reflection;
using TALXIS.CLI.Platform.Xrm;
using Xunit;

namespace TALXIS.CLI.Tests.Xrm;

public class LegacyAssemblyRuntimeTests
{
    [Fact]
    public void Load_SystemActivities_WithNetFrameworkIdentity_ResolvesStub()
    {
        LegacyAssemblyRuntime.EnsureInitialized();

        // Exact identity that net462 package assemblies reference.
        var assembly = Assembly.Load("System.Activities, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");

        Assert.NotNull(assembly);
        var codeActivity = assembly.GetType("System.Activities.CodeActivity");
        Assert.NotNull(codeActivity);
        Assert.NotNull(codeActivity!.GetMethod("Execute", BindingFlags.Instance | BindingFlags.NonPublic));
    }

    [Fact]
    public void GetTypes_OnActivitySubclass_DoesNotThrow()
    {
        LegacyAssemblyRuntime.EnsureInitialized();

        var types = typeof(SampleActivity).Assembly.GetTypes();

        Assert.Contains(typeof(SampleActivity), types);
    }

    private sealed class SampleActivity : global::System.Activities.CodeActivity
    {
        protected override void Execute(global::System.Activities.CodeActivityContext context)
        {
        }
    }
}
