using Noesis;
using System;
using System.Numerics;

namespace VECS.UI
{
    public class Bool3Field : VECSEditorControl
    {

        public static readonly DependencyProperty _Label = DependencyProperty.Register("Label", typeof(string), typeof(Bool3Field), new PropertyMetadata("Label"));
        public static readonly DependencyProperty _ValueX = DependencyProperty.Register("ValueX", typeof(bool), typeof(Bool3Field), new PropertyMetadata(false));
        public static readonly DependencyProperty _ValueY = DependencyProperty.Register("ValueY", typeof(bool), typeof(Bool3Field), new PropertyMetadata(false));
        public static readonly DependencyProperty _ValueZ = DependencyProperty.Register("ValueZ", typeof(bool), typeof(Bool3Field), new PropertyMetadata(false));

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

        private bool X;
        private bool Y;
        private bool Z;

        public Bool3Field()
        {
            InitializeComponent();
            var valueX = (CheckBox)FindName("XComp");
            var valueY = (CheckBox)FindName("YComp");
            var valueZ = (CheckBox)FindName("ZComp");

            WeakReference weak = new(this);

            valueX.Checked += (s, e) =>
            {
                var weakRef = (Bool3Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.X = (bool)((CheckBox)s).IsChecked;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueY.Checked += (s, e) =>
            {
                var weakRef = (Bool3Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.Y = (bool)((CheckBox)s).IsChecked;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueZ.Checked += (s, e) =>
            {
                var weakRef = (Bool3Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.Z = (bool)((CheckBox)s).IsChecked;
                    weakRef.InternalValueChanged(s, e);
                }
            };

            valueX.Unchecked += (s, e) =>
            {
                var weakRef = (Bool3Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.X = (bool)((CheckBox)s).IsChecked;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueY.Unchecked += (s, e) =>
            {
                var weakRef = (Bool3Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.Y = (bool)((CheckBox)s).IsChecked;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueZ.Unchecked += (s, e) =>
            {
                var weakRef = (Bool3Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.Z = (bool)((CheckBox)s).IsChecked;
                    weakRef.InternalValueChanged(s, e);
                }
            };
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

        public override void SetValue(object value)
        {
            _internalSet = true;
            if(value is Bool3 bool3)
            {
                SetBool3(bool3);
            }
            _internalSet = false;
        }

        public override object TryParse(object currentValue)
        {
            if (currentValue is Bool3)
            {
                return GetBool3();
            }
            else
            {
                return currentValue;
            }
        }
    }
}
