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
After spending a long time on a workflow I got a compute shader for the terrain generator working and it is a lot faster, at generating terrain. 360 to 90ms a nice improvement at this time scale
![alt text](https://github.falmouth.ac.uk/GA-Undergrad-Student-Work-24-25/COMP305-2202796/blob/WS-3/Profiling/WS3/GPU/Generation%20is%20fast%20normals%20slow.png)
However the vertex normal calculation got worse and after some digging, it was the copying back of the vertex buffer that was the culprit.

So I went away and made a vertex normal compute shader (two compute shaders actually)
![alt text](https://github.falmouth.ac.uk/GA-Undergrad-Student-Work-24-25/COMP305-2202796/blob/WS-3/Profiling/WS3/GPU/GPU%20Vertex%20Normals.png)
This brought down normal vector calculation from 500ms to 95ms, making the total gpu compute shader workflow take <200ms when just raise mesh before vertex normals calculations, took 360ms on the cpu.
Very statisfactory improvements.

## Frame Time improvements
### Get Component ID

Get component id was really slow because it calculted the component type guid every time. to fix this I made an accessor in as part of the interface for a component that gets component id.
Before:
![alt text](https://github.falmouth.ac.uk/GA-Undergrad-Student-Work-24-25/COMP305-2202796/blob/WS-3/Profiling/WS3/WS2-Again/DebugFrameTimePresent.png)
![alt text](https://github.falmouth.ac.uk/GA-Undergrad-Student-Work-24-25/COMP305-2202796/blob/WS-3/Profiling/WS3/WS2-Again/PresentMon.png)
After:
![alt text](https://github.falmouth.ac.uk/GA-Undergrad-Student-Work-24-25/COMP305-2202796/blob/WS-3/Profiling/WS3/CPU-Parallel/GetComponentIdImprovement.png)
![alt text](https://github.falmouth.ac.uk/GA-Undergrad-Student-Work-24-25/COMP305-2202796/blob/WS-3/Profiling/WS3/CPU-Parallel/Better%20GPU%20Busy%20Higher%20framerate.png)
As you can see here getcomponent went from being 12% of total cpu time to less than half of a %.
And the frame rate went from 280s to 580s.
This frame rate software is called intel presentmon. One of the good thing about it is the GPU Busy graph which shows how much of the frametime the gpu is actually doing work for.
Ideally the CPU and GPU lines are ontop of each other. You can see before i made the performance update there was a little gap, then afterwards there is no gap.
This indicates more efficient GPU utilisation.


## Memory Improvements

![alt text](https://github.falmouth.ac.uk/GA-Undergrad-Student-Work-24-25/COMP305-2202796/blob/WS-3/Profiling/WS3/WS2-Again/DebugMemory.png)
Before in WS2 the cpu side mesh data sticks around even though its not used this is not a memory leak but it is wasted memory
![alt text](https://github.falmouth.ac.uk/GA-Undergrad-Student-Work-24-25/COMP305-2202796/blob/WS-3/Profiling/WS3/CPU-Parallel/CPU%20Side%20mesh%20data%20Cleaned%20up%20after%20flush.png)
I made a change to discard this data when a mesh is flushed to the GPU which entirely removed teh huge uin32 and vertex allocations.

