namespace OrganizeMedia.Framework;

public readonly record struct TaskInstanceId(TaskTemplateId TaskTemplateId, int InstanceNumber)
{
    public override string ToString()
    {
        return $"{TaskTemplateId}/{InstanceNumber}";
    }
}