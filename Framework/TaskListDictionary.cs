namespace OrganizeMedia.Framework;

public class TaskListDictionary : Dictionary<Task, TaskList>
{
    public TaskListDictionary()
    {
    }

    public TaskListDictionary(IEnumerable<KeyValuePair<Task, TaskList>> keyValuePairs) : base(keyValuePairs)
    {
    }

    public string ToDisplayString()
    {
        return string.Join(
            Environment.NewLine,
            this.Select(pair => $"{pair.Key}: {pair.Value.ToDisplayString()}"));
    }
}
