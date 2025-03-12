using System;

namespace VECS.ECS
{
    /// <summary>
    /// an entity composed of a uint id and int version.
    /// the first version number is 1
    /// An null entity can be defined as an entity with a version number of zero
    /// </summary>
    public struct Entity
    {
        public static Entity Null => new(0, 0);

        public uint Id;
        public int Version;

        public Entity(uint id, int version)
        {
            Id = id;
            Version = version;
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(Id, Version);
        }

        public static bool operator ==(Entity left, Entity right)
        {
            return left.Id == right.Id && left.Version == right.Version;
        }

        public static bool operator !=(Entity left, Entity right)
        {
            return !(left == right);
        }

        public static bool Equals(Entity x, Entity y)
        {
            return x == y;
        }

        public readonly bool Equals(Entity other)
        {
            return this == other;
        }

        public override readonly bool Equals(object obj)
        {
            return obj is Entity vertex && vertex.Equals(this);
        }
    }
}
