namespace OrganizeMedia.Framework;

public readonly record struct TaskType(string Value)
{
    public static explicit operator TaskType(string value)
    {
        return new TaskType(value);
    }

    public static implicit operator string(TaskType taskType)
    {
        return taskType.Value;
    }

    public override string ToString()
    {
        return Value;
    }
}