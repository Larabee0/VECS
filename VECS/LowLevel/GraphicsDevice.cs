using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Vortice.Vulkan;

namespace VECS.LowLevel
{
    /// <summary>
    /// Manages the vulkan device
    /// Picks the physical device
    /// Responsible for the vulkan instance.
    /// Responsible for the Vulkan Memory Allocator (VMA)
    /// </summary>
    public sealed class GraphicsDevice : IDisposable
    {
#if DEBUG
        private const bool ENABLE_VALIDATION_LAYERS = true;
#else
        private const bool ENABLE_VALIDATION_LAYERS = false;
#endif
        private readonly static string[] _requiredValidationLayers = ["VK_LAYER_KHRONOS_validation"];
        private readonly static VkUtf8String[] _requiredDeviceExtensions = [Vulkan.VK_KHR_SWAPCHAIN_EXTENSION_NAME, Vulkan.VK_KHR_SHADER_DRAW_PARAMETERS_EXTENSION_NAME,Vulkan.VK_KHR_SYNCHRONIZATION_2_EXTENSION_NAME,Vulkan.VK_EXT_SAMPLER_FILTER_MINMAX_EXTENSION_NAME];

        public static GraphicsDevice Instance { get; private set; }

        private readonly IWindow _window;

        private readonly VkDebugUtilsMessengerEXT _debugMessenger;

        private VkInstance _instance;

        public VkPhysicalDeviceProperties Properties;
        private VkPhysicalDevice _physicalDevice;

        private VkDevice _device;
        private VkSurfaceKHR _surface;

        private VkCommandPool _commandPool;

        private VkQueue _graphicsQueue;
        private VkQueue _computeQueue;
        private VkQueue _presentQueue;

        private VmaAllocator _allocator;

        public VkPhysicalDevice PhysucalDevice => _physicalDevice;
        public VkDevice Device => _device;
        public VkSurfaceKHR Surface => _surface;

        public VkCommandPool CommandBufferPool => _commandPool;

        public VkQueue GraphicsQueue => _graphicsQueue;
        public VkQueue PresentQueue => _presentQueue;

        public VmaAllocator VmaAllocator => _allocator;

        public VkInstance VkInstance => _instance;
        public SwapChainSupportDetails SwapChainSupport => QuerySwapChainSupport(_physicalDevice);
        public QueueFamilyIndices PhysicalQueueFamilies => FindQueueFamilies(_physicalDevice);

        public GraphicsDevice(IWindow window)
        {
            _window = window;

            CreateInstance();
            SetUpDebugMessenger();
            CreateSurface();
            PickPhysicalDevice();
            CreateLogicalDevice();
            CreateCommandPool();
            CreateVmaAllocator();
            Instance = this;
        }

        #region Create Instance

        /// <summary>
        /// This configures and starts the vulkan instance used by the application.
        /// 
        /// It will check the require device hardware extenstions needed.
        /// It will also setup validation layers if using the debug compiler.
        /// 
        /// </summary>
        /// <exception cref="Exception">Exceptions are thrown when validation layers are requesed but not avalible or when the vulkan instance fails to be created.</exception>
        private unsafe void CreateInstance()
        {
            if (ENABLE_VALIDATION_LAYERS && !CheckValidationLayerSupport())
            {
                throw new Exception("Validation layers requested, but not avaliable!");
            }

            VkApplicationInfo appInfo = GenerateAppInfo();

            using VkStringArray vkInstanceExtensions = new(GetRequiredExtensions());
            using VkStringArray validationlayers = new(_requiredValidationLayers);

            VkInstanceCreateInfo createInfo = new()
            {
                pApplicationInfo = &appInfo,
                enabledExtensionCount = vkInstanceExtensions.Length,
                ppEnabledExtensionNames = vkInstanceExtensions
            };


            if (ENABLE_VALIDATION_LAYERS)
            {
                createInfo.enabledLayerCount = (uint)_requiredValidationLayers.Length;
                createInfo.ppEnabledLayerNames = validationlayers;
                VkDebugUtilsMessengerCreateInfoEXT debugCreateInfo = PopulateDebugMessengerCreateInfo();
                createInfo.pNext = &debugCreateInfo;
            }
            else
            {
                createInfo.enabledLayerCount = 0;
                createInfo.pNext = null;
            }


            if (Vulkan.vkCreateInstance(&createInfo, null, out _instance) != VkResult.Success)
            {
                throw new Exception("Failed to create vulkan instance!");
            }

            Vulkan.vkLoadInstanceOnly(_instance);

            HasRequiredInstanceExtensions();
        }

