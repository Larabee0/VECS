using System;
using System.Collections.Generic;
using Vortice.Vulkan;

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

        public List<RenderPassAttachmentUsageInfo> Attachments = [];

        public List<string> DependantPasses = [];

        public Action<RendererFrameInfo> ExecuteFunc;

        public Func<RendererFrameInfo, bool> HasCommands;

        internal int ImageMemoryBarrierStackSize;

        public RenderPass(string name, PassType passType, PassCategory passCategory, List<RenderPassAttachmentUsageInfo> attachments, List<string> dependantPasses, Action<RendererFrameInfo> executeFunc, Func<RendererFrameInfo, bool> hasCommands)
        {
            Name = name;
            PassType = passType;
            PassCategory = passCategory;
            Attachments = attachments;
            DependantPasses = dependantPasses;
            ExecuteFunc = executeFunc;
            HasCommands = hasCommands;
        }

        public RenderPass(string name, PassType passType, PassCategory passCategory, List<RenderPassAttachmentUsageInfo> attachments, Action<RendererFrameInfo> executeFunc, Func<RendererFrameInfo, bool> hasCommands)
        {
            Name = name;
            PassType = passType;
            PassCategory = passCategory;
            Attachments = attachments;
            ExecuteFunc = executeFunc;
            HasCommands = hasCommands;
        }

        public RenderPass(string name, PassType passType, PassCategory passCategory, List<RenderPassAttachmentUsageInfo> attachments, List<string> dependantPasses, Action<RendererFrameInfo> executeFunc)
        {
            Name = name;
            PassType = passType;
            PassCategory = passCategory;
            Attachments = attachments;
            DependantPasses = dependantPasses;
            ExecuteFunc = executeFunc;
            HasCommands = RenderGraph.AlwaysRun;
        }

        public RenderPass(string name, PassType passType, PassCategory passCategory, List<RenderPassAttachmentUsageInfo> attachments, Action<RendererFrameInfo> executeFunc)
        {
            Name = name;
            PassType = passType;
            PassCategory = passCategory;
            Attachments = attachments;
            ExecuteFunc = executeFunc;
            HasCommands = RenderGraph.AlwaysRun;
        }

        public override string ToString()
        {
            return Name;
        }
    }

    public class RenderPassAttachmentUsageInfo
    {
        public string AttachmentResourceName;
        public VkImageLayout InputLayoutExpectations;
        public VkImageLayout OutputLayout;
        public bool InternalOnly;

        internal VkImageLayout InputOp = VkImageLayout.Undefined;
        internal VkImageLayout OutputOp = VkImageLayout.Undefined;

        public RenderPassAttachmentUsageInfo(string attachmentResourceName)
        {
            AttachmentResourceName = attachmentResourceName;
            InputLayoutExpectations = VkImageLayout.Undefined;
            OutputLayout = VkImageLayout.Undefined;
            InternalOnly = false;
        }
        public RenderPassAttachmentUsageInfo(string attachmentResourceName, VkImageLayout inputLayoutExpectations, VkImageLayout outputLayout)
        {
            AttachmentResourceName = attachmentResourceName;
            InputLayoutExpectations = inputLayoutExpectations;
            OutputLayout = outputLayout;
            InternalOnly = false;
        }
        public RenderPassAttachmentUsageInfo(string attachmentResourceName, VkImageLayout inputLayoutExpectations, VkImageLayout outputLayout, bool internalOnly)
        {
            AttachmentResourceName = attachmentResourceName;
            InputLayoutExpectations = inputLayoutExpectations;
            OutputLayout = outputLayout;
            InternalOnly = internalOnly;
        }

        public override string ToString()
        {
            return AttachmentResourceName;
        }

        public void ClearInOutOps()
        {
            InputOp = VkImageLayout.Undefined;
            OutputOp = VkImageLayout.Undefined;
        }

    }
}
