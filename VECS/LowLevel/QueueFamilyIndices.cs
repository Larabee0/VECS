namespace VECS.LowLevel
{
    /// <summary>
    /// graphics queue famil indices.
    /// </summary>
    public struct QueueFamilyIndices
    {
        public uint graphicsFamily;
        public uint computeFamily;
        public uint presentFamily;
        public int computeIndex;
        public int presentIndex;

        public bool graphicsFamilyHasValue;
        public bool computeFamilyHasValue;
        public bool presentFamilyHasValue;
        public readonly bool IsComplete => graphicsFamilyHasValue && computeFamilyHasValue && presentFamilyHasValue;
    }
}