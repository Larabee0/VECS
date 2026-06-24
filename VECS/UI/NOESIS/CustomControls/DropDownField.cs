using Noesis;
using System;

namespace VECS.UI
{
    public class DropDownField : UserControl, IEditorField
    {

        public static readonly DependencyProperty _label = DependencyProperty.Register("Label", typeof(string), typeof(DropDownField), new PropertyMetadata("Label"));
        //public static readonly DependencyProperty _combBox = DependencyProperty.Register("CombBox", typeof(ComboBox), typeof(DropDownField), new PropertyMetadata(new ComboBox()));

        public string Label
        {
            get { return (string)GetValue(_label); }
            set { SetValue(_label, value); }
        }

        public TreeViewItem RadioContainer
        {
            get 
            { 
                return (TreeViewItem)FindName("RadioContainer");
            }
        }

        public bool IsFlagsEnum { get; set; }

        public DropDownField()
        {
            InitializeComponent();
        }

        void InitializeComponent()
        {
            GUI.LoadComponent(this, "Editor/DropDownField.xaml");
        }

        public void SetValue(object value)
        {
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
        }

    }
}