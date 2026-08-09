using Noesis;
using System;

namespace VECS.UI
{
    public class Bool1Field : VECSEditorControl
    {

        public static readonly DependencyProperty _Label = DependencyProperty.Register("Label", typeof(string), typeof(Bool1Field), new PropertyMetadata("Label"));
        public static readonly DependencyProperty _ValueX = DependencyProperty.Register("ValueX", typeof(bool), typeof(Bool1Field), new PropertyMetadata(false));

        public override string Label
        {
            get { return (string)GetValue(_Label); }
            set { SetValue(_Label, value); }
        }

        public bool ValueX
        {
            get => X;
            set { SetValue(_ValueX, value); X = value; }
        }

        private bool X;

        public Bool1Field()
        {

        }

        public override void SetValue(object value)
        {
            _internalSet = true;
            if(value is bool boolean)
            {
                ValueX = boolean;
            }
            _internalSet = false;
            ApplyTemplate();
        }

        public override object TryParse(object currentValue)
        {
            if(currentValue is bool)
            {
                return ValueX;
            }
            else
            {
                return currentValue;
            }
        }

        public override void OnApplyTemplate()
        {

            base.OnApplyTemplate();
            
            var valueX = (CheckBox)GetTemplateChild("XComp");
            WeakReference weak = new(this);
            
            valueX.Checked += (s, e) =>
            {
                var weakRef = (Bool1Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.X = (bool)((CheckBox)s).IsChecked;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueX.Unchecked += (s, e) =>
            {
                var weakRef = (Bool1Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.X = (bool)((CheckBox)s).IsChecked;
                    weakRef.InternalValueChanged(s, e);
                }
            };
        }

    }
}
