using Noesis;
using System;

namespace VECS.UI
{
    public class TextField : VECSEditorControl
    {

        public static readonly DependencyProperty _LabelV1 = DependencyProperty.Register("Label", typeof(string), typeof(TextField), new PropertyMetadata("Label"));
        public static readonly DependencyProperty _ValueXV1 = DependencyProperty.Register("ValueX", typeof(string), typeof(TextField), new PropertyMetadata(""));

        public override string Label
        {
            get { return (string)GetValue(_LabelV1); }
            set { SetValue(_LabelV1, value); }
        }

        public string ValueX
        {
            get { return X; }
            set { SetValue(_ValueXV1, value); X = value; }
        }

        private string X;

        public TextField()
        {

        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            WeakReference weak = new(this);
            var valueX = (TextBox)FindName("XComp");
            valueX.TextChanged += (s, e) =>
            {
                var weakRef = (TextField)weak.Target;
                if (weakRef != null)
                {
                    weakRef.X = ((TextBox)s).Text;
                    weakRef.InternalValueChanged(s, e);
                }
            };
        }

        public override void SetValue(object value)
        {
            _internalSet = true;
            ValueX = value.ToString();
            _internalSet = false;
        }

        public override object TryParse(object currentValue)
        {
            if (currentValue is string)
            {
                return ValueX;
            }
            else
            {
                return currentValue;
            }
        }

    }
}
