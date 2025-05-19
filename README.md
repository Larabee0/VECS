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
- (fixing) occlusion culling
- adding a html ui library 
- ~~adding a physics library~~
- ~~submission queue on separate thread~~
- multi-threaded rendering
- ~~shadow casting~~
- improved transparency support
- improved lighting system

These are things I'd like to do, who knows how many will end up getting implemented 
