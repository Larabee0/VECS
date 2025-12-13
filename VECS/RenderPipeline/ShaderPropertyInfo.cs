using System;
using System.Collections.Generic;

namespace VECS
{
    public struct ShaderPropertyInfo
    {
#if DEBUG
        public const bool LOG_MISSING_GLOBAL_SHADER_PROPERTIES = false;
#endif
        public static readonly int CameraInfoProperty = "cameraMain".GetHashCode();
        public static readonly int CameraInverseProperty = "cameraInverse".GetHashCode();
        public static readonly int AdditionalCameraInfoProperty = "cameraPlanes".GetHashCode();
        public static readonly int OrthographicInfoProperty = "orthographic".GetHashCode();
        public static readonly int LightingInfoProperty = "lighting".GetHashCode();
        public static readonly int PointLightsBufferProperty = "pointLightBuffer".GetHashCode();
        public static readonly HashSet<int> GlobalProperties;

        static ShaderPropertyInfo()
        {
            Console.WriteLine("GlobalProperty | cameraMain: {0}", CameraInfoProperty);
            Console.WriteLine("GlobalProperty | cameraInverse: {0}", CameraInverseProperty);
            Console.WriteLine("GlobalProperty | cameraPlanes: {0}", AdditionalCameraInfoProperty);
            Console.WriteLine("GlobalProperty | orthographic: {0}", OrthographicInfoProperty);
            Console.WriteLine("GlobalProperty | lighting: {0}", LightingInfoProperty);
            Console.WriteLine("GlobalProperty | pointLightBuffer: {0}", PointLightsBufferProperty);
            GlobalProperties =
            [
                CameraInfoProperty,
                CameraInverseProperty,
                AdditionalCameraInfoProperty,
                OrthographicInfoProperty,
                LightingInfoProperty,
                PointLightsBufferProperty,
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
