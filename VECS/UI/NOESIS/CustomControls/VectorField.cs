using Noesis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VECS.UI
{
    public class VectorField : UserControl
    {

        public static readonly DependencyProperty _Label = DependencyProperty.Register("Label", typeof(string), typeof(TextBlock), new PropertyMetadata("Label"));
        public static readonly DependencyProperty _ValueX = DependencyProperty.Register("ValueX", typeof(string), typeof(TextBox), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueY = DependencyProperty.Register("ValueY", typeof(string), typeof(TextBox), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueZ = DependencyProperty.Register("ValueZ", typeof(string), typeof(TextBox), new PropertyMetadata("0"));
        public static readonly DependencyProperty _ValueW = DependencyProperty.Register("ValueW", typeof(string), typeof(TextBox), new PropertyMetadata("0"));

        public string Label
        {
            get { return (string)GetValue(_Label); }
            set { SetValue(_Label, value); }
        }

        public string ValueX
        {
            get { return (string)GetValue(_ValueX); }
            set { SetValue(_ValueX, value); }
        }

        public string ValueY
        {
            get { return (string)GetValue(_ValueY); }
            set { SetValue(_ValueY, value); }
        }

        public string ValueZ
        {
            get { return (string)GetValue(_ValueZ); }
            set { SetValue(_ValueZ, value); }
        }

        public string ValueW
        {
            get { return (string)GetValue(_ValueW); }
            set { SetValue(_ValueW, value); }
        }

        public VectorField()
        {
            InitializeComponent();
        }

        void InitializeComponent()
        {
            GUI.LoadComponent(this, "Editor/VectorPage.xaml");
        }
    }
}
