using Noesis;

namespace VECS.UI
{
    public class Bool2Field : UserControl, IEditorField
    {

        public static readonly DependencyProperty _Label = DependencyProperty.Register("Label", typeof(string), typeof(Bool2Field), new PropertyMetadata("Label"));
        public static readonly DependencyProperty _ValueX = DependencyProperty.Register("ValueX", typeof(bool), typeof(Bool2Field), new PropertyMetadata(false));
        public static readonly DependencyProperty _ValueY = DependencyProperty.Register("ValueY", typeof(bool), typeof(Bool2Field), new PropertyMetadata(false));

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

        public Bool2Field()
        {
            InitializeComponent();
        }

        void InitializeComponent()
        {
            GUI.LoadComponent(this, "Editor/Bool2Field.xaml");
        }

        public void SetValue(object value)
        {
            throw new System.NotImplementedException();
        }

    }
}
