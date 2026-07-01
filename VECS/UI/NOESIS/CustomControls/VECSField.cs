using Noesis;
using System;

namespace VECS.UI
{
    public interface IEditorField
    {
        public string Label {get; set; }
        public Action<object, RoutedEventArgs> ValueChanged { get; set; }

        public void SetValue(object value);
    }

    public abstract class VECSEditorControl : UserControl, IEditorField
    {
        protected bool _internalSet = false;

        public abstract string Label { get; set; }
        public Action<object, RoutedEventArgs> ValueChanged { get; set; }

        public abstract void SetValue(object value);

        public abstract object TryParse(object currentValue);

        protected void InternalValueChanged(object sender, RoutedEventArgs args)
        {
            if (!_internalSet)
            {
                ValueChanged?.Invoke(this, args);
            }
        }

    }
}