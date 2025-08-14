namespace VECS.LowLevel
{
    /// <summary>
    /// graphics queue famil indices.
    /// </summary>
    public struct QueueFamilyIndices
    {
        public int graphicsFamily;
        public int computeFamily;
        public int presentFamily;
        public int computeIndex;
        public int presentIndex;

        public bool graphicsFamilyHasValue;
        public bool computeFamilyHasValue;
        public bool presentFamilyHasValue;
        public readonly bool IsComplete => graphicsFamilyHasValue && computeFamilyHasValue && presentFamilyHasValue;
    }
}