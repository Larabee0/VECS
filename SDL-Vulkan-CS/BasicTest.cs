using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SDL3;
using Vortice.Vulkan;
using System.Numerics;
using System.Runtime.Intrinsics;

using SDL = SDL3.SDL3;

namespace SDL_Vulkan_CS
{
    /// <summary>
    /// Variation the test main function provided by vulkan-tutorial.com
    /// https://vulkan-tutorial.com/Development_environment
    /// 
    /// </summary>
    /// 

    public class TestDisposal : IDisposable
    {
        public ulong frames = 0;
        public void Dispose()
        {
            Console.WriteLine("Auto dispose");
        }

        ~TestDisposal() { Dispose(); }

    }


    internal unsafe class BasicTest
    {
        internal unsafe static void Main(string[] args)
        {
            if (!SDL.SDL_Init(SDL_InitFlags.Video))
            {
                throw new Exception("Failed to initialise SDL3");
            }


            if (!SDL.SDL_Vulkan_LoadLibrary())
            {
                throw new Exception("SDL failed to load Vulkan");
            }

            if (Vulkan.vkInitialize() != VkResult.Success)
            {
                throw new Exception("Failed Initialise vulkan");
            }


            SDL_Window window = SDL.SDL_CreateWindow("Vulkan window", 800, 600, SDL_WindowFlags.Vulkan);

            Vulkan.vkEnumerateInstanceExtensionProperties(out uint extensionCount);

            Console.WriteLine(string.Format("{0} Extensions supported",extensionCount));

            Matrix4x4 matrix = default;
            Vector4 vec = default;


            Vector4 test = Vector4.Transform(vec, matrix);

            bool run = true;
            while (run)
            {
                TestDisposal testDisposal = new TestDisposal();
                testDisposal.frames++;
                while (SDL.SDL_PollEvent(out SDL_Event @event))
                {
                    if (@event.type == SDL_EventType.Quit)
                    {
                        run = false;
                        break;
                    }
                }
            }
            SDL.SDL_DestroyWindow(window);

        }
    }
}
