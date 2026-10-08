using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Vortice.Vulkan;

namespace VECS
{
    public enum ConstantFormat
    {
        UInt,
        UVec2,
        UVec3,
        UVec4,

        Int,
        IVec2,
        IVec3,
        IVec4,

        Float,
        Vec2,
        Vec3,
        Vec4,
        Mat3,
        Mat4
    }

    public static class ConstantFormatExtension
    {
        public static unsafe uint ToByteSize(this ConstantFormat format)
        {
            return format switch
            {
                ConstantFormat.UInt => sizeof(uint),
                ConstantFormat.Int => sizeof(int),
                ConstantFormat.UVec2 => (uint)sizeof(Vector3UInt),
                ConstantFormat.UVec3 => (uint)sizeof(Vector3UInt),
                ConstantFormat.UVec4 => (uint)sizeof(Vector4UInt),
                ConstantFormat.IVec2 => (uint)sizeof(Vector2Int),
                ConstantFormat.IVec3 => (uint)sizeof(Vector3Int),
                ConstantFormat.IVec4 => (uint)sizeof(Vector4Int),
                ConstantFormat.Float => sizeof(float),
                ConstantFormat.Vec2 => (uint)sizeof(Vector2),
                ConstantFormat.Vec3 => (uint)sizeof(Vector3),
                ConstantFormat.Vec4 => (uint)sizeof(Vector4),
                ConstantFormat.Mat3 => (uint)sizeof(Matrix3x3),
                ConstantFormat.Mat4 => (uint)sizeof(Matrix4x4),
                _ => 0,
            };
        }
    }

    public unsafe struct VECSSpecialisationConstant
    {
        public int TargetShaderHash;
        public uint ConstantId;
        public ConstantFormat DataFormatHint;

        public fixed byte Data[64];

        public VECSSpecialisationConstant(int shaderHash, uint id)
        {
            TargetShaderHash = shaderHash;
            ConstantId = id;
        }

        public VECSSpecialisationConstant(int shaderHash, uint id, uint data) : this(shaderHash, id)
        {
            DataFormatHint = ConstantFormat.UInt;
            fixed (byte* pData = &Data[0])
            {
                NativeMemory.Copy(&data, pData, sizeof(uint));
            }
        }

        public VECSSpecialisationConstant(int shaderHash, uint id, Vector2UInt data) : this(shaderHash, id)
        {
            DataFormatHint = ConstantFormat.UVec2;
            fixed (byte* pData = &Data[0])
            {
                NativeMemory.Copy(&data, pData, (uint)sizeof(Vector2UInt));
            }
        }

        public VECSSpecialisationConstant(int shaderHash, uint id, Vector3UInt data) : this(shaderHash, id)
        {
            DataFormatHint = ConstantFormat.UVec3;
            fixed (byte* pData = &Data[0])
            {
                NativeMemory.Copy(&data, pData, (uint)sizeof(Vector3UInt));
            }
        }

        public VECSSpecialisationConstant(int shaderHash, uint id, Vector4UInt data) : this(shaderHash, id)
        {
            DataFormatHint = ConstantFormat.UVec4;
            fixed (byte* pData = &Data[0])
            {
                NativeMemory.Copy(&data, pData, (uint)sizeof(Vector4UInt));
            }
        }

        public VECSSpecialisationConstant(int shaderHash, uint id, int data) : this(shaderHash, id)
        {
            DataFormatHint = ConstantFormat.Int;
            fixed (byte* pData = &Data[0])
            {
                NativeMemory.Copy(&data, pData, sizeof(int));
            }
        }

        public VECSSpecialisationConstant(int shaderHash, uint id, Vector2Int data) : this(shaderHash, id)
        {
            DataFormatHint = ConstantFormat.IVec2;
            fixed (byte* pData = &Data[0])
            {
                NativeMemory.Copy(&data, pData, (uint)sizeof(Vector2Int));
            }
        }

        public VECSSpecialisationConstant(int shaderHash, uint id, Vector3Int data) : this(shaderHash, id)
        {
            DataFormatHint = ConstantFormat.IVec3;
            fixed (byte* pData = &Data[0])
            {
                NativeMemory.Copy(&data, pData, (uint)sizeof(Vector3Int));
            }
        }

