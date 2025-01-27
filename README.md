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


# Project
I want to create a solar system filled with procedurally generated planets based off an interesting shape, a spherical shape made up for hexagons and pentagons
Base shap: https://levskaya.github.io/polyhedronisme/?recipe=A100ccD 
As a starting point i will be following a series of videos from Sebastian Lague on procedural generating sphereical planets based on inflated cube to form a sphere
https://www.youtube.com/playlist?list=PLFt_AvWsXl0cONs3T0By4puYy6GM22ko8
To add some more complexity I will add to the noise algorithim an efficient but convicning ersion method I first discovered through this video.
https://youtu.be/gsJHzBTPG0Y
which itself is based off this article https://iquilezles.org/articles/morenoise/
This erison method looks at direction point to see where it points up hill and measures how steep it is, the gradient of the point.
Using the derivative of the gradient, to lower areas of low slope using a weighting creates more rigid eroded terrain without the high overhead of hydraulic erosion algorithims

## Final Changes
- Multiple planets
- sun (as point light)
- very simple culling
It measures the angle between the tiles up vector and the camera foward vector and culls anything above 100 degrees. works great other than some pop-in

## Comparison to WS3-Colour
### Culling
Culling gained back most of the performance lost by the heavy shader
Before: <br>
![alt text](https://github.com/Larabee0/SDL-Vulkan-CS/blob/sub-build/Profiling/WS3-Colour/Framerate%20hit.png)
<br>
After: <br>
![alt text](https://github.com/Larabee0/SDL-Vulkan-CS/blob/sub-build/Profiling/WS4/Culling%20one%20planet%206%20subdivisons.png)

## Final Performance
The final artefact has 7 plants with 7 subdivison, much heavier than the 6 tested with so far
6 subdivisons: <br>
![alt text](https://github.com/Larabee0/SDL-Vulkan-CS/blob/sub-build/Profiling/WS4/Release%20Compiler%20culling%207%20planets%206%20subdivisons.png)
<br>
7 Subdivisons: <br>
![alt text](https://github.com/Larabee0/SDL-Vulkan-CS/blob/sub-build/Profiling/WS4/Release%20Compiler%20culling%207%20planets%207%20subdivisons.png)

This is because an extra subdivison quadruples the geometry the final scene has 125m polys while with 6 only 25m
The frame rate hit is massive as you can see

GPU Busy remains optimally ontop of the cpu time
