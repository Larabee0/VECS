using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS
{

    public unsafe sealed class GraphicsDevice : IDisposable
    {
#if DEBUG
        private const bool ENABLE_VALIDATION_LAYERS = true;
        private readonly static string[] _validationLayers = ["VK_LAYER_KHRONOS_validation"];
        private readonly static VkUtf8String[] deviceExtensions = [Vulkan.VK_KHR_SWAPCHAIN_EXTENSION_NAME];
#else
        const bool ENABLE_VALIDATION_LAYERS = false;
#endif
    private Window _window;

        private VkInstance _instance;
        private VkDebugUtilsMessengerEXT _debugMessenger;
        private VkSurfaceKHR _surface;
        private VkPhysicalDevice _physicalDevice;
        private VkDevice _device;
        private VkQueue _graphicsQueue;
        private VkQueue _presentQueue;
        private VkCommandPool _commandPool;

        public VkPhysicalDeviceProperties Properties;

        public GraphicsDevice(Window window)
        {
            _window = window;

            CreateInstance();
            SetUpDebugMessenger();
            CreateSurface();
            PickPhysicalDevice();
            CreateLogicalDevice();
            CreateCommandPool();
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
        private void CreateInstance()
        {
            if (ENABLE_VALIDATION_LAYERS && !CheckValidationLayerSupport())
            {
                throw new Exception("Validation layers requested, but not avaliable!");
            }

            VkApplicationInfo appInfo = GenerateAppInfo();

            using VkStringArray vkInstanceExtensions = new(GetRequiredExtensions());

            VkInstanceCreateInfo createInfo = new()
            {
                pApplicationInfo = &appInfo,
                enabledExtensionCount = vkInstanceExtensions.Length,
                ppEnabledExtensionNames = vkInstanceExtensions
            };


            if (ENABLE_VALIDATION_LAYERS)
            {
                using VkStringArray validationlayers = new(_validationLayers);
                createInfo.enabledLayerCount = (uint)_validationLayers.Length;
                createInfo.ppEnabledLayerNames = validationlayers;
                VkDebugUtilsMessengerCreateInfoEXT debugCreateInfo = PopulateDebugMessengerCreateInfo();
                createInfo.pNext = &debugCreateInfo;
            }
            else
            {
                createInfo.enabledLayerCount = 0;
                createInfo.pNext = null;
            }


            if (Vulkan.vkCreateInstance(in createInfo, null, out VkInstance instance) != VkResult.Success)
            {
                throw new Exception("Failed to create vulkan instance!");
            }

            _instance = instance;
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
        /// Gets the required extensions needed by SDL3, move to window file?
        /// 
        /// Also appends the debug utils extension if validation layers are enabled.
        /// </summary>
        /// <returns>List of Device extensions to configure the vulkan instance with</returns>
        private static List<VkUtf8String> GetRequiredExtensions()
        {
            string[] sdlRequiredExtensions = SDL3.SDL3.SDL_Vulkan_GetInstanceExtensions();

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

        /// <summary>
        /// Checks if our hardware can support validation layers requrested in <see cref="_validationLayers"/>
        /// </summary>
        /// <returns></returns>
        private static bool CheckValidationLayerSupport()
        {
            ReadOnlySpan<VkLayerProperties> availableLayers = Vulkan.vkEnumerateInstanceLayerProperties();

            for (int i = 0; i < _validationLayers.Length; i++)
            {
                bool supportsLayer = false;
                for (int j = 0; j < availableLayers.Length; j++)
                {
                    if (_validationLayers[i] == _validationLayers[j])
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
        private static VkDebugUtilsMessengerCreateInfoEXT PopulateDebugMessengerCreateInfo() => new()
        {
            messageSeverity = VkDebugUtilsMessageSeverityFlagsEXT.Warning | VkDebugUtilsMessageSeverityFlagsEXT.Error,
            messageType = VkDebugUtilsMessageTypeFlagsEXT.General | VkDebugUtilsMessageTypeFlagsEXT.Validation | VkDebugUtilsMessageTypeFlagsEXT.Performance,
            pfnUserCallback = &ValidationDebugCallback,
            pUserData = null,
        };

        // VkDebugUtilsMessageSeverityFlagsEXT, VkDebugUtilsMessageTypeFlagsEXT, VkDebugUtilsMessengerCallbackDataEXT*, void*, uint

        /// <summary>
        /// Validation layer callback for logging validation servirty and message to the console.
        /// </summary>
        /// <param name="messageSeverity"></param>
        /// <param name="messageType"></param>
        /// <param name="pCallbackData"></param>
        /// <param name="pUserData"></param>
        /// <returns></returns>
        [UnmanagedCallersOnly]
        private unsafe static uint ValidationDebugCallback(VkDebugUtilsMessageSeverityFlagsEXT messageSeverity, VkDebugUtilsMessageTypeFlagsEXT messageType, VkDebugUtilsMessengerCallbackDataEXT* pCallbackData,void* pUserData)
        {
            var message = new VkUtf8String(pCallbackData->pMessage);

            Console.WriteLine(string.Format("[{0}] Vulkan: Validation Layer: {1}", messageSeverity, Encoding.UTF8.GetString(message.Span)));

            return 0;
        }

        #endregion

        #region DebugMessenger
        private void SetUpDebugMessenger()
        {
            if (!ENABLE_VALIDATION_LAYERS) return;
            VkDebugUtilsMessengerCreateInfoEXT createInfoEXT = PopulateDebugMessengerCreateInfo();

            fixed (VkDebugUtilsMessengerEXT* toPtr = &_debugMessenger)
            if (CreateDebugUtilsMessengerEXT(_instance, &createInfoEXT, null, toPtr) != VkResult.Success)
            {
                throw new Exception("failed to set up debug messenger!");
            }
            
        }

        private static VkResult CreateDebugUtilsMessengerEXT(VkInstance instance, VkDebugUtilsMessengerCreateInfoEXT* pCreateInfo, VkAllocationCallbacks* pAllocator, VkDebugUtilsMessengerEXT* pDebugMessenger)
        {
            var func = (delegate* unmanaged<VkInstance, VkDebugUtilsMessengerCreateInfoEXT*, VkAllocationCallbacks*, VkDebugUtilsMessengerEXT*, VkResult>)Vulkan.vkGetInstanceProcAddr(instance, "vkCreateDebugUtilsMessengerEXT");

            if (func != null)
            {
                
                return func(instance, pCreateInfo, pAllocator, pDebugMessenger);
            }
            else
            {
                return VkResult.ErrorExtensionNotPresent;
            }
        }

        #endregion


        private void CreateSurface()
        {
            _window.CreateWindowSurface(_instance);
        }

        #region Pick Physical Device

        private void PickPhysicalDevice()
        {

            var devices = Vulkan.vkEnumeratePhysicalDevices(_instance);

            if(devices.Length == 0)
            {
                throw new Exception("Failed to find GPUs with Vulkan support!");
            }

            Console.WriteLine(string.Format("Device count: {0}",devices.Length));

            for (int i = 0; i < devices.Length; i++)
            {
                var device = devices[i];
                if (IsDeviceSuitable(device))
                {
                    _physicalDevice = device;
                    break;
                }
            }

            if(_physicalDevice == VkPhysicalDevice.Null)
            {
                throw new Exception("Failed to find a sutiable GPU!");
            }

            Vulkan.vkGetPhysicalDeviceProperties(_physicalDevice, out Properties);
            fixed(byte* devName = Properties.deviceName)
            {
                var str = new VkUtf8String(devName);
                Console.WriteLine(string.Format("Physical device: {0}", str));
            }
            

        }

        private bool IsDeviceSuitable(VkPhysicalDevice device)
        {
            QueueFamilyIndices indices = FindQueueFamilies(device);

            bool extensionsSupported = CheckDeviceExtensionSupport(device);

            bool swapChainAdequate = false;
            if (extensionsSupported)
            {
                SwapChainSupportDetails  swapChainSupport = QuerySwapChainSupport(device);
                swapChainAdequate = swapChainSupport.formats.Length > 0 && swapChainSupport.presentMode.Length > 0;
            }

            Vulkan.vkGetPhysicalDeviceFeatures(device, out VkPhysicalDeviceFeatures supportedFeatures);

            return indices.IsComplete && extensionsSupported && swapChainAdequate && supportedFeatures.samplerAnisotropy;
        }

        private QueueFamilyIndices FindQueueFamilies(VkPhysicalDevice device)
        {
            QueueFamilyIndices indices = default;
            var queueFamilies = Vulkan.vkGetPhysicalDeviceQueueFamilyProperties(device);

            for (int i = 0; i < queueFamilies.Length; i++)
            {
                var family = queueFamilies[i];
                if(family.queueCount > 0 && family.queueFlags .HasFlag(VkQueueFlags.Graphics))
                {
                    indices.graphicsFamily = i;
                    indices.graphicsFamilyHasValue = true;
                }
                Vulkan.vkGetPhysicalDeviceSurfaceSupportKHR(device, (uint)i, _surface, out VkBool32 presentSupport);

                if(family.queueCount > 0 && presentSupport)
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

        private struct QueueFamilyIndices
        {
            public int graphicsFamily;
            public int presentFamily;
            public bool graphicsFamilyHasValue;
            public bool presentFamilyHasValue;
            public bool IsComplete => graphicsFamilyHasValue && presentFamilyHasValue;
        }

        private SwapChainSupportDetails QuerySwapChainSupport(VkPhysicalDevice device)
        {
            SwapChainSupportDetails details = default;
            Vulkan.vkGetPhysicalDeviceSurfaceCapabilitiesKHR(device, _surface, out details.capabilities);

            var formats = Vulkan.vkGetPhysicalDeviceSurfaceFormatsKHR(device, _surface);
            details.formats = new VkSurfaceFormatKHR[formats.Length];
            formats.CopyTo(details.formats);

            var presentModes = Vulkan.vkGetPhysicalDeviceSurfacePresentModesKHR(device, _surface);
            details.presentMode = new VkPresentModeKHR[presentModes.Length];
            presentModes.CopyTo(details.presentMode);


            return details;
        }

        private struct SwapChainSupportDetails
        {
            public VkSurfaceCapabilitiesKHR capabilities;
            public VkSurfaceFormatKHR[] formats;
            public VkPresentModeKHR[] presentMode;
        }

        private static bool CheckDeviceExtensionSupport(VkPhysicalDevice device)
        {
            var availableExtensions = Vulkan.vkEnumerateDeviceExtensionProperties(device);

            HashSet<VkUtf8String> avaliableHashSet = new(availableExtensions.Length);

            for (int i = 0; i < availableExtensions.Length; i++)
            {
                var ext = availableExtensions[i];
                avaliableHashSet.Add(new VkUtf8String(ext.extensionName));
            }


            return avaliableHashSet.IsSupersetOf(deviceExtensions);
        }

        #endregion

        #region Create Logical Device
        private void CreateLogicalDevice()
        {
            QueueFamilyIndices indices = FindQueueFamilies(_physicalDevice);

            HashSet<int> uniqueQueueFamilies = [indices.graphicsFamily, indices.presentFamily];
            List<VkDeviceQueueCreateInfo> queueCreateInfos = [];

            float queuePriority = 1f;

            foreach (var queueFamily in uniqueQueueFamilies)
            {
                VkDeviceQueueCreateInfo queueCreateInfo = new()
                {
                    queueFamilyIndex = (uint)queueFamily,
                    queueCount = 1,
                    pQueuePriorities = &queuePriority
                };
                queueCreateInfos.Add(queueCreateInfo);
            }

            VkPhysicalDeviceFeatures deviceFeature = new()
            {
                samplerAnisotropy = true,
            };

            fixed (VkDeviceQueueCreateInfo* createInfos = &queueCreateInfos.ToArray()[0])
            {
                using VkStringArray deviceExtensionNames = new VkStringArray(deviceExtensions);
                VkDeviceCreateInfo createInfo = new()
                {
                    queueCreateInfoCount = (uint)queueCreateInfos.Count,
                    pQueueCreateInfos = createInfos,
                    pEnabledFeatures = &deviceFeature,
                    enabledExtensionCount = (uint)deviceExtensions.Length,
                    ppEnabledExtensionNames = deviceExtensionNames
                };

                if (ENABLE_VALIDATION_LAYERS)
                {   
                    using VkStringArray enabledValidationlayers = new(_validationLayers);
                    createInfo.enabledLayerCount = (uint)_validationLayers.Length;
                    createInfo.ppEnabledLayerNames = enabledValidationlayers;
                }
                else
                {
                    createInfo.enabledLayerCount = 0;
                }

                if(Vulkan.vkCreateDevice(_physicalDevice,in createInfo,null,out _device) != VkResult.Success)
                {
                    throw new Exception("Failed to create logical device");
                }
            }

            Vulkan.vkGetDeviceQueue(_device, (uint)indices.graphicsFamily, 0, out _graphicsQueue);
            Vulkan.vkGetDeviceQueue(_device, (uint)indices.presentFamily,0, out _presentQueue);
        }

        #endregion

        #region Create Command Pool
        private void CreateCommandPool()
        {
            QueueFamilyIndices queueFamilyIndices = FindPhysicalQueueFamilies();

            VkCommandPoolCreateInfo poolInfo = new()
            {
                queueFamilyIndex = (uint)queueFamilyIndices.graphicsFamily,
                flags = VkCommandPoolCreateFlags.Transient | VkCommandPoolCreateFlags.ResetCommandBuffer
            };

            if (Vulkan.vkCreateCommandPool(_device, poolInfo, null,out _commandPool) != VkResult.Success)
            {
                throw new Exception("failed to create command pool!");
            }
        }

        private QueueFamilyIndices FindPhysicalQueueFamilies() => FindQueueFamilies(_physicalDevice);
        
        #endregion

        public void Dispose()
        {
            Vulkan.vkDestroyCommandPool(_device, _commandPool);
            Vulkan.vkDestroyDevice(_device);

            if (ENABLE_VALIDATION_LAYERS)
            {
                DestroyDebugUtilsMessengerEXT(_instance, _debugMessenger, null);
            }

            Vulkan.vkDestroySurfaceKHR(_instance, _surface);
            Vulkan.vkDestroyInstance(_instance);
        }

        // VkInstance instance, VkDebugUtilsMessengerEXT messenger, const VkAllocationCallbacks* pAllocator
        // (delegate* unmanaged<VkInstance, VkDebugUtilsMessengerEXT, VkAllocationCallbacks*, void>)
        private static void DestroyDebugUtilsMessengerEXT(VkInstance instance, VkDebugUtilsMessengerEXT debugMessenger, VkAllocationCallbacks* pAllocator)
        {
            var func = (delegate* unmanaged<VkInstance, VkDebugUtilsMessengerEXT, VkAllocationCallbacks*, void>)Vulkan.vkGetInstanceProcAddr(instance, "vkDestroyDebugUtilsMessengerEXT");
            if (func != null)
            {
                func(instance,debugMessenger, pAllocator);
            }
        }
    }
}
