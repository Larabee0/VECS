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

## inital implementation
In blender i imported the polyheron and split all the faces into seperate mesh
I then triangulated each face with a vertex in the center so the triangules that make up each face are of relatively equal size.
This allows me to import the mesh in my application then subdivide it to different levels to gain lower or higher levels of detail in the terrain gneration algorithim at the cost of performance.
I then wrote a subdivison algorithim to subdivide the mesh further.
after that I implemented Sebastian Lague's Noise filters and shape generator method. the Vertices that make up the polyherodn all point away from the center, so simply by multiplayer the elevation value with the vertex
moves hte vertedx further away from the center, raising the terrain.
After this I modiifed teh noise filter algorithims to implement the Gradient trick fo erosion.

This worked but things didn't look correct in the lighting, the vertex normals of the mesh need to be recalculated to be able to see the terrain properly
so I implemented a vertex normal generation algorithim [cite]

I also added a randomisation system, by assigning a random number to the center point vector of the noise input. This can be represented as a seed for each planet.

For shaders I am just rendering white with direction lights and

## Performance anayslsis
- Subdivision takes a long time for each face
- Generation takes a long time
- Normal calculation takes a long time
- Outside of generation, the ECS implementation, one of the get component Id overloads incurs a major cost in execution speed from looking up te component type guid
- Vertex struct has a lot of unused variables which add a lot of memory overhead which is simply not used

## Next
- Improve performance of the application by addressing all the above
- Add parent-child transform to be able to form a solar system
- Add sphere to work as the sun
- Modify the lighting system to place a light in the sun to more realistically light the planets
- Add colouration to the planets and randomisation for colour and perhaps more terrain varation
- Added orbital animations
- Culling system for faces of planets that aren't visible
