using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace VECS
{
    public static class ShaderProperties
    {
        private static readonly ConcurrentDictionary<int, string> _propertyIdToString = new();

#if DEBUG
        public const bool LOG_MISSING_GLOBAL_SHADER_PROPERTIES = false;
#endif
        public static readonly int CameraInfoId = "cameraInfo".GetShaderPropertyId();
        public static readonly int CameraInverseId = "cameraInverse".GetShaderPropertyId();
        public static readonly int AdditionalCameraInfoId = "cameraPlanes".GetShaderPropertyId();
        public static readonly int OrthographicInfoId = "orthographic".GetShaderPropertyId();

        public static readonly int LightingInfoId = "lighting".GetShaderPropertyId();
        public static readonly int PointLightsBufferId = "pointLightBuffer".GetShaderPropertyId();
        public static readonly int SpotLightsBufferId = "spotLightBuffer".GetShaderPropertyId();

        public static readonly int BoundsBufferId = "boundsBuffer".GetShaderPropertyId();
        public static readonly int MatricesBufferId = "matricesBuffer".GetShaderPropertyId();
        public static readonly int ColourBufferId = "colourBuffer".GetShaderPropertyId();

        public static readonly int GeometrySBOId = "geometrySBO".GetShaderPropertyId();
        public static readonly int LinkedListSBOId = "linkedListSBO".GetShaderPropertyId();
        public static readonly int HeadIndexImageId = "headIndexImage".GetShaderPropertyId();

        public static readonly int DirShadowImageId = "dirShadow".GetShaderPropertyId();
        public static readonly int PLShadowImageId = "plShadow".GetShaderPropertyId();
        public static readonly int SLShadowImageId = "slShadow".GetShaderPropertyId();

        public static readonly HashSet<int> IgnoreUnFoundShaderProperties;

        private static readonly Dictionary<int, uint> ImageBindingArrayCounts;

        static ShaderProperties()
        {
            IgnoreUnFoundShaderProperties =
            [
                CameraInfoId,
                CameraInverseId,
                AdditionalCameraInfoId,
                OrthographicInfoId,
                LightingInfoId,
                PointLightsBufferId,
                SpotLightsBufferId,
                BoundsBufferId,
                MatricesBufferId,
                GeometrySBOId,
                DirShadowImageId,
                PLShadowImageId,
                SLShadowImageId,
                ColourBufferId
            ];

            ImageBindingArrayCounts = new()
            {
                { PLShadowImageId, PointLightShadows.MAX_POINT_LIGHT_SHADOW_CASTERS },
                { SLShadowImageId, SpotLightShadows.MAX_SPOT_LIGHT_SHADOW_CASTERS }
            };
        }

        public static readonly ShaderProperty Invalid = new()
        {
            SetIndex = uint.MaxValue,
            BindPoint = uint.MaxValue,
            BindingInfo = null,
            Property = null
        };

        public static int GetShaderPropertyId(this string property)
        {
            
            var hash = Hash(property);
            _propertyIdToString.TryAdd(hash, property);
            return hash;
        }

        public static string GetPropertyIdString(this int propertyId)
        {
            if(_propertyIdToString.TryGetValue(propertyId,out string value))
                return value;
            return "Property Not Found";
        }


        public static unsafe int Hash(string text)
        {
            var memory = text.AsMemory();
            var pinned = memory.Pin();
            var result = Hash((byte*)pinned.Pointer, sizeof(char) * (uint)memory.Length);
            pinned.Dispose();
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int Hash(byte* key, ulong length)
        {
            ulong i = 0;
            uint hash = 0;
            while (i != length)
            {
                hash += key[i++];
                hash += hash << 10;
                hash ^= hash >> 6;
            }
            hash += hash << 3;
            hash ^= hash >> 11;
            hash += hash << 15;
            return NumericsExtensions.asint(hash);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint GetImageBindingArraySize(this DescriptorBinding binding)
        {
            return GetImageBindingArraySize(binding.Id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint GetImageBindingArraySize(int property)
        {
            if (ImageBindingArrayCounts.TryGetValue(property, out var count))
            {
                return count;
            }
            return 0;
        }
    }
    

    public struct ShaderProperty
    {
        public uint SetIndex;
        public uint BindPoint;
        public DescriptorBinding BindingInfo;
        public DescriptorPropertyInfo Property;

        public ShaderProperty(DescriptorBinding bindingInfo, DescriptorPropertyInfo propertyInfo)
        {
            BindingInfo = bindingInfo;
            Property = propertyInfo;
            SetIndex = bindingInfo.DescriptorSetIndex;
            BindPoint = bindingInfo.BindPoint;
        }

        public static bool operator ==(ShaderProperty a, ShaderProperty b)
        {
            return a.SetIndex == b.SetIndex && a.BindPoint == b.BindPoint && a.BindingInfo == b.BindingInfo && a.Property == b.Property;
        }

        public static bool operator !=(ShaderProperty a, ShaderProperty b)
        {
            return !(a == b);
        }

        public readonly override bool Equals(object obj)
        {
            if(obj is ShaderProperty propertyInfo)
            {
                return this == propertyInfo;
            }
            return false;
        }

        public readonly override int GetHashCode()
        {
            return HashCode.Combine(SetIndex, BindPoint, BindingInfo.GetHashCode(), Property.GetHashCode());
        }
    }
}
