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
            Vulkan.VK_KHR_TIMELINE_SEMAPHORE_EXTENSION_NAME,
            Vulkan.VK_KHR_SYNCHRONIZATION_2_EXTENSION_NAME,
            Vulkan.VK_EXT_MEMORY_PRIORITY_EXTENSION_NAME,
            Vulkan.VK_EXT_PAGEABLE_DEVICE_LOCAL_MEMORY_EXTENSION_NAME,
            Vulkan.VK_EXT_CONSERVATIVE_RASTERIZATION_EXTENSION_NAME,
            Vulkan.VK_EXT_NESTED_COMMAND_BUFFER_EXTENSION_NAME,
            Vulkan.VK_EXT_DESCRIPTOR_BUFFER_EXTENSION_NAME
        ];

        private const bool ForceMeshShadingOff = false;
        private readonly static VkUtf8String[] _meshShaderExtensions = [
            Vulkan.VK_EXT_MESH_SHADER_EXTENSION_NAME,
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

            Vulkan.vkCreateInstance(&createInfo, null, out _instance).CheckResult("Failed to create vulkan instance!");

            _instanceApi = Vulkan.GetApi(_instance);
            //Vulkan.vkLoadInstanceOnly(_instance);

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
            _instanceApi.vkEnumeratePhysicalDevices(_instance, out uint deviceCount).CheckResult("Failed to find GPUs with Vulkan Support!");
            VkPhysicalDevice[] devices = new VkPhysicalDevice[deviceCount];
            _instanceApi.vkEnumeratePhysicalDevices(_instance, devices).CheckResult("Failed to find GPUs with Vulkan Support!");

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
            
            List<DeviceInfo> meshShaderSupported = [.. deviceHeapInfo];
            for (int i = meshShaderSupported.Count - 1; i >= 0; i--)
            {
                if (!CheckDeviceExtensionSupport(meshShaderSupported[i].Device, _meshShaderExtensions))
                {
                    meshShaderSupported.RemoveAt(i);
                }
            }

            if (meshShaderSupported.Count == 0 || ForceMeshShadingOff)
            {
                MeshShading = false;
            }
            else
            {
                deviceHeapInfo = meshShaderSupported;
                MeshShading = true;
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

            
            if (MeshShading)
            {
                CheckRequiredMeshShadingFeaturesSupported(_physicalDevice);
            }

            GetDeviceProperties(_physicalDevice);


            var swapChainSupport = QuerySwapChainSupport(_physicalDevice);
            if (swapChainSupport.capabilities.maxImageCount > 0)
            {
                Debug.Assert(swapChainSupport.capabilities.maxImageCount >= swapChainSupport.capabilities.minImageCount, string.Format("Max Swapchain image count ({0}) is less than min image count ({1}). Cannot compute valid swapchain image count.", swapChainSupport.capabilities.maxImageCount, swapChainSupport.capabilities.minImageCount));

                SwapChain.SWAP_CHAIN_IMAGE_COUNT = Math.Max(3, (int)swapChainSupport.capabilities.minImageCount);
            }

            SwapChain.SWAP_CHAIN_IMAGE_COUNT = Math.Max(3, (int)swapChainSupport.capabilities.minImageCount);
            SwapChainSupport = swapChainSupport;
            var propertiesVK10 = PropertiesVK10;
            var str = new VkUtf8String(propertiesVK10.deviceName);
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

            VkPhysicalDevicePageableDeviceLocalMemoryFeaturesEXT pageableDeviceLocalMemoryFeaturesEXT = new() { pageableDeviceLocalMemory = true };


            VkPhysicalDeviceNestedCommandBufferFeaturesEXT nestedCommandBufferFeatures = new()
            {
                nestedCommandBuffer = true,
                nestedCommandBufferRendering = true,
                pNext = &pageableDeviceLocalMemoryFeaturesEXT
            };

            VkPhysicalDeviceDescriptorBufferFeaturesEXT descriptorBuffers = new()
            {
                descriptorBuffer = true,
                pNext = &nestedCommandBufferFeatures
            };


            VkPhysicalDeviceMeshShaderFeaturesEXT meshShaderFeatures = new()
            {
                taskShader = true,
                meshShader = true
            };

            if (MeshShading)
            {
                nestedCommandBufferFeatures.pNext = &meshShaderFeatures;
            }

            VkPhysicalDeviceVulkan11Features deviceFeatures11 = new()
            {
                shaderDrawParameters = true,
                pNext = &descriptorBuffers
            };


            VkPhysicalDeviceVulkan12Features deviceFeatures12 = new()
            {
                bufferDeviceAddress = true,
                imagelessFramebuffer = true,
                samplerFilterMinmax = true,
                timelineSemaphore = true,
                storageBuffer8BitAccess = true,
                pNext = &deviceFeatures11
            };



            VkPhysicalDeviceVulkan13Features deviceFeatures13 = new()
            {
                maintenance4 = true,
                dynamicRendering = true,
                synchronization2 = true,
                shaderDemoteToHelperInvocation = true,
                pNext = &deviceFeatures12
            };

            VkPhysicalDeviceVulkan14Features deviceFeatures14 = new()
            {
                hostImageCopy = false,
                pNext = & deviceFeatures13
            };

            VkPhysicalDeviceFeatures deviceFeature = new()
            {
                samplerAnisotropy = true,
                fillModeNonSolid = true,
                multiDrawIndirect = true,
                drawIndirectFirstInstance = true,
                dualSrcBlend = true,
                fragmentStoresAndAtomics = true,
                geometryShader = true,
                imageCubeArray = true,
            };

            VkPhysicalDeviceFeatures2 deviceFeatures2 = new()
            {
                features = deviceFeature,
                pNext = &deviceFeatures14
            };

            VkUtf8String[] loadExtensions;
            if (MeshShading)
            {
                loadExtensions = [.. _requiredDeviceExtensions, .. _meshShaderExtensions];
            }
            else
            {
                loadExtensions = _requiredDeviceExtensions;
            }

            using VkStringArray deviceExtensionNames = new(loadExtensions);


            VkDeviceCreateInfo createInfo = new()
            {
                queueCreateInfoCount = (uint)uniqueQueueFamilies.Count,
                pQueueCreateInfos = pQueueCreateInfos,
                pEnabledFeatures = null,
                enabledExtensionCount = (uint)loadExtensions.Length,
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
            _instanceApi.vkCreateDevice(_physicalDevice, in createInfo, null, out _device).CheckResult("Failed to create logical device");

            _deviceApi = Vulkan.GetApi(_instance, _device);

            _deviceApi.vkGetDeviceQueue(_device, (uint)indices.graphicsFamily, 0, out _mainQueue);
            _deviceApi.vkGetDeviceQueue(_device, (uint)indices.computeFamily, (uint)indices.computeIndex, out _computeQueue);
            _deviceApi.vkGetDeviceQueue(_device, (uint)indices.presentFamily, (uint)indices.presentIndex, out _presentQueue);
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
            _deviceApi.vkCreateCommandPool(_device, poolInfo, null, out _commandPoolMain).CheckResult("Failed to create main command pool!");

            for (int i = 0; i < _secondaryMainPipeCommandBuffers.Length; i++)
            {
                _deviceApi.vkCreateCommandPool(_device, poolInfo, null, out _secondaryMainPipeCommandBuffers[i]).CheckResult("Failed to create secondary main command pool!");
            }


            poolInfo.queueFamilyIndex = queueFamilyIndices.computeFamily;
            _deviceApi.vkCreateCommandPool(_device, poolInfo, null, out _commandPoolCompute).CheckResult("Failed to create compute command pool!");

            for (int i = 0; i < _secondaryComputePipeCommandBuffers.Length; i++)
            {
                _deviceApi.vkCreateCommandPool(_device, poolInfo, null, out _secondaryComputePipeCommandBuffers[i]).CheckResult("Failed to create secondary main compute pool!");
            }

            poolInfo.queueFamilyIndex = queueFamilyIndices.presentFamily;

            _deviceApi.vkCreateCommandPool(_device, poolInfo, null, out _commandPoolPresent).CheckResult("Failed to create present command pool!");
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
                flags = VmaAllocatorCreateFlags.KHRDedicatedAllocation | VmaAllocatorCreateFlags.KHRBindMemory2 | VmaAllocatorCreateFlags.BufferDeviceAddress | VmaAllocatorCreateFlags.EXTMemoryPriority,
                instance = _instance,
                vulkanApiVersion = VkVersion.Version_1_4,
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

            bool requiredExtensionsSupported = CheckDeviceExtensionSupport(device, _requiredDeviceExtensions);

            bool swapChainAdequate = false;

            if (requiredExtensionsSupported)
            {
                SwapChainSupportDetails swapChainSupport = QuerySwapChainSupport(device);
                swapChainAdequate = swapChainSupport.formats.Length > 0 && swapChainSupport.presentModes.Length > 0;
            }

            _instanceApi.vkGetPhysicalDeviceFeatures(device, out VkPhysicalDeviceFeatures supportedFeatures);

            if (!indices.IsComplete)
            {
                Console.WriteLine("Device did not have required queues");
            }

            if (!requiredExtensionsSupported)
            {
                Console.WriteLine("Device did not have required extensions");
            }

            if (!swapChainAdequate)
            {
                Console.WriteLine("Device did not have swapchain adequate");
            }

            if (!supportedFeatures.samplerAnisotropy)
            {
                Console.WriteLine("Device did not have sampler anisotropy");
            }


            return indices.IsComplete && requiredExtensionsSupported && swapChainAdequate && supportedFeatures.samplerAnisotropy;
        }

        /// <summary>
        /// Gets the queue families for the physical device
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        internal static QueueFamilyIndices FindQueueFamilies(VkPhysicalDevice device)
        {
            QueueFamilyIndices indices = default;
            _instanceApi.vkGetPhysicalDeviceQueueFamilyProperties(device,out uint queuefamilyCount);
            VkQueueFamilyProperties[] queueFamilies  = new VkQueueFamilyProperties[queuefamilyCount];
            _instanceApi.vkGetPhysicalDeviceQueueFamilyProperties(device, queueFamilies);

            for (uint i = 0; i < queueFamilies.Length; i++)
            {
                var family = queueFamilies[(int)i];

                _instanceApi.vkGetPhysicalDeviceSurfaceSupportKHR(device, i, Surface, out VkBool32 presentSupport);

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
            _instanceApi.vkGetPhysicalDeviceSurfaceCapabilitiesKHR(device, Surface, out details.capabilities).CheckResult("Device has no surface capabilities!");
            _instanceApi.vkGetPhysicalDeviceSurfaceFormatsKHR(device, Surface, out uint surfaceFormatCount).CheckResult("Device has no surface formats!");
            details.formats = new VkSurfaceFormatKHR[surfaceFormatCount];
            _instanceApi.vkGetPhysicalDeviceSurfaceFormatsKHR(device, Surface, details.formats);

            _instanceApi.vkGetPhysicalDeviceSurfacePresentModesKHR(device, Surface,out uint presentModeCount).CheckResult("Device has not present modes!");
            details.presentModes = new VkPresentModeKHR[presentModeCount];
            _instanceApi.vkGetPhysicalDeviceSurfacePresentModesKHR(device, Surface,details.presentModes);


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
                CreateDebugUtilsMessengerEXT(_instance, &createInfoEXT, null, toPtr).CheckResult( "failed to set up debug messenger! {0}");

        }

        #endregion


        #region Validation and Debugging statics
        /// <summary>
        /// Checks if our hardware can support validation layers requrested in <see cref="_requiredValidationLayers"/>
        /// </summary>
        /// <returns></returns>
        private static unsafe bool CheckValidationLayerSupport()
        {
            Vulkan.vkEnumerateInstanceLayerProperties(out uint propertyCount).CheckResult();
            VkLayerProperties[] availableLayers = new VkLayerProperties[propertyCount];
            Vulkan.vkEnumerateInstanceLayerProperties(availableLayers);

            for (int i = 0; i < _requiredValidationLayers.Length; i++)
            {
                bool supportsLayer = false;
                for (int j = 0; j < availableLayers.Length; j++)
                {
                    var layer = availableLayers[j];
                    var name = new VkUtf8String(layer.layerName, 86).ToString();
                    if (name.Contains( _requiredValidationLayers[i]) )
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

        private unsafe static bool CheckDeviceExtensionSupport(VkPhysicalDevice device, VkUtf8String[] extensions)
        {
            _instanceApi.vkEnumerateDeviceExtensionProperties(device,out uint extensionCount).CheckResult("Failed to get device extensions!");
            var availableExtensions = new VkExtensionProperties[extensionCount];
            _instanceApi.vkEnumerateDeviceExtensionProperties(device, availableExtensions);
            HashSet<VkUtf8String> requiredSet = [.. extensions];


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

            if (requiredSet.Count > 0)
            {
                foreach (var ext in requiredSet)
                {
                    Console.WriteLine("Device Missing {0}", ext);
                }
            }

            return requiredSet.Count == 0;
        }

        private unsafe static void CheckRequiredMeshShadingFeaturesSupported(VkPhysicalDevice device)
        {

            VkPhysicalDeviceMeshShaderFeaturesEXT meshShaderFeatures = new();

            VkPhysicalDeviceVulkan12Features deviceFeatures12 = new()
            {
                pNext = &meshShaderFeatures
            };

            VkPhysicalDeviceVulkan13Features deviceFeatures13 = new()
            {
                pNext = &deviceFeatures12
            };

            VkPhysicalDeviceVulkan14Features deviceFeatures14 = new()
            {
                pNext = &deviceFeatures13
            };

            VkPhysicalDeviceFeatures2 deviceFeatures2 = new()
            {
                pNext = &deviceFeatures14
            };

            _instanceApi.vkGetPhysicalDeviceFeatures2(device, &deviceFeatures2);

            if (MeshShading)
            {
                if (!meshShaderFeatures.taskShader)
                {
                    throw new InvalidOperationException("Device flagged as supporting mesh shading but task shader feature is unavaliable!");
                }

                if (!meshShaderFeatures.meshShader)
                {
                    throw new InvalidOperationException("Device flagged as supporting mesh shading but mesh shader feature is unavaliable!");
                }
            }
        }

        private unsafe static void GetDeviceProperties(VkPhysicalDevice device)
        {
            VkPhysicalDeviceDescriptorBufferPropertiesEXT descriptorBufferProperties = new();

            VkPhysicalDeviceVulkan11Properties deviceProperties11 = new()
            {
                pNext = &descriptorBufferProperties
            };

            VkPhysicalDeviceMeshShaderPropertiesEXT meshShaderProperties = new()
            {
                pNext = &deviceProperties11
            };

            VkPhysicalDeviceVulkan12Properties deviceProperties12 = new()
            {
                pNext = &meshShaderProperties
            };

            VkPhysicalDeviceVulkan13Properties deviceProperties13 = new()
            {
                pNext = &deviceProperties12
            };

            VkPhysicalDeviceVulkan14Properties deviceProperties14 = new()
            {
                pNext = &deviceProperties13
            };

            VkPhysicalDeviceProperties2 deviceProperties2 = new()
            {
                pNext = &deviceProperties14
            };

            _instanceApi.vkGetPhysicalDeviceProperties2(device, &deviceProperties2);

            PropertiesVK10 = deviceProperties2.properties;
            PropertiesVK11 = deviceProperties11;
            PropertiesVK12 = deviceProperties12;
            PropertiesVK13 = deviceProperties13;
            PropertiesVK14 = deviceProperties14;
            PropertiesMeshShading = meshShaderProperties;
            PropertiesDescriptorBuffer = descriptorBufferProperties;

        }

        #endregion

    }
}