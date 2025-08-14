using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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

        private readonly unsafe VkDescriptorSet* _setsToBind;


        private int _executionThisFrame;
        private int _lastFrameIndex;

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
            _setsToBind = (VkDescriptorSet*)NativeMemory.AllocZeroed((uint)_allLayouts.Length, (uint)sizeof(VkDescriptorSet));
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

        private unsafe void UpdateSetsToWrite(int frameIndex)
        {
            if (HasPreAllocSet)
            {
                _setsToBind[_preAllocDescriptorHandlerIndex] = PreAllocated.GetOrCreateChild(_executionThisFrame).GetDescriptorSet(frameIndex);
            }
            if (HasUnAllocSet)
            {
                _setsToBind[_unAllocDescriptorHandlerIndex] = UnAllocated.GetOrCreateChild(_executionThisFrame).GetDescriptorSet(frameIndex);
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

        private void UpdateSetHandlers(int frameIndex, DescriptorPool pool)
        {
            for (int i = 0; i < _allHandlers.Length; i++)
            {
                _allHandlers[i].GetOrCreateChild(_executionThisFrame).Update(frameIndex, pool);
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

            UpdateSetsToWrite(frameIndex);

            Vulkan.vkCmdBindPipeline(commandBuffer, VkPipelineBindPoint.Compute, _pipline);
            Vulkan.vkCmdBindDescriptorSets(commandBuffer, VkPipelineBindPoint.Compute, _pipelineLayout, 0, _descriptorSetCount, _setsToBind);

            _pushConstantsHandler.BindPushConstants(commandBuffer, _pipelineLayout);

            Vulkan.vkCmdDispatch(commandBuffer, workGroupCountX, workGroupCountY, workGroupCountZ);
            _executionThisFrame++;
            _lastFrameIndex = frameIndex;
        }

        public void NextFrame()
        {
            _executionThisFrame = 0;
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

            if (_setsToBind != null)
            {
                NativeMemory.Free(_setsToBind);
            }

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