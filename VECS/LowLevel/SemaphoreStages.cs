namespace VECS.LowLevel
{
    public enum SemaphoreStages : ulong
    {
        Submit = 1,
        ComputeComplete,
        QueuePresent,
        RenderComplete,
        MAX_STAGES
    }
}
