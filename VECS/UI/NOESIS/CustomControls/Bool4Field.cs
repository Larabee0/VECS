using Noesis;
using System;
using System.Numerics;

namespace VECS.UI
{
    public class Bool4Field : VECSEditorControl
    {

        public static readonly DependencyProperty _Label = DependencyProperty.Register("Label", typeof(string), typeof(Bool4Field), new PropertyMetadata("Label"));
        public static readonly DependencyProperty _ValueX = DependencyProperty.Register("ValueX", typeof(bool), typeof(Bool4Field), new PropertyMetadata(false));
        public static readonly DependencyProperty _ValueY = DependencyProperty.Register("ValueY", typeof(bool), typeof(Bool4Field), new PropertyMetadata(false));
        public static readonly DependencyProperty _ValueZ = DependencyProperty.Register("ValueZ", typeof(bool), typeof(Bool4Field), new PropertyMetadata(false));
        public static readonly DependencyProperty _ValueW = DependencyProperty.Register("ValueW", typeof(bool), typeof(Bool4Field), new PropertyMetadata(false));

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

        public bool ValueY
        {
            get => Y;
            set { SetValue(_ValueY, value); Y = value; }
        }

        public bool ValueZ
        {
            get => Z;
            set { SetValue(_ValueZ, value); Z = value; }
        }

        public bool ValueW
        {
            get => W;
            set { SetValue(_ValueW, value); W = value; }
        }

        private bool X;
        private bool Y;
        private bool Z;
        private bool W;

        public Bool4Field()
        {

        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            var valueX = (CheckBox)FindName("XComp");
            var valueY = (CheckBox)FindName("YComp");
            var valueZ = (CheckBox)FindName("ZComp");
            var valueW = (CheckBox)FindName("WComp");

            WeakReference weak = new(this);

            valueX.Checked += (s, e) =>
            {
                var weakRef = (Bool4Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.X = (bool)((CheckBox)s).IsChecked;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueY.Checked += (s, e) =>
            {
                var weakRef = (Bool4Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.Y = (bool)((CheckBox)s).IsChecked;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueZ.Checked += (s, e) =>
            {
                var weakRef = (Bool4Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.Z = (bool)((CheckBox)s).IsChecked;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueW.Checked += (s, e) =>
            {
                var weakRef = (Bool4Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.W = (bool)((CheckBox)s).IsChecked;
                    weakRef.InternalValueChanged(s, e);
                }
            };


            valueX.Unchecked += (s, e) =>
            {
                var weakRef = (Bool4Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.X = (bool)((CheckBox)s).IsChecked;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueY.Unchecked += (s, e) =>
            {
                var weakRef = (Bool4Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.Y = (bool)((CheckBox)s).IsChecked;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueZ.Unchecked += (s, e) =>
            {
                var weakRef = (Bool4Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.Z = (bool)((CheckBox)s).IsChecked;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueW.Unchecked += (s, e) =>
            {
                var weakRef = (Bool4Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.W = (bool)((CheckBox)s).IsChecked;
                    weakRef.InternalValueChanged(s, e);
                }
            };
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

        public override void SetValue(object value)
        {
            _internalSet = true;
            if(value is Bool4 bool4)
            {
                SetBool4(bool4);
            }
            _internalSet = false;
        }
        public override object TryParse(object currentValue)
        {
            if (currentValue is Bool4)
            {
                return GetBool4();
            }
            else
            {
                return currentValue;
            }
        }
    }
}