        /// <summary>
        /// Configure the VkApplicationInfo struct.
        /// </summary>
        /// <returns></returns>
        private VkApplicationInfo GenerateAppInfo()
        {
            VkUtf8ReadOnlyString pApplicationName = Encoding.UTF8.GetBytes(_window.WindowName);
            VkUtf8ReadOnlyString pEngineName = "SDLVCS"u8;

            VkApplicationInfo appInfo = new()
            {
                pApplicationName = pApplicationName,
                pEngineName = pEngineName,
                engineVersion = new VkVersion(1, 0, 0),
                apiVersion = VkVersion.Version_1_3
            };
            return appInfo;
        }

        /// <summary>
        /// Determines if the hardware meets the requirements for the application
        /// </summary>
        /// <exception cref="Exception"></exception>
        private unsafe void HasRequiredInstanceExtensions()
        {
            Vulkan.vkEnumerateInstanceExtensionProperties(out uint propertyCount);

            VkExtensionProperties* extensions = stackalloc VkExtensionProperties[(int)propertyCount];


            Vulkan.vkEnumerateInstanceExtensionProperties(&propertyCount, extensions);

            Console.WriteLine("Available extensions:");
            HashSet<string> available = [];
            for (int i = 0; i < propertyCount; i++)
            {
                string extension = Encoding.UTF8.GetString(extensions[i].extensionName, 256);
                int terminator = extension.IndexOf('\0');
                extension = extension[..terminator];
                available.Add(extension);
                Console.WriteLine("\t" + extension);
            }
            Console.WriteLine("Required extensions:");
            var required = GetRequiredExtensions();

            for (int i = 0; i < required.Count; i++)
            {
                string extension = Encoding.UTF8.GetString(required[i].Buffer, 256);
                int terminator = extension.IndexOf('\0');
                extension = extension[..terminator];
                Console.WriteLine("\t" + extension);
                if (!available.Contains(extension))
                {
                    throw new Exception("Missing required extension");
                }
            }

        }

        /// <summary>
        /// Gets the required extensions needed by SDL3, move to window file?
        /// 
        /// Also appends the debug utils extension if validation layers are enabled.
        /// </summary>
        /// <returns>List of Device extensions to configure the vulkan instance with</returns>
        private List<VkUtf8String> GetRequiredExtensions()
        {
            string[] sdlRequiredExtensions = _window.GetWindowExtensionRequirements();

            List<VkUtf8String> requiredExtensions = new(sdlRequiredExtensions.Length);

            for (int i = 0; i < sdlRequiredExtensions.Length; i++)
            {
                requiredExtensions.Add(new ReadOnlySpan<byte>(Encoding.UTF8.GetBytes(sdlRequiredExtensions[i])));
            }

            if (ENABLE_VALIDATION_LAYERS)
            {
                requiredExtensions.Add(Vulkan.VK_EXT_DEBUG_UTILS_EXTENSION_NAME);
            }

            return requiredExtensions;
        }
        #endregion

        #region DebugMessenger
        /// <summary>
        /// Validation messenger setup
        /// </summary>
        /// <exception cref="Exception"></exception>
        private unsafe void SetUpDebugMessenger()
        {
            if (!ENABLE_VALIDATION_LAYERS) return;
            VkDebugUtilsMessengerCreateInfoEXT createInfoEXT = PopulateDebugMessengerCreateInfo();

            fixed (VkDebugUtilsMessengerEXT* toPtr = &_debugMessenger)
            {
                if (CreateDebugUtilsMessengerEXT(_instance, &createInfoEXT, null, toPtr) != VkResult.Success)
                {
                    throw new Exception("failed to set up debug messenger!");
                }
            }
        }

        #endregion

        /// <summary>
        /// creates the VK surface to output to
        /// </summary>
        private void CreateSurface()
        {
            _surface = _window.CreateWindowSurface(_instance);
        }

