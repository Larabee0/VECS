namespace VECS.LowLevel
{
    public enum SemaphoreStages : ulong
    {
        // Signaled by main thread when it is ready to begin recording command buffers
        Submit = 1,

        // Signaled by compute thread when VkQueueSubmit has been called. Waited on by Graphics thread before submission when waitForCompute is true
        ComputeQueued,

        // Signaled by graphics thread signals when VkQueueSubmit has been called AND waitForCompute is false. Waited on by present thread before swapchain submit when waitForCompute is false
        QueuePresentEarly,

        // Signaled by graphics thread when waitForCompute is true. Waited on by GPU to begin execution of compute queue
        StartCompute,


        // Signaled by GPU when compute queue is completed. Waited on by GPU to begin exeuction of graphics queue
        ComputeComplete,

        // Signaled by graphis thread when VkQueueSubmit has been called AND waitForCompute is true. Waited on by present thread before swapchain submit when waitForCompute is true
        QueuePresentLate,

        // Signaled by GPU when graphics queue is completed. Waited on by present thread after swapchain submission
        RenderComplete,

        // indicates next frame
        MAX_STAGES
    }
}
