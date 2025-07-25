using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class ComputeShader : IDisposable
    {
        private bool _disposed = false;

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
        private readonly VkShaderModule _shaderModule;
        private readonly VkPipelineLayout _pipelineLayout;
        private readonly VkPipeline _pipline;
        
        private readonly unsafe VkDescriptorSet* _setsToBind;
        
        public bool HasPreAllocSet => _preAllocBindings.Count > 0;
        public bool HasUnAllocSet => _unAllocBindings.Count > 0;

        public unsafe ComputeShader(string shaderFilePath)
        {
            var shaderBytes = File.ReadAllBytes(shaderFilePath);

            Vulkan.vkCreateShaderModule(GraphicsDevice.Instance.Device, shaderBytes, null, out _shaderModule);

            var spirShader = SPIRVReflectUtil.CreateReflectShaderModule(shaderBytes);

            _computeBindings = GPUPipelineUtil.GenerateSharedDescriptorBindings(spirShader);
            _pushConstantsHandler = new(spirShader);

            SPIRVReflectUtil.DestroyReflectShaderModule(spirShader);

            GenerateDescriptorSetLayouts();
            _pipelineLayout = GPUPipelineUtil.CreatePipelineLayout(_allLayouts, _pushConstantsHandler);
            _setsToBind = (VkDescriptorSet*)NativeMemory.AllocZeroed((uint)_allLayouts.Length,(uint)sizeof(VkDescriptorSet));

            // Descriptor Set bollocks
            _preAllocBindings = GPUPipelineUtil.ExtractBindingsForSet(0, _computeBindings);
            _unAllocBindings = GPUPipelineUtil.ExtractBindingsForSet(1, _computeBindings);
            _allHandlers = new DescriptorHandler[_allLayouts.Length];
            _descriptorSetCount = (uint)_allLayouts.Length;
            CreateDescriptorSetHandler();

            VkUtf8ReadOnlyString main = "main"u8;
            VkPipelineShaderStageCreateInfo computeShaderStageInfo = new()
            {
                stage = VkShaderStageFlags.Compute,
                module = _shaderModule,
                pName = main
            };

            VkComputePipelineCreateInfo computePipelineInfo = new()
            {
                layout = _pipelineLayout,
                stage = computeShaderStageInfo
            };

            Vulkan.vkCreateComputePipeline(GraphicsDevice.Instance.Device, computePipelineInfo, out _pipline);
        }

        private void GenerateDescriptorSetLayouts()
        {
            DescriptorBinding[] workingBindings;

            int workingBindingIndex = 0;
            _allLayouts = [];

            if (HasPreAllocSet)
            {
                workingBindingIndex = 0;
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
                GPUPipelineUtil.CreateDescriptorSetHandler(_allHandlers,_computeBindings, _allLayouts, index, DescriptorLevel.ComputePreGen, _preAllocBindings);
                _preAllocDescriptorHandlerIndex = index;
                index++;
            }
            if (HasUnAllocSet)
            {
                GPUPipelineUtil.CreateDescriptorSetHandler(_allHandlers, _computeBindings,_allLayouts, index, DescriptorLevel.ComputeEmpty, _unAllocBindings);
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

        private unsafe void UpdateSetsToWrite(RendererFrameInfo frameInfo, int variant, int entity)
        {
            if (HasPreAllocSet)
            {
                _setsToBind[_preAllocDescriptorHandlerIndex] = _allHandlers[_preAllocDescriptorHandlerIndex].GetDescriptorSet(frameInfo.FrameIndex);
            }
            if (HasUnAllocSet)
            {
                _setsToBind[_unAllocDescriptorHandlerIndex] = _allHandlers[_unAllocDescriptorHandlerIndex].GetOrCreateChild(variant).GetDescriptorSet(frameInfo.FrameIndex);
            }
        }

        public unsafe void Dispatch(VkCommandBuffer commandBuffer, uint workGroupCountX, uint workGroupCountY = 1, uint workGroupCountZ = 1)
        {
            Vulkan.vkCmdBindPipeline(commandBuffer, VkPipelineBindPoint.Compute, _pipline);
            Vulkan.vkCmdBindDescriptorSets(commandBuffer, VkPipelineBindPoint.Compute, _pipelineLayout, 0, _descriptorSetCount, _setsToBind);

            _pushConstantsHandler.BindPushConstants(commandBuffer, _pipelineLayout);

            Vulkan.vkCmdDispatch(commandBuffer, workGroupCountX, workGroupCountY, workGroupCountZ);
        }

        public unsafe void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            GC.SuppressFinalize(this);

            _disposed = true;

            if (_setsToBind != null)
            {
                NativeMemory.Free(_setsToBind);
            }

            for (int i = 0; i < _allHandlers.Length; i++)
            {
                _allHandlers[i]?.Dispose();
            }

            Vulkan.vkDestroyPipeline(GraphicsDevice.Instance.Device, _pipline);
            Vulkan.vkDestroyPipelineLayout(GraphicsDevice.Instance.Device, _pipelineLayout);

            if (_preAllocDescriptorLayout != VkDescriptorSetLayout.Null)
            {
                Vulkan.vkDestroyDescriptorSetLayout(GraphicsDevice.Instance.Device, _preAllocDescriptorLayout, null);
            }
            if (_unAllocDescriptorLayout != VkDescriptorSetLayout.Null)
            {
                Vulkan.vkDestroyDescriptorSetLayout(GraphicsDevice.Instance.Device, _unAllocDescriptorLayout, null);
            }

            Vulkan.vkDestroyShaderModule(GraphicsDevice.Instance.Device, _shaderModule);
        }
    }
}