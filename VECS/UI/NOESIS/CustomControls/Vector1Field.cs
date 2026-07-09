using Noesis;
using System;

namespace VECS.UI
{
    public class Vector1Field : VECSEditorControl
    {

        public static readonly DependencyProperty _LabelV1 = DependencyProperty.Register("Label", typeof(string), typeof(Vector1Field), new PropertyMetadata("Label"));
        public static readonly DependencyProperty _ValueXV1 = DependencyProperty.Register("ValueX", typeof(string), typeof(Vector1Field), new PropertyMetadata("0"));

        public override string Label
        {
            get { return (string)GetValue(_LabelV1); }
            set { SetValue(_LabelV1, value); }
        }

        public string ValueX
        {
            get { return X; }
            set { SetValue(_ValueXV1, value); X = value; }
        }

        private string X;

        public Vector1Field()
        {
            InitializeComponent();
            WeakReference weak = new(this);
            var valueX = (TextBox)FindName("XComp");
            valueX.TextChanged += (s, e) =>
            {
                var weakRef = (Vector1Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.X = ((TextBox)s).Text;
                    weakRef.InternalValueChanged(s, e);
                }
            };
        }

        void InitializeComponent()
        {
            GUI.LoadComponent(this, "Editor/Vector1Field.xaml");
        }

        public override void SetValue(object value)
        {
            _internalSet = true;
            ValueX = value.ToString();
            _internalSet = false;
        }

        public override object TryParse(object currentValue)
        {
            if(currentValue is float valueFloat)
            {
                return GetFloat(valueFloat);
            }
            else if (currentValue is double valueDouble)
            {
                return GetDouble(valueDouble);
            }
            else if (currentValue is decimal valueDecimal)
            {
                return GetDecimal(valueDecimal);
            }
            else if (currentValue is sbyte valueSByte)
            {
                return GetSbyte(valueSByte);
            }
            else if (currentValue is byte valueByte)
            {
                return GetByte(valueByte);
            }
            else if (currentValue is short valueShort)
            {
                return GetShort(valueShort);
            }
            else if (currentValue is ushort valueUshort)
            {
                return GetUshort(valueUshort);
            }
            else if (currentValue is int valueInt)
            {
                return GetInt(valueInt);
            }
            else if (currentValue is uint valueUint)
            {
                return GetUint(valueUint);
            }
            else if (currentValue is long valueLong)
            {
                return GetLong(valueLong);
            }
            else if (currentValue is ulong valueUlong)
            {
                return GetUlong(valueUlong);
            }
            else
            {
                return currentValue;
            }
        }

#region Floating Point Types
        public float GetFloat(float value)
        {
            return float.TryParse(ValueX, out var valueOut) ? valueOut : value;
        }

        public double GetDouble(double value)
        {
            return double.TryParse(ValueX, out var valueOut) ? valueOut : value;
        }

        public decimal GetDecimal(decimal value)
        {
            return decimal.TryParse(ValueX, out var valueOut) ? valueOut : value;
        }

        public void SetFloat(float value)
        {
            ValueX = value.ToString();
        }

        public void SetDouble(double value)
        {
            ValueX = value.ToString();
        }

        public void SetDecimal(decimal value)
        {
            ValueX = value.ToString();
        }
#endregion
        
        #region  Intergral Types
        public sbyte GetSbyte(sbyte value)
        {   
            return sbyte.TryParse(ValueX, out var valueOut) ? valueOut : value;
        }

        public byte GetByte(byte value)
        {   
            return byte.TryParse(ValueX, out var valueOut) ? valueOut : value;
        }
        
        public short GetShort(short value)
        {   
            return short.TryParse(ValueX, out var valueOut) ? valueOut : value;
        }
        
        public ushort GetUshort(ushort value)
        {   
            return ushort.TryParse(ValueX, out var valueOut) ? valueOut : value;
        }
        
        public int GetInt(int value)
        {   
            return int.TryParse(ValueX, out var valueOut) ? valueOut : value;
        }

        public uint GetUint(uint value)
        {   
            return uint.TryParse(ValueX, out var valueOut) ? valueOut : value;
        }

        public long GetLong(long value)
        {   
            return long.TryParse(ValueX, out var valueOut) ? valueOut : value;
        }

        public ulong GetUlong(ulong value)
        {   
            return ulong.TryParse(ValueX, out var valueOut) ? valueOut : value;
        }

        #endregion
    }
}
