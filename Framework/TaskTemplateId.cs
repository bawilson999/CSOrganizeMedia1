namespace OrganizeMedia.Framework;

public readonly record struct TaskTemplateId(string Value)
{
    public static explicit operator TaskTemplateId(string value)
    {
        return new TaskTemplateId(value);
    }

    public static implicit operator string(TaskTemplateId taskTemplateId)
    {
        return taskTemplateId.Value;
    }

    public override string ToString()
    {
        return Value;
    }
}