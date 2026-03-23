namespace OrganizeMedia.Framework;

public record TaskSpecification
{
    public TaskSpecification(
        TaskTemplateId TaskTemplateId,
        TaskType TaskType,
        InputType? InputType = null,
        string? InputJson = null,
        TaskCardinality Cardinality = TaskCardinality.Singleton)
        : this(
            TaskTemplateId,
            TaskType,
            InputType,
            InputJson,
            Cardinality,
            GetDefaultInitialInstanceCount(Cardinality))
    {
    }

    internal TaskSpecification(
        TaskTemplateId TaskTemplateId,
        TaskType TaskType,
        InputType? InputType,
        string? InputJson,
        TaskCardinality Cardinality,
        int InitialInstanceCount)
    {
        this.TaskTemplateId = TaskTemplateId;
        this.TaskType = TaskType;
        this.InputType = InputType;
        this.InputJson = InputJson;
        this.Cardinality = Cardinality;
        this.InitialInstanceCount = InitialInstanceCount;
    }

    public TaskTemplateId TaskTemplateId { get; init; }

    public TaskType TaskType { get; init; }

    public InputType? InputType { get; init; }

    public string? InputJson { get; init; }

    public TaskCardinality Cardinality { get; init; }

    public int InitialInstanceCount { get; internal init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TaskTemplateId.Value))
        {
            throw new InvalidOperationException("Task specifications must have a non-empty TaskTemplateId.");
        }

        if (string.IsNullOrWhiteSpace(TaskType.Value))
        {
            throw new InvalidOperationException($"Task specification {TaskTemplateId} must have a non-empty TaskType.");
        }

        if (!string.IsNullOrWhiteSpace(InputJson) &&
            (!InputType.HasValue || string.IsNullOrWhiteSpace(InputType.Value.Value)))
        {
            throw new InvalidOperationException(
                $"Task specification {TaskTemplateId} must provide InputType when InputJson is present.");
        }

        if (InitialInstanceCount < 0)
        {
            throw new InvalidOperationException(
                $"Task specification {TaskTemplateId} must use a non-negative InitialInstanceCount.");
        }

        if (Cardinality == TaskCardinality.Singleton && InitialInstanceCount != 1)
        {
            throw new InvalidOperationException(
                $"Task specification {TaskTemplateId} must use InitialInstanceCount = 1 when Cardinality is Singleton.");
        }

        if (Cardinality == TaskCardinality.ZeroToMany && InitialInstanceCount != 0)
        {
            throw new InvalidOperationException(
                $"Task specification {TaskTemplateId} must use InitialInstanceCount = 0 when Cardinality is ZeroToMany.");
        }
    }

    private static int GetDefaultInitialInstanceCount(TaskCardinality cardinality)
    {
        return cardinality switch
        {
            TaskCardinality.Singleton => 1,
            TaskCardinality.ZeroToMany => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(cardinality), cardinality, "Unsupported task cardinality.")
        };
    }
}
