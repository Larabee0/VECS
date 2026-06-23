using Noesis;
using System.Numerics;

namespace VECS.UI
{
    public class Matrix3x2Field : UserControl, IEditorField
    {

        public static readonly DependencyProperty _Label = DependencyProperty.Register("Label", typeof(string), typeof(Matrix3x2Field), new PropertyMetadata("Label"));

        public static readonly DependencyProperty _ValueM11 = DependencyProperty.Register("ValueM11", typeof(string), typeof(Matrix3x2Field), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueM12 = DependencyProperty.Register("ValueM12", typeof(string), typeof(Matrix3x2Field), new PropertyMetadata("0"));

        public static readonly DependencyProperty _ValueM21 = DependencyProperty.Register("ValueM21", typeof(string), typeof(Matrix3x2Field), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueM22 = DependencyProperty.Register("ValueM22", typeof(string), typeof(Matrix3x2Field), new PropertyMetadata("0"));

        public static readonly DependencyProperty _ValueM31 = DependencyProperty.Register("ValueM31", typeof(string), typeof(Matrix3x2Field), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueM32 = DependencyProperty.Register("ValueM32", typeof(string), typeof(Matrix3x2Field), new PropertyMetadata("0"));
        
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


        public Matrix3x2Field()
        {
            InitializeComponent();
        }

        void InitializeComponent()
        {
            GUI.LoadComponent(this, "Editor/Matrix3x2Field.xaml");
        }

        public void FromMatrix3x2(Matrix3x2 matrix)
        {
            ValueM11 = matrix.M11.ToString();
            ValueM12 = matrix.M12.ToString();

            ValueM21 = matrix.M21.ToString();
            ValueM22 = matrix.M22.ToString();

            ValueM31 = matrix.M31.ToString();
            ValueM32 = matrix.M32.ToString();
        }


        public Matrix3x2 ToMatrix3x2(Matrix3x2 matrix)
        {
            
            matrix.M11 = float.TryParse(ValueM11, out var value) ? value : matrix.M11;
            matrix.M12 = float.TryParse(ValueM12, out value) ? value : matrix.M12;

            matrix.M21 = float.TryParse(ValueM21, out value) ? value : matrix.M21;
            matrix.M22 = float.TryParse(ValueM22, out value) ? value : matrix.M22;

            matrix.M31 = float.TryParse(ValueM31, out value) ? value : matrix.M31;
            matrix.M32 = float.TryParse(ValueM32, out value) ? value : matrix.M32;

            return matrix;
        }

        public void SetValue(object value)
        {
            if(value is Matrix3x2 mat3x2)
            {
                FromMatrix3x2(mat3x2);
            }
        }

    }
}
