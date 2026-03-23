namespace OrganizeMedia.Framework;

public readonly record struct TaskInstanceId(TaskId TemplateTaskId, int InstanceNumber)
{
    public override string ToString()
    {
        return $"{TemplateTaskId}/{InstanceNumber}";
    }
}