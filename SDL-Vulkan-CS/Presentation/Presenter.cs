using SDL_Vulkan_CS.ECS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS
{
    public sealed class Presenter : IDisposable
    {
        private readonly GraphicsDevice _device;
        private readonly Renderer _renderer;

        private VmaAllocator _allocator;

        private readonly VkDescriptorSet[] _globalDescriptorSets = new VkDescriptorSet[SwapChain.MAX_FRAMES_IN_FLIGHT];
        private readonly CsharpVulkanBuffer[] _uboBuffers = new CsharpVulkanBuffer[SwapChain.MAX_FRAMES_IN_FLIGHT];
        private DescriptorSetLayout _globalDescriptorSetLayout;

        private DescriptorPool _globalPool;

        private readonly DescriptorPool[] framePools = new DescriptorPool[SwapChain.MAX_FRAMES_IN_FLIGHT];
        
        private Entity frameInfoEntity;

        public Presenter(IWindow window, GraphicsDevice  device)
        {
            _device = device;
            _renderer = new(window, device);
            InitaliseVmaAllocator();

            InitGloalPool();
            InitFramePools();
        }

        private void InitaliseVmaAllocator()
        {
            VmaAllocatorCreateInfo allocatorCreateInfo = new()
            {
                flags = VmaAllocatorCreateFlags.KHRDedicatedAllocation | VmaAllocatorCreateFlags.KHRBindMemory2,
                instance = _device.VkInstance,
                vulkanApiVersion = VkVersion.Version_1_3,
                physicalDevice = _device.PhysucalDevice,
                device = _device.Device,
            };
            Vma.vmaCreateAllocator(in allocatorCreateInfo, out _allocator);
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


            World.DefaultWorld.PresentationSystems.Add(new TriangleSystem(_device, _renderer.SwapChainRenderPass, _globalDescriptorSetLayout.SetLayout));

            frameInfoEntity = World.DefaultWorld.EntityManager.CreateEntity();

            World.DefaultWorld.EntityManager.AddComponent<FrameInfo>(frameInfoEntity);

        }

        private unsafe DescriptorSetLayout ConfigureUboBuffers(CsharpVulkanBuffer[] uboBuffers, VkDescriptorSet[] globalDescriptorSets)
        {
            for (int i = 0; i < uboBuffers.Length; i++)
            {
                uboBuffers[i] = new(
                    _allocator,
                    (uint)GlobalUbo.SizeInBytes,
                    1,
                    VkBufferUsageFlags.UniformBuffer,
                    true);
                //uboBuffers[i].Map(); // map the GPU device memory to the System memory.
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
                fixed(VkDescriptorSet* pSet = &globalDescriptorSets[i])
                {
                    new DescriptorWriter(globalSetLayout, _globalPool)
                        .WriteBuffer(0, bufferInfo)
                        .Build(pSet);
                }
            }

            return globalSetLayout;
        }


        public void UpdateEntityFrameInfo(EntityManager entityManager)
        {
            entityManager.SetComponent(frameInfoEntity, new FrameInfo()
            {
                screenAspect = _renderer.AspectRatio
            });
        }

        public unsafe RendererFrameInfo BeginPresent(float deltaTime)
        {
            UpdateEntityFrameInfo(World.DefaultWorld.EntityManager);

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

                Camera camera = Camera.Identity;

                if(World.DefaultWorld.EntityManager.SingletonEntity<MainCamera>(out Entity mainCamera))
                {
                    camera = World.DefaultWorld.EntityManager.GetComponent<Camera>(mainCamera);
                }

                GlobalUbo ubo = new()
                {
                    Projection = camera.ProjectionMatrix,
                    View = camera.ViewMatrix,
                    InverseView = camera.InverseViewMatrix,
                    NumLights = 0,
                    AmbientLightColour = Vector4.One
                };

                _uboBuffers[frameIndex].WriteToBuffer(_allocator, &ubo);
                _uboBuffers[frameIndex].Flush(_allocator);

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
            Vma.vmaDestroyAllocator(_allocator);
            _renderer.Dispose();
        }

    }
}
