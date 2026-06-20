using Noesis;
using  System.Numerics;
using Vector4 = System.Numerics.Vector4;
namespace VECS.UI
{
    public class Vector4Field : UserControl
    {

        public static readonly DependencyProperty _Label = DependencyProperty.Register("Label", typeof(string), typeof(TextBlock), new PropertyMetadata("Label"));
        public static readonly DependencyProperty _ValueX = DependencyProperty.Register("ValueX", typeof(string), typeof(TextBox), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueY = DependencyProperty.Register("ValueY", typeof(string), typeof(TextBox), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueZ = DependencyProperty.Register("ValueZ", typeof(string), typeof(TextBox), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueW = DependencyProperty.Register("ValueW", typeof(string), typeof(TextBox), new PropertyMetadata("0"));

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

        public string ValueY
        {
            get { return (string)GetValue(_ValueY); }
            set { SetValue(_ValueY, value); }
        }

        public string ValueZ
        {
            get { return (string)GetValue(_ValueZ); }
            set { SetValue(_ValueZ, value); }
        }

        public string ValueW
        {
            get { return (string)GetValue(_ValueW); }
            set { SetValue(_ValueW, value); }
        }

        public Vector4Field()
        {
            InitializeComponent();
        }

        void InitializeComponent()
        {
            GUI.LoadComponent(this, "Editor/Vector4Field.xaml");
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
