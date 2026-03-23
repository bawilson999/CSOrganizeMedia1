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
        WorkflowSpecificationId workflowSpecificationId = new WorkflowSpecificationId(workflowIdString);
        List<TaskSpecification> taskSpecifications = new List<TaskSpecification>();
        List<TaskDependencySpecification> dependencySpecifications = new List<TaskDependencySpecification>();

        for (int i = 0; i < adjacencyArray.Length; i++)
        {
            TaskSpecificationId taskSpecificationId = new TaskSpecificationId("T" + i.ToString());
            taskSpecifications.Add(
                new TaskSpecification(
                    TaskSpecificationId: taskSpecificationId,
                    TaskType: new TaskType("StaticTask"),
                    InputType: new InputType("application/json"),
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
                        PrerequisiteTaskSpecificationId: new TaskSpecificationId("T" + i.ToString()),
                        DependentTaskSpecificationId: new TaskSpecificationId("T" + adjacentIndex.ToString())));
            }
        }

        WorkflowSpecification specification = new WorkflowSpecification(
            WorkflowSpecificationId: workflowSpecificationId,
            Tasks: taskSpecifications,
            Dependencies: dependencySpecifications);

        return Workflow.FromSpecification(specification);
    }
}