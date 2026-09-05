using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{

    public static class RenderGraph
    {
        private static Dictionary<string, RenderTargetDefintion> ResourceDefinitons = [];
        private static Dictionary<string, RenderTarget> Resources = [];
        private static List<string> MatchScreenSize = [];
        private static List<RenderPass> Passes = [];
        private static List<int> ExecutionOrder = [];

        private static bool Recompile = true;

        private static int ImageMemoryBarrierStackSize;

        private static HashSet<string> RemovePasses = [];
        private static HashSet<string> DisabledPasses = [];

#if DEBUG
        private static string[] ExecutionOrderEnglish;
#endif

        // public static void AddResource(string name, VkFormat format, VkExtent2D extent, VkImageUsageFlags usage, VkImageLayout initialLayout, VkImageLayout finalLayout)
        // {
        //     RenderTargetDefintion renderTargetDef = new(name, format, extent, usage, initialLayout, finalLayout);
        //     AddResource(renderTargetDef);
        // }

        public static RenderTarget GetResource(string name)
        {
            Resources.TryGetValue(name, out RenderTarget rt);
            return rt;
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

        public static void AddPass(string name, PassType passType, List<string> dependantPasses, List<string> inputs, List<string> outputs, Action<RendererFrameInfo> executeFunc)
        {
            var pass = new RenderPass()
            {
                Name = name,
                PassType = passType,
                Inputs = inputs,
                Outputs = outputs,
                ExecuteFunc = executeFunc,
                DependantPasses = dependantPasses,
            };
            Passes.Add(pass);
            Recompile = true;
        }

        public static void AddPass(string name, PassType passType, int order, List<string> inputs, List<string> outputs, Action<RendererFrameInfo> executeFunc)
        {
            var pass = new RenderPass()
            {
                Name = name,
                PassType = passType,
                Inputs = inputs,
                Outputs = outputs,
                ExecuteFunc = executeFunc,
                RelativeOrder = order,

            };
            Passes.Add(pass);
            Recompile = true;
        }

        public static void AddPass(string name, PassType passType, PassCategory category, List<string> inputs, List<string> outputs, Action<RendererFrameInfo> executeFunc)
        {
            var pass = new RenderPass()
            {
                Name = name,
                PassType = passType,
                Inputs = inputs,
                Outputs = outputs,
                ExecuteFunc = executeFunc,
                PassCategory = category,

            };
            Passes.Add(pass);
            Recompile = true;
        }

        public static void AddPass(string name, PassType passType, PassCategory category, List<string> dependantPasses, List<string> inputs, List<string> outputs, Action<RendererFrameInfo> executeFunc)
        {
            var pass = new RenderPass()
            {
                Name = name,
                PassType = passType,
                Inputs = inputs,
                Outputs = outputs,
                ExecuteFunc = executeFunc,
                PassCategory = category,
                DependantPasses = dependantPasses

            };
            Passes.Add(pass);
            Recompile = true;
        }
        public static void AddPass(string name, PassType passType, PassCategory category, int order, List<string> inputs, List<string> outputs, Action<RendererFrameInfo> executeFunc)
        {
            var pass = new RenderPass()
            {
                Name = name,
                PassType = passType,
                Inputs = inputs,
                Outputs = outputs,
                ExecuteFunc = executeFunc,
                PassCategory = category,
                RelativeOrder = order,

            };
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

        public unsafe static void Execute(RendererFrameInfo frameInfo)
        {
            if (Recompile) Compile();
            VkCommandBuffer commandBuffer = frameInfo.CommandBuffer;
            VkImageMemoryBarrier2* imageBarriers = stackalloc VkImageMemoryBarrier2[ImageMemoryBarrierStackSize];
            uint imageBarrierCount;
            ExecutionOrder.ForEach(passIndex =>
            {
                var pass = Passes[passIndex];
                if (DisabledPasses.Contains(pass.Name))
                {
                    return;
                }
                GraphicsDevice.BeginLabelCmd(commandBuffer, pass.Name);


                imageBarrierCount = 0;
                NativeMemory.Fill(imageBarriers, (uint)(sizeof(VkImageMemoryBarrier2) * ImageMemoryBarrierStackSize), 0);
                pass.Inputs.ForEach(input =>
                {
                    if (!Resources.TryGetValue(input, out var resource)) return;

                    switch (pass.PassType)
                    {
                        case PassType.Render:
                            if (resource.Target.ImageLayout != resource.AttachmentInputLayout)
                            {
                                imageBarriers[imageBarrierCount] = resource.Target.GetImageLayoutBarrierAuto(resource.AttachmentInputLayout);
                                imageBarrierCount++;
                                resource.Target.SetImageLayoutSilent(resource.AttachmentInputLayout);
                            }
                            break;
                        case PassType.Compute:
                            if (resource.Target.ImageLayout != resource.ComputeInputLayout)
                            {
                                imageBarriers[imageBarrierCount] = resource.Target.GetImageLayoutBarrierAuto(resource.ComputeInputLayout);
                                imageBarrierCount++;
                                resource.Target.SetImageLayoutSilent(resource.ComputeInputLayout);
                            }
                            break;
                    }

                });

                pass.Outputs.ForEach(output =>
                {
                    if (!Resources.TryGetValue(output, out var resource)) return;
                    switch (pass.PassType)
                    {
                        case PassType.Render:
                            if (resource.Target.ImageLayout != resource.AttachmentOutputLayout)
                            {
                                imageBarriers[imageBarrierCount] = resource.Target.GetImageLayoutBarrierAuto(resource.AttachmentOutputLayout);
                                imageBarrierCount++;
                                resource.Target.SetImageLayoutSilent(resource.AttachmentOutputLayout);
                            }
                            break;
                        case PassType.Compute:
                            if (resource.Target.ImageLayout != resource.ComputeOutputLayout)
                            {
                                imageBarriers[imageBarrierCount] = resource.Target.GetImageLayoutBarrierAuto(resource.ComputeOutputLayout);
                                imageBarrierCount++;
                                resource.Target.SetImageLayoutSilent(resource.ComputeOutputLayout);
                            }
                            break;
                    }
                });

                MemoryBarrierHelper.ImageMemoryBarrier(commandBuffer, imageBarriers, imageBarrierCount);

                pass.ExecuteFunc(frameInfo);

                imageBarrierCount = 0;
                NativeMemory.Fill(imageBarriers, (uint)(sizeof(VkImageMemoryBarrier2) * ImageMemoryBarrierStackSize), 0);
                pass.Outputs.ForEach(output =>
                {
                    if (!Resources.TryGetValue(output, out var resource)) return;
                    switch (pass.PassType)
                    {
                        case PassType.Render:
                            if (resource.Target.ImageLayout != resource.AttachmentInputLayout)
                            {
                                imageBarriers[imageBarrierCount] = resource.Target.GetImageLayoutBarrierAuto(resource.AttachmentInputLayout);
                                imageBarrierCount++;
                                resource.Target.SetImageLayoutSilent(resource.AttachmentInputLayout);
                            }
                            break;
                        case PassType.Compute:
                            if (resource.Target.ImageLayout != resource.ComputeInputLayout)
                            {
                                imageBarriers[imageBarrierCount] = resource.Target.GetImageLayoutBarrierAuto(resource.ComputeInputLayout);
                                imageBarrierCount++;
                                resource.Target.SetImageLayoutSilent(resource.ComputeInputLayout);
                            }
                            break;
                    }
                });

                MemoryBarrierHelper.ImageMemoryBarrier(commandBuffer, imageBarriers, imageBarrierCount);

                GraphicsDevice.EndLabelCmd(commandBuffer);
            });
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
            ImageMemoryBarrierStackSize = 0;
            for (int i = 0; i < passes.Count; i++)
            {
                if (!visited[i])
                {
                    Visit(i, executionOrder);
                }

                ImageMemoryBarrierStackSize = Math.Max(ImageMemoryBarrierStackSize,Math.Max(passes[i].Inputs.Count, passes[i].Outputs.Count));
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

        internal static void DisablePasses(string[] passes)
        {
            DisabledPasses.UnionWith(passes);
        }


        internal static void EnablePasses(string[] passes)
        {
            DisabledPasses.ExceptWith(passes);
        }

    }
}
