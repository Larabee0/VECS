using System;
using System.Collections.Generic;
using Vortice.SPIRV;
using Vortice.SPIRV.Reflect;
using Vortice.Vulkan;

namespace VECS
{
    public class DescriptorPropertyInfo
    {
        public readonly string Name;
        public readonly SpvOp Type;
        public readonly VertexAttributeFormat VectorFormat;
        public readonly uint Offset;
        public readonly uint PaddedSize;
        public readonly bool Signed = false;
        public readonly bool Matrix;
        public readonly uint Rows;
        public readonly uint Columns;
        public readonly uint ArrayDimentions;
        public readonly uint[] ArrayDimentionSizes;
        public readonly DescriptorPropertyInfo[] Members;
        public readonly VkImageType ImageType;
        public readonly bool ImageArray;
        public readonly uint ImageDepth;

        public uint CachedMemberSize = 0;
        public uint CachedArraySize = 0;

        public uint Size
        {
            get
            {
                switch (Type)
                {
                    case SpvOp.TypeFloat: return VectorFormat.GetAttributeByteSize();
                    case SpvOp.TypeInt: return VectorFormat.GetAttributeByteSize();
                    case SpvOp.TypeVector: return VectorFormat.GetAttributeByteSize();
                    case SpvOp.TypeMatrix: return VectorFormat.GetAttributeByteSize() * Rows;
                    case SpvOp.TypeStruct:

                        if (CachedMemberSize == 0) {
                            for (int i = 0; i < Members.Length; i++)
                            {
                                CachedMemberSize += Members[i].Size;
                            } 
                        }
                        return CachedMemberSize;
                    case SpvOp.TypeArray:

                        if (CachedMemberSize == 0)
                        {
                            for (int i = 0; i < Members.Length; i++)
                            {
                                CachedMemberSize += Members[i].Size;
                            }
                        }
                        if(CachedArraySize == 0)
                        {
                            CachedArraySize = ArrayDimentionSizes[0] * CachedMemberSize;
                            for (int i = 1;i < ArrayDimentions; i++)
                            {
                                CachedArraySize *= ArrayDimentionSizes[i];
                            }
                        }
                        return CachedArraySize;
                    default:
                        throw new NotSupportedException(string.Format("SpvOpType {0} is not an expected variable type",Type.ToString()));
                }
            }
        }

        public DescriptorPropertyInfo(string name, SpvOp type, uint paddedSize, SpvReflectNumericTraits traits, uint offset)
        {
            Name = name;
            Type = type;
            Offset = offset;
            PaddedSize = paddedSize;
            if (type == SpvOp.TypeVector)
            {
                VectorFormat = (traits.vector.component_count * (traits.scalar.width / 8)).GetAttributeFromByteSize();
            }
            if (type == SpvOp.TypeMatrix)
            {
                Rows = traits.matrix.row_count;
                Columns = traits.matrix.column_count;
                VectorFormat = (traits.vector.component_count * Rows).GetAttributeFromByteSize();
            }
            if (type == SpvOp.TypeInt || type == SpvOp.TypeFloat)
            {
                VectorFormat = (traits.scalar.width / 8).GetAttributeFromByteSize();
                Signed = traits.scalar.signedness == 1;
            }
        }

        public unsafe DescriptorPropertyInfo(string name, SpvOp type, uint paddedSize, uint offset, SpvReflectArrayTraits arrayTraits, List<DescriptorPropertyInfo> children)
        {
            Name = name;
            Type = type;
            Offset = offset;
            PaddedSize = paddedSize;
            Members = [.. children];
            ArrayDimentions = arrayTraits.dims_count;
            ArrayDimentionSizes = new uint[arrayTraits.dims_count];
            for (uint i = 0; i < arrayTraits.dims_count; i++)
            {
                ArrayDimentionSizes[i] = arrayTraits.dims[i];
            }
        }

        public DescriptorPropertyInfo(string name, SpvOp type, uint paddedSize, uint offset, List<DescriptorPropertyInfo> members)
        {
            Name = name;
            Type = type;
            Offset = offset;
            PaddedSize = paddedSize;
            Members = [.. members];
        }

        public DescriptorPropertyInfo(string name, SpvOp type, uint offset, SpvReflectImageTraits imageTraits)
        {
            Name = name;

            Type = type;
            Offset = offset;
            ImageType = imageTraits.dim switch
            {
                SpvDim.Dim1D => VkImageType.Image1D,
                SpvDim.Dim2D => VkImageType.Image2D,
                SpvDim.Dim3D => VkImageType.Image3D,
                _ => throw new NotImplementedException(string.Format("Image dimention {0} is not implemented for descriptor variables", imageTraits.dim.ToString())),
            };

            if (imageTraits.arrayed != 0)
            {
                ImageArray = true;
            }

            if (imageTraits.depth != 0)
            {
                throw new NotImplementedException(string.Format("Image Depth = {0} unhandled", imageTraits.depth));
            }

            if(imageTraits.ms != 0)
            {
                throw new NotImplementedException(string.Format("Image ms = {0} unhandled", imageTraits.ms));
            }
        }
    }
}
