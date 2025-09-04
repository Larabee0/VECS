using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Vortice.Vulkan;

using static VECS.LowLevel.GraphicsDevice;

namespace VECS.LowLevel
{
    internal static class GraphicsDeviceInit
    {
#if DEBUG
        public static bool BreakOnValidationError = true;
        private readonly static string[] _requiredValidationLayers = ["VK_LAYER_KHRONOS_validation"];
#endif
        private readonly static VkUtf8String[] _requiredDeviceExtensions = [
            Vulkan.VK_KHR_SWAPCHAIN_EXTENSION_NAME,
            Vulkan.VK_KHR_SHADER_DRAW_PARAMETERS_EXTENSION_NAME,
            Vulkan.VK_KHR_SYNCHRONIZATION_2_EXTENSION_NAME,
            Vulkan.VK_EXT_SAMPLER_FILTER_MINMAX_EXTENSION_NAME,
            Vulkan.VK_KHR_IMAGELESS_FRAMEBUFFER_EXTENSION_NAME,
            Vulkan.VK_KHR_TIMELINE_SEMAPHORE_EXTENSION_NAME,

            Vulkan.VK_KHR_SPIRV_1_4_EXTENSION_NAME,
            Vulkan.VK_EXT_MESH_SHADER_EXTENSION_NAME,
            Vulkan.VK_KHR_SHADER_FLOAT_CONTROLS_EXTENSION_NAME
        ];

#if DEBUG
        internal static readonly VkDebugUtilsMessengerEXT _debugMessenger;
#endif

        #region Create Instance

        /// <summary>
        /// This configures and starts the vulkan instance used by the application.
        /// 
        /// It will check the require device hardware extenstions needed.
        /// It will also setup validation layers if using the debug compiler.
        /// 
        /// </summary>
        /// <exception cref="Exception">Exceptions are thrown when validation layers are requesed but not avalible or when the vulkan instance fails to be created.</exception>
        internal static unsafe void CreateInstance()
        {

#if DEBUG
            if (!CheckValidationLayerSupport())
            {
                throw new Exception("Validation layers requested, but not avaliable!");
            }
#endif
            VkApplicationInfo appInfo = GenerateAppInfo();

            using VkStringArray vkInstanceExtensions = new(GetRequiredExtensions());

            VkInstanceCreateInfo createInfo = new()
            {
                pApplicationInfo = &appInfo,
                enabledExtensionCount = vkInstanceExtensions.Length,
                ppEnabledExtensionNames = vkInstanceExtensions
            };

#if DEBUG
            using VkStringArray validationlayers = new(_requiredValidationLayers);


            createInfo.enabledLayerCount = (uint)_requiredValidationLayers.Length;
            createInfo.ppEnabledLayerNames = validationlayers;
            VkDebugUtilsMessengerCreateInfoEXT debugCreateInfo = PopulateDebugMessengerCreateInfo();
            createInfo.pNext = &debugCreateInfo;

#else
            createInfo.enabledLayerCount = 0;
            createInfo.pNext = null;
#endif

            Vulkan.CheckResult(Vulkan.vkCreateInstance(&createInfo, null, out _instance), "Failed to create vulkan instance!");
            

            Vulkan.vkLoadInstanceOnly(_instance);

            HasRequiredInstanceExtensions();
        }

        /// <summary>
        /// Configure the VkApplicationInfo struct.
        /// </summary>
        /// <returns></returns>
        internal static VkApplicationInfo GenerateAppInfo()
        {
            VkUtf8ReadOnlyString pApplicationName = Encoding.UTF8.GetBytes(_window.WindowName);
            VkUtf8ReadOnlyString pEngineName = "SDLVCS"u8;

            VkApplicationInfo appInfo = new()
            {
                pApplicationName = pApplicationName,
                pEngineName = pEngineName,
                engineVersion = new VkVersion(1, 0, 0),
                apiVersion = VkVersion.Version_1_4
            };
            return appInfo;
        }

        /// <summary>
        /// Determines if the hardware meets the requirements for the application
        /// </summary>
        /// <exception cref="Exception"></exception>
        internal static unsafe void HasRequiredInstanceExtensions()
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
        internal static List<VkUtf8String> GetRequiredExtensions()
        {
            string[] sdlRequiredExtensions = _window.GetWindowExtensionRequirements();

            List<VkUtf8String> requiredExtensions = new(sdlRequiredExtensions.Length);

            for (int i = 0; i < sdlRequiredExtensions.Length; i++)
            {
                requiredExtensions.Add(new ReadOnlySpan<byte>(Encoding.UTF8.GetBytes(sdlRequiredExtensions[i])));
            }
#if DEBUG
            requiredExtensions.Add(Vulkan.VK_EXT_DEBUG_UTILS_EXTENSION_NAME);
#endif
            return requiredExtensions;
        }
        #endregion

