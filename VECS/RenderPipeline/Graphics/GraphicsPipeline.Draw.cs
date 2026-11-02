using CommunityToolkit.HighPerformance;
using System;
using System.Diagnostics;
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
            SetCullModeAuto(commandBuffer, (uint)lastVariant);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetCullModeAuto(in VkCommandBuffer commandBuffer, in uint lastVariant)
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
        internal void GetInitialDescriptors(in int frameIndex, in int variantIndex, Span<VkDescriptorBufferBindingInfoEXT> bindingInfo, Span<ulong> offsets, Span<uint> indices)
        {
            GetInitialDescriptors(frameIndex,(uint)variantIndex, bindingInfo, offsets, indices);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void GetInitialDescriptors(in int frameIndex, in uint variantIndex, Span<VkDescriptorBufferBindingInfoEXT> bindingInfo, Span<ulong> offsets, Span<uint> indices)
        {
            for (int i = 0; i < _descriptorSetCount; i++)
            {
                if (GraphicsDevice.MeshShading && i == _meshShaderDescriptorSetIndex)
                {
                    indices[i] = (uint)i;
                    continue;
                }

                DescriptorSetInfo descriptorSetInfo = _descriptorSetInfos[i];
                DescriptorBuffer buffer = descriptorSetInfo.DescriptorBuffers[frameIndex];

                bindingInfo[i] = buffer.BindingInfo;
                offsets[i] = buffer.AlignedSize * variantIndex;
                indices[i] = (uint)i;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AddMeshShaderDescriptors(in int frameIndex, in MeshDrawCommand command, Span<VkDescriptorBufferBindingInfoEXT> bindingInfo, Span<ulong> offsets)
        {
            AddMeshShaderDescriptors(AssetDataBase<DirectMesh>.GetHashed(command.DirectMeshHash), frameIndex, (uint)command.MaterialVariantIndex, bindingInfo, offsets);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AddMeshShaderDescriptors(in DirectMesh mesh, in int frameIndex, in uint variantIndex, Span<VkDescriptorBufferBindingInfoEXT> bindingInfo, Span<ulong> offsets)
        {
            Debug.Assert(GraphicsDevice.MeshShading);
            if (!mesh.MeshShaderSet.TryGetDescriptorBuffer(frameIndex, _meshShaderDescriptorHash, out DescriptorBuffer buffer))
            {
                MeshShaderDescriptorBuffer descriptor = mesh.MeshShaderSet.RegisterMaterial(_descriptorSetLayouts[_meshShaderDescriptorSetIndex], _meshShaderVertexAttributes);
                mesh.MeshShaderSet.UpdateDescriptorBuffer(frameIndex, descriptor);
                buffer = descriptor.DescriptorBuffers[frameIndex];
            }
            bindingInfo[_meshShaderDescriptorSetIndex] = buffer.BindingInfo;
            offsets[_meshShaderDescriptorSetIndex] = 0;// buffer.AlignedSize;// * variantIndex;
        }

        internal void SetDescriptors(VkCommandBuffer commandBuffer, Span<VkDescriptorBufferBindingInfoEXT> bindingInfo, Span<ulong> offsets, Span<uint> indices)
        {
            if (_descriptorSetCount > 0)
            {
                DescriptorBuffer.BindSets(commandBuffer, bindingInfo);
                DescriptorBuffer.SetOffsets(commandBuffer, _pipelineLayout, VkPipelineBindPoint.Graphics, offsets, indices);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateVariantDescriptors(ref int lastVariant, in int frameIndex, in int variant, Span<ulong> offsets)
        {
            for (int i = 0; i < _descriptorSetCount; i++)
            {
                if (GraphicsDevice.MeshShading && i == _meshShaderDescriptorSetIndex) continue;
                DescriptorSetInfo descriptorSetInfo = _descriptorSetInfos[i];
                DescriptorBuffer buffer = descriptorSetInfo.DescriptorBuffers[frameIndex];

                offsets[i] = buffer.AlignedSize * (uint)variant;
            }
            lastVariant = variant;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateOffsets(in VkCommandBuffer commandBuffer, Span<ulong> offsets, Span<uint> indices)
        {
            DescriptorBuffer.SetOffsets(commandBuffer, _pipelineLayout, VkPipelineBindPoint.Graphics, offsets, indices);
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


            Span<VkDescriptorBufferBindingInfoEXT> bindingInfo = stackalloc VkDescriptorBufferBindingInfoEXT[_descriptorSetCount];
            Span<ulong> offsets = stackalloc ulong[_descriptorSetCount];
            Span<uint> indices = stackalloc uint[_descriptorSetCount];

            GetInitialDescriptors(frameIndex, command.Variant, bindingInfo, offsets, indices);
            BindPipe(commandBuffer);
            SetDescriptors(commandBuffer, bindingInfo, offsets, indices);

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

        public void ExecuteDrawMeshCommands(RendererFrameInfo frameInfo, VkCommandBuffer commandBuffer, Span<MeshDrawCommand> drawCmds, int matDrawCount)
        {
            if (matDrawCount <= 0) return;
            var frameIndex = Presenter.FrameIndex;

            int firstCommand = FirstCommand(matDrawCount);

            var command = drawCmds[firstCommand];
            if (_preBindUpdate)
            {
                Update(this);
            }


            Span<VkDescriptorBufferBindingInfoEXT> bindingInfo = stackalloc VkDescriptorBufferBindingInfoEXT[_descriptorSetCount];
            Span<ulong> offsets = stackalloc ulong[_descriptorSetCount];
            Span<uint> indices = stackalloc uint[_descriptorSetCount];

            GetInitialDescriptors(frameIndex, command.MaterialVariantIndex, bindingInfo, offsets, indices);
            AddMeshShaderDescriptors(frameIndex, command, bindingInfo, offsets);
            BindPipe(commandBuffer);
            SetDescriptors(commandBuffer, bindingInfo, offsets, indices);

            int lastVariant = command.MaterialVariantIndex;
            int lastMesh = command.DirectMeshHash;
            SetCullModeAuto(commandBuffer, lastVariant);

            for (int i = firstCommand; i < matDrawCount; i++)
            {
                command = drawCmds[i];

                if (_matVariants[command.MaterialVariantIndex] == null)
                {
                    continue;
                }
                var pushConstantBuffer = _pushConstantsHandler.GetBufferInstance(command.PushConstantIndex);

                ExecuteDrawMeshCommand(commandBuffer, frameIndex, pushConstantBuffer,  command, bindingInfo, offsets, indices, ref lastVariant, ref lastMesh);
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


            Span<VkDescriptorBufferBindingInfoEXT> bindingInfo = stackalloc VkDescriptorBufferBindingInfoEXT[_descriptorSetCount];
            Span<ulong> offsets = stackalloc ulong[_descriptorSetCount];
            Span<uint> indices = stackalloc uint[_descriptorSetCount];

            GetInitialDescriptors(frameIndex, command.Variant, bindingInfo, offsets, indices);
            BindPipe(commandBuffer);
            SetDescriptors(commandBuffer, bindingInfo, offsets, indices);

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
        public void ExecuteDrawMeshCommandsPushConstantOverride(RendererFrameInfo frameInfo, int pushConstantOverride, VkCommandBuffer commandBuffer, Span<MeshDrawCommand> drawCmds, int matDrawCount)
        {
            var pushConstantsBuffer = _pushConstantsHandler.GetBufferInstance(pushConstantOverride);
            ExecuteDrawMeshCommandsPushConstantOverride(frameInfo, pushConstantsBuffer, commandBuffer, drawCmds, matDrawCount);
        }

        public void ExecuteDrawMeshCommandsPushConstantOverride<T>(RendererFrameInfo frameInfo, Span<T> pushConstantBuffer, VkCommandBuffer commandBuffer, Span<MeshDrawCommand> drawCmds, int matDrawCount) where T : unmanaged
        {
            if (matDrawCount <= 0) return;
            var frameIndex = Presenter.FrameIndex;

            int firstCommand = FirstCommand(matDrawCount);

            var command = drawCmds[firstCommand];

            if (_preBindUpdate)
            {
                Update(this);
            }


            Span<VkDescriptorBufferBindingInfoEXT> bindingInfo = stackalloc VkDescriptorBufferBindingInfoEXT[_descriptorSetCount];
            Span<ulong> offsets = stackalloc ulong[_descriptorSetCount];
            Span<uint> indices = stackalloc uint[_descriptorSetCount];

            GetInitialDescriptors(frameIndex, command.MaterialVariantIndex, bindingInfo, offsets, indices);
            AddMeshShaderDescriptors(frameIndex, command, bindingInfo, offsets);
            BindPipe(commandBuffer);
            SetDescriptors(commandBuffer, bindingInfo, offsets, indices);

            int rollingOffset = 0;
            int lastVariant = command.MaterialVariantIndex;
            int lastMesh = command.DirectMeshHash;
            SetCullModeAuto(commandBuffer, lastVariant);
            for (int i = 0; i < matDrawCount; i++)
            {
                command = drawCmds[i];
                _pushConstantsHandler.WriteToPushConstantBuffer("taskOffset".GetShaderPropertyId(), pushConstantBuffer.AsBytes(), rollingOffset);
                rollingOffset += command.TaskCount;
                if (_matVariants[command.MaterialVariantIndex] == null)
                {
                    continue;
                }

                ExecuteDrawMeshCommand(commandBuffer, frameIndex, pushConstantBuffer, command, bindingInfo, offsets, indices, ref lastVariant, ref lastMesh);
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

            Span<VkDescriptorBufferBindingInfoEXT> bindingInfo = stackalloc VkDescriptorBufferBindingInfoEXT[_descriptorSetCount];
            Span<ulong> offsets = stackalloc ulong[_descriptorSetCount];
            Span<uint> indices = stackalloc uint[_descriptorSetCount];

            GetInitialDescriptors(frameIndex, command.Variant, bindingInfo, offsets, indices);
            BindPipe(commandBuffer);
            SetDescriptors(commandBuffer, bindingInfo, offsets, indices);

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
        public void ExecuteDrawMeshCommandsPushConstantOverride(int pushConstantOverride, VkCommandBuffer commandBuffer, Span<MeshDrawCommand> drawCmds, int matDrawCount,VkCullModeFlags cullMode)
        {
            var pushConstantsBuffer = _pushConstantsHandler.GetBufferInstance(pushConstantOverride);
            ExecuteDrawMeshCommandsPushConstantOverride(pushConstantsBuffer, commandBuffer, drawCmds, matDrawCount, cullMode);
        }

        public void ExecuteDrawMeshCommandsPushConstantOverride<T>(Span<T> pushConstantBuffer, VkCommandBuffer commandBuffer, Span<MeshDrawCommand> drawCmds, int matDrawCount, VkCullModeFlags cullMode) where T : unmanaged
        {
            if (matDrawCount <= 0) return;
            var frameIndex = Presenter.FrameIndex;

            int firstCommand = FirstCommand(matDrawCount);

            var command = drawCmds[firstCommand];

            if (_preBindUpdate)
            {
                Update(this);
            }

            Span<VkDescriptorBufferBindingInfoEXT> bindingInfo = stackalloc VkDescriptorBufferBindingInfoEXT[_descriptorSetCount];
            Span<ulong> offsets = stackalloc ulong[_descriptorSetCount];
            Span<uint> indices = stackalloc uint[_descriptorSetCount];

            GetInitialDescriptors(frameIndex, command.MaterialVariantIndex, bindingInfo, offsets, indices);
            AddMeshShaderDescriptors(frameIndex, command, bindingInfo, offsets);
            BindPipe(commandBuffer);
            SetDescriptors(commandBuffer, bindingInfo, offsets, indices);

            int lastVariant = command.MaterialVariantIndex;

            int lastMesh = command.DirectMeshHash;

            int rollingOffset = 0;

            SetCullModeAuto(commandBuffer, lastVariant, cullMode);
            for (int i = 0; i < matDrawCount; i++)
            {
                command = drawCmds[i];
                if( lastVariant != command.MaterialVariantIndex)
                {
                    rollingOffset = 0;
                }

                _pushConstantsHandler.WriteToPushConstantBuffer("taskOffset".GetShaderPropertyId(), pushConstantBuffer.AsBytes(), rollingOffset);
                rollingOffset += command.TaskCount;
                if (_matVariants[command.MaterialVariantIndex] == null)
                {
                    continue;
                }
                ExecuteDrawMeshCommand(commandBuffer, frameIndex, pushConstantBuffer, command,bindingInfo, offsets, indices, ref lastVariant, ref lastMesh, cullMode);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ExecuteDrawCommand<T>(VkCommandBuffer commandBuffer, int frameIndex, Span<T> pushConstantBuffer, SwapChainBuffer<VECSDrawIndexIndirectCommand> indirectCmdBuffer, MaterialDrawCommand command, Span<ulong> offsets, Span<uint> indices, ref int lastVariant, VkCullModeFlags cullMode) where T : unmanaged
        {
            if (lastVariant != command.Variant)
            {
                UpdateVariantDescriptors(ref lastVariant, frameIndex, command.Variant, offsets);
                UpdateOffsets(commandBuffer, offsets, indices);
                SetCullModeAuto(commandBuffer,lastVariant, cullMode);
            }

            ExecuteDrawCommand(commandBuffer, pushConstantBuffer, indirectCmdBuffer, command);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ExecuteDrawCommand<T>(VkCommandBuffer commandBuffer, int frameIndex, Span<T> pushConstantBuffer, SwapChainBuffer<VECSDrawIndexIndirectCommand> indirectCmdBuffer, MaterialDrawCommand command, Span<ulong> offsets, Span<uint> indices, ref int lastVariant) where T : unmanaged
        {
            if (lastVariant != command.Variant)
            {
                UpdateVariantDescriptors(ref lastVariant, frameIndex, command.Variant, offsets);
                UpdateOffsets(commandBuffer, offsets, indices);
                SetCullModeAuto(commandBuffer, lastVariant);
            }

            ExecuteDrawCommand(commandBuffer, pushConstantBuffer, indirectCmdBuffer, command);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ExecuteDrawMeshCommand<T>(VkCommandBuffer commandBuffer, int frameIndex, Span<T> pushConstantBuffer, MeshDrawCommand command, Span<VkDescriptorBufferBindingInfoEXT> bindingInfo, Span<ulong> offsets, Span<uint> indices, ref int lastVariant, ref int lastMesh, VkCullModeFlags cullMode) where T : unmanaged
        {
            bool variantChanged = lastVariant != command.MaterialVariantIndex;
            bool meshChanged = lastMesh != command.DirectMeshHash;
            if (variantChanged)
            {
                UpdateVariantDescriptors(ref lastVariant, frameIndex, command.MaterialVariantIndex, offsets);
                SetCullModeAuto(commandBuffer, lastVariant, cullMode);
            }

            if (meshChanged)
            {
                AddMeshShaderDescriptors(frameIndex,command, bindingInfo, offsets);
                lastMesh = command.DirectMeshHash;
            }

            if (meshChanged)
            {
                SetDescriptors(commandBuffer, bindingInfo, offsets, indices);
            }
            else if (variantChanged)
            {
                UpdateOffsets(commandBuffer, offsets, indices);
            }

            ExecuteDrawCommandMesh(commandBuffer, pushConstantBuffer, command, DrawBlob._indirectTaskBuffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ExecuteDrawMeshCommand<T>(VkCommandBuffer commandBuffer, int frameIndex, Span<T> pushConstantBuffer,MeshDrawCommand command, Span<VkDescriptorBufferBindingInfoEXT> bindingInfo, Span<ulong> offsets, Span<uint> indices, ref int lastVariant, ref int lastMesh) where T : unmanaged
        {
            bool variantChanged = lastVariant != command.MaterialVariantIndex;
            bool meshChanged = lastMesh != command.DirectMeshHash;
            if (variantChanged)
            {
                UpdateVariantDescriptors(ref lastVariant, frameIndex, command.MaterialVariantIndex, offsets);
                SetCullModeAuto(commandBuffer, lastVariant);
            }

            if (meshChanged)
            {
                AddMeshShaderDescriptors(AssetDataBase<DirectMesh>.GetHashed(command.DirectMeshHash), frameIndex, (uint)command.MaterialVariantIndex, bindingInfo, offsets);
                lastMesh = command.DirectMeshHash;
            }

            if(meshChanged)
            {
                SetDescriptors(commandBuffer, bindingInfo, offsets, indices);
            }
            else if(variantChanged)
            {
                UpdateOffsets(commandBuffer,offsets, indices);
            }

            ExecuteDrawCommandMesh(commandBuffer, pushConstantBuffer, command, DrawBlob._indirectTaskBuffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExecuteDrawCommand<T>(VkCommandBuffer commandBuffer, Span<T> pushConstantBuffer, SwapChainBuffer<VECSDrawIndexIndirectCommand> indirectCmdBuffer, MaterialDrawCommand command) where T : unmanaged
        {
            BindPushConstants(commandBuffer, pushConstantBuffer);
            var mesh = AssetDataBase<DirectMesh>.GetHashed(command.DirectMesh);

            mesh.BindSpecificBuffers(commandBuffer, _graphicsPipelineConfigInfo.BindingDescriptions, _graphicsPipelineConfigInfo.AttributeDescriptions);
            unsafe
            {
                uint commandSize = (uint)sizeof(VECSDrawIndexIndirectCommand);
                GraphicsDevice.DeviceAPI.vkCmdDrawIndexedIndirect(
                    commandBuffer,
                    indirectCmdBuffer.ActiveVkBuffer,
                    (uint)command.MeshSubRegion.StartIndex * commandSize,
                    (uint)command.MeshSubRegion.Count, commandSize
                );
            }
        }

        private void ExecuteDrawCommandMesh<T>(VkCommandBuffer commandBuffer, Span<T> pushConstantBuffer, MeshDrawCommand command) where T : unmanaged
        {
            var host = ((SwapChainBuffer<MeshTaskCommand>)EngineBuffers.TryGetBuffer("taskCommands".GetShaderPropertyId())).HostBuffer.Slice(command.TaskOffset,command.TaskCount);
            uint totalPaddedMeshlets = 0;
            for ( var i = 0; i < host.Length; i++)
            {
                host[i].TaskStartIndex = totalPaddedMeshlets;
                totalPaddedMeshlets += host[i].MeshletCount.GetComputeShaderGroupCount(MeshShaderWorkGroupSizes.X);
            }
            _pushConstantsHandler.WriteToPushConstantBuffer("taskCount".GetShaderPropertyId(), pushConstantBuffer.AsBytes(), command.TaskCount);
            BindPushConstants(commandBuffer, pushConstantBuffer);
            GraphicsDevice.DeviceAPI.vkCmdDrawMeshTasksEXT(commandBuffer, totalPaddedMeshlets, 1, 1);
        }

        private unsafe void ExecuteDrawCommandMesh<T>(VkCommandBuffer commandBuffer, Span<T> pushConstantBuffer, MeshDrawCommand command, SwapChainBuffer<MeshIndirectDraw> indirectCmdBuffer) where T : unmanaged
        {
            uint commandSize = (uint)sizeof(MeshIndirectDraw);
            //_pushConstantsHandler.WriteToPushConstantBuffer("taskOffset".GetShaderPropertyId(), pushConstantBuffer.AsBytes(), command.TaskOffset);
            _pushConstantsHandler.WriteToPushConstantBuffer("taskCount".GetShaderPropertyId(), pushConstantBuffer.AsBytes(), command.TaskCount);

            // var indirectHost = indirectCmdBuffer.HostBuffer.Slice(command.TaskOffset, 1);
            // var host = ((SwapChainBuffer<MeshTaskCommand>)EngineBuffers.TryGetBuffer("taskCommands".GetShaderPropertyId())).HostBuffer.Slice(command.TaskOffset, command.TaskCount);
            // uint totalPaddedMeshlets = 0;
            // for (int i = 0; i < host.Length; i++)
            // {
            //     host[i].TaskStartIndex = totalPaddedMeshlets;
            //     totalPaddedMeshlets += host[i].MeshletCount.GetComputeShaderGroupCount(MeshShaderWorkGroupSizes.X);
            // }
            // indirectHost[0].GroupCountX = totalPaddedMeshlets;
            // indirectHost[0].CommandStart = (uint)command.TaskOffset;
            // indirectHost[0].CommandStart = (uint)command.TaskCount;
            BindPushConstants(commandBuffer, pushConstantBuffer);
            GraphicsDevice.DeviceAPI.vkCmdDrawMeshTasksIndirectEXT(commandBuffer, indirectCmdBuffer.ActiveVkBuffer, command.Index * commandSize, 1, commandSize);


        }
    }
}
