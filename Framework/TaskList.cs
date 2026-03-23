namespace OrganizeMedia.Framework;

public class TaskList : List<Task>
{
    public TaskList() : base()
    {
    }

    public TaskList(IEnumerable<Task> tasks) : base(tasks)
    {
    }

    public string ToDisplayString()
    {
        return $"[{string.Join(",", this)}]";
    }
}
