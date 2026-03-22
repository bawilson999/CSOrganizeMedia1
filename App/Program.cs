namespace OrganizeMedia.App;

using OrganizeMedia.Framework;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
        Workflow workflow = WorkflowFromArray("W0", [[1], [], [1], [2], [2]]);
        workflow.ConsoleWrite();
        workflow.RunToCompletion();
        Console.WriteLine("Goodbye, World!");
    }

    static Workflow WorkflowFromArray(string workflowIdString, int[][] adjacencyArray)
    {
        WorkflowId workflowId = new WorkflowId(workflowIdString);
        List<TaskSpecification> taskSpecifications = new List<TaskSpecification>();
        List<TaskDependencySpecification> dependencySpecifications = new List<TaskDependencySpecification>();

        for (int i = 0; i < adjacencyArray.Length; i++)
        {
            TaskId taskId = new TaskId("T" + i.ToString());
            taskSpecifications.Add(
                new TaskSpecification(
                    TaskId: taskId,
                    TaskType: "StaticTask",
                    InputType: "application/json",
                    InputJson: $"{{\"taskIndex\":{i}}}"));
        }

        for (int i = 0; i < adjacencyArray.Length; i++)
        {
            for (int j = 0; j < adjacencyArray[i].Length; j++)
            {
                int adjacentIndex = adjacencyArray[i][j];
                dependencySpecifications.Add(
                    new TaskDependencySpecification(
                        PrerequisiteTaskId: new TaskId("T" + i.ToString()),
                        DependentTaskId: new TaskId("T" + adjacentIndex.ToString())));
            }
        }

        WorkflowSpecification specification = new WorkflowSpecification(
            WorkflowId: workflowId,
            Tasks: taskSpecifications,
            Dependencies: dependencySpecifications);

        return Workflow.FromSpecification(specification);
    }
}