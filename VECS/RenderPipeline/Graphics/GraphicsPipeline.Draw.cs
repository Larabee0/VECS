using System;
using System.Runtime.CompilerServices;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public partial class GraphicsPipeline
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int FirstCommand(in int matDrawCount)
        {
            int firstCommand = 0;
            for (; firstCommand < matDrawCount; firstCommand++)
            {
                if (_matVariants[firstCommand] != null)
                {
                    break;
                }
            }
            return firstCommand;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetCullModeAuto(in VkCommandBuffer commandBuffer, in int lastVariant)
        {
            if (_matVariants[lastVariant].OverrideCullMode)
            {
                GraphicsDevice.DeviceAPI.vkCmdSetCullMode(commandBuffer, _matVariants[lastVariant].CullMode);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetCullModeAuto(in VkCommandBuffer commandBuffer, in int lastVariant, in VkCullModeFlags cullModeOverride)
        {
            if (_matVariants[lastVariant].OverrideCullMode)
            {
                GraphicsDevice.DeviceAPI.vkCmdSetCullMode(commandBuffer, _matVariants[lastVariant].CullMode);
            }
            else
            {
                GraphicsDevice.DeviceAPI.vkCmdSetCullMode(commandBuffer, cullModeOverride);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetInitialDescriptors(in VkCommandBuffer commandBuffer, in int frameIndex, in MaterialDrawCommand command, Span<VkDescriptorBufferBindingInfoEXT> bindingInfo, Span<ulong> offsets, Span<uint> indices)
        {
            for (int i = 0; i < _descriptorSetCount; i++)
            {
                DescriptorSetInfo descriptorSetInfo = _descriptorSetInfos[i];
                DescriptorBuffer buffer = descriptorSetInfo.DescriptorBuffers[frameIndex];

                bindingInfo[i] = buffer.BindingInfo;
                offsets[i] = buffer.AlignedSize * (uint)command.Variant;
                indices[i] = (uint)i;
            }

            DescriptorBuffer.BindSets(commandBuffer, bindingInfo);
            DescriptorBuffer.SetOffsets(commandBuffer, _pipelineLayout, VkPipelineBindPoint.Graphics, offsets, indices);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateVariantDescriptors(in VkCommandBuffer commandBuffer, ref int lastVariant, in int frameIndex, in MaterialDrawCommand command, Span<ulong> offsets, Span<uint> indices)
        {
            for (int i = 0; i < _descriptorSetCount; i++)
            {
                DescriptorSetInfo descriptorSetInfo = _descriptorSetInfos[i];
                DescriptorBuffer buffer = descriptorSetInfo.DescriptorBuffers[frameIndex];

                offsets[i] = buffer.AlignedSize * (uint)command.Variant;
            }
            DescriptorBuffer.SetOffsets(commandBuffer, _pipelineLayout, VkPipelineBindPoint.Graphics, offsets, indices);
            lastVariant = command.Variant;
        }

        public void ExecuteDrawCommands(RendererFrameInfo frameInfo, VkCommandBuffer commandBuffer, Span<MaterialDrawCommand> drawCmds, int matDrawCount, SwapChainBuffer<VECSDrawIndexIndirectCommand> indirectCmdBuffer)
        {
            if (matDrawCount <= 0) return;
            var frameIndex = Presenter.FrameIndex;

            int firstCommand = FirstCommand(matDrawCount);

            var command = drawCmds[firstCommand];
            if (_preBindUpdate)
            {
                Update(this);
            }

            BindPipe(commandBuffer);

            Span<VkDescriptorBufferBindingInfoEXT> bindingInfo = stackalloc VkDescriptorBufferBindingInfoEXT[_descriptorSetCount];
            Span<ulong> offsets = stackalloc ulong[_descriptorSetCount];
            Span<uint> indices = stackalloc uint[_descriptorSetCount];

            SetInitialDescriptors(commandBuffer, frameIndex, command, bindingInfo, offsets, indices);

            int lastVariant = command.Variant;
            SetCullModeAuto(commandBuffer, lastVariant);

            for (int i = firstCommand; i < matDrawCount; i++)
            {
                command = drawCmds[i];

                if (_matVariants[command.Variant] == null)
                {
                    continue;
                }
                var pushConstantBuffer = _pushConstantsHandler.GetBufferInstance(command.Entity);

                ExecuteDrawCommand(commandBuffer, frameIndex, pushConstantBuffer, indirectCmdBuffer, command, offsets, indices, ref lastVariant);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExecuteDrawCommandsPushConstantOverride(RendererFrameInfo frameInfo, int pushConstantOverride, VkCommandBuffer commandBuffer, Span<MaterialDrawCommand> drawCmds, int matDrawCount, SwapChainBuffer<VECSDrawIndexIndirectCommand> indirectCmdBuffer)
        {
            var pushConstantsBuffer = _pushConstantsHandler.GetBufferInstance(pushConstantOverride);
            ExecuteDrawCommandsPushConstantOverride(frameInfo, pushConstantsBuffer, commandBuffer, drawCmds, matDrawCount, indirectCmdBuffer);
        }

        public void ExecuteDrawCommandsPushConstantOverride<T>(RendererFrameInfo frameInfo, Span<T> pushConstantBuffer, VkCommandBuffer commandBuffer, Span<MaterialDrawCommand> drawCmds, int matDrawCount, SwapChainBuffer<VECSDrawIndexIndirectCommand> indirectCmdBuffer) where T : unmanaged
        {
            if (matDrawCount <= 0) return;
            var frameIndex = Presenter.FrameIndex;

            int firstCommand = FirstCommand(matDrawCount);

            var command = drawCmds[firstCommand];

            if (_preBindUpdate)
            {
                Update(this);
            }

            BindPipe(commandBuffer);

            Span<VkDescriptorBufferBindingInfoEXT> bindingInfo = stackalloc VkDescriptorBufferBindingInfoEXT[_descriptorSetCount];
            Span<ulong> offsets = stackalloc ulong[_descriptorSetCount];
            Span<uint> indices = stackalloc uint[_descriptorSetCount];

            SetInitialDescriptors(commandBuffer, frameIndex, command, bindingInfo, offsets, indices);

            int lastVariant = command.Variant;
            SetCullModeAuto(commandBuffer, lastVariant);
            for (int i = 0; i < matDrawCount; i++)
            {
                command = drawCmds[i];
                if (_matVariants[command.Variant] == null)
                {
                    continue;
                }

                ExecuteDrawCommand(commandBuffer, frameIndex, pushConstantBuffer, indirectCmdBuffer, command, offsets, indices, ref lastVariant);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExecuteDrawCommandsPushConstantOverride(int pushConstantOverride, VkCommandBuffer commandBuffer, Span<MaterialDrawCommand> drawCmds, int matDrawCount, SwapChainBuffer<VECSDrawIndexIndirectCommand> indirectCmdBuffer, VkCullModeFlags cullMode)
        {
            var pushConstantsBuffer = _pushConstantsHandler.GetBufferInstance(pushConstantOverride);
            ExecuteDrawCommandsPushConstantOverride(pushConstantsBuffer, commandBuffer, drawCmds, matDrawCount, indirectCmdBuffer, cullMode);
        }
        
        public void ExecuteDrawCommandsPushConstantOverride<T>(Span<T> pushConstantBuffer, VkCommandBuffer commandBuffer, Span<MaterialDrawCommand> drawCmds, int matDrawCount, SwapChainBuffer<VECSDrawIndexIndirectCommand> indirectCmdBuffer, VkCullModeFlags cullMode) where T : unmanaged
        {
            if (matDrawCount <= 0) return;
            var frameIndex = Presenter.FrameIndex;

            int firstCommand = FirstCommand(matDrawCount);

            var command = drawCmds[firstCommand];

            if (_preBindUpdate)
            {
                Update(this);
            }

            BindPipe(commandBuffer);

            Span<VkDescriptorBufferBindingInfoEXT> bindingInfo = stackalloc VkDescriptorBufferBindingInfoEXT[_descriptorSetCount];
            Span<ulong> offsets = stackalloc ulong[_descriptorSetCount];
            Span<uint> indices = stackalloc uint[_descriptorSetCount];

            SetInitialDescriptors(commandBuffer, frameIndex, command, bindingInfo, offsets, indices);

            int lastVariant = command.Variant;
            SetCullModeAuto(commandBuffer, lastVariant, cullMode);
            for (int i = 0; i < matDrawCount; i++)
            {
                command = drawCmds[i];
                if (_matVariants[command.Variant] == null)
                {
                    continue;
                }

                ExecuteDrawCommand(commandBuffer, frameIndex, pushConstantBuffer, indirectCmdBuffer, command, offsets, indices, ref lastVariant, cullMode);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ExecuteDrawCommand<T>(VkCommandBuffer commandBuffer, int frameIndex, Span<T> pushConstantBuffer, SwapChainBuffer<VECSDrawIndexIndirectCommand> indirectCmdBuffer, MaterialDrawCommand command, Span<ulong> offsets, Span<uint> indices, ref int lastVariant, VkCullModeFlags cullMode) where T : unmanaged
        {
            if (lastVariant != command.Variant)
            {
                UpdateVariantDescriptors(commandBuffer, ref lastVariant, frameIndex, command, offsets, indices);
                SetCullModeAuto(commandBuffer,lastVariant, cullMode);
            }

            ExecuteDrawCommand(commandBuffer, pushConstantBuffer, indirectCmdBuffer, command);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ExecuteDrawCommand<T>(VkCommandBuffer commandBuffer, int frameIndex, Span<T> pushConstantBuffer, SwapChainBuffer<VECSDrawIndexIndirectCommand> indirectCmdBuffer, MaterialDrawCommand command, Span<ulong> offsets, Span<uint> indices, ref int lastVariant) where T : unmanaged
        {
            if (lastVariant != command.Variant)
            {
                UpdateVariantDescriptors(commandBuffer, ref lastVariant, frameIndex, command, offsets, indices);
                SetCullModeAuto(commandBuffer, lastVariant);
            }

            ExecuteDrawCommand(commandBuffer, pushConstantBuffer, indirectCmdBuffer, command);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExecuteDrawCommand<T>(VkCommandBuffer commandBuffer, Span<T> pushConstantBuffer, SwapChainBuffer<VECSDrawIndexIndirectCommand> indirectCmdBuffer, MaterialDrawCommand command) where T : unmanaged
        {
            _pushConstantsHandler.BindPushConstants(commandBuffer, _pipelineLayout, pushConstantBuffer);
            var mesh = AssetDataBase<DirectMesh>.GetHashed(command.DirectMesh);
            mesh.BindSpecificBuffers(commandBuffer, _graphicsPipelineConfigInfo.BindingDescriptions, _graphicsPipelineConfigInfo.AttributeDescriptions);
            unsafe
            {
                GraphicsDevice.DeviceAPI.vkCmdDrawIndexedIndirect(
                    commandBuffer,
                    indirectCmdBuffer.ActiveVkBuffer,
                    (uint)command.MeshSubRegion.StartIndex * (uint)sizeof(VECSDrawIndexIndirectCommand),
                    (uint)command.MeshSubRegion.Count, (uint)sizeof(VECSDrawIndexIndirectCommand)
                );
            }
        }
    }
}
