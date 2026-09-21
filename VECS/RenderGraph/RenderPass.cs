using System;
using System.Collections.Generic;

namespace VECS
{
    public enum PassType
    {
        Render,
        Compute
    }

    [Flags]
    public enum PassCategory
    {
        None = 0,
        FixedMap = 1,
        PreRendering = 2,
        Opaque = 4,
        Transparent = 8,
        PostRendering = 16,
        PostProcessing = 32,
        UI = 64,
        All = FixedMap | PreRendering | Opaque | Transparent | PostRendering | PostProcessing | UI,

        MainView = PreRendering | Opaque | Transparent | PostRendering | PostProcessing | UI,
        SecondaryView = PreRendering | Opaque | Transparent | PostRendering,
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
