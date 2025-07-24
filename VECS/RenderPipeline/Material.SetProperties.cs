using System.Numerics;
using System.Runtime.CompilerServices;

namespace VECS
{
    public sealed partial class Material
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPushConstantInt(string property, int value)
        {
            _materialPushConstantsHandler.SetPushConstantInt(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPushConstantFloat(string property, float value)
        {
            _materialPushConstantsHandler.SetPushConstantFloat(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPushConstantVector2(string property, Vector2 value)
        {
            _materialPushConstantsHandler.SetPushConstantVector2(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPushConstantVector4(string property, Vector4 value)
        {
            _materialPushConstantsHandler.SetPushConstantVector4(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPushConstantMatrix3x2(string property, Matrix3x2 value)
        {
            _materialPushConstantsHandler.SetPushConstantMatrix3x2(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPushConstantMatrix4x4(string property, Matrix4x4 value)
        {
            _materialPushConstantsHandler.SetPushConstantMatrix4x4(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPushConstantUniform<T>(string property, T value) where T : unmanaged
        {
            _materialPushConstantsHandler.SetPushConstantUniform(property, value);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetInt(string property, int value)
        {
            WriteToBuffer(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetFloat(string property, float value)
        {
            WriteToBuffer(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVector2(string property, Vector2 value)
        {
            WriteToBuffer(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVector4(string property, Vector4 value)
        {
            WriteToBuffer(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetMatrix3x2(string property, Matrix3x2 value)
        {
            WriteToBuffer(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetMatrix4x4(string property, Matrix4x4 value)
        {
            WriteToBuffer(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetUniform<T>(string property, T value) where T : unmanaged
        {
            WriteToBuffer(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetUniform<T>(string property, T value, int variant, int entity) where T : unmanaged
        {
            WriteToBuffer(property, value, variant, entity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetFloatArray(string property, float[] value)
        {
            WriteArrayToBuffer(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVector2Array(string property, Vector2[] value)
        {
            WriteArrayToBuffer(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVector4Array(string property, Vector4[] value)
        {
            WriteArrayToBuffer(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetMatrix3x2Array(string property, Matrix3x2[] value)
        {
            WriteArrayToBuffer(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetMatrix4x4Array(string property, Matrix4x4[] value)
        {
            WriteArrayToBuffer(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void WriteArrayToBuffer<T>(string property, T[] array) where T : unmanaged
        {
            if (LookUpProperty(property, out var handler, out uint bindingIndex, out var propertyInfo))
            {
                handler.WriteArrayToBuffer(bindingIndex, propertyInfo, array);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteToBuffer<T>(string property, T element) where T : unmanaged
        {
            if (LookUpProperty(property, out var handler, out var bindingIndex, out var propertyInfo))
            {
                handler.WriteToBuffer(bindingIndex, propertyInfo, element);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteToBuffer<T>(string property, T element, int variant, int entity) where T : unmanaged
        {
            if (LookUpProperty(property, out var handler, out var bindingIndex, out var propertyInfo))
            {
                if (handler.DescriptorLevel == DescriptorLevel.Material)
                {
                    handler = handler.GetOrCreateChild(variant);
                }
                else if (handler.DescriptorLevel == DescriptorLevel.Entity)
                {
                    handler = handler.GetOrCreateChild(entity);
                }
                handler.WriteToBuffer(bindingIndex, propertyInfo, element);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetStorageBufferUsageSize(string property, uint instanceSize)
        {
            if (LookUpProperty(property, out var handler, out var bindingIndex, out var propertyInfo))
            {
                handler.SetStorageBufferUsageSize(bindingIndex, propertyInfo, instanceSize);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetTexture(string property, Texture2D texture)
        {
            if (LookUpProperty(property, out var handler, out var bindingIndex, out var propertyInfo))
            {
                handler.SetTexture(bindingIndex, propertyInfo, texture);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetTexture(string property, Texture2D texture, int variant, int entity)
        {
            if (LookUpProperty(property, out var handler, out var bindingIndex, out var propertyInfo))
            {
                if (handler.DescriptorLevel == DescriptorLevel.Material)
                {
                    handler = handler.GetOrCreateChild(variant);
                }
                else if (handler.DescriptorLevel == DescriptorLevel.Entity)
                {
                    handler = handler.GetOrCreateChild(entity);
                }
                handler.SetTexture(bindingIndex, propertyInfo, texture);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetTextureArray(string property, Texture2DArray texture)
        {
            if (LookUpProperty(property, out var handler, out var bindingIndex, out var propertyInfo))
            {
                handler.SetTextureArray(bindingIndex, propertyInfo, texture);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetCubeMap(string property, Cubemap cubemap)
        {
            if (LookUpProperty(property, out var handler, out var bindingIndex, out var propertyInfo))
            {
                handler.SetCubeMap(bindingIndex, propertyInfo, cubemap);
            }
        }
    }
}
