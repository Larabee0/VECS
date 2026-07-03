using Noesis;
using System;

namespace VECS.UI
{
    public class DropDownField : VECSEditorControl
    {

        public static readonly DependencyProperty _label = DependencyProperty.Register("Label", typeof(string), typeof(DropDownField), new PropertyMetadata("Label"));

        public override string Label
        {
            get { return (string)GetValue(_label); }
            set { SetValue(_label, value); }
        }

        public TreeViewItem RadioContainer => (TreeViewItem)FindName("RadioContainer");

        public bool IsFlagsEnum { get; set; }

        public DropDownField()
        {
            InitializeComponent();
        }

        void InitializeComponent()
        {
            GUI.LoadComponent(this, "Editor/DropDownField.xaml");
        }

        public void AddRadioButton(string name, bool randomGroupName, string tag, bool isChecked)
        {

            var button = new RadioButton()
            {
                Content = name,
                GroupName = randomGroupName ? Random.Shared.Next().ToString() : "0",
                Tag = tag,
                IsChecked = isChecked
            };
            RadioContainer.Items.Add(button);

            WeakReference weak = new(this);
            WeakReference weakButton = new(button);
            ((RadioButton)weakButton.Target).Checked += (s, e) => ((DropDownField)weak.Target)?.InternalValueChanged(s, e);
            ((RadioButton)weakButton.Target).Unchecked += (s, e) => ((DropDownField)weak.Target)?.InternalValueChanged(s, e);
        }

        public override void SetValue(object value)
        {
            _internalSet = true;
            var values = Enum.GetNames(value.GetType());
            var items = RadioContainer.Items;

            string valueAsString =value.ToString();
            RadioContainer.Header = valueAsString;
            
            if (IsFlagsEnum)
            {
                for (int i = 0; i < values.Length; i++)
                {
                    bool isChecked = valueAsString.Contains(values[i]);
                    if (isChecked != ((RadioButton)items[i]).IsChecked)
                    {
                        ((RadioButton)items[i]).IsChecked = isChecked;                        
                    }
                }
            }
            else
            {
                var index = Array.IndexOf(values, valueAsString);
                ((RadioButton)items[index]).IsChecked = true;
            }
            _internalSet = false;
        }

        public override object TryParse(object currentValue)
        {
            var type = currentValue.GetType();
            
            if (!type.IsEnum) return currentValue;

            var values = Enum.GetValues(type);
            var items = RadioContainer.Items;

            if (values.Length != items.Count) return currentValue;

            if (IsFlagsEnum)
            {
                string newValue = Activator.CreateInstance(type).ToString();
                 
                for (int i = 0; i < items.Count; i++)
                {
                    if ((bool)((RadioButton)items[i]).IsChecked)
                    {
                        newValue += "|" + values.GetValue(i).ToString();
                    }
                }
                return Enum.Parse(type,newValue);
            }
            else
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if ((bool)((RadioButton)items[i]).IsChecked)
                    {
                        return values.GetValue(i);
                    }
                }
                return values.GetValue(0);
            }
        }
    }
}