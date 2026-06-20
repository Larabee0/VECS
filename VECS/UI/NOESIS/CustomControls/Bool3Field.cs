using Noesis;
using System.Numerics;

namespace VECS.UI
{
    public class Bool3Field : UserControl
    {

        public static readonly DependencyProperty _Label = DependencyProperty.Register("Label", typeof(string), typeof(TextBlock), new PropertyMetadata("Label"));
        public static readonly DependencyProperty _ValueX = DependencyProperty.Register("ValueX", typeof(bool), typeof(CheckBox), new PropertyMetadata(false));
        public static readonly DependencyProperty _ValueY = DependencyProperty.Register("ValueY", typeof(bool), typeof(CheckBox), new PropertyMetadata(false));
        public static readonly DependencyProperty _ValueZ = DependencyProperty.Register("ValueZ", typeof(bool), typeof(CheckBox), new PropertyMetadata(false));

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

        public Bool3Field()
        {
            InitializeComponent();
        }

        void InitializeComponent()
        {
            GUI.LoadComponent(this, "Editor/Bool3Field.xaml");
        }

        public Bool3 GetBool3()
        {
            return new(ValueX, ValueY,ValueZ);
        }

        public void SetBool3(Bool3 value)
        {
            ValueX = value.X;
            ValueY = value.Y;
            ValueZ = value.Z;
        }
    }
}
