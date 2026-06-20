using Noesis;

namespace VECS.UI
{
    public class Matrix4x4Field : UserControl
    {

        public static readonly DependencyProperty _Label = DependencyProperty.Register("LabelM4x4", typeof(string), typeof(TextBlock), new PropertyMetadata("Label"));
        public static readonly DependencyProperty _ValueM11 = DependencyProperty.Register("ValueM11", typeof(string), typeof(TextBox), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueM12 = DependencyProperty.Register("ValueM12", typeof(string), typeof(TextBox), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueM13 = DependencyProperty.Register("ValueM13", typeof(string), typeof(TextBox), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueM14 = DependencyProperty.Register("ValueM14", typeof(string), typeof(TextBox), new PropertyMetadata("0"));

        public static readonly DependencyProperty _ValueM21 = DependencyProperty.Register("ValueM21", typeof(string), typeof(TextBox), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueM22 = DependencyProperty.Register("ValueM22", typeof(string), typeof(TextBox), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueM23 = DependencyProperty.Register("ValueM23", typeof(string), typeof(TextBox), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueM24 = DependencyProperty.Register("ValueM24", typeof(string), typeof(TextBox), new PropertyMetadata("0"));

        public static readonly DependencyProperty _ValueM31 = DependencyProperty.Register("ValueM31", typeof(string), typeof(TextBox), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueM32 = DependencyProperty.Register("ValueM32", typeof(string), typeof(TextBox), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueM33 = DependencyProperty.Register("ValueM33", typeof(string), typeof(TextBox), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueM34 = DependencyProperty.Register("ValueM34", typeof(string), typeof(TextBox), new PropertyMetadata("0"));

        public static readonly DependencyProperty _ValueM41 = DependencyProperty.Register("ValueM41", typeof(string), typeof(TextBox), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueM42 = DependencyProperty.Register("ValueM42", typeof(string), typeof(TextBox), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueM43 = DependencyProperty.Register("ValueM43", typeof(string), typeof(TextBox), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueM44 = DependencyProperty.Register("ValueM44", typeof(string), typeof(TextBox), new PropertyMetadata("0"));
        public string Label
        {
            get { return (string)GetValue(_Label); }
            set { SetValue(_Label, value); }
        }

        public string ValueM11
        {
            get { return (string)GetValue(_ValueM11); }
            set { SetValue(_ValueM11, value); }
        }

        public string ValueM12
        {
            get { return (string)GetValue(_ValueM12); }
            set { SetValue(_ValueM12, value); }
        }

        public string ValueM13
        {
            get { return (string)GetValue(_ValueM13); }
            set { SetValue(_ValueM13, value); }
        }

        public string ValueM14
        {
            get { return (string)GetValue(_ValueM14); }
            set { SetValue(_ValueM14, value); }
        }


        public string ValueM21
        {
            get { return (string)GetValue(_ValueM21); }
            set { SetValue(_ValueM21, value); }
        }

        public string ValueM22
        {
            get { return (string)GetValue(_ValueM22); }
            set { SetValue(_ValueM22, value); }
        }

        public string ValueM23
        {
            get { return (string)GetValue(_ValueM23); }
            set { SetValue(_ValueM23, value); }
        }

        public string ValueM24
        {
            get { return (string)GetValue(_ValueM24); }
            set { SetValue(_ValueM24, value); }
        }


        public string ValueM31
        {
            get { return (string)GetValue(_ValueM31); }
            set { SetValue(_ValueM31, value); }
        }

        public string ValueM32
        {
            get { return (string)GetValue(_ValueM32); }
            set { SetValue(_ValueM32, value); }
        }

        public string ValueM33
        {
            get { return (string)GetValue(_ValueM33); }
            set { SetValue(_ValueM33, value); }
        }

        public string ValueM34
        {
            get { return (string)GetValue(_ValueM34); }
            set { SetValue(_ValueM34, value); }
        }


        public string ValueM41
        {
            get { return (string)GetValue(_ValueM41); }
            set { SetValue(_ValueM41, value); }
        }

        public string ValueM42
        {
            get { return (string)GetValue(_ValueM42); }
            set { SetValue(_ValueM42, value); }
        }

        public string ValueM43
        {
            get { return (string)GetValue(_ValueM43); }
            set { SetValue(_ValueM43, value); }
        }

        public string ValueM44
        {
            get { return (string)GetValue(_ValueM44); }
            set { SetValue(_ValueM44, value); }
        }

        public Matrix4x4Field()
        {
            InitializeComponent();
        }

        void InitializeComponent()
        {
            GUI.LoadComponent(this, "Editor/Matrix4x4Field.xaml");
        }

        public void FromMatrix4x4(System.Numerics.Matrix4x4 matrix)
        {
            ValueM11 = matrix.M11.ToString();
            ValueM12 = matrix.M12.ToString();
            ValueM13 = matrix.M13.ToString();
            ValueM14 = matrix.M14.ToString();

            ValueM21 = matrix.M21.ToString();
            ValueM22 = matrix.M22.ToString();
            ValueM23 = matrix.M23.ToString();
            ValueM24 = matrix.M24.ToString();

            ValueM31 = matrix.M31.ToString();
            ValueM32 = matrix.M32.ToString();
            ValueM33 = matrix.M33.ToString();
            ValueM34 = matrix.M34.ToString();

            ValueM41 = matrix.M41.ToString();
            ValueM42 = matrix.M42.ToString();
            ValueM43 = matrix.M43.ToString();
            ValueM44 = matrix.M44.ToString();
        }


        public System.Numerics.Matrix4x4 ToMatrix4x4(System.Numerics.Matrix4x4 matrix)
        {
            
            matrix.M11 = float.TryParse(ValueM11, out var value) ? value : matrix.M11;
            matrix.M12 = float.TryParse(ValueM12, out value) ? value : matrix.M12;
            matrix.M13 = float.TryParse(ValueM13, out value) ? value : matrix.M13;
            matrix.M14 = float.TryParse(ValueM14, out value) ? value : matrix.M14;

            matrix.M21 = float.TryParse(ValueM21, out value) ? value : matrix.M21;
            matrix.M22 = float.TryParse(ValueM22, out value) ? value : matrix.M22;
            matrix.M23 = float.TryParse(ValueM23, out value) ? value : matrix.M23;
            matrix.M24 = float.TryParse(ValueM24, out value) ? value : matrix.M24;

            matrix.M31 = float.TryParse(ValueM31, out value) ? value : matrix.M31;
            matrix.M32 = float.TryParse(ValueM32, out value) ? value : matrix.M32;
            matrix.M33 = float.TryParse(ValueM33, out value) ? value : matrix.M33;
            matrix.M34 = float.TryParse(ValueM34, out value) ? value : matrix.M34;

            matrix.M41 = float.TryParse(ValueM41, out value) ? value : matrix.M41;
            matrix.M42 = float.TryParse(ValueM42, out value) ? value : matrix.M42;
            matrix.M43 = float.TryParse(ValueM43, out value) ? value : matrix.M43;
            matrix.M44 = float.TryParse(ValueM44, out value) ? value : matrix.M44;

            return matrix;
        }
    }
}
