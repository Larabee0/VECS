using Noesis;
using System;

namespace VECS.UI
{
    public class Bool2Field : VECSEditorControl
    {

        public static readonly DependencyProperty _Label = DependencyProperty.Register("Label", typeof(string), typeof(Bool2Field), new PropertyMetadata("Label"));
        public static readonly DependencyProperty _ValueX = DependencyProperty.Register("ValueX", typeof(bool), typeof(Bool2Field), new PropertyMetadata(false));
        public static readonly DependencyProperty _ValueY = DependencyProperty.Register("ValueY", typeof(bool), typeof(Bool2Field), new PropertyMetadata(false));

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

        private bool X;
        private bool Y;

        public Bool2Field()
        {
            InitializeComponent();
            var valueX = (CheckBox)FindName("XComp");
            var valueY = (CheckBox)FindName("YComp");

            WeakReference weak = new(this);

            valueX.Checked += (s, e) =>
            {
                var weakRef = (Bool2Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.X = (bool)((CheckBox)s).IsChecked;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueY.Checked += (s, e) =>
            {
                var weakRef = (Bool2Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.Y = (bool)((CheckBox)s).IsChecked;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueX.Unchecked += (s, e) =>
            {
                var weakRef = (Bool2Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.X = (bool)((CheckBox)s).IsChecked;
                    weakRef.InternalValueChanged(s, e);
                }
            };
            valueY.Unchecked += (s, e) =>
            {
                var weakRef = (Bool2Field)weak.Target;
                if (weakRef != null)
                {
                    weakRef.Y = (bool)((CheckBox)s).IsChecked;
                    weakRef.InternalValueChanged(s, e);
                }
            };
        }

        void InitializeComponent()
        {
            GUI.LoadComponent(this, "Editor/Bool2Field.xaml");
        }

        public override void SetValue(object value)
        {
            _internalSet = true;
            _internalSet = false;
            throw new System.NotImplementedException();
        }

        public override object TryParse(object currentValue)
        {
            throw new NotImplementedException();
        }
    }
}
