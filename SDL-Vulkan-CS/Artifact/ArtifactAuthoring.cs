using SDL_Vulkan_CS.ECS;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDL_Vulkan_CS.Artifact
{
    public class ArtifactAuthoring
    {
        public Entity MainCamera;

        private Vector3 initalCameraPos = new(0, 0, -2.5f);
        private Quaternion initalCameraRot = Quaternion.Identity;

        private CameraPerspective cameraPerspective = new()
        {
            FOV = 25,
            ClipNear = 0.1f,
            ClipFar = 100f
        };

        public ArtifactAuthoring()
        {
            EntityManager entityManager = World.DefaultWorld.EntityManager;

            MainCamera = entityManager.CreateEntity();
            entityManager.AddComponent(MainCamera, new Translation() { Value = initalCameraPos });
            entityManager.AddComponent(MainCamera, new Rotation() {Value = initalCameraRot });
            entityManager.AddComponent(MainCamera, cameraPerspective);
        }
    }
}
