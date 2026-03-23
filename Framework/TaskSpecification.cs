namespace OrganizeMedia.Framework;

public sealed record TaskSpecification
{
    public TaskSpecification(
        TaskSpecificationId TaskSpecificationId,
        TaskType TaskType,
        InputType? InputType = null,
        string? InputJson = null,
        TaskCardinality Cardinality = TaskCardinality.Singleton)
    {
        this.TaskSpecificationId = TaskSpecificationId;
        this.TaskType = TaskType;
        this.InputType = InputType;
        this.InputJson = InputJson;
        this.Cardinality = Cardinality;
    }

    public TaskSpecificationId TaskSpecificationId { get; }

    public TaskType TaskType { get; }

    public InputType? InputType { get; }

    public string? InputJson { get; }

    public TaskCardinality Cardinality { get; }

    internal TaskSpecification CreateRuntimeInstanceSpecification(InputType? inputType, string? inputJson)
    {
        return new TaskSpecification(
            TaskSpecificationId,
            TaskType,
            inputType,
            inputJson,
            TaskCardinality.Singleton);
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TaskSpecificationId.Value))
        {
            throw new InvalidOperationException("Task specifications must have a non-empty TaskSpecificationId.");
        }

        if (string.IsNullOrWhiteSpace(TaskType.Value))
        {
            throw new InvalidOperationException($"Task specification {TaskSpecificationId} must have a non-empty TaskType.");
        }

        if (!string.IsNullOrWhiteSpace(InputJson) &&
            (!InputType.HasValue || string.IsNullOrWhiteSpace(InputType.Value.Value)))
        {
            throw new InvalidOperationException(
                $"Task specification {TaskSpecificationId} must provide InputType when InputJson is present.");
        }

        switch (Cardinality)
        {
            case TaskCardinality.Singleton:
            case TaskCardinality.ZeroToMany:
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(Cardinality), Cardinality, "Unsupported task cardinality.");
        }
    }
}