        public VECSSpecialisationConstant(int shaderHash, uint id, Vector4Int data) : this(shaderHash, id)
        {
            DataFormatHint = ConstantFormat.IVec4;
            fixed (byte* pData = &Data[0])
            {
                NativeMemory.Copy(&data, pData, (uint)sizeof(Vector4Int));
            }
        }

        public VECSSpecialisationConstant(int shaderHash, uint id, float data) : this(shaderHash, id)
        {
            DataFormatHint = ConstantFormat.Float;
            fixed (byte* pData = &Data[0])
            {
                NativeMemory.Copy(&data, pData, sizeof(float));
            }
        }

        public VECSSpecialisationConstant(int shaderHash, uint id, Vector2 data) : this(shaderHash, id)
        {
            DataFormatHint = ConstantFormat.Vec2;
            fixed (byte* pData = &Data[0])
            {
                NativeMemory.Copy(&data, pData, (uint)sizeof(Vector2));
            }
        }

        public VECSSpecialisationConstant(int shaderHash, uint id, Vector3 data) : this(shaderHash, id)
        {
            DataFormatHint = ConstantFormat.Vec3;
            fixed (byte* pData = &Data[0])
            {
                NativeMemory.Copy(&data, pData, (uint)sizeof(Vector3));
            }
        }

        public VECSSpecialisationConstant(int shaderHash, uint id, Vector4 data) : this(shaderHash, id)
        {
            DataFormatHint = ConstantFormat.Vec4;
            fixed (byte* pData = &Data[0])
            {
                NativeMemory.Copy(&data, pData, (uint)sizeof(Vector4));
            }
        }

        public VECSSpecialisationConstant(int shaderHash, uint id, Matrix3x3 data) : this(shaderHash, id)
        {
            DataFormatHint = ConstantFormat.Mat3;
            fixed (byte* pData = &Data[0])
            {
                NativeMemory.Copy(&data, pData, (uint)sizeof(Matrix3x3));
            }
        }

        public VECSSpecialisationConstant(int shaderHash, uint id, Matrix4x4 data) : this(shaderHash, id)
        {
            DataFormatHint = ConstantFormat.Mat4;
            fixed (byte* pData = &Data[0])
            {
                NativeMemory.Copy(&data, pData, (uint)sizeof(Matrix4x4));
            }
        }

        public static VECSSpecialisationConstant FromJson(GraphicsPipelineDefinition.SpecialisationConstant src)
        {
            return src.DataFormatHint switch
            {
                ConstantFormat.UInt => new(src.ShaderModule.Hash, src.ConstantId, JsonSerializer.Deserialize<uint>(src.Data)),
                ConstantFormat.UVec2 => new(src.ShaderModule.Hash, src.ConstantId, JsonSerializer.Deserialize<Vector2UInt>(src.Data)),
                ConstantFormat.UVec3 => new(src.ShaderModule.Hash, src.ConstantId, JsonSerializer.Deserialize<Vector3UInt>(src.Data)),
                ConstantFormat.UVec4 => new(src.ShaderModule.Hash, src.ConstantId, JsonSerializer.Deserialize<Vector4UInt>(src.Data)),
                ConstantFormat.Int => new(src.ShaderModule.Hash, src.ConstantId, JsonSerializer.Deserialize<int>(src.Data)),
                ConstantFormat.IVec2 => new(src.ShaderModule.Hash, src.ConstantId, JsonSerializer.Deserialize<Vector2Int>(src.Data)),
                ConstantFormat.IVec3 => new(src.ShaderModule.Hash, src.ConstantId, JsonSerializer.Deserialize<Vector3Int>(src.Data)),
                ConstantFormat.IVec4 => new(src.ShaderModule.Hash, src.ConstantId, JsonSerializer.Deserialize<Vector4Int>(src.Data)),
                ConstantFormat.Float => new(src.ShaderModule.Hash, src.ConstantId, JsonSerializer.Deserialize<float>(src.Data)),
                ConstantFormat.Vec2 => new(src.ShaderModule.Hash, src.ConstantId, JsonSerializer.Deserialize<Vector2>(src.Data)),
                ConstantFormat.Vec3 => new(src.ShaderModule.Hash, src.ConstantId, JsonSerializer.Deserialize<Vector3>(src.Data)),
                ConstantFormat.Vec4 => new(src.ShaderModule.Hash, src.ConstantId, JsonSerializer.Deserialize<Vector4>(src.Data)),
                ConstantFormat.Mat3 => new(src.ShaderModule.Hash, src.ConstantId, JsonSerializer.Deserialize<Matrix3x3>(src.Data)),
                ConstantFormat.Mat4 => new(src.ShaderModule.Hash, src.ConstantId, JsonSerializer.Deserialize<Matrix4x4>(src.Data)),
                _ => default,
            };
        }

