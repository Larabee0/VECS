namespace VECS.UI
{
    public interface IEditorField
    {
        public string Label {get;set;}

        public void SetValue(object value);
    }
}