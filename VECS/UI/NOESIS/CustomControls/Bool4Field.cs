using Noesis;
using System.Numerics;

namespace VECS.UI
{
    public class Bool4Field : UserControl, IEditorField
    {

        public static readonly DependencyProperty _Label = DependencyProperty.Register("Label", typeof(string), typeof(Bool4Field), new PropertyMetadata("Label"));
        public static readonly DependencyProperty _ValueX = DependencyProperty.Register("ValueX", typeof(bool), typeof(Bool4Field), new PropertyMetadata(false));
        public static readonly DependencyProperty _ValueY = DependencyProperty.Register("ValueY", typeof(bool), typeof(Bool4Field), new PropertyMetadata(false));
        public static readonly DependencyProperty _ValueZ = DependencyProperty.Register("ValueZ", typeof(bool), typeof(Bool4Field), new PropertyMetadata(false));
        public static readonly DependencyProperty _ValueW = DependencyProperty.Register("ValueW", typeof(bool), typeof(Bool4Field), new PropertyMetadata(false));

        public string Label
        {
            get { return (string)GetValue(_Label); }
            set { SetValue(_Label, value); }
        }

        public bool ValueX
        {
            get { return (bool)GetValue(_ValueX); }
            set { SetValue(_ValueX, value); }
        }

        public bool ValueY
        {
            get { return (bool)GetValue(_ValueY); }
            set { SetValue(_ValueY, value); }
        }

        public bool ValueZ
        {
            get { return (bool)GetValue(_ValueZ); }
            set { SetValue(_ValueZ, value); }
        }

        public bool ValueW
        {
            get { return (bool)GetValue(_ValueW); }
            set { SetValue(_ValueW, value); }
        }

        public Bool4Field()
        {
            InitializeComponent();
        }

        void InitializeComponent()
        {
            GUI.LoadComponent(this, "Editor/Bool4Field.xaml");
        }

        public Bool4 GetBool4()
        {
            return new(ValueX, ValueY,ValueZ,ValueW);
        }

        public void SetBool4(Bool4 value)
        {
            ValueX = value.X;
            ValueY = value.Y;
            ValueZ = value.Z;
            ValueW = value.W;
        }

        public void SetValue(object value)
        {
            if(value is Bool4 bool4)
            {
                SetBool4(bool4);
            }
        }
    }
}
