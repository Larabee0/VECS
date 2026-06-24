using Noesis;

namespace VECS.UI
{
    public class Vector1Field : UserControl, IEditorField
    {

        public static readonly DependencyProperty _LabelV1 = DependencyProperty.Register("Label", typeof(string), typeof(Vector1Field), new PropertyMetadata("Label"));
        public static readonly DependencyProperty _ValueXV1 = DependencyProperty.Register("ValueX", typeof(string), typeof(Vector1Field), new PropertyMetadata("0"));

        public string Label
        {
            get { return (string)GetValue(_LabelV1); }
            set { SetValue(_LabelV1, value); }
        }

        public string ValueX
        {
            get { return (string)GetValue(_ValueXV1); }
            set { SetValue(_ValueXV1, value); }
        }

        public Vector1Field()
        {
            InitializeComponent();
        }

        void InitializeComponent()
        {
            GUI.LoadComponent(this, "Editor/Vector1Field.xaml");
        }

        public void SetValue(object value)
        {
            ValueX = value.ToString();
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
