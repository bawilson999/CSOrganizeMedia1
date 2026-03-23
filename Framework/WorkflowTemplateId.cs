namespace OrganizeMedia.Framework;

public readonly record struct WorkflowTemplateId(string Value)
{
    public static explicit operator WorkflowTemplateId(string value)
    {
        return new WorkflowTemplateId(value);
    }

    public static implicit operator string(WorkflowTemplateId workflowTemplateId)
    {
        return workflowTemplateId.Value;
    }

    public override string ToString()
    {
        return Value;
    }
}