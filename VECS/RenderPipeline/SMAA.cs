using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VECS.GraphicsPipelines;
using VECS.LowLevel;
using VECS.SMAA;
using Vortice.Vulkan;

namespace VECS.RenderPipeline
{
    public class SMAA
    {
        public RenderTarget EdgeTarget;
        public RenderTarget BlendTarget;

        public Texture2D AreaTexture;
        public Texture2D SearchTexture;

        public Material EdgeDetection;
        public Material BlendWeightCalc;
        public Material NeighbourhoodBlending;

        public SMAA()
        {
            SearchTexture = new Texture2D("SMAA_Search", SMAASearchTexture.SEARCHTEX_WIDTH, SMAASearchTexture.SEARCHTEX_HEIGHT, SMAASearchTexture.SEARCHTEX_FORMAT, VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled, false);

            SearchTexture.CopyFromArray(SMAASearchTexture.SearchTexBytes);

            AreaTexture = new Texture2D("SMAA_Area", SMAAAreaTexture.AREATEX_WIDTH, SMAAAreaTexture.AREATEX_HEIGHT, SMAAAreaTexture.AREATEX_FORMAT, VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled, false);

            AreaTexture.CopyFromArray(SMAAAreaTexture.AreaTexBytes);

            var pipelineConfig = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            EdgeDetection = new("SMAA_Edge", "smaa_edge_detection.vert", "smaa_edge_detection.frag", pipelineConfig);
            BlendWeightCalc = new("SMAA_BlendWeight", "smaa_blending_weight.vert", "smaa_blending_weight.frag", pipelineConfig);
            NeighbourhoodBlending = new("SMAA_Blending", "smaa_neighbourhood_blending.vert", "smaa_neighbourhood_blending.frag", pipelineConfig);

            BlendWeightCalc.SetTexture("uAreaTexture".GetShaderPropertyId(), 0, AreaTexture);
            BlendWeightCalc.SetTexture("uSearchTexture".GetShaderPropertyId(), 0, SearchTexture);
        }

        public void RecreateRenderTargets()
        {
            EdgeTarget?.Dispose();
            BlendTarget?.Dispose();

            var windowExtents = SwapChain.Instance._windowExtent;

            EdgeTarget = new("MainColourAttachment", (int)windowExtents.width, (int)windowExtents.height, VkFormat.R32G32B32A32Sfloat);
            BlendTarget = new("BrightObjectAttachment", (int)windowExtents.width, (int)windowExtents.height, VkFormat.R32G32B32A32Sfloat);

            EdgeDetection.SetVector2("texelSize".GetShaderPropertyId(), 0, new(windowExtents.width, windowExtents.height));
            EdgeDetection.SetTexture("uColorTexture".GetShaderPropertyId(), 0, Presenter.Instance.ForwardRenderer.MainColourAttachment.Target);

            BlendWeightCalc.SetVector2("texelSize".GetShaderPropertyId(), 0, new(windowExtents.width, windowExtents.height));
            BlendWeightCalc.SetTexture("uEdgeTexture".GetShaderPropertyId(), 0, EdgeTarget.Target);

            NeighbourhoodBlending.SetVector2("texelSize".GetShaderPropertyId(), 0, new(windowExtents.width, windowExtents.height));
            BlendWeightCalc.SetTexture("uBlendTexture".GetShaderPropertyId(), 0, BlendTarget.Target);
            BlendWeightCalc.SetTexture("uColorTexture".GetShaderPropertyId(), 0, Presenter.Instance.ForwardRenderer.MainColourAttachment.Target);
        }


        
    }
}
