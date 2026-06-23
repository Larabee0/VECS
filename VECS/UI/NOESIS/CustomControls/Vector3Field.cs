using Noesis;
using System.Numerics;

namespace VECS.UI
{
    public class Vector3Field : UserControl, IEditorField
    {

        public static readonly DependencyProperty _Label = DependencyProperty.Register("Label", typeof(string), typeof(Vector3Field), new PropertyMetadata("Label"));
        public static readonly DependencyProperty _ValueX = DependencyProperty.Register("ValueX", typeof(string), typeof(Vector3Field), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueY = DependencyProperty.Register("ValueY", typeof(string), typeof(Vector3Field), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueZ = DependencyProperty.Register("ValueZ", typeof(string), typeof(Vector3Field), new PropertyMetadata("0"));

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

        public Vector3Field()
        {
            InitializeComponent();
        }

        void InitializeComponent()
        {
            GUI.LoadComponent(this, "Editor/Vector3Field.xaml");
        }

        public void SetValue(object value)
        {
            if(value is Vector3 vector3)
            {
                SetVector3(vector3);
            }
            else if(value is Vector3Int vector3Int)
            {
                SetVector3Int(vector3Int);
            }
            else if(value is Vector3UInt vector3UInt)
            {
                SetVector3Uint(vector3UInt);
            }
        }

        public Vector3 GetVector3(Vector3 value)
        {
            value.X = float.TryParse(ValueX, out var valueOut) ? valueOut : value.X;
            value.Y = float.TryParse(ValueY, out valueOut) ? valueOut : value.Y;
            value.Z = float.TryParse(ValueZ, out valueOut) ? valueOut : value.Z;
            return value;
        }

        public Vector3Int GetVector3Int(Vector3Int value)
        {
            value.X = int.TryParse(ValueX, out var valueOut) ? valueOut : value.X;
            value.Y = int.TryParse(ValueY, out valueOut) ? valueOut : value.Y;
            value.Z = int.TryParse(ValueZ, out valueOut) ? valueOut : value.Z;
            return value;
        }

        public Vector3UInt GetVector3Uint(Vector3UInt value)
        {
            value.X = uint.TryParse(ValueX, out var valueOut) ? valueOut : value.X;
            value.Y = uint.TryParse(ValueY, out valueOut) ? valueOut : value.Y;
            value.Z = uint.TryParse(ValueZ, out valueOut) ? valueOut : value.Z;
            return value;
        }

        public void SetVector3(Vector3 value)
        {
            ValueX = value.X.ToString();
            ValueY = value.Y.ToString();
            ValueZ = value.Z.ToString();
        }

        public void SetVector3Int(Vector3Int value)
        {
            ValueX = value.X.ToString();
            ValueY = value.Y.ToString();
            ValueZ = value.Z.ToString();
        }

        public void SetVector3Uint(Vector3UInt value)
        {
            ValueX = value.X.ToString();
            ValueY = value.Y.ToString();
            ValueZ = value.Z.ToString();
        }

    }
}
