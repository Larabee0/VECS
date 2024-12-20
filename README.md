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

## Colouration addition
I added colours it is pretty and textures through triplanar mapping. But lets check to see if performance was terribly effected.
The ocean is even animated slightly

![alt text](https://github.falmouth.ac.uk/GA-Undergrad-Student-Work-24-25/COMP305-2202796/blob/WS-3-Colouration/Example.png)
# Generation time
Before:
<br>
![alt text](https://github.falmouth.ac.uk/GA-Undergrad-Student-Work-24-25/COMP305-2202796/blob/WS-3/Profiling/WS3/GPU/GPU%20Vertex%20Normals.png)
<br>
After:
<br>
![alt text](https://github.falmouth.ac.uk/GA-Undergrad-Student-Work-24-25/COMP305-2202796/blob/WS-3-Colouration/Profiling/WS3-Colour/Generation%20time%20not%20hit%20badly%20by%20colour.png)
# Frame time
Frame time incurred a hit of about 100fps, but the shader now takes in multiple 14 textures so thats understable. 400fps with no culling seems pretty good <br>
Before: 
<br>
![alt text](https://github.falmouth.ac.uk/GA-Undergrad-Student-Work-24-25/COMP305-2202796/blob/WS-3-Colouration/Profiling/WS3/CPU-Parallel/Better%20GPU%20Busy%20Higher%20framerate.png)
<br>
After: 
<br>
![alt text](https://github.falmouth.ac.uk/GA-Undergrad-Student-Work-24-25/COMP305-2202796/blob/WS-3-Colouration/Profiling/WS3-Colour/Framerate%20hit.png)

It just got heavier to render, culling would be a good improvment to not bother with tiles on the other side of the mesh
