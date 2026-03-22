namespace OrganizeMedia.Framework;

public enum ExecutionPhase
{
    NotStarted,
    Queued,
    ReadyToRun,
    Running,
    Finished,
    Unknown = -1,
}