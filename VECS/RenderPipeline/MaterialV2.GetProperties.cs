using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace VECS
{
    public sealed partial class Material
    {
        public int GetInt(string property)
        {
            return ReadFromBuffer<int>(property);
        }

        public float GetFloat(string property)
        {
            return ReadFromBuffer<float>(property);
        }

        public Vector2 GetVector2(string property)
        {
            return ReadFromBuffer<Vector2>(property);
        }

        public Vector4 GetVector4(string property)
        {
            return ReadFromBuffer<Vector4>(property);
        }

        public Matrix3x2 GetMatrix3x2(string property)
        {
            return ReadFromBuffer<Matrix3x2>(property);
        }

        public Matrix4x4 GetMatrix4x4(string property)
        {
            return ReadFromBuffer<Matrix4x4>(property);
        }

        public T GetUniform<T>(string property) where T : unmanaged
        {
            return ReadFromBuffer<T>(property);
        }

        public float[] GetFloatArray(string property)
        {
            return ReadArrayFromBuffer<float>(property);
        }

        public Vector2[] GetVector2Array(string property)
        {
            return ReadArrayFromBuffer<Vector2>(property);
        }

        public Vector4[] GetVector4Array(string property)
        {
            return ReadArrayFromBuffer<Vector4>(property);
        }

        public Matrix3x2[] GetMatrix3x2Array(string property)
        {
            return ReadArrayFromBuffer<Matrix3x2>(property);
        }

        public Matrix4x4[] GetMatrix4x4Array(string property)
        {
            return ReadArrayFromBuffer<Matrix4x4>(property);
        }

        private unsafe T[] ReadArrayFromBuffer<T>(string property) where T : unmanaged
        {
            if (LookUpProperty(property, out var handler, out uint bindingIndex, out var propertyInfo))
            {
                return handler.ReadArrayFromBuffer<T>(bindingIndex, propertyInfo);
            }

            return default;
        }

        private T ReadFromBuffer<T>(string property) where T : unmanaged
        {
            if (LookUpProperty(property, out var handler, out var bindingIndex, out var propertyInfo))
            {
                return handler.ReadFromBuffer<T>(bindingIndex, propertyInfo);
            }
            return default;
        }

        public Span<T> GetStorageBuffer<T>(string property) where T : unmanaged
        {
            if (LookUpProperty(property, out var handler, out var bindingIndex, out var propertyInfo))
            {
                return handler.GetStorageBuffer<T>(bindingIndex, propertyInfo);
            }
            return default;
        }
    }
}
