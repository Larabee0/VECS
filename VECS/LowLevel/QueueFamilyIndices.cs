namespace VECS.LowLevel
{
    /// <summary>
    /// graphics queue famil indices.
    /// </summary>
    public struct QueueFamilyIndices
    {
        public uint graphicsFamily;

        public bool graphicsFamilyHasValue;
        public readonly bool IsComplete => graphicsFamilyHasValue;
    }
}