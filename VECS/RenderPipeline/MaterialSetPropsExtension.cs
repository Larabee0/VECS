using System;
using System.Numerics;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace VECS
{
    public static class MaterialSetPropsExtension
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetInt(this Material material, int propertyId, int variant, int value)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.WriteToBuffer((uint)variant, propertyInfo, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetFloat(this Material material, int propertyId, int variant, float value)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.WriteToBuffer((uint)variant, propertyInfo, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetVector2(this Material material, int propertyId, int variant, Vector2 value)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.WriteToBuffer((uint)variant, propertyInfo, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetVector4(this Material material, int propertyId, int variant, Vector4 value)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.WriteToBuffer((uint)variant, propertyInfo, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetMatrix3x2(this Material material, int propertyId, int variant, Matrix3x2 value)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.WriteToBuffer((uint)variant, propertyInfo, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetMatrix3x3(this Material material, int propertyId, int variant, Matrix3x3 value)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.WriteToBuffer((uint)variant, propertyInfo, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetMatrix4x4(this Material material, int propertyId, int variant, Matrix4x4 value)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.WriteToBuffer((uint)variant, propertyInfo, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetUniform<T>(this Material material, int propertyId, int variant, T value) where T : unmanaged
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.WriteToBuffer((uint)variant, propertyInfo, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetFloatArray(this Material material, int propertyId, int variant, float[] value)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.WriteArrayToBuffer((uint)variant, propertyInfo, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetVector2Array(this Material material, int propertyId, int variant, Vector2[] value)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.WriteArrayToBuffer((uint)variant, propertyInfo, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetVector4Array(this Material material, int propertyId, int variant, Vector4[] value)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.WriteArrayToBuffer((uint)variant, propertyInfo, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetMatrix3x2Array(this Material material, int propertyId, int variant, Matrix3x2[] value)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.WriteArrayToBuffer((uint)variant, propertyInfo, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetMatrix3x3Array(this Material material, int propertyId, int variant, Matrix3x3[] value)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.WriteArrayToBuffer((uint)variant, propertyInfo, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetMatrix4x4Array(this Material material, int propertyId, int variant, Matrix4x4[] value)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.WriteArrayToBuffer((uint)variant, propertyInfo, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void WriteArrayToBuffer<T>(this Material material, int propertyId, int variant, T[] values) where T : unmanaged
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.WriteArrayToBuffer((uint)variant, propertyInfo, values);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteToBuffer<T>(this Material material, int propertyId, int variant, T element) where T : unmanaged
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.WriteToBuffer((uint)variant, propertyInfo, element);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetTexture(this Material material, int propertyId, int variant, Texture2D texture)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.SetTexture(propertyInfo, variant, texture);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetTextureArray(this Material material, int propertyId, int variant, Texture2DArray texture)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.SetTexture(propertyInfo, variant, texture);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetCubeMap(this Material material, int propertyId, int variant, Cubemap cubemap)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.SetTexture(propertyInfo, variant, cubemap);
            }
        }

        public static void SetGlobalUniforms(this Material material, int variant, RendererFrameInfo frameInfo)
        {
            material.TryCreateVariant((uint)variant);
            WriteToBuffer(material, ShaderPropertyInfo.CameraInfoProperty, variant, frameInfo.CameraInfo);
            WriteToBuffer(material, ShaderPropertyInfo.CameraInverseProperty, variant, frameInfo.CameraInverseInfo);
            WriteToBuffer(material, ShaderPropertyInfo.AdditionalCameraInfoProperty, variant, frameInfo.AdditionalCameraInfo);
            WriteToBuffer(material, ShaderPropertyInfo.OrthographicInfoProperty, variant, frameInfo.OrthographicInfo);
            WriteToBuffer(material, ShaderPropertyInfo.LightingInfoProperty, variant, frameInfo.LightingInfo);
            if (material.LookUpProperty(ShaderPropertyInfo.PointLightsBufferProperty, out _))
            {
                var pointLights = material.GetStorageBuffer<PointLightUniform>(ShaderPropertyInfo.PointLightsBufferProperty);
                frameInfo.PointLights.CopyTo(pointLights);
                material._matVariants[variant].SetStorageBufferLength(0, (uint)frameInfo.PointLights.Length);
            }
        }

    }
}
