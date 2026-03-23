namespace OrganizeMedia.App;

using OrganizeMedia.Framework;
using System.IO;

class Program
{
    static void Main(string[] args)
    {
        Workflow workflow = WorkflowExamples.CreateStaticSampleWorkflow();
        workflow.RunToCompletion(observer: new TextWriterWorkflowObserver(Console.Out));
    }
}