        public string GetJsonData()
        {
            switch (DataFormatHint)
            {
                case ConstantFormat.UInt:
                    {
                        uint vector = default;
                        fixed (byte* pData = &Data[0])
                        {
                            NativeMemory.Copy(pData, &vector, sizeof(uint));
                        }
                        return JsonSerializer.Serialize(vector);
                    }
                case ConstantFormat.UVec2:
                    {
                        Vector2UInt vector = default;
                        fixed (byte* pData = &Data[0])
                        {
                            NativeMemory.Copy(pData, &vector, (uint)sizeof(Vector2UInt));
                        }
                        return JsonSerializer.Serialize(vector);
                    }
                case ConstantFormat.UVec3:
                    {
                        Vector3UInt vector = default;
                        fixed (byte* pData = &Data[0])
                        {
                            NativeMemory.Copy(pData, &vector, (uint)sizeof(Vector3UInt));
                        }
                        return JsonSerializer.Serialize(vector);
                    }
                case ConstantFormat.UVec4:
                    {
                        Vector4UInt vector = default;
                        fixed (byte* pData = &Data[0])
                        {
                            NativeMemory.Copy(pData, &vector, (uint)sizeof(Vector4UInt));
                        }
                        return JsonSerializer.Serialize(vector);
                    }
                case ConstantFormat.Int:
                    {
                        int vector = default;
                        fixed (byte* pData = &Data[0])
                        {
                            NativeMemory.Copy(pData, &vector, sizeof(int));
                        }
                        return JsonSerializer.Serialize(vector);
                    }
                case ConstantFormat.IVec2:
                    {
                        Vector2Int vector = default;
                        fixed (byte* pData = &Data[0])
                        {
                            NativeMemory.Copy(pData, &vector, (uint)sizeof(Vector2Int));
                        }
                        return JsonSerializer.Serialize(vector);
                    }
                case ConstantFormat.IVec3:
                    {
                        Vector3Int vector = default;
                        fixed (byte* pData = &Data[0])
                        {
                            NativeMemory.Copy(pData, &vector, (uint)sizeof(Vector3Int));
                        }
                        return JsonSerializer.Serialize(vector);
                    }
                case ConstantFormat.IVec4:
                    {
                        Vector4Int vector = default;
                        fixed (byte* pData = &Data[0])
                        {
                            NativeMemory.Copy(pData, &vector, (uint)sizeof(Vector4Int));
                        }
                        return JsonSerializer.Serialize(vector);
                    }
                case ConstantFormat.Float:
                    {
                        float vector = default;
                        fixed (byte* pData = &Data[0])
                        {
                            NativeMemory.Copy(pData, &vector, sizeof(float));
                        }
                        return JsonSerializer.Serialize(vector);
                    }
                case ConstantFormat.Vec2:
                    {
                        Vector2 vector = default;
                        fixed (byte* pData = &Data[0])
                        {
                            NativeMemory.Copy(pData, &vector, (uint)sizeof(Vector2));
                        }
                        return JsonSerializer.Serialize(vector);
                    }
                case ConstantFormat.Vec3:
                    {
                        Vector3 vector = default;
                        fixed (byte* pData = &Data[0])
                        {
                            NativeMemory.Copy(pData, &vector, (uint)sizeof(Vector3));
                        }
                        return JsonSerializer.Serialize(vector);
                    }
                case ConstantFormat.Vec4:
                    {
                        Vector4 vector = default;
                        fixed (byte* pData = &Data[0])
                        {
                            NativeMemory.Copy(pData, &vector, (uint)sizeof(Vector4));
                        }
                        return JsonSerializer.Serialize(vector);
                    }
                case ConstantFormat.Mat3:
                    {
                        Matrix3x3 matrix = default;
                        fixed (byte* pData = &Data[0])
                        {
                            NativeMemory.Copy(pData, &matrix, (uint)sizeof(Matrix3x3));
                        }
                        return JsonSerializer.Serialize(matrix);
                    }
                case ConstantFormat.Mat4:
                    {
                        Matrix4x4 matrix = default;
                        fixed (byte* pData = &Data[0])
                        {
                            NativeMemory.Copy(pData, &matrix, (uint)sizeof(Matrix4x4));
                        }
                        return JsonSerializer.Serialize(matrix);
                    }
                default:
                    return "";
            }

        }
    }


