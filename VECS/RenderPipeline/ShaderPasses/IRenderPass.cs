using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
