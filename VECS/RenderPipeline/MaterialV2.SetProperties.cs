using System.Numerics;
using Vortice.Vulkan;

namespace VECS
{
    public sealed partial class MaterialV2
    {
        public void SetPushConstantInt(string property, int value)
        {
            WriteToPushConstantBuffer(property, value);
        }

        public void SetPushConstantFloat(string property, float value)
        {
            WriteToPushConstantBuffer(property, value);
        }

        public void SetPushConstantVector2(string property, Vector2 value)
        {
            WriteToPushConstantBuffer(property, value);
        }

        public void SetPushConstantVector4(string property, Vector4 value)
        {
            WriteToPushConstantBuffer(property, value);
        }

        public void SetPushConstantMatrix3x2(string property, Matrix3x2 value)
        {
            WriteToPushConstantBuffer(property, value);
        }

        public void SetPushConstantMatrix4x4(string property, Matrix4x4 value)
        {
            WriteToPushConstantBuffer(property, value);
        }

        public void SetPushConstantUniform<T>(string property, T value) where T : unmanaged
        {
            WriteToPushConstantBuffer(property, value);
        }

        private void WriteToPushConstantBuffer<T>(string property, T value) where T : unmanaged
        {
            for (int i = 0; i < _materialPushConstants.Length; i++)
            {
                if(_materialPushConstants[i].WriteToPushConstantBuffer(property, value))
                {
                    break;
                }
            }
        }

        public void SetInt(string property, int value)
        {
            WriteToBuffer(property, value);
        }

        public void SetFloat(string property, float value)
        {
            WriteToBuffer(property, value);
        }

        public void SetVector2(string property, Vector2 value)
        {
            WriteToBuffer(property, value);
        }

        public void SetVector4(string property, Vector4 value)
        {
            WriteToBuffer(property, value);
        }

        public void SetMatrix3x2(string property, Matrix3x2 value)
        {
            WriteToBuffer(property, value);
        }

        public void SetMatrix4x4(string property, Matrix4x4 value)
        {
            WriteToBuffer(property, value);
        }

        public void SetUniform<T>(string property, T value) where T : unmanaged
        {
            WriteToBuffer(property, value);
        }

        public void SetUniform<T>(string property, T value, int variant, int entity) where T : unmanaged
        {
            WriteToBuffer(property, value, variant, entity);
        }

        public void SetFloatArray(string property, float[] value)
        {
            WriteArrayToBuffer(property, value);
        }

        public void SetVector2Array(string property, Vector2[] value)
        {
            WriteArrayToBuffer(property, value);
        }

        public void SetVector4Array(string property, Vector4[] value)
        {
            WriteArrayToBuffer(property, value);
        }

        public void SetMatrix3x2Array(string property, Matrix3x2[] value)
        {
            WriteArrayToBuffer(property, value);
        }

        public void SetMatrix4x4Array(string property, Matrix4x4[] value)
        {
            WriteArrayToBuffer(property, value);
        }

        private unsafe void WriteArrayToBuffer<T>(string property, T[] array) where T : unmanaged
        {
            if(LookUpProperty(property, out var handler, out uint bindingIndex, out var propertyInfo))
            {
                handler.WriteArrayToBuffer(bindingIndex, propertyInfo, array);
            }
        }

        private void WriteToBuffer<T>(string property, T element) where T : unmanaged
        {
            if (LookUpProperty(property, out var handler, out var bindingIndex, out var propertyInfo))
            {
                handler.WriteToBuffer(bindingIndex, propertyInfo, element);
            }
        }

        private void WriteToBuffer<T>(string property, T element, int variant, int entity) where T : unmanaged
        {
            if (LookUpProperty(property, out var handler, out var bindingIndex, out var propertyInfo))
            {
                if(handler.DescriptorLevel == DescriptorLevel.Material)
                {
                    handler = handler.GetOrCreateChild(variant);
                }
                else if(handler.DescriptorLevel == DescriptorLevel.Entity)
                {
                    handler = handler.GetOrCreateChild(entity);
                }
                handler.WriteToBuffer(bindingIndex, propertyInfo, element);
            }
        }

        private bool LookUpProperty(string property, out DescriptorSetHandler handler, out uint bindingIndex, out DescriptorPropertyInfo propertyInfo)
        {
            for (int i = 0; i < _totalSets; i++)
            {
                handler = _allHandlers[i];
                if (handler != null && handler.LookUpProperty(property, out bindingIndex, out propertyInfo))
                {
                    return true;
                }
            }
            handler = null;
            bindingIndex = uint.MaxValue;
            propertyInfo = null;
            return false;
        }

        internal void SetStorageBufferUsageSize(string property, uint instanceSize)
        {
            if (LookUpProperty(property, out var handler, out var bindingIndex, out var propertyInfo))
            {
                handler.SetStorageBufferUsageSize(bindingIndex, propertyInfo, instanceSize);
            }
        }

        public void SetTexture(string property, Texture2d texture)
        {
            if(LookUpProperty(property, out var handler, out var bindingIndex, out var propertyInfo))
            {
                handler.SetTexture(bindingIndex,propertyInfo,texture);
            }
        }

        public void SetTexture(string property, Texture2d texture, int variant, int entity)
        {
            if (LookUpProperty(property, out var handler, out var bindingIndex, out var propertyInfo))
            {
                if(handler.DescriptorLevel == DescriptorLevel.Material)
                {
                    handler = handler.GetOrCreateChild(variant);
                }
                else if(handler.DescriptorLevel == DescriptorLevel.Entity)
                {
                    handler = handler.GetOrCreateChild(entity);
                }
                handler.SetTexture(bindingIndex, propertyInfo, texture);
            }
        }

        public void SetTextureArray(string property, Texture2d texture)
        {
            if(LookUpProperty(property,out var handler, out var bindingIndex,out var propertyInfo))
            {
                handler.SetTextureArray(bindingIndex, propertyInfo, texture);
            }
        }
    }
}
