using Noesis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace VECS.UI
{
    public class Vector2Field : UserControl
    {

        public static readonly DependencyProperty _Label = DependencyProperty.Register("Label", typeof(string), typeof(TextBlock), new PropertyMetadata("Label"));
        public static readonly DependencyProperty _ValueX = DependencyProperty.Register("ValueX", typeof(string), typeof(TextBox), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueY = DependencyProperty.Register("ValueY", typeof(string), typeof(TextBox), new PropertyMetadata("0"));

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

        public Vector2Field()
        {
            InitializeComponent();
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
    }
}
