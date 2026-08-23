using System;
using System.Collections.Generic;

namespace VECS
{
    public enum PassType
    {
        ColourDepthStencil,
        Compute
    }

    public class RenderPass
    {
        public string Name;
        public PassType PassType;
        public List<string> Inputs = [];
        public List<string> Outputs = [];

        public Action<RendererFrameInfo> ExecuteFunc;
    }
}