        /// <summary>
        /// creates the VK surface to output to
        /// </summary>
        internal static VkSurfaceKHR CreateSurface()
        {
            return _window.CreateWindowSurface(GraphicsDevice.VkInstance);
        }

        #region Pick Physical Device
        /// <summary>
        /// pick the phyiscal device to use from the avaliable graphics devices.
        /// This picks the first device compatible with the app
        /// (if this code is running on my laptop I force it to use the nvidia card (i = 1)
        /// </summary>
        /// <exception cref="Exception"></exception>
        internal static unsafe void PickPhysicalDevice()
        {
            var devices = Vulkan.vkEnumeratePhysicalDevices(_instance);

            if (devices.Length == 0)
            {
                throw new Exception("Failed to find GPUs with Vulkan support!");
            }

            Console.WriteLine(string.Format("Device count: {0}", devices.Length));
            List<DeviceInfo> deviceHeapInfo = [];
            for (int i = 0; i < devices.Length; i++)
            {
                var device = devices[i];
                if (IsDeviceSuitable(device))
                {
                    _physicalDevice = device;
                    deviceHeapInfo.Add(new(device));
                }
            }

            if (deviceHeapInfo.Count > 1)
            {
                deviceHeapInfo.Sort();
                _physicalDevice = deviceHeapInfo[0].Device;
            }

            if (_physicalDevice == VkPhysicalDevice.Null)
            {
                throw new Exception("Failed to find a sutiable GPU!");
            }

            Vulkan.vkGetPhysicalDeviceProperties(_physicalDevice, out var properties);

            Properties = properties;

            var swapChainSupport = QuerySwapChainSupport(_physicalDevice);
            if (swapChainSupport.capabilities.maxImageCount > 0)
            {
                Debug.Assert(swapChainSupport.capabilities.maxImageCount >= swapChainSupport.capabilities.minImageCount, string.Format("Max Swapchain image count ({0}) is less than min image count ({1}). Cannot compute valid swapchain image count.", swapChainSupport.capabilities.maxImageCount, swapChainSupport.capabilities.minImageCount));

                SwapChain.SWAP_CHAIN_IMAGE_COUNT = Math.Max(3, (int)swapChainSupport.capabilities.minImageCount);
            }

            SwapChain.SWAP_CHAIN_IMAGE_COUNT = Math.Max(3, (int)swapChainSupport.capabilities.minImageCount);
            SwapChainSupport = swapChainSupport;
            var str = new VkUtf8String(properties.deviceName);
            Console.WriteLine(string.Format("Physical device: {0}", str));
            Console.WriteLine("Selected swapchain frame count: {0}", SwapChain.SWAP_CHAIN_IMAGE_COUNT);
        }


