namespace OrganizeMedia.Framework;

public class TaskGraph
{
    private readonly Dictionary<Task, List<Task>> _adjacencyList;
    private readonly Dictionary<Task, List<Task>> _dependencyList;

    public TaskGraph()
    {
        _adjacencyList = new Dictionary<Task, List<Task>>();
        _dependencyList = new Dictionary<Task, List<Task>>();
    }

    public void AddTask(Task task)
    {
        if (task is null)
            throw new ArgumentNullException(nameof(task));

        if (_adjacencyList.ContainsKey(task))
        {
            throw new InvalidOperationException($"Task graph already contains task {task.TaskId}.");
        }

        _adjacencyList.Add(task, new List<Task>());
        _dependencyList.Add(task, new List<Task>());
    }

    public void AddAdjacency(Task task, Task adjacentTask)
    {
        if (task == adjacentTask)
        {
            throw new InvalidOperationException($"Task graph cannot add a self-dependency for task {task.TaskId}.");
        }

        if (!_adjacencyList.ContainsKey(task))
            throw new KeyNotFoundException("task not found");

        if (!_adjacencyList.ContainsKey(adjacentTask))
            throw new KeyNotFoundException("adjacentTask not found");

        if (_adjacencyList[task].Contains(adjacentTask))
        {
            throw new InvalidOperationException(
                $"Task graph already contains dependency {task.TaskId} -> {adjacentTask.TaskId}.");
        }

        if (HasPath(adjacentTask, task))
        {
            throw new InvalidOperationException(
                $"Task graph cannot add dependency {task.TaskId} -> {adjacentTask.TaskId} because it would introduce a cycle.");
        }

        _adjacencyList[task].Add(adjacentTask);
        _dependencyList[adjacentTask].Add(task);
    }

    public IReadOnlyCollection<Task> GetAdjacencies(Task task)
    {
        return _adjacencyList[task];
    }

    public IReadOnlyCollection<Task> GetTasks()
    {
        return _adjacencyList.Keys.ToArray();
    }

    public IReadOnlyCollection<Task> GetDependencies(Task task)
    {
        return _dependencyList[task];
    }

    public IReadOnlyList<Task> TopologicalSort()
    {
        Stack<Task> stack = new Stack<Task>();
        HashSet<Task> visited = new HashSet<Task>();

        foreach (Task task in _adjacencyList.Keys)
        {
            if (!visited.Contains(task))
            {
                FindTopologicalSort(task, visited, stack);
            }
        }

        return stack.ToArray();
    }

    private void FindTopologicalSort(Task task, HashSet<Task> visited, Stack<Task> stack)
    {
        visited.Add(task);
        foreach (Task adjacentTask in _adjacencyList[task])
        {
            if (!visited.Contains(adjacentTask))
            {
                FindTopologicalSort(adjacentTask, visited, stack);
            }
        }
        stack.Push(task);
    }

    private bool HasPath(Task startTask, Task targetTask)
    {
        Stack<Task> stack = new Stack<Task>();
        HashSet<Task> visited = new HashSet<Task>();
        stack.Push(startTask);

        while (stack.Count > 0)
        {
            Task task = stack.Pop();

            if (!visited.Add(task))
                continue;

            if (task == targetTask)
                return true;

            foreach (Task adjacentTask in _adjacencyList[task])
            {
                stack.Push(adjacentTask);
            }
        }

        return false;
    }
}
