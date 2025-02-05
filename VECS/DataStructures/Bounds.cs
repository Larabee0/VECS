using System.Numerics;

namespace VECS
{
    public struct Bounds
    {
        public Vector3 center;
        public Vector3 extents;
        public readonly Vector3 Min => center - extents;
        public readonly Vector3 Max => center + extents;

        public Vector3 Size
        {   
            readonly get => extents * 2f;
            set => extents = value * 0.5f;
        }

        public Bounds(Vector3 center, Vector3 extents)
        {
            this.center = center;
            this.extents = extents;
        }

        public void Encapsulate(Vector3 point)
        {
            SetMinMax(Vector3.Min(Min,point),Vector3.Max(Max,point));
        }

        private void SetMinMax(Vector3 min, Vector3 max)
        {
            extents = (max - min) * 0.5f;
            center = min + extents;
        }
    }
}