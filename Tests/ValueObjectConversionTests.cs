namespace OrganizeMedia.Tests;

using OrganizeMedia.Framework;

public class ValueObjectConversionTests
{
    [Fact]
    public void ValueObjects_CanBeExplicitlyCreatedFromStrings()
    {
        TaskTemplateId taskId = (TaskTemplateId)"A";
        WorkflowTemplateId workflowId = (WorkflowTemplateId)"W0";
        TaskType taskType = (TaskType)"ScanDirectory";
        InputType inputType = (InputType)"application/json";

        Assert.Equal("A", taskId.Value);
        Assert.Equal("W0", workflowId.Value);
        Assert.Equal("ScanDirectory", taskType.Value);
        Assert.Equal("application/json", inputType.Value);
    }

    [Fact]
    public void ValueObjects_CanBeImplicitlyConvertedToStrings()
    {
        string taskId = new TaskTemplateId("A");
        string workflowId = new WorkflowTemplateId("W0");
        string taskType = new TaskType("ScanDirectory");
        string inputType = new InputType("application/json");

        Assert.Equal("A", taskId);
        Assert.Equal("W0", workflowId);
        Assert.Equal("ScanDirectory", taskType);
        Assert.Equal("application/json", inputType);
    }
}