using Noesis;
using System;
using System.Numerics;

namespace VECS.UI
{
    public class Vector2Field : VECSEditorControl
    {

        public static readonly DependencyProperty _Label = DependencyProperty.Register("Label", typeof(string), typeof(Vector2Field), new PropertyMetadata("Label"));
        public static readonly DependencyProperty _ValueX = DependencyProperty.Register("ValueX", typeof(string), typeof(Vector2Field), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueY = DependencyProperty.Register("ValueY", typeof(string), typeof(Vector2Field), new PropertyMetadata("0"));

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

        private string X;
        private string Y;

        public Vector2Field()
        {
            InitializeComponent();
            var valueX = (TextBox)FindName("XComp");
            var valueY = (TextBox)FindName("YComp");

            WeakReference weak = new(this);

            valueX.TextChanged += (s, e) =>
            {
                var weakRef = (Vector2Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.X = ((TextBox)s).Text;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueY.TextChanged += (s, e) =>
            {
                var weakRef = (Vector2Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.Y = ((TextBox)s).Text;
                    weakRef.InternalValueChanged(s, e);
                }
            };

        }

        void InitializeComponent()
        {
            GUI.LoadComponent(this, "Editor/Vector2Field.xaml");
        }

        public Vector2 GetVector2(Vector2 value)
        {
            value.X = float.TryParse(ValueX, out var valueOut) ? valueOut : value.X;
            value.Y = float.TryParse(ValueY, out valueOut) ? valueOut : value.Y;
            return value;
        }

        public Vector2Int GetVector2Int(Vector2Int value)
        {
            value.X = int.TryParse(ValueX, out var valueOut) ? valueOut : value.X;
            value.Y = int.TryParse(ValueY, out valueOut) ? valueOut : value.Y;
            return value;
        }

        public Vector2UInt GetVector2Uint(Vector2UInt value)
        {
            value.X = uint.TryParse(ValueX, out var valueOut) ? valueOut : value.X;
            value.Y = uint.TryParse(ValueY, out valueOut) ? valueOut : value.Y;
            return value;
        }

        public void SetVector2(Vector2 value)
        {
            ValueX = value.X.ToString();
            ValueY = value.Y.ToString();
        }
        public void SetVector2Int(Vector2Int value)
        {
            ValueX = value.X.ToString();
            ValueY = value.Y.ToString();
        }
        public void SetVector2Uint(Vector2UInt value)
        {
            ValueX = value.X.ToString();
            ValueY = value.Y.ToString();
        }

        public override void SetValue(object value)
        {
            _internalSet = true;
            if (value is Vector2 vector2)
            {
                SetVector2(vector2);
            }
            else if(value is Vector2Int vector2Int)
            {
                SetVector2Int(vector2Int);
            }
            else if(value is Vector2UInt vector2UInt)
            {
                SetVector2Uint(vector2UInt);
            }
            _internalSet = false;
        }

        public override object TryParse(object currentValue)
        {
            if (currentValue is Vector2 vector2)
            {
                return GetVector2(vector2);
            }
            else if (currentValue is Vector2Int vector2Int)
            {
                return GetVector2Int(vector2Int);
            }
            else if (currentValue is Vector2UInt vector2UInt)
            {
                return GetVector2Uint(vector2UInt);
            }
            else
            {
                return currentValue;
            }
        }
    }
}
