using Noesis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VECS.UI
{
    public class Vector1Field : UserControl
    {

        public static readonly DependencyProperty _Label = DependencyProperty.Register("LabelV1", typeof(string), typeof(TextBlock), new PropertyMetadata("Label"));
        public static readonly DependencyProperty _ValueX = DependencyProperty.Register("ValueXV1", typeof(string), typeof(TextBox), new PropertyMetadata("0"));

        public string Label
        {
            get { return (string)GetValue(_Label); }
            set { SetValue(_Label, value); }
        }

        public string ValueX
        {
            get { return (string)GetValue(_ValueX); }
            set { SetValue(_ValueX, value); }
        }

        public Vector1Field()
        {
            InitializeComponent();
        }

        void InitializeComponent()
        {
            GUI.LoadComponent(this, "Editor/Vector1Field.xaml");
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
