using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDL_Vulkan_CS.ECS
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
    }
}
