namespace OrganizeMedia.Framework;

public readonly record struct WorkflowId(string Value)
{
    public static implicit operator WorkflowId(string value)
    {
        return new WorkflowId(value);
    }

    public static implicit operator string(WorkflowId workflowId)
    {
        return workflowId.Value;
    }

    public override string ToString()
    {
        return Value;
    }
}
