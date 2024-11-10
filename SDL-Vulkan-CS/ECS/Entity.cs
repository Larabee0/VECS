using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDL_Vulkan_CS.ECS
{
    public struct Entity
    {
        public uint Id;
        public int Version;

        public Entity(uint id,int version)
        {
            Id = id;
            Version = version;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, Version);
        }
    }
}
