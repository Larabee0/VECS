using Noesis;

namespace VECS.UI
{
    public class Matrix3x3Field : UserControl
    {

        public static readonly DependencyProperty _Label = DependencyProperty.Register("Label", typeof(string), typeof(TextBlock), new PropertyMetadata("Label"));
        public static readonly DependencyProperty _ValueM11 = DependencyProperty.Register("ValueM11", typeof(string), typeof(TextBox), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueM12 = DependencyProperty.Register("ValueM12", typeof(string), typeof(TextBox), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueM13 = DependencyProperty.Register("ValueM13", typeof(string), typeof(TextBox), new PropertyMetadata("0"));

        public static readonly DependencyProperty _ValueM21 = DependencyProperty.Register("ValueM21", typeof(string), typeof(TextBox), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueM22 = DependencyProperty.Register("ValueM22", typeof(string), typeof(TextBox), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueM23 = DependencyProperty.Register("ValueM23", typeof(string), typeof(TextBox), new PropertyMetadata("0"));

        public static readonly DependencyProperty _ValueM31 = DependencyProperty.Register("ValueM31", typeof(string), typeof(TextBox), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueM32 = DependencyProperty.Register("ValueM32", typeof(string), typeof(TextBox), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueM33 = DependencyProperty.Register("ValueM33", typeof(string), typeof(TextBox), new PropertyMetadata("0"));

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


        public Matrix3x3Field()
        {
            InitializeComponent();
        }

        void InitializeComponent()
        {
            GUI.LoadComponent(this, "Editor/Matrix3x3Field.xaml");
        }

        public void FromMatrix4x4(System.Numerics.Matrix3x3 matrix)
        {
            ValueM11 = matrix.M11.ToString();
            ValueM12 = matrix.M12.ToString();
            ValueM13 = matrix.M13.ToString();

            ValueM21 = matrix.M21.ToString();
            ValueM22 = matrix.M22.ToString();
            ValueM23 = matrix.M23.ToString();

            ValueM31 = matrix.M31.ToString();
            ValueM32 = matrix.M32.ToString();
            ValueM33 = matrix.M33.ToString();
        }


        public System.Numerics.Matrix3x3 ToMatrix4x4(System.Numerics.Matrix3x3 matrix)
        {
            
            matrix.M11 = float.TryParse(ValueM11, out var value) ? value : matrix.M11;
            matrix.M12 = float.TryParse(ValueM12, out value) ? value : matrix.M12;
            matrix.M13 = float.TryParse(ValueM13, out value) ? value : matrix.M13;

            matrix.M21 = float.TryParse(ValueM21, out value) ? value : matrix.M21;
            matrix.M22 = float.TryParse(ValueM22, out value) ? value : matrix.M22;
            matrix.M23 = float.TryParse(ValueM23, out value) ? value : matrix.M23;

            matrix.M31 = float.TryParse(ValueM31, out value) ? value : matrix.M31;
            matrix.M32 = float.TryParse(ValueM32, out value) ? value : matrix.M32;
            matrix.M33 = float.TryParse(ValueM33, out value) ? value : matrix.M33;

            return matrix;
        }
    }
}
