using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace VECS
{
    public sealed partial class MaterialV2
    {
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
            for (int i = 0; i < _totalSets; i++)
            {
                var handler = _allHandlers[i];
                if (handler.LookUpProperty(property,out uint bindingIndex, out DescriptorPropertyInfo propertyInfo))
                {
                    handler.WriteArrayToBuffer(bindingIndex, propertyInfo, array);
                    break;
                }
            }
        }

        private void WriteToBuffer<T>(string property, T element) where T : unmanaged
        {
            for (int i = 0; i < _totalSets; i++)
            {
                var handler = _allHandlers[i];
                if (handler.LookUpProperty(property, out uint bindingIndex, out DescriptorPropertyInfo propertyInfo))
                {
                    handler.WriteToBuffer(bindingIndex, propertyInfo, element);
                    break;
                }
            }
        }
    }
}
