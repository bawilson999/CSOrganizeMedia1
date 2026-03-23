namespace OrganizeMedia.Framework;

public readonly record struct TaskSpecificationId(string Value)
{
    public static explicit operator TaskSpecificationId(string value)
    {
        return new TaskSpecificationId(value);
    }

    public static implicit operator string(TaskSpecificationId taskSpecificationId)
    {
        return taskSpecificationId.Value;
    }

    public override string ToString()
    {
        return Value;
    }
}