    /// <summary>
    /// Managed none-pointer struct used in the creation and configuration of a <see cref="VkGraphicsPipelineCreateInfo"/>
    /// 
    /// A whole load of configuration data for a Vk Graphics Pipeline.
    /// </summary>
    public struct GraphicsPipelineConfigInfo
    {
        public VkVertexInputBindingDescription[] BindingDescriptions;
        public VkVertexInputAttributeDescription[] AttributeDescriptions;
        public VkPipelineViewportStateCreateInfo viewportInfo;
        public VkPipelineInputAssemblyStateCreateInfo inputAssemblyInfo;
        public VkPipelineRasterizationStateCreateInfo rasterizationInfo;
        public VkPipelineMultisampleStateCreateInfo multisampleInfo;
        public VkPipelineColorBlendAttachmentState colourBlendAttachment;
        public VkPipelineColorBlendStateCreateInfo colourBlendInfo;
        public VkPipelineDepthStencilStateCreateInfo depthStencilInfo;
        public VkDynamicState[] dynamicStateEnables;
        public VkPipelineDynamicStateCreateInfo dynamicInfo;
        public VkPipelineLayout pipelineLayout;
        public VkPipelineRenderingCreateInfo pipelineRenderingCreateInfo;
        //public bool dynamicRendering;
        public VkFormat[] colourFormats;
        public VkFormat depthFormat;
        public VkFormat stencilFormat;
        public uint viewMask;
        public GraphicsPipeline BasePipeline;
        public bool AllowDerivative;
        public VECSSpecialisationConstant[] SpecialisationConstants;
        /// <summary>
        /// Default graphics pipeline configuration. Because of course vulkan doesn't have a default.
        /// </summary>
        /// <returns></returns>
        public static GraphicsPipelineConfigInfo DefaultPipelineConfigInfo()
        {

            var attributes = new VertexAttributeDescription[]
            {
                new(VertexAttribute.Position,VertexAttributeFormat.Float3,0,0,0),
                new(VertexAttribute.Normal,VertexAttributeFormat.Float3,12,0,1),
            };

            return DefaultPipelineConfigInfo(MeshExtensions.GetBindingDescription(attributes), MeshExtensions.GetAttributeDescriptions(attributes));
        }


