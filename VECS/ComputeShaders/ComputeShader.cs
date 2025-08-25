using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class ComputeShader : DisposableAsset
    {
        private readonly PipelineCache _cache;
        private readonly DescriptorBinding[] _computeBindings;
        private readonly PushConstantsHandler _pushConstantsHandler;

        private readonly Dictionary<string, int> _preAllocBindings;
        private readonly Dictionary<string, int> _unAllocBindings;
        private int _preAllocDescriptorHandlerIndex = -1;
        private int _unAllocDescriptorHandlerIndex = -1;
        private readonly DescriptorHandler[] _allHandlers;

        private readonly uint _descriptorSetCount = 0;

        private VkDescriptorSetLayout _preAllocDescriptorLayout;
        private VkDescriptorSetLayout _unAllocDescriptorLayout;
        private VkDescriptorSetLayout[] _allLayouts;

        private readonly VkPipelineLayout _pipelineLayout;
        private readonly VkPipeline _pipline;

        private int _executionThisFrame;
        private int _lastFrameIndex;
        public int LastFrameIndex => _lastFrameIndex;
        public bool HasPreAllocSet => _preAllocBindings.Count > 0;
        public bool HasUnAllocSet => _unAllocBindings.Count > 0;

        private DescriptorHandler PreAllocated => _allHandlers[_preAllocDescriptorHandlerIndex];
        private DescriptorHandler UnAllocated => _allHandlers[_unAllocDescriptorHandlerIndex];
        public PushConstantsHandler PushConstants => _pushConstantsHandler;

        public unsafe ComputeShader(string assetName, string shaderName)
        {
            AssetName = assetName;
            var shaderModule = AssetDataBase<ShaderModule>.GetNamed(shaderName);
            var spirShader = shaderModule.SpvShaderModule;
            _computeBindings = GPUPipelineUtil.GenerateSharedDescriptorBindings(spirShader);
            _pushConstantsHandler = new(spirShader);

            // Descriptor Set bollocks
            _preAllocBindings = GPUPipelineUtil.ExtractBindingsForSet(0, _computeBindings);
            _unAllocBindings = GPUPipelineUtil.ExtractBindingsForSet(1, _computeBindings);

            GenerateDescriptorSetLayouts();
            _allHandlers = new DescriptorHandler[_allLayouts.Length];
            _pipelineLayout = GPUPipelineUtil.CreatePipelineLayout(_allLayouts, _pushConstantsHandler);
            _descriptorSetCount = (uint)_allLayouts.Length;
            CreateDescriptorSetHandler();

            _cache = AssetDataBase<PipelineCache>.GetNamedSilentFail(shaderName);
            if (_cache == null)
            {
                _cache = new PipelineCache(shaderName, _pipelineLayout);
                AssetDataBase<PipelineCache>.Add(_cache);
            }

            VkComputePipelineCreateInfo computePipelineInfo = new()
            {
                layout = _pipelineLayout,
                stage = shaderModule.ShaderStageCreateInfo
            };

            Vulkan.vkCreateComputePipeline(GraphicsDevice.Device, _cache.Cache, computePipelineInfo, out _pipline);

        }

        private void GenerateDescriptorSetLayouts()
        {
            DescriptorBinding[] workingBindings;

            int workingBindingIndex = 0;
            _allLayouts = [];

            if (HasPreAllocSet)
            {
                workingBindings = new DescriptorBinding[_preAllocBindings.Count];
                foreach (var item in _preAllocBindings)
                {
                    workingBindings[workingBindingIndex] = _computeBindings[item.Value];
                    workingBindingIndex++;
                }
                _preAllocDescriptorLayout = GPUPipelineUtil.CreateDescriptorSetLayout(workingBindings);
                _allLayouts = [.. _allLayouts, _preAllocDescriptorLayout];
            }

            if (HasUnAllocSet)
            {
                workingBindingIndex = 0;
                workingBindings = new DescriptorBinding[_unAllocBindings.Count];
                foreach (var item in _unAllocBindings)
                {
                    workingBindings[workingBindingIndex] = _computeBindings[item.Value];
                    workingBindingIndex++;
                }
                _unAllocDescriptorLayout = GPUPipelineUtil.CreateDescriptorSetLayout(workingBindings);
                _allLayouts = [.. _allLayouts, _unAllocDescriptorLayout];
            }
        }

        private void CreateDescriptorSetHandler()
        {
            int index = 0;
            if (HasPreAllocSet)
            {
                GPUPipelineUtil.CreateDescriptorSetHandler(_allHandlers, _computeBindings, _allLayouts, index, DescriptorLevel.ComputePreGen, _preAllocBindings);
                _preAllocDescriptorHandlerIndex = index;
                index++;
            }
            if (HasUnAllocSet)
            {
                GPUPipelineUtil.CreateDescriptorSetHandler(_allHandlers, _computeBindings, _allLayouts, index, DescriptorLevel.ComputeEmpty, _unAllocBindings);
                _unAllocDescriptorHandlerIndex = index;
            }
        }

        internal void Update(RendererFrameInfo frameInfo)
        {
            for (int i = 0; i < _allHandlers.Length; i++)
            {
                _allHandlers[i].Update(frameInfo);
            }
        }

        internal void Flush(RendererFrameInfo frameInfo)
        {
            for (int i = 0; i < _allHandlers.Length; i++)
            {
                _allHandlers[i].WriteFromBuffers(frameInfo.FrameIndex);
            }
        }

        private unsafe void UpdateSetsToWrite(VkDescriptorSet* sets, int frameIndex, int id)
        {
            if (HasPreAllocSet)
            {
                sets[_preAllocDescriptorHandlerIndex] = PreAllocated.GetOrCreateChild(id).GetDescriptorSet(frameIndex);
            }
            if (HasUnAllocSet)
            {
                sets[_unAllocDescriptorHandlerIndex] = UnAllocated.GetOrCreateChild(id).GetDescriptorSet(frameIndex);
            }
        }

        public void SetStorageBuffer(string property, SwapChainBuffer buffer)
        {
            if (HasUnAllocSet)
            {
                UnAllocated.GetOrCreateChild(_executionThisFrame).SetStorageBuffer(property, buffer);
            }
        }

        public void SetStorageBuffer(string property, GPUBuffer buffer)
        {
            var scb = SwapChainBuffer.AliasGPUBuffer(buffer);
            SetStorageBuffer(property, scb);
        }

        public void SetUInt(string property, uint value)
        {
            for (int i = 0; i < _allHandlers.Length; i++)
            {
                if (_allHandlers[i].HasProperty(property))
                {
                    _allHandlers[i].GetOrCreateChild(_executionThisFrame).SetUInt(property, value);
                }
            }
        }

        public void SetUniform<T>(string property, T value) where T : unmanaged
        {
            for (int i = 0; i < _allHandlers.Length; i++)
            {
                if (_allHandlers[i].HasProperty(property))
                {
                    _allHandlers[i].GetOrCreateChild(_executionThisFrame).SetUniform(property, value);
                }
            }
        }

        public Span<T> GetStorageBuffer<T>(string property) where T : unmanaged
        {
            for (int i = 0; i < _allHandlers.Length; i++)
            {
                if (_allHandlers[i].HasProperty(property))
                {
                    var span = _allHandlers[i].GetOrCreateChild(_executionThisFrame).GetStorageBuffer<T>(property);
                    if (span != Span<T>.Empty)
                    {
                        return span;
                    }
                }
            }
            return null;
        }

        public void SetStorageBufferUsageSize(string property, uint instanceSize)
        {
            for (int i = 0; i < _allHandlers.Length; i++)
            {
                if (_allHandlers[i].HasProperty(property))
                {
                    _allHandlers[i].GetOrCreateChild(_executionThisFrame).SetStorageBufferUsageSize(property, instanceSize);
                }
            }
        }

        public SwapChainBuffer GetStorageSwapChainBuffer(string property)
        {
            for (int i = 0; i < _allHandlers.Length; i++)
            {
                if (_allHandlers[i].HasProperty(property))
                {
                    var buffer = _allHandlers[i].GetOrCreateChild(_executionThisFrame).GetStorageSwapChainBuffer(property);
                    if (buffer != null)
                    {
                        return buffer;
                    }
                }
            }

            return null;
        }

        public void UpdateSetHandlers(int frameIndex, DescriptorPool pool)
        {
            for (int i = 0; i < _allHandlers.Length; i++)
            {
                _allHandlers[i].GetOrCreateChild(_executionThisFrame).Update(frameIndex, pool);
            }
        }

        public void EnsureCapacity(int calls)
        {
            for (int i = 0; i < _allHandlers.Length; i++)
            {
                if (_allHandlers[i].ChildCount + 1 < calls)
                {
                    for (int j = 1; j < calls; j++)
                    {
                        _allHandlers[i].CreateChildSet(j);
                    }
                }
            }
            _pushConstantsHandler.EnsureCapacity(calls);
        }

        public void EnsureSetsAllocated(int frameIndex, DescriptorPool pool)
        {
            for (int i = 0; i < _allHandlers.Length; i++)
            {
                _allHandlers[i].AllocateAll(frameIndex, pool);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispatch(RendererFrameInfo frameInfo, uint workGroupCountX, uint workGroupCountY = 1, uint workGroupCountZ = 1)
        {
            Dispatch(frameInfo.CommandBuffer, frameInfo.FrameIndex, frameInfo.ApplicationDescriptorPool, workGroupCountX, workGroupCountY, workGroupCountZ);
        }

        public unsafe void Dispatch(VkCommandBuffer commandBuffer, int frameIndex, DescriptorPool pool, uint workGroupCountX, uint workGroupCountY = 1, uint workGroupCountZ = 1)
        {
            if (_lastFrameIndex != frameIndex)
            {
                NextFrame();
            }

            UpdateSetHandlers(frameIndex, pool);

            Dispatch(commandBuffer, frameIndex, 0, workGroupCountX, workGroupCountY, workGroupCountZ);
            Interlocked.Increment(ref _executionThisFrame);
            _lastFrameIndex = frameIndex;
        }

        public unsafe void Dispatch(VkCommandBuffer commandBuffer, int frameIndex, int setId, uint workGroupCountX, uint workGroupCountY = 1, uint workGroupCountZ = 1)
        {
            VkDescriptorSet* setsToBind = stackalloc VkDescriptorSet[(int)_descriptorSetCount];
            for (int i = 0; i < _allHandlers.Length; i++)
            {
                _allHandlers[i].GetOrCreateChild(setId).UpdateDescriptorSet(frameIndex);
            }
            UpdateSetsToWrite(setsToBind, frameIndex, setId);
            Vulkan.vkCmdBindPipeline(commandBuffer, VkPipelineBindPoint.Compute, _pipline);
            Vulkan.vkCmdBindDescriptorSets(commandBuffer, VkPipelineBindPoint.Compute, _pipelineLayout, 0, _descriptorSetCount, setsToBind);
            _pushConstantsHandler.BindPushConstants(commandBuffer, _pipelineLayout, setId);
            Vulkan.vkCmdDispatch(commandBuffer, workGroupCountX, workGroupCountY, workGroupCountZ);
        }

        public void NextFrame()
        {
            _executionThisFrame = 0;
        }

        public void NextFrame(int frameIndex)
        {
            NextFrame();
            _lastFrameIndex = frameIndex;
        }

        public void Increment(int ammount)
        {
            _executionThisFrame += ammount;
        }

        public void DeallocateDescriptorSets()
        {
            for (int i = 0; i < _allHandlers.Length; i++)
            {
                _allHandlers[i].DeallocateDescriptorSets();
            }
        }

        public override unsafe void Dispose()
        {
            GC.SuppressFinalize(this);
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            for (int i = 0; i < _allHandlers.Length; i++)
            {
                _allHandlers[i]?.Dispose();
            }

            Vulkan.vkDestroyPipeline(GraphicsDevice.Device, _pipline);
            if (_cache == null)
            {
                Vulkan.vkDestroyPipelineLayout(GraphicsDevice.Device, _pipelineLayout);
            }
            

            if (_preAllocDescriptorLayout != VkDescriptorSetLayout.Null)
            {
                Vulkan.vkDestroyDescriptorSetLayout(GraphicsDevice.Device, _preAllocDescriptorLayout, null);
            }
            if (_unAllocDescriptorLayout != VkDescriptorSetLayout.Null)
            {
                Vulkan.vkDestroyDescriptorSetLayout(GraphicsDevice.Device, _unAllocDescriptorLayout, null);
            }
        }

        public static ComputeShader GetOrCreate(string shaderName)
        {
            var shader = AssetDataBase<ComputeShader>.GetNamedSilentFail(shaderName);

            if (shader == null)
            {
                shader = new ComputeShader(shaderName, shaderName);
                AssetDataBase<ComputeShader>.Add(shader);
            }

            return shader;
        }
    }
}