        #region Pick Physical Device
        /// <summary>
        /// pick the phyiscal device to use from the avaliable graphics devices.
        /// This picks the first device compatible with the app
        /// (if this code is running on my laptop I force it to use the nvidia card (i = 1)
        /// </summary>
        /// <exception cref="Exception"></exception>
        private unsafe void PickPhysicalDevice()
        {
            var devices = Vulkan.vkEnumeratePhysicalDevices(_instance);

            if (devices.Length == 0)
            {
                throw new Exception("Failed to find GPUs with Vulkan support!");
            }

            Console.WriteLine(string.Format("Device count: {0}", devices.Length));

            for (int i = devices.Length - 1; i >= 0; i--)
            {
                var device = devices[i];
                if (IsDeviceSuitable(device))
                {
                    _physicalDevice = device;
                    break;
                }
            }

            if (_physicalDevice == VkPhysicalDevice.Null)
            {
                throw new Exception("Failed to find a sutiable GPU!");
            }

            Vulkan.vkGetPhysicalDeviceProperties(_physicalDevice, out Properties);

            fixed (byte* devName = Properties.deviceName)
            {
                var str = new VkUtf8String(devName);
                Console.WriteLine(string.Format("Physical device: {0}", str));
            }
        }

        /// <summary>
        /// Determines if a given physical device is suitable for the app
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        private bool IsDeviceSuitable(VkPhysicalDevice device)
        {
            QueueFamilyIndices indices = FindQueueFamilies(device);

            bool extensionsSupported = CheckDeviceExtensionSupport(device);

            bool swapChainAdequate = false;

            if (extensionsSupported)
            {
                SwapChainSupportDetails swapChainSupport = QuerySwapChainSupport(device);
                swapChainAdequate = swapChainSupport.formats.Length > 0 && swapChainSupport.presentModes.Length > 0;
            }

            Vulkan.vkGetPhysicalDeviceFeatures(device, out VkPhysicalDeviceFeatures supportedFeatures);

            return indices.IsComplete && extensionsSupported && swapChainAdequate && supportedFeatures.samplerAnisotropy;
        }

        /// <summary>
        /// Gets the queue families for the physical device
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        private QueueFamilyIndices FindQueueFamilies(VkPhysicalDevice device)
        {
            QueueFamilyIndices indices = default;
            var queueFamilies = Vulkan.vkGetPhysicalDeviceQueueFamilyProperties(device);

            for (int i = 0; i < queueFamilies.Length; i++)
            {
                var family = queueFamilies[i];

                if (family.queueCount > 0 && family.queueFlags.HasFlag(VkQueueFlags.Graphics) && family.queueFlags.HasFlag(VkQueueFlags.Compute))
                {
                    indices.graphicsFamily = i;
                    indices.graphicsFamilyHasValue = true;
                }

                Vulkan.vkGetPhysicalDeviceSurfaceSupportKHR(device, (uint)i, _surface, out VkBool32 presentSupport);

                if (family.queueCount > 0 && presentSupport)
                {
                    indices.presentFamily = i;
                    indices.presentFamilyHasValue = true;
                }

                if (indices.IsComplete)
                {
                    break;
                }
            }

            return indices;
        }

        /// <summary>
        /// Gets the swapchain support details for a given physical device.
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        private SwapChainSupportDetails QuerySwapChainSupport(VkPhysicalDevice device)
        {
            SwapChainSupportDetails details = default;
            Vulkan.vkGetPhysicalDeviceSurfaceCapabilitiesKHR(device, _surface, out details.capabilities);

            var formats = Vulkan.vkGetPhysicalDeviceSurfaceFormatsKHR(device, _surface);
            details.formats = new VkSurfaceFormatKHR[formats.Length];
            formats.CopyTo(details.formats);

            var presentModes = Vulkan.vkGetPhysicalDeviceSurfacePresentModesKHR(device, _surface);
            details.presentModes = new VkPresentModeKHR[presentModes.Length];
            presentModes.CopyTo(details.presentModes);


            return details;
        }

        #endregion

