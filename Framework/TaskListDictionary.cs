namespace OrganizeMedia.Framework;

public class TaskListDictionary : Dictionary<Task, TaskList>
{
    public TaskListDictionary()
    {
    }

    public TaskListDictionary(IEnumerable<KeyValuePair<Task, TaskList>> keyValuePairs) : base(keyValuePairs)
    {
    }

    public void ConsoleWrite()
    {
        foreach ((Task task, TaskList taskList) in this)
        {
            Console.Write($"{task}: ");
            taskList.ConsoleWrite();
        }
    }
}
