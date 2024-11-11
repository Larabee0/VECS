using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS
{
    public sealed class Presenter : IDisposable
    {
        private readonly GraphicsDevice _device;
        private readonly Renderer _renderer;

        private readonly VkDescriptorSet[] _globalDescriptorSets = new VkDescriptorSet[SwapChain.MAX_FRAMES_IN_FLIGHT];
        private readonly Buffer[] _uboBuffers = new Buffer[SwapChain.MAX_FRAMES_IN_FLIGHT];
        private DescriptorSetLayout _globalDescriptorSetLayout;

        private DescriptorPool _globalPool;

        private readonly DescriptorPool[] framePools = new DescriptorPool[SwapChain.MAX_FRAMES_IN_FLIGHT];

        public Presenter(IWindow window, GraphicsDevice  device)
        {
            _device = device;
            _renderer = new(window, device);


            InitGloalPool();
            InitFramePools();
        }

        private void InitGloalPool()
        {
            _globalPool = new DescriptorPool.Builder(_device)
                .SetMaxSets(SwapChain.MAX_FRAMES_IN_FLIGHT)
                .AddPoolSize(VkDescriptorType.UniformBuffer, SwapChain.MAX_FRAMES_IN_FLIGHT)
                .Build();
        }

        private void InitFramePools()
        {
            DescriptorPool.Builder framePoolBuilder = new DescriptorPool.Builder(_device)
                            .SetMaxSets(1000)
                            .AddPoolSize(VkDescriptorType.CombinedImageSampler, 1000)
                            .AddPoolSize(VkDescriptorType.UniformBuffer, 1000)
                            .SetPoolFlags(VkDescriptorPoolCreateFlags.FreeDescriptorSet);
            for (int i = 0; i < framePools.Length; i++)
            {
                framePools[i] = framePoolBuilder.Build();
            }
        }

        public void Start()
        {
            _globalDescriptorSetLayout = ConfigureUboBuffers(_uboBuffers, _globalDescriptorSets);
        }

        private DescriptorSetLayout ConfigureUboBuffers(Buffer[] uboBuffers, VkDescriptorSet[] globalDescriptorSets)
        {
            for (int i = 0; i < uboBuffers.Length; i++)
            {
                uboBuffers[i] = new(
                    _device,
                    GlobalUbo.SizeInBytes,
                    1,
                     VkBufferUsageFlags.UniformBuffer,
                    VkMemoryPropertyFlags.HostVisible);
                uboBuffers[i].Map(); // map the GPU device memory to the System memory.
            }

            // add the binding for this buffer and set where it is avaliable in the shader pipeline
            // in this case its avaliable to all graphis stages.
            var globalSetLayout = new DescriptorSetLayout.Builder(_device)
                .AddBinding(0, VkDescriptorType.UniformBuffer, VkShaderStageFlags.AllGraphics)
                .Build();

            // write the buffer to the descriptor set linking all the data up
            for (int i = 0; i < globalDescriptorSets.Length; i++)
            {
                var bufferInfo = uboBuffers[i].DescriptorInfo();
                new DescriptorWriter(globalSetLayout, _globalPool)
                    .WriteBuffer(0, bufferInfo)
                    .Build(globalDescriptorSets[i]);
            }

            return globalSetLayout;
        }

        public RendererFrameInfo BeginPresent(float deltaTime)
        {
            VkCommandBuffer commandBuffer = _renderer.BeginFrame();
            if (commandBuffer != VkCommandBuffer.Null)
            {
                int frameIndex = _renderer.FrameIndex;
                RendererFrameInfo frameInfo = new()
                {
                    FrameIndex = frameIndex,
                    DeltaTime = deltaTime,
                    commandBuffer = commandBuffer,
                    GlobalDescriptorSet = _globalDescriptorSets[frameIndex]
                };
                _renderer.BeginSwapChainRenderPass(commandBuffer);
                return frameInfo;
            }

            return RendererFrameInfo.Null;
        }

        public void EndPresent(RendererFrameInfo frameInfo)
        {
            _renderer.EndSwapChainRenderPass(frameInfo.commandBuffer);
            _renderer.EndFrame();
        }

        public void Dispose()
        {
            _renderer.Dispose();
        }

    }
}