        #region Create Logical Device
        /// <summary>
        /// creates a logical vulkan device from the selected physical device <see cref="_physicalDevice"/>
        /// </summary>
        /// <exception cref="Exception"></exception>
        internal static unsafe void CreateLogicalDevice()
        {
            var indices = FindQueueFamilies(_physicalDevice);

            Dictionary<uint, int> uniqueQueueFamilies = new(3)
            {
                { indices.graphicsFamily, 1 }
            };

            if (!uniqueQueueFamilies.TryAdd(indices.computeFamily, 1))
            {
                indices.presentIndex = uniqueQueueFamilies[indices.computeFamily];
                uniqueQueueFamilies[indices.computeFamily]++;
            }

            if (!uniqueQueueFamilies.TryAdd(indices.presentFamily, 1))
            {
                indices.presentIndex = uniqueQueueFamilies[indices.presentFamily];
                uniqueQueueFamilies[indices.presentFamily]++;
            }

            PhysicalQueueFamilies = indices;

            VkDeviceQueueCreateInfo* pQueueCreateInfos = stackalloc VkDeviceQueueCreateInfo[uniqueQueueFamilies.Count];

            float* queuePriority = stackalloc float[3]
            {
                1f,
                1f,
                1f
            };
            int index = 0;
            foreach (var keyValuePair in uniqueQueueFamilies)
            {
                pQueueCreateInfos[index] = new VkDeviceQueueCreateInfo()
                {
                    queueFamilyIndex = (uint)keyValuePair.Key,
                    queueCount = (uint)keyValuePair.Value,
                    pQueuePriorities = queuePriority
                };
                index++;
            }

            VkPhysicalDeviceFeatures deviceFeature = new()
            {
                samplerAnisotropy = true,
                fillModeNonSolid = true,
                multiDrawIndirect = true,
                drawIndirectFirstInstance = true,  
            };

            VkPhysicalDeviceMeshShaderFeaturesEXT meshShaderFeatures = new()
            {
                taskShader = true,
                meshShader = true
            };

            VkPhysicalDeviceVulkan12Features deviceFeatures12 = new()
            {
                imagelessFramebuffer = true,
                samplerFilterMinmax = true,
                timelineSemaphore = true,
                pNext = &meshShaderFeatures
            };
            VkPhysicalDeviceVulkan13Features deviceFeatures13 = new()
            {
                maintenance4 = true,
                dynamicRendering = true,
                synchronization2 = true,
                pNext = &deviceFeatures12
            };

            VkPhysicalDeviceFeatures2 deviceFeatures2 = new()
            {
                features = deviceFeature,
                pNext = &deviceFeatures13
            };

            using VkStringArray deviceExtensionNames = new(_requiredDeviceExtensions);



            VkDeviceCreateInfo createInfo = new()
            {
                queueCreateInfoCount = (uint)uniqueQueueFamilies.Count,
                pQueueCreateInfos = pQueueCreateInfos,
                pEnabledFeatures = null,
                enabledExtensionCount = (uint)_requiredDeviceExtensions.Length,
                ppEnabledExtensionNames = deviceExtensionNames,
                pNext = &deviceFeatures2,
            };

#if DEBUG

            using VkStringArray enabledValidationlayers = new(_requiredValidationLayers);
            createInfo.enabledLayerCount = (uint)_requiredValidationLayers.Length;
            createInfo.ppEnabledLayerNames = enabledValidationlayers;

#else
            
            createInfo.enabledLayerCount = 0;
            
#endif
            Vulkan.CheckResult(Vulkan.vkCreateDevice(_physicalDevice, in createInfo, null, out _device), "Failed to create logical device");
            
            Vulkan.vkLoadDevice(_device);

            Vulkan.vkGetDeviceQueue(_device, (uint)indices.graphicsFamily, 0, out _mainQueue);
            Vulkan.vkGetDeviceQueue(_device, (uint)indices.computeFamily, (uint)indices.computeIndex, out _computeQueue);
            Vulkan.vkGetDeviceQueue(_device, (uint)indices.presentFamily, (uint)indices.presentIndex, out _presentQueue);
        }

        #endregion

#region Create Command Pool
        /// <summary>
        /// Creates the command buffer pool for submitting commands to the logical device
        /// </summary>
        /// <exception cref="Exception"></exception>
        internal static unsafe void CreateCommandPools()
        {
            QueueFamilyIndices queueFamilyIndices = PhysicalQueueFamilies;

            VkCommandPoolCreateInfo poolInfo = new()
            {
                queueFamilyIndex = queueFamilyIndices.graphicsFamily,
                flags = VkCommandPoolCreateFlags.Transient | VkCommandPoolCreateFlags.ResetCommandBuffer,
            };
            
            _secondaryMainPipeCommandBuffers = new VkCommandPool[Environment.ProcessorCount * 2];
            _secondaryComputePipeCommandBuffers = new VkCommandPool[Environment.ProcessorCount];
            Vulkan.CheckResult(Vulkan.vkCreateCommandPool(_device, poolInfo, null, out _commandPoolMain), "Failed to create main command pool!");

            for (int i = 0; i < _secondaryMainPipeCommandBuffers.Length; i++)
            {
                Vulkan.CheckResult(Vulkan.vkCreateCommandPool(_device, poolInfo, null, out _secondaryMainPipeCommandBuffers[i]), "Failed to create secondary main command pool!");
            }
            

            poolInfo.queueFamilyIndex = queueFamilyIndices.computeFamily;
            Vulkan.CheckResult(Vulkan.vkCreateCommandPool(_device, poolInfo, null, out _commandPoolCompute), "Failed to create compute command pool!");

            for (int i = 0; i < _secondaryComputePipeCommandBuffers.Length; i++)
            {
                Vulkan.CheckResult(Vulkan.vkCreateCommandPool(_device, poolInfo, null, out _secondaryComputePipeCommandBuffers[i]), "Failed to create secondary main compute pool!");
            }

            poolInfo.queueFamilyIndex = queueFamilyIndices.presentFamily;

            Vulkan.CheckResult(Vulkan.vkCreateCommandPool(_device, poolInfo, null, out _commandPoolPresent),"Failed to create present command pool!");
        }

