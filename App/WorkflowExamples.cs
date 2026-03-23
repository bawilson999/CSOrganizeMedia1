namespace OrganizeMedia.App;

using OrganizeMedia.Framework;

internal static class WorkflowExamples
{
    internal static Workflow CreateStaticSampleWorkflow()
    {
        return FromAdjacencyArray("W0", [[1], [], [1], [2], [2]]);
    }

    private static Workflow FromAdjacencyArray(string workflowIdString, int[][] adjacencyArray)
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
            int[] adjacentTaskIndices = adjacencyArray[i] ?? Array.Empty<int>();

            for (int j = 0; j < adjacentTaskIndices.Length; j++)
            {
                int adjacentIndex = adjacentTaskIndices[j];
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