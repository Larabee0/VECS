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

        public ButtonWithImage()
        {
            InitializeComponent();
            
            WeakReference weak = new(this);


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
