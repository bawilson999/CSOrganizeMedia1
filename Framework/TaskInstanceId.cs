namespace OrganizeMedia.Framework;

public readonly record struct TaskInstanceId(TaskSpecificationId TaskSpecificationId, int InstanceNumber)
{
    public override string ToString()
    {
        return $"{TaskSpecificationId}/{InstanceNumber}";
    }
}