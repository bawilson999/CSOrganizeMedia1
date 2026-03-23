namespace OrganizeMedia.App;

using OrganizeMedia.Framework;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
        Workflow workflow = WorkflowExamples.CreateStaticSampleWorkflow();
        workflow.ConsoleWrite();
        workflow.RunToCompletion();
        Console.WriteLine("Goodbye, World!");
    }
}