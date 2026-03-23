namespace OrganizeMedia.Tests;

using OrganizeMedia.Framework;

public class WorkflowValidationTests
{
    [Fact]
    public void FromAdjacencyArray_RejectsDuplicateDependencies()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => WorkflowTestSupport.FromAdjacencyArray("DuplicateDependency", [[1, 1], []]));

        Assert.Contains("duplicate dependency", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FromAdjacencyArray_RejectsCycles()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => WorkflowTestSupport.FromAdjacencyArray("Cycle", [[1], [0]]));

        Assert.Contains("cycle", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}