namespace OrganizeMedia.Framework;

public readonly record struct WorkflowSpecificationId(string Value)
{
    public static explicit operator WorkflowSpecificationId(string value)
    {
        return new WorkflowSpecificationId(value);
    }

    public static implicit operator string(WorkflowSpecificationId workflowSpecificationId)
    {
        return workflowSpecificationId.Value;
    }

    public override string ToString()
    {
        return Value;
    }
}