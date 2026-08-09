using Noesis;
using System;

namespace VECS.UI
{
    public class AssetRefField : VECSEditorControl
    {

        public static readonly DependencyProperty _Label = DependencyProperty.Register("Label", typeof(string), typeof(AssetRefField), new PropertyMetadata("Label"));
        public static readonly DependencyProperty _AssetName = DependencyProperty.Register("AssetName", typeof(string), typeof(AssetRefField), new PropertyMetadata("None"));

        public override string Label
        {
            get { return (string)GetValue(_Label); }
            set { SetValue(_Label, value); }
        }

        public string AssetName
        {
            get { return (string)GetValue(_AssetName); }
            set { SetValue(_AssetName, value); }
        }

        private Type _assetType;

        private Asset _assetValue;

        public AssetRefField()
        {

        }

        public void SetAssetType(Type type)
        {
            _assetType = type;
        }

        public override void SetValue(object value)
        {
            if(value.GetType() == _assetType)
            {
                _assetValue = (Asset)value;
                AssetName = value.ToString();
            }
        }

        public override object TryParse(object currentValue)
        {
            if (currentValue.GetType() == _assetType)
            {
                return _assetValue;
            }
            return currentValue;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            var source = GetTemplateChild("ChangeAssetButton");
            ((Button)source).Click += OnChangeAssetClick;
        }

        private void OnChangeAssetClick(object sender, RoutedEventArgs args)
        {
            throw new NotImplementedException();
        }
    }
}
