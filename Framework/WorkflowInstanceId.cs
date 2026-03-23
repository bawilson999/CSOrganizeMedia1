namespace OrganizeMedia.Framework;

public readonly record struct WorkflowInstanceId(WorkflowSpecificationId WorkflowSpecificationId, int InstanceNumber)
{
    public override string ToString()
    {
        return $"{WorkflowSpecificationId}/{InstanceNumber}";
    }
}