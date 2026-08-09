using Noesis;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace VECS.UI
{
    public interface IEditorField
    {
        public string Label {get; set; }
        public Action<object, RoutedEventArgs> ValueChanged { get; set; }
        public List<FieldInfo> LocalBindingPath { get; set; }

        public void SetValue(object value);
        public object TryParse(object currentValue);

        public void InternalValueChanged(object sender, RoutedEventArgs args);
    }

    public abstract class VECSEditorControl : Control, IEditorField
    {
        protected bool _internalSet = false;

        public abstract string Label { get; set; }
        public Action<object, RoutedEventArgs> ValueChanged { get; set; }
        public List<FieldInfo> LocalBindingPath { get; set; }

        public abstract void SetValue(object value);

        public abstract object TryParse(object currentValue);

        public void InternalValueChanged(object sender, RoutedEventArgs args)
        {
            if (!_internalSet && sender != null)
            {
                ValueChanged?.Invoke(this, args);
            }
        }

    }
}