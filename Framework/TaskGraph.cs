namespace OrganizeMedia.Framework;

public class TaskGraph
{
    TaskListDictionary _adjacencyList;
    TaskListDictionary _dependencyList;

    public TaskGraph()
    {
        _adjacencyList = new TaskListDictionary();
        _dependencyList = new TaskListDictionary();
    }

    public void AddTask(Task task)
    {
        if (task is null)
            throw new ArgumentNullException(nameof(task));

        if (_adjacencyList.ContainsKey(task))
        {
            throw new InvalidOperationException($"Task graph already contains task {task.TaskId}.");
        }

        _adjacencyList.Add(task, new TaskList());
        _dependencyList.Add(task, new TaskList());
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

    public TaskListDictionary GetAdjacencyList()
    {
        TaskListDictionary adjacencyList = new TaskListDictionary(_adjacencyList);
        return adjacencyList;
    }

    public TaskList GetAdjacencies(Task task)
    {
        return _adjacencyList[task];
    }

    public TaskList GetTasks()
    {
        return new TaskList(_adjacencyList.Keys);
    }

    public TaskListDictionary GetDependencyList()
    {
        TaskListDictionary dependencyList = new TaskListDictionary(_dependencyList);
        return dependencyList;
    }

    public TaskList GetDependencies(Task task)
    {
        return _dependencyList[task];
    }

    public TaskList TopologicalSort()
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

        return new TaskList(stack);
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

    // public TaskListDictionary BuildDependencyList()
    // {
    //     TaskListDictionary dependencyList = new TaskListDictionary();

    //     foreach ((Task task, TaskList adjacentTasks) in _adjacencyList)
    //     {
    //         foreach (Task adjacentTask in adjacentTasks)
    //         {
    //             if (dependencyList.TryGetValue(adjacentTask, out TaskList dependentTasks))
    //             {
    //                 dependentTasks.Add(task);
    //             }
    //             else
    //             {
    //                 dependentTasks = [task];
    //                 dependencyList.Add(adjacentTask, dependentTasks);
    //             }
    //         }
    //     }

    //     return dependencyList;
    // }
}
