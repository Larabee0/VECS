This is a C# Vulkan ECS Game engine with Bepu Physics.
# Features
- SPIR-V Reflection - auto generation of material pipelines and descriptor sets
- Automatic Material Variant handling
- GPU Driven Rendering - Draw Indirect is the main way of drawing objects
- Advanced Mesh class built for Draw Indirect
- Camera Fustrum culling using GPU compute shaders
- Compute shader vertex normal generation
- Real-time omni directional point light shadow casting optimised with GPU fustrum culling
- Simple ECS Arcitecture
- BepuPhysics intergration
- Assimp model & scene loading
- Freeimage + NVTT texture loading
- Mikktspace vertex tangent generation
- SDL3
- Vulkan 1.4
- .Net 9.0


For the basic graphics engine I wrote between November and December 2024 for a university module. See [WS-4 Branch](https://github.com/Larabee0/SDL-Vulkan-CS/tree/WS-4) for submission version

# Things I want to do with this 
In no particular order 
- ~~(fixing) occlusion culling~~ Abandoned for now
- adding a html ui library 
- ~~adding a physics library~~
- ~~submission queue on separate thread~~
- Moving to a task based architecture for renderering and game logic
- Better implementation of BepuPhysics
- ~~shadow casting~~
- improved transparency support
- improved lighting system
- Pre-loadable Asset database
- ~~PipelineCaches~~
- Mesh shaders and primative culling
- multiple queues 

These are things I'd like to do, who knows how many will end up getting implemented 
# Building and running 
Vulkan sdk 1.4 is required to build this project as it uses the glslc shader compiler. 
I recommend setting up something like glsl lint for writing shaders.
On windows or Linux you need to have the dotnet 9.0 runtime to run built applications and in addition the sdk to build it. 
This project should build and run just fine on windows using visual studio in debug or release mode. 
I am using vs code on Linux (steamos 3.0) and also on windows (10) now however, building in release mode I haven't implemented in vscode yet. 
This runs on a Framework 16 laptop with the integrated gpu and the rx 7700s running on steamos 3.0. That's the only Linux support I'm confident in claiming - but it is a native vulkan application it should run on anything if dotnet is installed and the vulkan driver is compliant for 1.4.304+
