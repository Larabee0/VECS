using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{

    public static class RenderGraph
    {
        // assumed render targets

        public static readonly string MainColourAttachment = "MainColourAttachment";
        public static readonly string MainDepthAttachment = "MainDepthAttachment";
        public static readonly string PostProcessingColourAttachment = "PostProcessingColourAttachment";

        private static Dictionary<string, RenderTargetDefintion> ResourceDefinitons = [];
        private static Dictionary<string, RenderTarget> Resources = [];
        private static List<string> MatchScreenSize = [];
        private static List<RenderPass> Passes = [];
        private static List<int> ExecutionOrder = [];

        private static bool Recompile = true;


        private static HashSet<string> RemovePasses = [];
        private static HashSet<string> DisabledPasses = [];

#if DEBUG
        private static string[] ExecutionOrderEnglish;
#endif

        public static RenderTarget GetResource(string name)
        {
            Resources.TryGetValue(name, out RenderTarget rt);
            return rt;
        }

        public static bool TryGetResource(string name, out RenderTarget resource)
        {
            resource = GetResource(name);
            return resource != null;
        }

        public static bool GetResourceDefintion(string name, out RenderTargetDefintion rt)
        {
            return ResourceDefinitons.TryGetValue(name,out  rt);
        }

        public static void RemoveResource(string name)
        {
            Resources.Remove(name);
        }

        public static void AddResource(RenderTargetDefintion renderTargetDef)
        {
            if (renderTargetDef.TargetDisplay != -1)
            {
                MatchScreenSize.Add(renderTargetDef.Name);
            }
            ResourceDefinitons[renderTargetDef.Name] = renderTargetDef;
        }

        public static void AddResource(string name, RenderTarget renderTarget)
        {
            Resources[name] = renderTarget;
        }

        public static RenderTarget GetOrCreateResource(string name, VkExtent2D extent)
        {

            var attachment = GetResource(name);

            if (GetResourceDefintion(name, out var rtDef))
            {
                attachment = IRenderer.CreateOrUpdateRT(attachment, rtDef, extent);
            }

            AddResource(name, attachment);

            return attachment;
        }
        public static bool AlwaysRun(RendererFrameInfo frameInfo)
        {
            return true;
        }

        public static void AddPass(RenderPass pass)
        {
            Passes.Add(pass);
            Recompile = true;
        }

        public static void RecreateAttachments(int display, VkExtent2D extent)
        {
            foreach (var item in Resources)
            {
                if(item.Value.TargetDisplay == display)
                {
                    IRenderer.UpdateRT(Resources[item.Key], extent);
                }
            }

            foreach (var item in ResourceDefinitons)
            {
                if(!Resources.ContainsKey(item.Key))
                {
                    if (item.Value.TargetDisplay == display)
                    {
                        Resources[item.Key] = IRenderer.CreateOrUpdateRT(null, item.Value, extent);
                    }
                }
            }

        }

        private static List<RenderPass> localExecutePass = [];

        private static Dictionary<string, int> _attachmentFrequency = [];
        private static Dictionary<string, VkImageLayout> _imaginaryLayout = [];

        private static void BuildMemoryBarriers()
        {
            _attachmentFrequency.Clear();
            _imaginaryLayout.Clear();
            localExecutePass.ForEach(pass =>
            {
                pass.Attachments.ForEach(attachment =>
                {
                    if (!_attachmentFrequency.TryAdd(attachment.AttachmentResourceName, 1))
                    {
                        _attachmentFrequency[attachment.AttachmentResourceName]++;
                    }
                    _imaginaryLayout.TryAdd(attachment.AttachmentResourceName, VkImageLayout.Undefined);
                });
            });

            for (int i = 0; i < localExecutePass.Count; i++)
            {
                List<RenderPassAttachmentUsageInfo> attachments = localExecutePass[i].Attachments;
                localExecutePass[i].ImageMemoryBarrierStackSize = 0;
                for (int j = 0; j < attachments.Count; j++)
                {
                    attachments[j].ClearInOutOps();
                    if (TryGetResource(attachments[j].AttachmentResourceName, out var resource))
                    {
                        localExecutePass[i].ImageMemoryBarrierStackSize += resource.Target.ImageCount;
                    }
                    if (_attachmentFrequency[ attachments[j].AttachmentResourceName] == 1)
                    {
                        _imaginaryLayout[attachments[j].AttachmentResourceName] = 
                            attachments[j].InputOp = 
                                _imaginaryLayout[attachments[j].AttachmentResourceName] == VkImageLayout.Undefined 
                                    ? attachments[j].InputLayoutExpectations 
                                    : VkImageLayout.Undefined;
                        if (attachments[j].OutputLayout != VkImageLayout.Undefined && attachments[j].OutputLayout != attachments[j].InputOp)
                        {
                            //_imaginaryLayout[attachments[j].AttachmentResourceName] = attachments[j].OutputOp = attachments[j].OutputLayout;
                        }
                    }
                    else
                    {
                        var currentLayout = _imaginaryLayout[attachments[j].AttachmentResourceName];
                        var inputLayout = attachments[j].InputLayoutExpectations;
                        var outputLayout = attachments[j].OutputLayout;
                        outputLayout = outputLayout == VkImageLayout.Undefined ? inputLayout : outputLayout;
                        if (inputLayout != VkImageLayout.Undefined && currentLayout != inputLayout)
                        {
                            currentLayout = attachments[j].InputOp = inputLayout;
                        }

                        var nextOccurance = GetNextPassWithAttachment(i, attachments[j].AttachmentResourceName);
                        if (nextOccurance != null)
                        {
                            if (nextOccurance.InputLayoutExpectations != outputLayout)
                            {
                                outputLayout = nextOccurance.InputLayoutExpectations;
                            }

                            if (outputLayout != VkImageLayout.Undefined && outputLayout != currentLayout)
                            {
                                //currentLayout = attachments[j].OutputOp = outputLayout;
                            }
                        }

                        _imaginaryLayout[attachments[j].AttachmentResourceName] = currentLayout;
                    }
                    Debug.Assert(_imaginaryLayout[attachments[j].AttachmentResourceName] != VkImageLayout.Undefined, "Render target used in pass has potentially undefined layout");
                }
            }
        }

        private static RenderPassAttachmentUsageInfo GetNextPassWithAttachment(int index, string attachment)
        {
            for (int i = index + 1; i < localExecutePass.Count; i++)
            {
                var nextAttachment = localExecutePass[i].Attachments.FirstOrDefault(a => a.AttachmentResourceName == attachment);
                if (nextAttachment == null) continue;
                return nextAttachment;
            }
            return null;
        }

        public static void Execute(RendererFrameInfo frameInfo, ulong semaphoreOffset, PassCategory category = PassCategory.All)
        {
            if (Recompile) Compile();
            _category = category;
            _frameInfo = frameInfo;
            _semaphoreOffset = semaphoreOffset;
            
            localExecutePass.Clear();
            for (int i = 0; i < ExecutionOrder.Count; i++)
            {

                int passIndex = ExecutionOrder[i];
                var pass = Passes[passIndex];
                if ((_category & pass.PassCategory) == 0 || DisabledPasses.Contains(pass.Name) || !pass.HasCommands(new(_frameInfo, VkCommandBuffer.Null))) continue;

                localExecutePass.Add(pass);
            }

            if (localExecutePass.Count == 0) return;

            if(_secondaries.Length < localExecutePass.Count)
            {
                Array.Resize(ref _secondaries, localExecutePass.Count);
            }

            BuildMemoryBarriers();

            for (int i = 0; i < localExecutePass.Count; i++)
            {
                ExecuatePass(i);
            }
            Span<VkCommandBuffer> commandBuffers = stackalloc VkCommandBuffer[localExecutePass.Count];
            for (int i = 0; i < localExecutePass.Count; i++)
            {
                commandBuffers[i] = _secondaries[i].Item2;
                //GraphicsDevice.DeviceAPI.vkCmdExecuteCommands(frameInfo.CommandBuffer, _secondaries[i].Item2);
            }

            //GraphicsDevice.DeviceAPI.vkCmdExecuteCommands(frameInfo.CommandBuffer, commandBuffers);

            Array.Clear(_secondaries);
        }

        private static PassCategory _category;
        private static RendererFrameInfo _frameInfo;
        private static ulong _semaphoreOffset;
        private static (string,VkCommandBuffer)[] _secondaries = [];

        public static ulong GetMaxPassRelativeSemaphoreValue()
        {
            return (ulong)ExecutionOrder.Count;
        }

        private static void ExecuatePass(int index)
        {
            var pass = localExecutePass[index];
            //if ((_category & pass.PassCategory) == 0 || DisabledPasses.Contains(pass.Name) || !pass.HasCommands(new(_frameInfo, VkCommandBuffer.Null))) return;

            var commandBuffer = _frameInfo.CommandBuffer;// GraphicsDevice.GetSecondaryCommandBuffer();
            //_secondaries[index] = (pass.Name, commandBuffer);
            //GraphicsDevice.SetObjectName(VkObjectType.CommandBuffer, commandBuffer.Handle, $"{pass.Name} Cmd Buffer");

            //GraphicsDevice.BeginSecondaryCommandBuffer(commandBuffer);
            RendererFrameInfo frameInfo = _frameInfo;// new(_frameInfo,commandBuffer);
            GraphicsDevice.BeginLabelCmd(commandBuffer, pass.Name);

            Span<VkImageMemoryBarrier2> imageBarriers = stackalloc VkImageMemoryBarrier2[pass.ImageMemoryBarrierStackSize];

            int imageBarrierCount = GetMemoryBarriers(pass, imageBarriers, false);

            MemoryBarrierHelper.ImageMemoryBarrier(commandBuffer, imageBarriers[..imageBarrierCount]);
            
            pass.ExecuteFunc(frameInfo);

            imageBarriers.Clear();

            imageBarrierCount = GetMemoryBarriers(pass, imageBarriers, true);

            MemoryBarrierHelper.ImageMemoryBarrier(commandBuffer, imageBarriers[..imageBarrierCount]);
            
            GraphicsDevice.EndLabelCmd(commandBuffer);

            //GraphicsDevice.DeviceAPI.vkEndCommandBuffer(commandBuffer).CheckResult("Failed to end command buffer!");
        }

        private static int GetMemoryBarriers(RenderPass pass, Span<VkImageMemoryBarrier2> imageBarriers, bool output)
        {
            int imageBarrierCount = 0;
            for (int i = 0; i < pass.Attachments.Count; i++)
            {
                var a = pass.Attachments[i];
                var layout = output ? a.OutputOp : a.InputOp;
                if (TryGetResource(a.AttachmentResourceName, out var resource) && layout != VkImageLayout.Undefined)
                {
                    imageBarrierCount += resource.Target.GetImageLayoutBarrierAuto(imageBarriers.Slice(imageBarrierCount, resource.Target.ImageCount), layout);
                    resource.Target.SetImageLayoutSilent(layout);
                }
            }

            return imageBarrierCount;
        }

        public static List<int> Compile(List<RenderPass> passes)
        {
            List<int> localExecutionOrder = new(passes.Count);
            List<int>[] dependencies = new List<int>[passes.Count];
            List<int>[] dependents = new List<int>[passes.Count];

            for (int i = 0; i < dependencies.Length; i++)
            {
                dependents[i] = [];
                dependencies[i] = [];
            }

            Dictionary<string, int> resourceWriters = [];
            DependencyDiscovery(dependencies, dependents, resourceWriters, passes);

            bool[] visited = new bool[passes.Count];
            bool[] inStack = new bool[passes.Count];

            SortExecution(dependents, visited, inStack, passes, localExecutionOrder);
            localExecutionOrder.Reverse();

#if DEBUG
            ExecutionOrderEnglish = new string[localExecutionOrder.Count];

            for (int i = 0; i < localExecutionOrder.Count; i++)
            {
                ExecutionOrderEnglish[i] = passes[localExecutionOrder[i]].Name;
            }

            Console.WriteLine("Logging RenderGraph execution order..");

            for (int i = 0; i < ExecutionOrderEnglish.Length; i++)
            {
                Console.WriteLine(ExecutionOrderEnglish[i]);
            }
#endif

            return localExecutionOrder;
        }

        public static void Compile()
        {
            if (RemovePasses.Count > 0)
            {
                for (int i = Passes.Count - 1; i >= 0; i--)
                {
                    if (RemovePasses.Contains(Passes[i].Name))
                    {
                        Passes.RemoveAt(i);
                    }
                }
                RemovePasses.Clear();
            }
            Passes.Sort((x,y)=>x.PassCategory.CompareTo(y.PassCategory));


            ExecutionOrder = Compile(Passes);
            Recompile = false;
            localExecutePass.TrimExcess();
            localExecutePass.EnsureCapacity(ExecutionOrder.Count);
        }

        private static void SortExecution(List<int>[] dependents, bool[] visited, bool[] inStack, List<RenderPass> passes, List<int> executionOrder)
        {
            void Visit(int node, List<int> executionOrder)
            {
                if (inStack[node])
                {
                    throw new Exception("Cycle detected in rendergraph");
                }

                if (visited[node])
                {
                    return;
                }

                inStack[node] = true;
                dependents[node].ForEach(dependent => Visit(dependent, executionOrder));
                inStack[node] = false;
                visited[node] = true;
                executionOrder.Add(node);
            }

            for (int i = 0; i < passes.Count; i++)
            {
                if (!visited[i])
                {
                    Visit(i, executionOrder);
                }
            }
        }

        private static void DependencyDiscovery(List<int>[] dependencies, List<int>[] dependents, Dictionary<string, int> resourceWriters, List<RenderPass> passes)
        {
            for (int i = 0; i < passes.Count; i++)
            {
                var pass = passes[i];

                resourceWriters[pass.Name] = i;
            }

            for (int i = 0; i < passes.Count; i++)
            {
                var pass = passes[i];

                pass.DependantPasses.ForEach(input =>
                {
                    if (resourceWriters.TryGetValue(input, out var it))
                    {
                        dependencies[i].Add(it);
                        dependents[it].Add(i);
                    }
                });
            }
            for (int i = 0; i < Passes.Count; i++)
            {
                dependencies[i].Reverse();
                dependents[i].Reverse();
            }
        }

        internal static void RemovePass(string passName)
        {
            RemovePasses.Add(passName);
            Recompile = true;
        }

        internal static void DisablePass(string passName)
        {
            DisabledPasses.Add(passName);
        }

        internal static void EnablePass(string passName)
        {
            DisabledPasses.Remove(passName);
        }

        internal static void DisablePass(params string[] passes)
        {
            DisabledPasses.UnionWith(passes);
        }


        internal static void EnablePass(params string[] passes)
        {
            DisabledPasses.ExceptWith(passes);
        }

    }
}
