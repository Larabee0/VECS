using System;
using System.Collections.Generic;

namespace VECS
{
    public enum PassType
    {
        Render,
        Compute
    }

    public enum PassCategory
    {
        Opaque,
        Transparent,
        AntiAliasing,
        PostProcessing,
        UI
    }

    public class RenderPass
    {
        public string Name;
        public PassType PassType;
        public PassCategory PassCategory;
        public int RelativeOrder;
        public List<string> Inputs = [];
        public List<string> Outputs = [];

        public List<string> DependantPasses = [];

        public Action<RendererFrameInfo> ExecuteFunc;

        public override string ToString()
        {
            return Name;
        }
    }
}
