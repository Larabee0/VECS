using Noesis;
using System;

namespace VECS.UI
{
    public class ButtonWithImage : VECSEditorControl
    {

        public static readonly DependencyProperty _Label = DependencyProperty.Register("Label", typeof(string), typeof(ButtonWithImage), new PropertyMetadata("Label"));
        public static readonly DependencyProperty _Image = DependencyProperty.Register("Image", typeof(TextureSource), typeof(ButtonWithImage), new PropertyMetadata(null));

        public override string Label
        {
            get { return (string)GetValue(_Label); }
            set { SetValue(_Label, value); }
        }

        public TextureSource IconImage
        {
            get { return (TextureSource)GetValue(_Image); }
            set { SetValue(_Image, value); }
        }

        public Action<object,MouseButtonEventArgs> OnDoubleClick;


        public ButtonWithImage()
        {
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            WeakReference weak = new(this);

            MouseDoubleClick += (s, e) => { ((ButtonWithImage)weak.Target)?.OnMouseDoubleClick(s, e); };

        }

        private void OnMouseDoubleClick(object s, MouseButtonEventArgs e)
        {
            OnDoubleClick?.Invoke(s,e);
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
