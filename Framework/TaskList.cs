namespace OrganizeMedia.Framework;

public class TaskList : List<Task>
{
    public TaskList() : base()
    {
    }

    public TaskList(IEnumerable<Task> tasks) : base(tasks)
    {
    }

    public void ConsoleWrite()
    {
        Console.Write("[");
        bool writeComma = false;
        foreach (Task task in this)
        {
            if (writeComma)
                Console.Write(",");
            else
                writeComma = true;
            Console.Write(task);
        }
        Console.WriteLine("]");
    }
}
