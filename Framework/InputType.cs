namespace OrganizeMedia.Framework;

public readonly record struct InputType(string Value)
{
    public static explicit operator InputType(string value)
    {
        return new InputType(value);
    }

    public static implicit operator string(InputType inputType)
    {
        return inputType.Value;
    }

    public override string ToString()
    {
        return Value;
    }
}