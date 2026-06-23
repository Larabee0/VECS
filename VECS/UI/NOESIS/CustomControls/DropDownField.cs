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

        public ComboBox ComboBox
        {
            get 
            { 
                return (ComboBox)FindName("ComboBox");
            }
            
        }

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
            var index = Array.IndexOf(values,value);
            ComboBox.SelectedIndex = index;
        }

    }
}