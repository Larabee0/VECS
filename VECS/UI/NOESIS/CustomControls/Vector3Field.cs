using Noesis;
using System;
using System.Numerics;

namespace VECS.UI
{
    public class Vector3Field : VECSEditorControl
    {

        public static readonly DependencyProperty _Label = DependencyProperty.Register("Label", typeof(string), typeof(Vector3Field), new PropertyMetadata("Label"));
        public static readonly DependencyProperty _ValueX = DependencyProperty.Register("ValueX", typeof(string), typeof(Vector3Field), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueY = DependencyProperty.Register("ValueY", typeof(string), typeof(Vector3Field), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueZ = DependencyProperty.Register("ValueZ", typeof(string), typeof(Vector3Field), new PropertyMetadata("0"));

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

        private string X;
        private string Y;
        private string Z;

        public Vector3Field()
        {

        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            var valueX = (TextBox)GetTemplateChild("XComp");
            var valueY = (TextBox)GetTemplateChild("YComp");
            var valueZ = (TextBox)GetTemplateChild("ZComp");

            WeakReference weak = new(this);

            valueX.TextChanged += (s, e) =>
            {
                var weakRef = (Vector3Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.X = ((TextBox)s).Text;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueY.TextChanged += (s, e) =>
            {
                var weakRef = (Vector3Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.Y = ((TextBox)s).Text;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueZ.TextChanged += (s, e) =>
            {
                var weakRef = (Vector3Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.Z = ((TextBox)s).Text;
                    weakRef.InternalValueChanged(s, e);
                }
            };
        }

        public override void SetValue(object value)
        {
            _internalSet = true;
            if (value is Vector3 vector3)
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
            _internalSet = false;
        }

        public override object TryParse(object currentValue)
        {
            if (currentValue is Vector3 vector3)
            {
                return GetVector3(vector3);
            }
            else if (currentValue is Vector3Int vector3Int)
            {
                return GetVector3Int(vector3Int);
            }
            else if (currentValue is Vector3UInt vector3UInt)
            {
                return GetVector3Uint(vector3UInt);
            }
            else
            {
                return currentValue;
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

