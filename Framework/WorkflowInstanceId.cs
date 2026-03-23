namespace OrganizeMedia.Framework;

public readonly record struct WorkflowInstanceId(WorkflowTemplateId WorkflowTemplateId, int InstanceNumber)
{
    public override string ToString()
    {
        return $"{WorkflowTemplateId}/{InstanceNumber}";
    }
}