        #region Create Logical Device
        /// <summary>
        /// creates a logical vulkan device from the selected physical device <see cref="_physicalDevice"/>
        /// </summary>
        /// <exception cref="Exception"></exception>
        private unsafe void CreateLogicalDevice()
        {
            QueueFamilyIndices indices = FindQueueFamilies(_physicalDevice);

            HashSet<int> uniqueQueueFamilies = [indices.graphicsFamily, indices.presentFamily];
            VkDeviceQueueCreateInfo[] queueCreateInfos = new VkDeviceQueueCreateInfo[uniqueQueueFamilies.Count];

            float queuePriority = 1f;
            int index = 0;
            foreach (var queueFamily in uniqueQueueFamilies)
            {
                VkDeviceQueueCreateInfo queueCreateInfo = new()
                {
                    queueFamilyIndex = (uint)queueFamily,
                    queueCount = 1,
                    pQueuePriorities = &queuePriority
                };
                queueCreateInfos[index] = queueCreateInfo;
            }


            VkDeviceQueueCreateInfo* pQueueCreateInfos = stackalloc VkDeviceQueueCreateInfo[queueCreateInfos.Length];

            fixed (VkDeviceQueueCreateInfo* pTempQueueCreateInfos = &queueCreateInfos[0])
            {
                int byteSize = sizeof(VkDeviceQueueCreateInfo) * queueCreateInfos.Length;
                NativeMemory.Copy(pTempQueueCreateInfos, pQueueCreateInfos, (uint)byteSize);
            }

            VkPhysicalDeviceFeatures deviceFeature = new()
            {
                samplerAnisotropy = true,
                fillModeNonSolid = true,
                multiDrawIndirect = true,
                drawIndirectFirstInstance = true
            };
            using VkStringArray deviceExtensionNames = new(_requiredDeviceExtensions);


            VkPhysicalDeviceSynchronization2Features sync2 = new() { synchronization2 = true };

            VkDeviceCreateInfo createInfo = new()
            {
                queueCreateInfoCount = (uint)queueCreateInfos.Length,
                pQueueCreateInfos = pQueueCreateInfos,
                pEnabledFeatures = &deviceFeature,
                enabledExtensionCount = (uint)_requiredDeviceExtensions.Length,
                ppEnabledExtensionNames = deviceExtensionNames,
                pNext = &sync2
            };

            if (ENABLE_VALIDATION_LAYERS)
            {
                using VkStringArray enabledValidationlayers = new(_requiredValidationLayers);
                createInfo.enabledLayerCount = (uint)_requiredValidationLayers.Length;
                createInfo.ppEnabledLayerNames = enabledValidationlayers;
            }
            else
            {
                createInfo.enabledLayerCount = 0;
            }

            if (Vulkan.vkCreateDevice(_physicalDevice, in createInfo, null, out _device) != VkResult.Success)
            {
                throw new Exception("Failed to create logical device");
            }


            Vulkan.vkLoadDevice(_device);

            Vulkan.vkGetDeviceQueue(_device, (uint)indices.graphicsFamily, 0, out _graphicsQueue);
            Vulkan.vkGetDeviceQueue(_device, (uint)indices.graphicsFamily, 0, out _computeQueue);
            Vulkan.vkGetDeviceQueue(_device, (uint)indices.presentFamily, 0, out _presentQueue);
        }

        #endregion

        #region Create Command Pool
        /// <summary>
        /// Creates the command buffer pool for submitting commands to the logical device
        /// </summary>
        /// <exception cref="Exception"></exception>
        private unsafe void CreateCommandPool()
        {
            QueueFamilyIndices queueFamilyIndices = PhysicalQueueFamilies;

            VkCommandPoolCreateInfo poolInfo = new()
            {
                queueFamilyIndex = (uint)queueFamilyIndices.graphicsFamily,
                flags = VkCommandPoolCreateFlags.Transient | VkCommandPoolCreateFlags.ResetCommandBuffer
            };

            if (Vulkan.vkCreateCommandPool(_device, poolInfo, null, out _commandPool) != VkResult.Success)
            {
                throw new Exception("failed to create command pool!");
            }
        }

        #endregion

        #region Create VmaAllocator
        /// <summary>
        /// Create a Vma allocator for the allocation of VkBuffers and VKImages constructed during the application lifetime.
        /// </summary>
        private void CreateVmaAllocator()
        {
            VmaAllocatorCreateInfo allocatorCreateInfo = new()
            {
                flags = VmaAllocatorCreateFlags.KHRDedicatedAllocation | VmaAllocatorCreateFlags.KHRBindMemory2,
                instance = VkInstance,
                vulkanApiVersion = VkVersion.Version_1_3,
                physicalDevice = PhysucalDevice,
                device = Device,
            };
            Vma.vmaCreateAllocator(in allocatorCreateInfo, out _allocator);
        }
        #endregion

