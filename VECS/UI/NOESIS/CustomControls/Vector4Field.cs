using Noesis;
using System;
using  System.Numerics;
using Vector4 = System.Numerics.Vector4;
namespace VECS.UI
{
    public class Vector4Field : VECSEditorControl
    {

        public static readonly DependencyProperty _Label = DependencyProperty.Register("Label", typeof(string), typeof(Vector4Field), new PropertyMetadata("Label"));
        public static readonly DependencyProperty _ValueX = DependencyProperty.Register("ValueX", typeof(string), typeof(Vector4Field), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueY = DependencyProperty.Register("ValueY", typeof(string), typeof(Vector4Field), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueZ = DependencyProperty.Register("ValueZ", typeof(string), typeof(Vector4Field), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueW = DependencyProperty.Register("ValueW", typeof(string), typeof(Vector4Field), new PropertyMetadata("0"));

        public override string Label
        {
            get { return (string)GetValue(_Label); }
            set { SetValue(_Label, value); }
        }

        public string ValueX
        {
            get { return X; }
            set { SetValue(_ValueX, value); X = value; }
        }

        public string ValueY
        {
            get { return Y; }
            set { SetValue(_ValueY, value); Y = value; }
        }

        public string ValueZ
        {
            get { return Z; }
            set { SetValue(_ValueZ, value); Z = value; }
        }

        public string ValueW
        {
            get { return W; }
            set { SetValue(_ValueW, value); W = value; }
        }

        private string X;
        private string Y;
        private string Z;
        private string W;

        public Vector4Field()
        {
            InitializeComponent();

            var valueX = (TextBox)FindName("XComp");
            var valueY = (TextBox)FindName("YComp");
            var valueZ = (TextBox)FindName("ZComp");
            var valueW = (TextBox)FindName("WComp");

            WeakReference weak = new(this);

            valueX.TextChanged += (s, e) =>
            {
                var weakRef = (Vector4Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.X = ((TextBox)s).Text;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueY.TextChanged += (s, e) =>
            {
                var weakRef = (Vector4Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.Y = ((TextBox)s).Text;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueZ.TextChanged += (s, e) =>
            {
                var weakRef = (Vector4Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.Z = ((TextBox)s).Text;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueW.TextChanged += (s, e) =>
            {
                var weakRef = (Vector4Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.W = ((TextBox)s).Text;
                    weakRef.InternalValueChanged(s, e);
                }
            };
        }

        void InitializeComponent()
        {
            GUI.LoadComponent(this, "Editor/Vector4Field.xaml");
        }

        public override void SetValue(object value)
        {
            _internalSet = true;
            if(value is Vector4 vector4)
            {
                SetVector4(vector4);
            }
            else if(value is Vector4Int vector4Int)
            {
                SetVector4Int(vector4Int);
            }
            else if(value is Vector4UInt vector4UInt)
            {
                SetVector4Uint(vector4UInt);
            }
            else if(value is Quaternion quaternion)
            {
                SetQuaternion(quaternion);
            }
            _internalSet = false;
        }

        public override object TryParse(object currentValue)
        {
            if (currentValue is Vector4 vec4Val)
            {
                return GetVector4(vec4Val);
            }
            if (currentValue is Vector4Int vec4IntVal)
            {
                return GetVector4Int(vec4IntVal);
            }
            else if(currentValue is Vector4UInt vec4uintVal)
            {
                return GetVector4Uint(vec4uintVal);
            }
            else if(currentValue is Quaternion quaternion)
            {
                return GetQuaternion(quaternion);
            }
            else
            {
                return currentValue;
            }
        }

        public Vector4 GetVector4(Vector4 value)
        {
            value.X = float.TryParse(ValueX, out var valueOut) ? valueOut : value.X;
            value.Y = float.TryParse(ValueY, out valueOut) ? valueOut : value.Y;
            value.Z = float.TryParse(ValueZ, out valueOut) ? valueOut : value.Z;
            value.W = float.TryParse(ValueW, out valueOut) ? valueOut : value.W;
            return value;
        }

        public Vector4Int GetVector4Int(Vector4Int value)
        {
            value.X = int.TryParse(ValueX, out var valueOut) ? valueOut : value.X;
            value.Y = int.TryParse(ValueY, out valueOut) ? valueOut : value.Y;
            value.Z = int.TryParse(ValueZ, out valueOut) ? valueOut : value.Z;
            value.W = int.TryParse(ValueW, out valueOut) ? valueOut : value.W;
            return value;
        }
        public Vector4UInt GetVector4Uint(Vector4UInt value)
        {
            value.X = uint.TryParse(ValueX, out var valueOut) ? valueOut : value.X;
            value.Y = uint.TryParse(ValueY, out valueOut) ? valueOut : value.Y;
            value.Z = uint.TryParse(ValueZ, out valueOut) ? valueOut : value.Z;
            value.W = uint.TryParse(ValueW, out valueOut) ? valueOut : value.W;
            return value;
        }
        public Quaternion GetQuaternion(Quaternion value)
        {
            value.X = float.TryParse(ValueX, out var valueOut) ? valueOut : value.X;
            value.Y = float.TryParse(ValueY, out valueOut) ? valueOut : value.Y;
            value.Z = float.TryParse(ValueZ, out valueOut) ? valueOut : value.Z;
            value.W = float.TryParse(ValueW, out valueOut) ? valueOut : value.W;
            return value;
        }
        public void SetVector4(Vector4 value)
        {
            ValueX = value.X.ToString();
            ValueY = value.Y.ToString();
            ValueZ = value.Z.ToString();
            ValueW = value.W.ToString();
        }
        public void SetVector4Int(Vector4Int value)
        {
            ValueX = value.X.ToString();
            ValueY = value.Y.ToString();
            ValueZ = value.Z.ToString();
            ValueW = value.W.ToString();
        }
        public void SetVector4Uint(Vector4UInt value)
        {
            ValueX = value.X.ToString();
            ValueY = value.Y.ToString();
            ValueZ = value.Z.ToString();
            ValueW = value.W.ToString();
        }
        public void SetQuaternion(Quaternion value)
        {
            ValueX = value.X.ToString();
            ValueY = value.Y.ToString();
            ValueZ = value.Z.ToString();
            ValueW = value.W.ToString();
        }

    }
}
