using System.Text.Json;

namespace OrganizeMedia.Framework;

public abstract record ExecutionOutput
{
    public virtual string OutputType => GetType().Name;

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, GetType());
    }
}

public record TextExecutionOutput(string Value) : ExecutionOutput;