        #region For Extneral use
        /// <summary>
        /// Used by the swapchain class to work out what which VkFormat from the given candidates is supported
        /// by the currently selected physical device <see cref="_physicalDevice"/>
        /// </summary>
        /// <param name="candidates">VkFormats to pick from</param>
        /// <param name="tiling">tiling mode</param>
        /// <param name="features">required format feature flags</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public VkFormat FindSupportFormat(VkFormat[] candidates, VkImageTiling tiling, VkFormatFeatureFlags features)
        {
            for (int i = 0; i < candidates.Length; i++)
            {
                VkFormat format = candidates[i];
                Vulkan.vkGetPhysicalDeviceFormatProperties(_physicalDevice, format, out VkFormatProperties props);
                if (tiling == VkImageTiling.Linear && (props.linearTilingFeatures & features) == features)
                {
                    return format;
                }
                else if (tiling == VkImageTiling.Optimal && (props.optimalTilingFeatures & features) == features)
                {
                    return format;
                }
            }

            throw new Exception("Failed to find support image format");
        }

        /// <summary>
        /// Creates a Vk Image and assocsiated device memory with the given settings from imageInfo and properties
        /// </summary>
        /// <param name="imageInfo"></param>
        /// <param name="properties"></param>
        /// <param name="image"></param>
        /// <param name="imageMemory"></param>
        /// <exception cref="Exception"></exception>
        public unsafe void CreateImageWithInfo(
            VkImageCreateInfo imageInfo,
            VkMemoryPropertyFlags properties,
            out VkImage image,
            out VkDeviceMemory imageMemory)
        {
            if (Vulkan.vkCreateImage(_device, imageInfo, null, out image) != VkResult.Success)
            {
                throw new Exception("Failed to create image with info");
            }


            Vulkan.vkGetImageMemoryRequirements(_device, image, out VkMemoryRequirements memoryRequirements);

            VkMemoryAllocateInfo allocInfo = new()
            {
                allocationSize = memoryRequirements.size,
                memoryTypeIndex = FindMemoryType(memoryRequirements.memoryTypeBits, properties),
            };

            if (Vulkan.vkAllocateMemory(_device, &allocInfo, null, out imageMemory) != VkResult.Success)
            {
                throw new Exception("failed to allocate image memory!");
            }

            if (Vulkan.vkBindImageMemory(_device, image, imageMemory, 0) != VkResult.Success)
            {
                throw new Exception("failed to bind image memory!");
            }
        }

        /// <summary>
        /// Funky method for determing the memory type index for a vkMemoryAllocation
        /// </summary>
        /// <param name="typeFilter"></param>
        /// <param name="properties"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private uint FindMemoryType(uint typeFilter, VkMemoryPropertyFlags properties)
        {
            Vulkan.vkGetPhysicalDeviceMemoryProperties(_physicalDevice, out VkPhysicalDeviceMemoryProperties memoryProperties);

            for (int i = 0; i < memoryProperties.memoryTypeCount; i++)
            {
                if ((typeFilter & 1) == 1)
                {
                    if ((memoryProperties.memoryTypes[i].propertyFlags & properties) == properties)
                    {
                        return (uint)i;
                    }
                }
                typeFilter >>= 1;
            }

            throw new Exception("Failed to find suitable memory type!");
        }

        /// <summary>
        /// Copies the src buffer to the dst buffer from 0 point in both buffers to size
        /// </summary>
        /// <param name="srcBuffer"></param>
        /// <param name="dstBuffer"></param>
        /// <param name="size"></param>
        public unsafe void CopyBuffer(VkBuffer srcBuffer, VkBuffer dstBuffer, uint size)
        {
            CopyBuffer(size, srcBuffer, 0, dstBuffer, 0);
        }

        public void CopyBuffer(ulong size, VkBuffer srcBuffer, ulong srcOffset, VkBuffer dstBuffer, ulong dstOffset)
        {
            VkCommandBuffer commandBuffer = BeginSingleTimeCommands();

            CopyBuffer(commandBuffer,size,srcBuffer,srcOffset, dstBuffer, dstOffset);

            EndSingleTimeCommands(commandBuffer);
        }