        #endregion

        #region Create VmaAllocator
        /// <summary>
        /// Create a Vma allocator for the allocation of VkBuffers and VKImages constructed during the application lifetime.
        /// </summary>
        internal static void CreateVmaAllocator()
        {
            VmaAllocatorCreateInfo allocatorCreateInfo = new()
            {
                flags = VmaAllocatorCreateFlags.KHRDedicatedAllocation | VmaAllocatorCreateFlags.KHRBindMemory2,
                instance = _instance,
                vulkanApiVersion = VkVersion.Version_1_3,
                physicalDevice = _physicalDevice,
                device = Device,
            };
            Vma.vmaCreateAllocator(in allocatorCreateInfo, out _allocator);
        }
        #endregion

        /// <summary>
        /// Determines if a given physical device is suitable for the app
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        internal static bool IsDeviceSuitable(VkPhysicalDevice device)
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
        internal static QueueFamilyIndices FindQueueFamilies(VkPhysicalDevice device)
        {
            QueueFamilyIndices indices = default;
            var queueFamilies = Vulkan.vkGetPhysicalDeviceQueueFamilyProperties(device);

            for (uint i = 0; i < queueFamilies.Length; i++)
            {
                var family = queueFamilies[(int)i];

                Vulkan.vkGetPhysicalDeviceSurfaceSupportKHR(device, i, Surface, out VkBool32 presentSupport);

                if (family.queueCount > 0 && family.queueFlags.HasFlag(VkQueueFlags.Graphics) && family.queueFlags.HasFlag(VkQueueFlags.Compute))
                {
                    indices.graphicsFamily = i;
                    indices.graphicsFamilyHasValue = true;
                }

                else if (family.queueCount > 1 && family.queueFlags.HasFlag(VkQueueFlags.Compute) && !family.queueFlags.HasFlag(VkQueueFlags.Graphics))
                {
                    indices.computeFamily = i;
                    indices.computeFamilyHasValue = true;
                    if (presentSupport)
                    {
                        indices.presentFamily = i;
                        indices.presentFamilyHasValue = true;
                    }
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
        internal static SwapChainSupportDetails QuerySwapChainSupport(VkPhysicalDevice device)
        {
            SwapChainSupportDetails details = default;
            Vulkan.vkGetPhysicalDeviceSurfaceCapabilitiesKHR(device, Surface, out details.capabilities);

            var formats = Vulkan.vkGetPhysicalDeviceSurfaceFormatsKHR(device, Surface);
            details.formats = new VkSurfaceFormatKHR[formats.Length];
            formats.CopyTo(details.formats);

            var presentModes = Vulkan.vkGetPhysicalDeviceSurfacePresentModesKHR(device, Surface);
            details.presentModes = new VkPresentModeKHR[presentModes.Length];
            presentModes.CopyTo(details.presentModes);


            return details;
        }

        #endregion

#if DEBUG
        #region DebugMessenger
        /// <summary>
        /// Validation messenger setup
        /// </summary>
        /// <exception cref="Exception"></exception>
        internal static unsafe void SetUpDebugMessenger()
        {
            VkDebugUtilsMessengerCreateInfoEXT createInfoEXT = PopulateDebugMessengerCreateInfo();

            fixed (VkDebugUtilsMessengerEXT* toPtr = &_debugMessenger)
                Vulkan.CheckResult(CreateDebugUtilsMessengerEXT(_instance, &createInfoEXT, null, toPtr), "failed to set up debug messenger! {0}");

        }

        #endregion


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
            StackTrace trace = new(true);

            Console.WriteLine(string.Format("Validation layer trace\n {0}", trace.ToString()));
            if (BreakOnValidationError)
            {
                Debugger.Break();
            }
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
        internal unsafe static void DestroyDebugUtilsMessengerEXT(
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
#endif
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

            HashSet<VkUtf8String> requiredSet = [.. _requiredDeviceExtensions];

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

    }
}