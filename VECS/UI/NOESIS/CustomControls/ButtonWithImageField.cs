using Noesis;
using System;

namespace VECS.UI
{
    public class ButtonWithImage : VECSEditorControl
    {

        public static readonly DependencyProperty _Label = DependencyProperty.Register("Label", typeof(string), typeof(Bool2Field), new PropertyMetadata("Label"));

        public override string Label
        {
            get { return (string)GetValue(_Label); }
            set { SetValue(_Label, value); }
        }

        public Action<object,MouseButtonEventArgs> OnDoubleClick;

        private readonly Image _iconImage;

        public Image IconImage => _iconImage;

        public ButtonWithImage()
        {
            InitializeComponent();
            
            WeakReference weak = new(this);

            _iconImage = (Image)FindName("Icon");

            this.MouseDoubleClick += (s, e) => { ((ButtonWithImage)weak.Target)?.OnMouseDoubleClick(s, e); };
        }

        private void OnMouseDoubleClick(object s, MouseButtonEventArgs e)
        {
            OnDoubleClick?.Invoke(s,e);
        }

        void InitializeComponent()
        {
            GUI.LoadComponent(this, "Editor/ButtonWithImage.xaml");
        }

        public override void SetValue(object value)
        {
            
        }

        public override object TryParse(object currentValue)
        {
            return null;
        }
    }
}
