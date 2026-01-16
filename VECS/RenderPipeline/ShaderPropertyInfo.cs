using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace VECS
{
    public static class ShaderPropertyExtention
    {
        private static readonly ConcurrentDictionary<int, string> _propertyIdToString = new();

        public static int GetShaderPropertyId(this string property)
        {
            var hash = property.GetHashCode();
            _propertyIdToString.TryAdd(hash, property);
            return hash;
        }

        public static string GetPropertyIdString(this int propertyId)
        {
            if(_propertyIdToString.TryGetValue(propertyId,out string value))
                return value;
            return "Property Not Found";
        }
    }

    public struct ShaderPropertyInfo
    {
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

        static ShaderPropertyInfo()
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
        }

        public static readonly ShaderPropertyInfo Invalid = new()
        {
            SetIndex = uint.MaxValue,
            BindPoint = uint.MaxValue,
            BindingInfo = null,
            Property = null
        };

        public uint SetIndex;
        public uint BindPoint;
        public DescriptorBinding BindingInfo;
        public DescriptorPropertyInfo Property;

        public ShaderPropertyInfo(DescriptorBinding bindingInfo, DescriptorPropertyInfo propertyInfo)
        {
            BindingInfo = bindingInfo;
            Property = propertyInfo;
            SetIndex = bindingInfo.DescriptorSetIndex;
            BindPoint = bindingInfo.BindPoint;
        }

        public static bool operator ==(ShaderPropertyInfo a, ShaderPropertyInfo b)
        {
            return a.SetIndex == b.SetIndex && a.BindPoint == b.BindPoint && a.BindingInfo == b.BindingInfo && a.Property == b.Property;
        }

        public static bool operator !=(ShaderPropertyInfo a, ShaderPropertyInfo b)
        {
            return !(a == b);
        }

        public readonly override bool Equals(object obj)
        {
            if(obj is ShaderPropertyInfo propertyInfo)
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