        public static unsafe void CopyBuffer(VkCommandBuffer commandBuffer, ulong size, VkBuffer srcBuffer, ulong srcOffset, VkBuffer dstBuffer, ulong dstOffset)
        {
            VkBufferCopy copyRegion = new()
            {
                srcOffset = srcOffset,
                dstOffset = dstOffset,
                size = size
            };
            Vulkan.vkCmdCopyBuffer(commandBuffer, srcBuffer, dstBuffer, 1, &copyRegion);
        }

        /// <summary>
        /// Gets a command buffer to a single command.
        /// </summary>
        /// <returns></returns>
        public VkCommandBuffer BeginSingleTimeCommands()
        {
            Vulkan.vkAllocateCommandBuffer(Device, _commandPool, VkCommandBufferLevel.Primary, out VkCommandBuffer commandBuffer);
            Vulkan.vkBeginCommandBuffer(commandBuffer, VkCommandBufferUsageFlags.OneTimeSubmit);
            return commandBuffer;
        }

        /// <summary>
        /// Submit the given single time command buffer to the gpu
        /// </summary>
        /// <param name="commandBuffer"></param>
        public unsafe void EndSingleTimeCommands(VkCommandBuffer commandBuffer)
        {
            Vulkan.vkEndCommandBuffer(commandBuffer);
            VkSubmitInfo submitInfo = new()
            {
                commandBufferCount = 1,
                pCommandBuffers = &commandBuffer
            };
            Vulkan.vkQueueSubmit(_graphicsQueue, submitInfo, VkFence.Null);
            Vulkan.vkQueueWaitIdle(_graphicsQueue);
            Vulkan.vkFreeCommandBuffers(Device, _commandPool, commandBuffer);
        }
        #endregion

        /// <summary>
        /// Cleans up the vulkan device and vulkan instance and Vma Allocator.
        /// </summary>
        public unsafe void Dispose()
        {
            Instance = null;
            Vma.vmaDestroyAllocator(_allocator);

            Vulkan.vkDestroyCommandPool(_device, _commandPool);
            Vulkan.vkDestroyDevice(_device);

            if (ENABLE_VALIDATION_LAYERS)
            {
                DestroyDebugUtilsMessengerEXT(_instance, _debugMessenger, null);
            }

            Vulkan.vkDestroySurfaceKHR(_instance, _surface);
            Vulkan.vkDestroyInstance(_instance);
        }

