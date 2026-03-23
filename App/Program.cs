namespace OrganizeMedia.App;

using OrganizeMedia.Framework;
using System.IO;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
        Workflow workflow = WorkflowExamples.CreateStaticSampleWorkflow();
        Console.WriteLine(workflow.ToGraphDisplayString());
        workflow.RunToCompletion(observer: new TextWriterWorkflowObserver(Console.Out));
        Console.WriteLine("Goodbye, World!");
    }
}