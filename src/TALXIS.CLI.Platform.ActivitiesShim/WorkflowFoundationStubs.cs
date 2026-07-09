namespace System.Activities;

// Windows Workflow Foundation does not exist on modern .NET. These are
// reflection-only stubs so the Package Deployer host can Assembly.GetTypes()
// net462 package assemblies that reference System.Activities (typically via
// shared libraries also used by workflow-activity projects). Workflow code
// never executes locally — activities run server-side in the Dataverse sandbox.

public abstract class Activity
{
    public string? DisplayName { get; set; }
}

public abstract class ActivityWithResult : Activity
{
}

public abstract class Activity<TResult> : ActivityWithResult
{
}

public abstract class CodeActivity : Activity
{
    protected abstract void Execute(CodeActivityContext context);

    protected virtual void CacheMetadata(CodeActivityMetadata metadata)
    {
    }
}

public abstract class CodeActivity<TResult> : Activity<TResult>
{
    protected abstract TResult Execute(CodeActivityContext context);

    protected virtual void CacheMetadata(CodeActivityMetadata metadata)
    {
    }
}

public class ActivityContext
{
    internal ActivityContext()
    {
    }
}

public class CodeActivityContext : ActivityContext
{
    internal CodeActivityContext()
    {
    }
}

public struct CodeActivityMetadata
{
}

public abstract class Argument
{
}

public class InArgument : Argument
{
}

public class InArgument<T> : InArgument
{
}

public class OutArgument : Argument
{
}

public class OutArgument<T> : OutArgument
{
}

public class InOutArgument : Argument
{
}

public class InOutArgument<T> : InOutArgument
{
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class RequiredArgumentAttribute : Attribute
{
}
