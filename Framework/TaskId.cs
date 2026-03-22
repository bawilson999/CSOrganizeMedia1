namespace OrganizeMedia.Framework;

public readonly record struct TaskId(string Value)
{
    public static explicit operator TaskId(string value)
    {
        return new TaskId(value);
    }

    public static implicit operator string(TaskId taskId)
    {
        return taskId.Value;
    }

    public override string ToString()
    {
        return Value;
    }
}