using Noesis;
using System;
using System.Numerics;

namespace VECS.UI
{
    public class Matrix3x3Field : VECSEditorControl
    {

        public static readonly DependencyProperty _Label = DependencyProperty.Register("Label", typeof(string), typeof(Matrix3x3Field), new PropertyMetadata("Label"));
        public static readonly DependencyProperty _ValueM11 = DependencyProperty.Register("ValueM11", typeof(string), typeof(Matrix3x3Field), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueM12 = DependencyProperty.Register("ValueM12", typeof(string), typeof(Matrix3x3Field), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueM13 = DependencyProperty.Register("ValueM13", typeof(string), typeof(Matrix3x3Field), new PropertyMetadata("0"));

        public static readonly DependencyProperty _ValueM21 = DependencyProperty.Register("ValueM21", typeof(string), typeof(Matrix3x3Field), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueM22 = DependencyProperty.Register("ValueM22", typeof(string), typeof(Matrix3x3Field), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueM23 = DependencyProperty.Register("ValueM23", typeof(string), typeof(Matrix3x3Field), new PropertyMetadata("0"));

        public static readonly DependencyProperty _ValueM31 = DependencyProperty.Register("ValueM31", typeof(string), typeof(Matrix3x3Field), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueM32 = DependencyProperty.Register("ValueM32", typeof(string), typeof(Matrix3x3Field), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueM33 = DependencyProperty.Register("ValueM33", typeof(string), typeof(Matrix3x3Field), new PropertyMetadata("0"));

        public override string Label
        {
            get { return (string)GetValue(_Label); }
            set { SetValue(_Label, value); }
        }

        public string ValueM11
        {
            get { return M11; }
            set { SetValue(_ValueM11, value); M11 = value; }
        }

        public string ValueM12
        {
            get { return M12; }
            set { SetValue(_ValueM12, value); M12 = value; }
        }

        public string ValueM13
        {
            get { return M13; }
            set { SetValue(_ValueM13, value); M13 = value; }
        }


        public string ValueM21
        {
            get { return M21; }
            set { SetValue(_ValueM21, value); M21 = value; }
        }

        public string ValueM22
        {
            get { return M22; }
            set { SetValue(_ValueM22, value); M22 = value; }
        }

        public string ValueM23
        {
            get { return M23; }
            set { SetValue(_ValueM23, value); M23 = value; }
        }

        public string ValueM31
        {
            get { return M31; }
            set { SetValue(_ValueM31, value); M31 = value; }
        }

        public string ValueM32
        {
            get { return M32; }
            set { SetValue(_ValueM32, value); M32 = value; }
        }

        public string ValueM33
        {
            get { return M33; }
            set { SetValue(_ValueM33, value); M33 = value; }
        }

        private string M11;
        private string M12;
        private string M13;
        private string M21;
        private string M22;
        private string M23;
        private string M31;
        private string M32;
        private string M33;

        public Matrix3x3Field()
        {
            InitializeComponent();
            var valueM11 = (TextBox)FindName("M11Comp");
            var valueM12 = (TextBox)FindName("M12Comp");
            var valueM13 = (TextBox)FindName("M13Comp");
            var valueM21 = (TextBox)FindName("M21Comp");
            var valueM22 = (TextBox)FindName("M22Comp");
            var valueM23 = (TextBox)FindName("M23Comp");
            var valueM31 = (TextBox)FindName("M31Comp");
            var valueM32 = (TextBox)FindName("M32Comp");
            var valueM33 = (TextBox)FindName("M33Comp");

            WeakReference weak = new(this);

            valueM11.TextChanged += (s, e) =>
            {
                var weakRef = (Matrix3x3Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.M11 = ((TextBox)s).Text;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueM12.TextChanged += (s, e) =>
            {
                var weakRef = (Matrix3x3Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.M12 = ((TextBox)s).Text;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueM13.TextChanged += (s, e) =>
            {
                var weakRef = (Matrix3x3Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.M13 = ((TextBox)s).Text;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueM21.TextChanged += (s, e) =>
            {
                var weakRef = (Matrix3x3Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.M21 = ((TextBox)s).Text;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueM22.TextChanged += (s, e) =>
            {
                var weakRef = (Matrix3x3Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.M22 = ((TextBox)s).Text;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueM23.TextChanged += (s, e) =>
            {
                var weakRef = (Matrix3x3Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.M23 = ((TextBox)s).Text;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueM31.TextChanged += (s, e) =>
            {
                var weakRef = (Matrix3x3Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.M31 = ((TextBox)s).Text;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueM32.TextChanged += (s, e) =>
            {
                var weakRef = (Matrix3x3Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.M32 = ((TextBox)s).Text;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueM33.TextChanged += (s, e) =>
            {
                var weakRef = (Matrix3x3Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.M33 = ((TextBox)s).Text;
                    weakRef.InternalValueChanged(s, e);
                }
            };
        }

        void InitializeComponent()
        {
            GUI.LoadComponent(this, "Editor/Matrix3x3Field.xaml");
        }

        public void FromMatrix3x3(Matrix3x3 matrix)
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


        public Matrix3x3 ToMatrix3x3(Matrix3x3 matrix)
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

        public override void SetValue(object value)
        {
            _internalSet = true;
            if(value is Matrix3x3 mat3x3)
            {
                FromMatrix3x3(mat3x3);
            }
            _internalSet = false;
        }

        public override object TryParse(object currentValue)
        {
            if (currentValue is Matrix3x3 mat3x3)
            {
                return ToMatrix3x3(mat3x3);
            }
            else
            {
                return currentValue;
            }
        }
    }
}
