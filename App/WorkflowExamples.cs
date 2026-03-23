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
        WorkflowTemplateId workflowTemplateId = new WorkflowTemplateId(workflowIdString);
        List<TaskSpecification> taskSpecifications = new List<TaskSpecification>();
        List<TaskDependencySpecification> dependencySpecifications = new List<TaskDependencySpecification>();

        for (int i = 0; i < adjacencyArray.Length; i++)
        {
            TaskTemplateId taskTemplateId = new TaskTemplateId("T" + i.ToString());
            taskSpecifications.Add(
                new TaskSpecification(
                    TaskTemplateId: taskTemplateId,
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
                        PrerequisiteTaskTemplateId: new TaskTemplateId("T" + i.ToString()),
                        DependentTaskTemplateId: new TaskTemplateId("T" + adjacentIndex.ToString())));
            }
        }

        WorkflowSpecification specification = new WorkflowSpecification(
            WorkflowTemplateId: workflowTemplateId,
            Tasks: taskSpecifications,
            Dependencies: dependencySpecifications);

        return Workflow.FromSpecification(specification);
    }
}