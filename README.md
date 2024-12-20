# COMP305-2202796
My implementation of COMP305 relies on Vulkan SDK 1.3.290 or higher being installed https://vulkan.lunarg.com/sdk/home

This project uses C# Bindings for Vulkan, Vulkan Memory Allocator & SDL3, managed by the NuGet package manager in Visual Studio.
If you aren't using VS or NuGet the bindings are avaliable at the source links below.
- Alimer.Bindings.SDL - SDL3 C# Bindings, LICENSE: MIT, SOURCE: https://github.com/amerkoleci/Alimer.Bindings.SDL
- Vortice.Vulkan - Vulkan C# Bindings, LICENSE: MIT, SOURCE: https://github.com/amerkoleci/Vortice.Vulkan
- Vortice.Vma - Vulkan Memory Allocator C# Bindings, LICENSE: MIT, Source: https://github.com/amerkoleci/Vortice.Vulkan
- AssimpNet - Assimp C# implmentation, LICENSE: MIT, Source: https://bitbucket.org/Starnick/assimpnet/src
- TeximpNet - Freeimage & Nvidia Texture Tools C# libarary for loading images, LICENSE: MIT, Source: https://bitbucket.org/Starnick/teximpnet
I did not write my own C# bindings for Vulkan or SDL3 or Vulkan Memory Allocator

Vulkan: https://www.vulkan.org/  
SDL3: https://www.libsdl.org/  
Vulkan Memory Allocator: https://gpuopen.com/vulkan-memory-allocator/


# WS3
All profiling from now on was done on my laptop which isn't as powerful as my desktop.

## Generation time improvements
### CPU Parallisation
I was able to quite easily use C# Parallel.For to parallise the CPU subdivision, raise mesh and vertex normal calculations. Unfortunately this had the side effect of making the VS profiling tool useless as it counts other threads seequtially instead of in parallel for CPU time.
So I switched to measuring times to execute a method with DateTime
Here is WS2 base time for 6 subdivisions + Generation + Normals
![alt text](https://github.falmouth.ac.uk/GA-Undergrad-Student-Work-24-25/COMP305-2202796/blob/WS-3/Profiling/WS3/WS2-Again/WS2-Subdivide-Raise-Normals.png)
And after parallisation

![alt text](https://github.falmouth.ac.uk/GA-Undergrad-Student-Work-24-25/COMP305-2202796/blob/WS-3/Profiling/WS3/CPU-Parallel/WS3%20CPU%20parallalisation%20Metrics.png)

Very dramatic decrease in exeuction time. But more can still be had from a compute shader for generation time.
### GPU Generation

![alt text](https://github.falmouth.ac.uk/GA-Undergrad-Student-Work-24-25/COMP305-2202796/blob/WS-3/Profiling/WS3/GPU/Generation%20is%20fast%20normals%20slow.png)
