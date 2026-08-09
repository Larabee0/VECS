using Noesis;
using System;
using System.Numerics;

namespace VECS.UI
{
    public class Matrix4x4Field : VECSEditorControl
    {

        public static readonly DependencyProperty _Label = DependencyProperty.Register("Label", typeof(string), typeof(Matrix4x4Field), new PropertyMetadata("Label"));
        public static readonly DependencyProperty _ValueM11 = DependencyProperty.Register("ValueM11", typeof(string), typeof(Matrix4x4Field), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueM12 = DependencyProperty.Register("ValueM12", typeof(string), typeof(Matrix4x4Field), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueM13 = DependencyProperty.Register("ValueM13", typeof(string), typeof(Matrix4x4Field), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueM14 = DependencyProperty.Register("ValueM14", typeof(string), typeof(Matrix4x4Field), new PropertyMetadata("0"));

        public static readonly DependencyProperty _ValueM21 = DependencyProperty.Register("ValueM21", typeof(string), typeof(Matrix4x4Field), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueM22 = DependencyProperty.Register("ValueM22", typeof(string), typeof(Matrix4x4Field), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueM23 = DependencyProperty.Register("ValueM23", typeof(string), typeof(Matrix4x4Field), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueM24 = DependencyProperty.Register("ValueM24", typeof(string), typeof(Matrix4x4Field), new PropertyMetadata("0"));

        public static readonly DependencyProperty _ValueM31 = DependencyProperty.Register("ValueM31", typeof(string), typeof(Matrix4x4Field), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueM32 = DependencyProperty.Register("ValueM32", typeof(string), typeof(Matrix4x4Field), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueM33 = DependencyProperty.Register("ValueM33", typeof(string), typeof(Matrix4x4Field), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueM34 = DependencyProperty.Register("ValueM34", typeof(string), typeof(Matrix4x4Field), new PropertyMetadata("0"));

        public static readonly DependencyProperty _ValueM41 = DependencyProperty.Register("ValueM41", typeof(string), typeof(Matrix4x4Field), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueM42 = DependencyProperty.Register("ValueM42", typeof(string), typeof(Matrix4x4Field), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueM43 = DependencyProperty.Register("ValueM43", typeof(string), typeof(Matrix4x4Field), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueM44 = DependencyProperty.Register("ValueM44", typeof(string), typeof(Matrix4x4Field), new PropertyMetadata("0"));
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

        public string ValueM14
        {
            get { return M14; }
            set { SetValue(_ValueM14, value); M14 = value; }
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

        public string ValueM24
        {
            get { return M24; }
            set { SetValue(_ValueM24, value); M24 = value; }
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

        public string ValueM34
        {
            get { return M34; }
            set { SetValue(_ValueM34, value); M34 = value; }
        }


        public string ValueM41
        {
            get { return M41; }
            set { SetValue(_ValueM41, value); M41 = value; }
        }

        public string ValueM42
        {
            get { return M42; }
            set { SetValue(_ValueM42, value); M42 = value; }
        }

        public string ValueM43
        {
            get { return M43; }
            set { SetValue(_ValueM43, value); M43 = value; }
        }

        public string ValueM44
        {
            get { return M44; }
            set { SetValue(_ValueM44, value); M44 = value; }
        }

        private string M11;
        private string M12;
        private string M13;
        private string M14;
        private string M21;
        private string M22;
        private string M23;
        private string M24;
        private string M31;
        private string M32;
        private string M33;
        private string M34;
        private string M41;
        private string M42;
        private string M43;
        private string M44;

        public Matrix4x4Field()
        {

        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            var valueM11 = (TextBox)GetTemplateChild("M11Comp");
            var valueM12 = (TextBox)GetTemplateChild("M12Comp");
            var valueM13 = (TextBox)GetTemplateChild("M13Comp");
            var valueM14 = (TextBox)GetTemplateChild("M14Comp");
            var valueM21 = (TextBox)GetTemplateChild("M21Comp");
            var valueM22 = (TextBox)GetTemplateChild("M22Comp");
            var valueM23 = (TextBox)GetTemplateChild("M23Comp");
            var valueM24 = (TextBox)GetTemplateChild("M24Comp");
            var valueM31 = (TextBox)GetTemplateChild("M31Comp");
            var valueM32 = (TextBox)GetTemplateChild("M32Comp");
            var valueM33 = (TextBox)GetTemplateChild("M33Comp");
            var valueM34 = (TextBox)GetTemplateChild("M34Comp");
            var valueM41 = (TextBox)GetTemplateChild("M41Comp");
            var valueM42 = (TextBox)GetTemplateChild("M42Comp");
            var valueM43 = (TextBox)GetTemplateChild("M43Comp");
            var valueM44 = (TextBox)GetTemplateChild("M44Comp");

            WeakReference weak = new(this);

            valueM11.TextChanged += (s, e) =>
            {
                var weakRef = (Matrix4x4Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.M11 = ((TextBox)s).Text;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueM12.TextChanged += (s, e) =>
            {
                var weakRef = (Matrix4x4Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.M12 = ((TextBox)s).Text;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueM13.TextChanged += (s, e) =>
            {
                var weakRef = (Matrix4x4Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.M13 = ((TextBox)s).Text;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueM14.TextChanged += (s, e) =>
            {
                var weakRef = (Matrix4x4Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.M14 = ((TextBox)s).Text;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueM21.TextChanged += (s, e) =>
            {
                var weakRef = (Matrix4x4Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.M21 = ((TextBox)s).Text;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueM22.TextChanged += (s, e) =>
            {
                var weakRef = (Matrix4x4Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.M22 = ((TextBox)s).Text;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueM23.TextChanged += (s, e) =>
            {
                var weakRef = (Matrix4x4Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.M23 = ((TextBox)s).Text;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueM24.TextChanged += (s, e) =>
            {
                var weakRef = (Matrix4x4Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.M24 = ((TextBox)s).Text;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueM31.TextChanged += (s, e) =>
            {
                var weakRef = (Matrix4x4Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.M31 = ((TextBox)s).Text;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueM32.TextChanged += (s, e) =>
            {
                var weakRef = (Matrix4x4Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.M32 = ((TextBox)s).Text;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueM33.TextChanged += (s, e) =>
            {
                var weakRef = (Matrix4x4Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.M33 = ((TextBox)s).Text;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueM34.TextChanged += (s, e) =>
            {
                var weakRef = (Matrix4x4Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.M34 = ((TextBox)s).Text;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueM41.TextChanged += (s, e) =>
            {
                var weakRef = (Matrix4x4Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.M41 = ((TextBox)s).Text;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueM42.TextChanged += (s, e) =>
            {
                var weakRef = (Matrix4x4Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.M42 = ((TextBox)s).Text;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueM43.TextChanged += (s, e) =>
            {
                var weakRef = (Matrix4x4Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.M43 = ((TextBox)s).Text;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueM44.TextChanged += (s, e) =>
            {
                var weakRef = (Matrix4x4Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.M44 = ((TextBox)s).Text;
                    weakRef.InternalValueChanged(s, e);
                }
            };
        }

        public void FromMatrix4x4(Matrix4x4 matrix)
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


        public Matrix4x4 ToMatrix4x4(Matrix4x4 matrix)
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

        public override void SetValue(object value)
        {
            _internalSet = true;
            if(value is Matrix4x4 mat4x4)
            {
                FromMatrix4x4(mat4x4);
            }
            _internalSet = false;
        }

        public override object TryParse(object currentValue)
        {
            if(currentValue is Matrix4x4 mat4x4)
            {
                return ToMatrix4x4(mat4x4);
            }
            else
            {
                return currentValue;
            }
        }
    }
}