        #region Validation and Debugging statics
        /// <summary>
        /// Checks if our hardware can support validation layers requrested in <see cref="_requiredValidationLayers"/>
        /// </summary>
        /// <returns></returns>
        private static bool CheckValidationLayerSupport()
        {
            ReadOnlySpan<VkLayerProperties> availableLayers = Vulkan.vkEnumerateInstanceLayerProperties();

            for (int i = 0; i < _requiredValidationLayers.Length; i++)
            {
                bool supportsLayer = false;
                for (int j = 0; j < availableLayers.Length; j++)
                {
                    if (_requiredValidationLayers[i] == _requiredValidationLayers[j])
                    {
                        supportsLayer = true;
                        break;
                    }
                }

                if (!supportsLayer)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Configures the debug messenger callback for validation layer errors.
        /// </summary>
        /// <returns></returns>
        private unsafe static VkDebugUtilsMessengerCreateInfoEXT PopulateDebugMessengerCreateInfo() => new()
        {
            messageSeverity = VkDebugUtilsMessageSeverityFlagsEXT.Warning
            | VkDebugUtilsMessageSeverityFlagsEXT.Error,

            messageType = VkDebugUtilsMessageTypeFlagsEXT.General
            | VkDebugUtilsMessageTypeFlagsEXT.Validation
            | VkDebugUtilsMessageTypeFlagsEXT.Performance,

            pfnUserCallback = &ValidationDebugCallback,
            pUserData = null,
        };


        /// <summary>
        /// Validation layer callback for logging validation servirty and messages to the console.
        /// </summary>
        /// <param name="messageSeverity"></param>
        /// <param name="messageType"></param>
        /// <param name="pCallbackData"></param>
        /// <param name="pUserData"></param>
        /// <returns></returns>
        [UnmanagedCallersOnly]
        private unsafe static uint ValidationDebugCallback(
            VkDebugUtilsMessageSeverityFlagsEXT messageSeverity,
            VkDebugUtilsMessageTypeFlagsEXT messageType,
            VkDebugUtilsMessengerCallbackDataEXT* pCallbackData,
            void* pUserData)
        {
            var message = new VkUtf8String(pCallbackData->pMessage);

            Console.WriteLine(string.Format("[{0}] Vulkan: Validation Layer: {1}", messageSeverity, Encoding.UTF8.GetString(message.Span)));

            return 0;
        }

        /// <summary>
        /// Creates the validation layer debug messenger
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="pCreateInfo"></param>
        /// <param name="pAllocator"></param>
        /// <param name="pDebugMessenger"></param>
        /// <returns></returns>
        private unsafe static VkResult CreateDebugUtilsMessengerEXT(
            VkInstance instance,
            VkDebugUtilsMessengerCreateInfoEXT* pCreateInfo,
            VkAllocationCallbacks* pAllocator,
            VkDebugUtilsMessengerEXT* pDebugMessenger)
        {
            // Horrific function pointer cast.
            var func = (delegate*
                unmanaged<VkInstance,
                VkDebugUtilsMessengerCreateInfoEXT*,
                VkAllocationCallbacks*,
                VkDebugUtilsMessengerEXT*,
                VkResult>
                )Vulkan.vkGetInstanceProcAddr(instance, "vkCreateDebugUtilsMessengerEXT").Value;

            if (func != null)
            {

                return func(instance, pCreateInfo, pAllocator, pDebugMessenger);
            }
            else
            {
                return VkResult.ErrorExtensionNotPresent;
            }
        }

        /// <summary>
        /// Destroys the validation layer debug messenger
        /// </summary>
        /// <param name="instance">active vulkan instance</param>
        /// <param name="debugMessenger">target debug messenger</param>
        /// <param name="pAllocator"></param>
        private unsafe static void DestroyDebugUtilsMessengerEXT(
            VkInstance instance,
            VkDebugUtilsMessengerEXT debugMessenger,
            VkAllocationCallbacks* pAllocator)
        {
            // Slightly less horrific function pointer cast.
            var func = (delegate*
                unmanaged<VkInstance,
                VkDebugUtilsMessengerEXT,
                VkAllocationCallbacks*,
                void>
                )Vulkan.vkGetInstanceProcAddr(instance, "vkDestroyDebugUtilsMessengerEXT").Value;
            if (func != null)
            {
                func(instance, debugMessenger, pAllocator);
            }
        }


        #endregion

        #region Extensions Statics
        /// <summary>
        /// Checks if the given physical device supports the required
        /// device extentions in <see cref="_requiredDeviceExtensions"/>
        /// </summary>
        /// <param name="device"></param>
        /// <returns>true if the physical devices supports the extensions requested </returns>
        private unsafe static bool CheckDeviceExtensionSupport(VkPhysicalDevice device)
        {
            var availableExtensions = Vulkan.vkEnumerateDeviceExtensionProperties(device);

            HashSet<VkUtf8String> requiredSet = new(_requiredDeviceExtensions);

            for (int i = 0; i < availableExtensions.Length; i++)
            {
                var ext = availableExtensions[i];
                string extension = Encoding.UTF8.GetString(ext.extensionName, 256);
                int terminator = extension.IndexOf('\0');
                extension = extension[..terminator];
                byte[] bytes = Encoding.UTF8.GetBytes(extension);
                fixed (byte* pByes = &bytes[0])
                {
                    VkUtf8String vkUtf8 = new(pByes, bytes.Length);
                    requiredSet.Remove(vkUtf8);
                }
            }


            return requiredSet.Count == 0;
        }

        #endregion

        /// <summary>
        /// graphics queue famil indices.
        /// </summary>
        public struct QueueFamilyIndices
        {
            public int graphicsFamily;
            public int presentFamily;
            public bool graphicsFamilyHasValue;
            public bool presentFamilyHasValue;
            public readonly bool IsComplete => graphicsFamilyHasValue && presentFamilyHasValue;
        }

        /// <summary>
        /// Swap chain information about the graphics card
        /// </summary>
        public struct SwapChainSupportDetails
        {
            public VkSurfaceCapabilitiesKHR capabilities;
            public VkSurfaceFormatKHR[] formats;
            public VkPresentModeKHR[] presentModes;
        }
    }
}
