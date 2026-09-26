namespace VECS
{
    public interface IRenderPass
    {
        public void AddToRenderGraph();
        public void SetEnabled(bool enabled);
        public void RecreateRenderTargets();
        public void PrePresent();
    }
}
