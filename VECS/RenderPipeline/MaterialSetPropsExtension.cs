using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace VECS
{
    public static class MaterialSetPropsExtension
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetInt(this Material material, int propertyId, int value)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.WriteToBuffer(propertyInfo, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetUint(this Material material, int propertyId, uint value)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.WriteToBuffer(propertyInfo, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetFloat(this Material material, int propertyId, float value)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.WriteToBuffer(propertyInfo, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetVector2(this Material material, int propertyId, Vector2 value)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.WriteToBuffer(propertyInfo, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetVector4(this Material material, int propertyId, Vector4 value)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.WriteToBuffer(propertyInfo, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetMatrix3x2(this Material material, int propertyId, Matrix3x2 value)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.WriteToBuffer(propertyInfo, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetMatrix3x3(this Material material, int propertyId, Matrix3x3 value)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.WriteToBuffer(propertyInfo, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetMatrix4x4(this Material material, int propertyId, Matrix4x4 value)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.WriteToBuffer(propertyInfo, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetFloatArray(this Material material, int propertyId, Span<float> value)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.WriteArrayToBuffer( propertyInfo, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetVector2Array(this Material material, int propertyId, Span<Vector2> value)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.WriteArrayToBuffer( propertyInfo, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetVector4Array(this Material material, int propertyId, Span<Vector4> value)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.WriteArrayToBuffer(propertyInfo, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetMatrix3x2Array(this Material material, int propertyId, Span<Matrix3x2> value)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.WriteArrayToBuffer(propertyInfo, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetMatrix3x3Array(this Material material, int propertyId, Span<Matrix3x3> value)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.WriteArrayToBuffer(propertyInfo, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetMatrix4x4Array(this Material material, int propertyId, Span<Matrix4x4> value)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.WriteArrayToBuffer(propertyInfo, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetArray<T>(this Material material, int propertyId, Span<T> value) where T : unmanaged
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.WriteArrayToBuffer(propertyInfo, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void WriteArrayToBuffer<T>(this Material material, ShaderPropertyInfo propertyInfo, Span<T> values) where T : unmanaged
        {
            material.ShaderSet.WriteArrayToBuffer(material.pUniformBuffer, propertyInfo, values);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void WriteToBuffer<T>(this Material material, int propertyId, T element) where T : unmanaged
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.ShaderSet.WriteToUniformBuffer(material.pUniformBuffer, propertyInfo, element);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void WriteToBuffer<T>(this Material material, ShaderPropertyInfo propertyInfo, T element) where T : unmanaged
        {
            material.ShaderSet.WriteToUniformBuffer(material.pUniformBuffer, propertyInfo, element);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteToBuffer<T>(this ShaderSet shaders, int propertyId, uint variant, T element) where T : unmanaged
        {
            if (shaders.LookUpProperty(propertyId, out var propertyInfo))
            {
                shaders.WriteToUniformBuffer(variant, propertyInfo, element);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteToBufferArray<T>(this ShaderSet shaders, int propertyId, uint variant, Span<T> values) where T : unmanaged
        {
            if (shaders.LookUpProperty(propertyId, out var propertyInfo))
            {
                shaders.WriteArrayToBuffer(variant, propertyInfo, values);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool LookUpProperty(this Material material, int propertyId, out ShaderPropertyInfo propertyInfo)
        {
            return material.ShaderSet.LookUpProperty(propertyId, out propertyInfo);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetTexture(this Material material, ShaderPropertyInfo propertyInfo, Texture texture)
        {
            material.SetTexture(propertyInfo.SetIndex, propertyInfo.BindPoint, texture);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetTexture(this ShaderSet shaders, int propertyId, uint variant, Texture texture)
        {
            if(shaders.LookUpProperty(propertyId, out ShaderPropertyInfo shaderPropertyInfo))
            {
                shaders.SetTexture(shaderPropertyInfo, variant, texture);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetTexture(this Material material, int propertyId,Texture2D texture)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.SetTexture(propertyInfo, texture);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetTextureArray(this Material material, int propertyId, Texture2DArray texture)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.SetTexture(propertyInfo, texture);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetCubeMap(this Material material, int propertyId, Cubemap cubemap)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.SetTexture(propertyInfo, cubemap);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetCubeMapArray(this Material material, int propertyId, CubemapArray cubemap)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.SetTexture(propertyInfo, cubemap);
            }
        }

        internal unsafe static void SetGlobalUniforms(this ShaderSet shaders, uint variant, in RendererFrameInfo frameInfo)
        {
            WriteToBuffer(shaders, ShaderPropertyInfo.LightingInfoId, variant, frameInfo.LightingInfo);

            if (variant != 0) return;
            uint camreaCount = (uint)frameInfo.CameraCount;
            SetUniformBuffer(shaders, ShaderPropertyInfo.CameraInfoId, frameInfo.CameraInfo, camreaCount);
            SetUniformBuffer(shaders, ShaderPropertyInfo.CameraInverseId, frameInfo.CameraInverseInfo, camreaCount);
            SetUniformBuffer(shaders, ShaderPropertyInfo.AdditionalCameraInfoId, frameInfo.AdditionalCameraInfo, camreaCount);
            SetUniformBuffer(shaders, ShaderPropertyInfo.OrthographicInfoId, frameInfo.OrthographicInfo, camreaCount);

            SetUniformBuffer(shaders, ShaderPropertyInfo.PointLightsBufferId,  frameInfo.PointLights, (uint)frameInfo.LightingInfo.NumPointLights);
            SetUniformBuffer(shaders, ShaderPropertyInfo.SpotLightsBufferId,  frameInfo.SpotLights, (uint)frameInfo.LightingInfo.NumSpotLights);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetUniformBuffer<T>(this ShaderSet shaders, int bufferProperyId, T resource, uint count) where T : unmanaged
        {
            if (!shaders.LookUpProperty(bufferProperyId, out var propertyInfo)) return;
            var buffer = shaders.GetStorageSwapChainBuffer(bufferProperyId);
            unsafe
            {
                Buffer.MemoryCopy(&resource, buffer.HostPtr, buffer.HostBufferSize32, sizeof(T));
            }
            shaders.SetDescriptorStorageBufferLength(propertyInfo.SetIndex,  propertyInfo.BindPoint, count);
        }
    }
}
