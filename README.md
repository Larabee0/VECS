# COMP305-2202796

## Peer review Demo Video [Link Here](https://falmouthac-my.sharepoint.com/:v:/g/personal/wv276829_falmouth_ac_uk/EfE2Nx1LVpROnpwmRMmH3ZIBk80XOGLOI3MoUP5FZV6dBw?nav=eyJyZWZlcnJhbEluZm8iOnsicmVmZXJyYWxBcHAiOiJPbmVEcml2ZUZvckJ1c2luZXNzIiwicmVmZXJyYWxBcHBQbGF0Zm9ybSI6IldlYiIsInJlZmVycmFsTW9kZSI6InZpZXciLCJyZWZlcnJhbFZpZXciOiJNeUZpbGVzTGlua0NvcHkifX0&e=I7PP3X)

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
### Generation
The largest performance bottle neck according to visual studio cpu performance profiler is the raisemesh function (blue) which occupied 46% of total cpu time for loading the shape.

The rest is taken up by the subdivison system (green & orange), which turns the simple shape loaded from disk into a high geometry count shape for terrain generation.
Most of the subdividers times is spen on simplifySubdivison (green), which merges duplicate vertices. Looking in here most of the cost comes from dictionary oeprations, account for a combined 18% of total cpu time, where the hole simplify subdivsion method call takes 19.82%
In both cases for the green and orange sections show a low self time to total cpu time ratio. This indicates the cost of the method is low, but it is being called a lot. These operation would benefit from parallelisation then.
In both cases this makes sense, as they are mostly memory copy operations.

Looking at hte blue area, the results are similar to teh subdivide, a relatively low self time but high total time. Indicating the actual cost to run raise mesh once is small, it is just being called a lot.
Digging into raise mesh, the noise filter Evaluate calls are what occupy a vast majority of the total cpu time for this method, and within these the noise3Dgrad.snoise operation is the big cost. This is the simplex noise algorithim.
Once again like the subdivider, this whole operation would benefit from parallisation.

In both cases, parallising these operations is relatively simple. For Raise Mesh it is as simple as turning the vertex Evaluation for loop into a parallel for.
For the subdivider, the subdivide operation is also simply parallised in the same way, as the size and therefore indices of the index and vertex buffers can be pre-calcuated.
The simplfy operation is more complex as it involes a dictionary, which is not a thread-safe collection. Lucky something called a concurrent dictionary exists which is, but several parallel operations and steps will be needed still.

### Frame Rendering
Starting in off with the compiler in release mode, the entity world OnUpdate method took 49% of cpu time, and we can see it was the LocalToWorldSystem OnUpdate that is responsible for that. In release mode we can't see what exactly is taking most of the time.
Another 47% of total cpu time is taken up by the presentation system, specifically the TexturelessRenderSystem OnPresent call, which is where all the meshes are bound and drawn. Similar to the LocalToWorldSystem, exactly what is taking time isn't shown in release compiler mode.

Switching to the debug compiler to see whats taking so long, the LocalToWorld and presentation swap places as the presentation system raises from 47% of total time to 60%, with LocalToWorld falling proportionally to 37% they are still the two main bottlenecks.
#### Presentation
Looking at the presentation system first, the two major hogs are the GetComponent<T> (24% of total time) and the material bind and draw method (22% of total time).
GetComponent (Cyan) we can see that its the Type.get guid incurring the the bulk of the cost at 23.78% of total time. This is quite signficiant and could be easily improved by not calling get component id.

Next looking at the bind and draw (Green), we can see its external vulkan calls bind & draw incuring the cost. Nothing I can do to improve that other than providing smaller meshes.

#### LocalToWorld
Highlighted in cyan, we can see the main bottlenecks are once again get component id, taking a combined 29.76% of total cpu time and local to world is taking 31.65% of total. The camera system takes 6.05%, which accounts for pretty much all of the world OnUpdate (37.77%)

#### Improvements to make
The top thing to look at for frame rendering right now is GetComponentId. The way this works now is it gets the Type class of the component type, and computes the Type GUID. It seems this quite an expensive operation in C#, finding a way around needing to get the component id or using a differenet more efficient method of identifiying component types to lookup.

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
