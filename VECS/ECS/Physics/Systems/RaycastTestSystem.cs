using System.Numerics;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.ECS.Transforms;

namespace VECS.Physics
{
    public class RaycastTestSystem : SystemBase
    {
        public override void OnUpdate(EntityManager entityManager)
        {
            if (entityManager.SingletonEntity<MainCamera>(out Entity camEntity) && InputManager.Instance.GetMouseButton(0))
            {
                var camera = entityManager.GetComponent<Camera>(camEntity);
                var cameraLTW = entityManager.GetComponent<LocalToWorld>(camEntity);
                var mousePosition = new Vector3(InputManager.Instance.MousePos,3);

                
            }
        }
    }
}
