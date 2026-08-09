using Noesis;
using System;
using System.Collections.Generic;

namespace VECS.UI
{
    public class DropDownField : VECSEditorControl
    {

        public static readonly DependencyProperty _label = DependencyProperty.Register("Label", typeof(string), typeof(DropDownField), new PropertyMetadata("Label"));
        public static readonly DependencyProperty _header = DependencyProperty.Register("Header", typeof(string), typeof(DropDownField), new PropertyMetadata(""));

        public override string Label
        {
            get { return (string)GetValue(_label); }
            set { SetValue(_label, value); }
        }

        public string Header
        {
            get { return (string)GetValue(_header); }
            set { SetValue(_header, value); }
        }

        private bool _hasValueEverBeenSet;

        public TreeViewItem RadioContainer => (TreeViewItem)GetTemplateChild("RadioContainer");

        public bool IsFlagsEnum { get; set; }

        public List<RadioButton> RadioButtons = [];

        private bool _appliedTemplate;

        public DropDownField()
        {
            ApplyTemplate();
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            if (_appliedTemplate) return;
            _internalSet = true;
            //WeakReference weak = new(this);
            for (int i = 0; i < RadioButtons.Count; i++)
            {
                RadioContainer.Items.Add(RadioButtons[i]);
                WeakReference weakButton = new(RadioButtons[i]);
                ((RadioButton)weakButton.Target).Checked += (s, e) => (this)?.InternalValueChanged(s, e);
                ((RadioButton)weakButton.Target).Unchecked += (s, e) => (this)?.InternalValueChanged(s, e);
            }

            _internalSet = false;
            _appliedTemplate = true;
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
            if (RadioContainer == null)
            {
                RadioButtons.Add(button);
            }
            else
            {
                RadioContainer.Items.Add(button);
                WeakReference weak = new(this);
                WeakReference weakButton = new(button);
                ((RadioButton)weakButton.Target).Checked += (s, e) => ((DropDownField)weak.Target)?.InternalValueChanged(s, e);
                ((RadioButton)weakButton.Target).Unchecked += (s, e) => ((DropDownField)weak.Target)?.InternalValueChanged(s, e);
            }
        }

        public override void SetValue(object value)
        {
            _internalSet = true;
            var values = Enum.GetNames(value.GetType());
            var items = RadioButtons;

            string valueAsString =value.ToString();
            Header = valueAsString;
            
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
            _hasValueEverBeenSet = true;
        }

        public override object TryParse(object currentValue)
        {
            if (!_hasValueEverBeenSet) return currentValue;
            var type = currentValue.GetType();
            
            if (!type.IsEnum) return currentValue;

            var values = Enum.GetValues(type);
            var items = RadioButtons;

            if (values.Length != items.Count) return currentValue;

            if (IsFlagsEnum)
            {
                string newValue = "";
                 
                for (int i = 0; i < items.Count; i++)
                {
                    if ((bool)((RadioButton)items[i]).IsChecked)
                    {
                        if (newValue == "")
                        {
                            newValue = values.GetValue(i).ToString();
                        }
                        else
                        {
                            newValue += "|" +values.GetValue(i).ToString();
                        }

                    }
                }
                if (newValue == "")
                {
                    newValue = Activator.CreateInstance(type).ToString();
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
                SetValue(currentValue);
                return currentValue;
            }
        }
    }
}