        public static unsafe GraphicsPipelineConfigInfo DefaultPipelineConfigInfo(VkVertexInputBindingDescription[] vkVertexInputBindings, VkVertexInputAttributeDescription[] vkVertexInputAttributes)
        {

            VkPipelineColorBlendStateCreateInfo colourBlendInfo = new()
            {
                logicOpEnable = false,
                logicOp = VkLogicOp.Copy,
                attachmentCount = 1,
            };
            colourBlendInfo.blendConstants[0] = 0;
            colourBlendInfo.blendConstants[1] = 0;
            colourBlendInfo.blendConstants[2] = 0;
            colourBlendInfo.blendConstants[3] = 0;

            VkDynamicState[] dynamicStateEnables = [VkDynamicState.Viewport, VkDynamicState.Scissor, VkDynamicState.CullMode];
            VkPipelineDynamicStateCreateInfo dynamicInfo = new()
            {
                dynamicStateCount = (uint)dynamicStateEnables.Length,
                flags = 0
            };
            var attributes = new VertexAttributeDescription[]
            {
                new(VertexAttribute.Position,VertexAttributeFormat.Float3,0,0,0),
                new(VertexAttribute.Normal,VertexAttributeFormat.Float3,12,0,1),
            };
            return new()
            {
                colourFormats = [Presenter.MainColourFormat],
                depthFormat = Presenter.DepthFormat,
                stencilFormat = VkFormat.Undefined,
                pipelineRenderingCreateInfo = new(),
                inputAssemblyInfo = new()
                {
                    topology = VkPrimitiveTopology.TriangleList,
                    primitiveRestartEnable = false
                },

                viewportInfo = new()
                {
                    viewportCount = 1,
                    pViewports = null,
                    scissorCount = 1,
                    pScissors = null
                },

                rasterizationInfo = new()
                {
                    depthClampEnable = false,
                    rasterizerDiscardEnable = false,
                    polygonMode = VkPolygonMode.Fill,
                    lineWidth = 1,
                    cullMode = VkCullModeFlags.Back,
                    frontFace = VkFrontFace.CounterClockwise,
                    depthBiasEnable = false,
                    depthBiasConstantFactor = 0,
                    depthBiasClamp = 0,
                    depthBiasSlopeFactor = 0
                },

                multisampleInfo = new()
                {
                    sampleShadingEnable = false,
                    rasterizationSamples = VkSampleCountFlags.Count1,
                    minSampleShading = 1,
                    pSampleMask = null,
                    alphaToCoverageEnable = false,
                    alphaToOneEnable = false
                },

                colourBlendInfo = colourBlendInfo,

                colourBlendAttachment = new()
                {
                    colorWriteMask = VkColorComponentFlags.All,
                    blendEnable = false,
                    colorBlendOp = VkBlendOp.Add,
                    alphaBlendOp = VkBlendOp.Add,
                    srcAlphaBlendFactor = VkBlendFactor.One,
                    dstAlphaBlendFactor = VkBlendFactor.Zero,
                    srcColorBlendFactor = VkBlendFactor.One,
                    dstColorBlendFactor = VkBlendFactor.Zero,
                },

                depthStencilInfo = new()
                {
                    sType = VkStructureType.PipelineDepthStencilStateCreateInfo,
                    depthTestEnable = true,
                    depthWriteEnable = false,
                    depthCompareOp = VkCompareOp.LessOrEqual,
                    depthBoundsTestEnable = false,
                    minDepthBounds = 0.0f,
                    maxDepthBounds = 1.0f,
                    stencilTestEnable = false,
                    front = default,
                    back = default
                },

                dynamicStateEnables = dynamicStateEnables,

                dynamicInfo = dynamicInfo,

                BindingDescriptions = vkVertexInputBindings,
                AttributeDescriptions = vkVertexInputAttributes
            };
        }

        public static GraphicsPipelineConfigInfo DefaultPipelineConfigInfo(VkPipelineLayout pipelineLayout)
        {
            var pipelineConfigInfo = DefaultPipelineConfigInfo();
            pipelineConfigInfo.pipelineLayout = pipelineLayout;

            return pipelineConfigInfo;
        }

        /// <summary>
        /// Modify the given configInfo to enable alpha blending of the colour channel.
        /// </summary>
        /// <param name="configInfo">Configuration to modify</param>
        public static void EnableAlphaBlending(ref GraphicsPipelineConfigInfo configInfo)
        {
            var colourBlendAttachment = configInfo.colourBlendAttachment;

            colourBlendAttachment.colorWriteMask = VkColorComponentFlags.All;
            colourBlendAttachment.blendEnable = true;

            colourBlendAttachment.colorBlendOp = VkBlendOp.Add;
            colourBlendAttachment.alphaBlendOp = VkBlendOp.Add;

            colourBlendAttachment.srcColorBlendFactor = VkBlendFactor.SrcAlpha;
            colourBlendAttachment.dstColorBlendFactor = VkBlendFactor.OneMinusSrcAlpha;

            colourBlendAttachment.srcAlphaBlendFactor = VkBlendFactor.One;
            colourBlendAttachment.dstAlphaBlendFactor = VkBlendFactor.Zero;

            configInfo.colourBlendAttachment = colourBlendAttachment;
        }
    }
}
