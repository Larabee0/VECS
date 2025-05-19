using SDL3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using VECS;
using VECS.ECS;
using VECS.ECS.Physics;
using VECS.ECS.Presentation;
using VECS.ECS.Transforms;

namespace Planets
{

    public struct XWingGuns : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public Vector3 TopRight;
        public Vector3 TopLeft;
        public Vector3 BottomRight;
        public Vector3 BottomLeft;

        public readonly Vector3 this[int i] => i switch
        {
            0 => TopRight,
            1 => BottomRight,
            2 => TopLeft,
            3 => BottomLeft,
            _=> throw new IndexOutOfRangeException()
        };
    }

    public struct GunSequencer : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public int Index;
        public float FireTime;
        public float WaitTime;
        public float Clock;
    }

    public class ShipGuns : SystemBase
    {
        private EntityQuery _gunQuery;
        public override void OnCreate(EntityManager entityManager)
        {
            _gunQuery = new EntityQuery(entityManager)
                .WithAll(typeof(XWingGuns),typeof(GunSequencer), typeof(LocalToWorld))
                .WithNone(typeof(Prefab))
                .Build();
        }

        public override void OnUpdate(EntityManager entityManager)
        {

            if (_gunQuery.HasEntities)
            {
                var drawUtils = World.GetSystem<DebugDrawUtilities>();
                _gunQuery.GetEntities().ForEach(entity =>
                {
                    var guns = entityManager.GetComponent<XWingGuns>(entity);
                    var ltw = entityManager.GetComponent<LocalToWorld>(entity).Value;
                    var squencer = entityManager.GetComponent<GunSequencer>(entity);
                    
                    if(InputManager.Instance.GetMouseButton(0) || squencer.Clock != 0)
                    {
                        squencer.Clock += Time.DeltaTime;
                    }

                    if (squencer.Clock != 0 &&squencer.Clock <= squencer.FireTime)
                    {
                        FireLaser(drawUtils, guns[squencer.Index], ltw);
                    }
                    else if (squencer.Clock >= squencer.WaitTime + squencer.FireTime)
                    {
                        squencer.Index = (squencer.Index + 1) % 4;
                        squencer.Clock = 0;
                    }
                    entityManager.SetComponent(entity, squencer);

                });
            }
        }

        private void FireLaser(DebugDrawUtilities drawUtils, Vector3 pos, Matrix4x4 ltw)
        {
            var aimPos = ltw.Translation + (ltw.Forward() * 50f);
            var worldPos = Vector3.Transform(pos, ltw);
            var dir = Vector3.Normalize(aimPos - worldPos);
            var ray = new RaycastInput(worldPos, dir, 50);
            if (World.Simulation.Raycast(ray, out var hit))
            {
                drawUtils.DrawLine(worldPos, ray.GetPoint(hit.T), new(255, 0, 0, 255));
            }
            else
            {
                drawUtils.DrawLine(worldPos, aimPos, new(255, 0, 0, 255));
            }
        }
    }
}
