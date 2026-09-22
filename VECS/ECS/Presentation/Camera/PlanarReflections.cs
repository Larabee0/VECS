using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using VECS.ECS.Transforms;

namespace VECS.ECS.Presentation
{

    public struct ReflectMainCamera : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public Entity Target;
    }

    [UpdateAfter(typeof(CameraSystem))]
    internal class PlanarReflections : SystemBase
    {
        private EntityQuery _reflectCamera; // query for indexing cameras
        private EntityQuery _mainCameraQuery;

        private static readonly Matrix4x4 yReflect =  Matrix4x4.CreateReflection(new Plane(Vector3.UnitY, 0));

        public override void OnCreate(EntityManager entityManager)
        {
            _reflectCamera = new EntityQuery(entityManager)
                .WithAll(typeof(Camera), typeof(ReflectMainCamera))
                .WithAny(typeof(CameraOrthographic), typeof(CameraPerspective))
                .WithNone(typeof(Prefab))
                .Build();

            _mainCameraQuery = new EntityQuery(entityManager)
                .WithAll(typeof(Camera), typeof(MainCamera))
                .WithAny(typeof(CameraOrthographic), typeof(CameraPerspective))
                .WithNone(typeof(Prefab))
                .Build();
        }

        public override void OnUpdate(EntityManager entityManager)
        {
            if(_reflectCamera.HasEntities && _mainCameraQuery.HasEntities)
            {

                var mainCamera = entityManager.GetComponent<Camera>(_mainCameraQuery.GetEntities()[0]);
                var mainCameraLTW = Matrix4x4.Identity;
                if(entityManager.GetComponent<LocalToWorld>(_mainCameraQuery.GetEntities()[0], out var ltw))
                {
                    mainCameraLTW = ltw.Value;
                }



                _reflectCamera.GetEntities().ForEach(entity =>
                {
                    var targetPlane = entityManager.GetComponent<ReflectMainCamera>(entity).Target;

                    var targetPlaneLTW = TransformExtensions.TRS(new Vector3(0,2,1), TransformExtensions.Euler(0 * TransformExtensions.Deg2Rad,90 * TransformExtensions.Deg2Rad, 00 * TransformExtensions.Deg2Rad), Vector3.One);

                    if(entityManager.GetComponent<LocalToWorld>(targetPlane,out var ltw))
                    {
                        //targetPlaneLTW = ltw.Value;
                    }
                    

                    DebugDrawer.DrawLine(targetPlaneLTW.Translation, targetPlaneLTW.Translation + (targetPlaneLTW.Forward()));
                    var plane = Vector3.Transform(Vector3.UnitX * -1, targetPlaneLTW);
                    Matrix4x4 reflect = Reflect(new(-targetPlaneLTW.Forward(), 0));
                    
                    reflect = reflect* mainCamera.ViewMatrix;

                    //reflect = reflect * Matrix4x4.CreateRotationY(0 * TransformExtensions.Deg2Rad);
                    //reflect[2, 2] = -1.0f;
                    //reflect[0, 0] = -1.0f;


                    var viewReflected = Reflect(new(-Vector3.UnitX,0)) * mainCamera.ViewMatrix;// UnityBasedReflected(mainCameraLTW, targetPlaneLTW);// CameraSystem.GetViewMatrix(reflect); //mainCamera.ViewMatrix * reflect;



                    Matrix4x4.Invert(viewReflected, out var inverted);

                    var cam = entityManager.GetComponent<Camera>(entity);
                    cam.ProjectionMatrix = mainCamera.ProjectionMatrix;
                    cam.ViewMatrix = viewReflected;
                    cam.InverseViewMatrix = inverted;
                    cam.CullMode = mainCamera.CullMode;
                    cam.ClipNear = mainCamera.ClipNear;
                    cam.ClipFar = mainCamera.ClipFar;
                    entityManager.SetComponent(entity,cam);
                    
                });
            }
        }

        private static Matrix4x4 UnityBasedReflected(Matrix4x4 maincamera, Matrix4x4 reflector)
        {
            Matrix4x4.Invert(reflector, out var reflectorInverse);

            var reflectedPosition = Vector3.Transform(maincamera.Translation, reflectorInverse);
            var reflectedDirection = Vector3.Transform(maincamera.Translation + maincamera.Forward(), reflectorInverse);
            Matrix4x4.Decompose(reflector, out _, out var reflectorRot, out _);
            var reflectorTransform = TransformExtensions.TRS(reflector.Translation, reflectorRot, -Vector3.One);
            reflectedPosition = Vector3.Transform(reflectedPosition, reflectorTransform);
            reflectedDirection = Vector3.Transform(reflectedDirection, reflectorTransform);
            reflectedDirection = Vector3.Normalize(reflectedDirection - reflectedPosition);
            return CameraSystem.GetViewMatrix( TransformExtensions.TRS(reflectedPosition, TransformExtensions.QuaternionLookRotation(reflectedDirection,Vector3.UnitY), Vector3.One));
        }

        public static Matrix4x4 Reflect(Vector4 planeWorldSpace)
        {
            Matrix4x4 reflectM = Matrix4x4.Identity;

            if (planeWorldSpace.AsVector3().Length() > 0.5f && MathF.Abs(planeWorldSpace.Y - 1.0f) < 1e-3f && MathF.Abs(planeWorldSpace.X) < 1e-3f && MathF.Abs(planeWorldSpace.Z) < 1e-3f)
            {
                reflectM[1,1] = -1.0f;
            }
            else
            {
                // General plane reflection matrix R = I - 2*n*n^T for normalized plane; ignore translation for now
                Vector3 n = Vector3.Normalize(planeWorldSpace.AsVector3());
                Matrix3x3 R = new Matrix3x3(Vector3.One, Vector3.One, Vector3.One) - 2.0f * Matrix3x3.OuterProduct(n, n);
                reflectM = Matrix3x3.Make4x4(R);
            }

            //reflectM = Matrix4x4.Transpose(reflectM);

            return reflectM;
        }

        private static void ComputeFromMirroredReference(Matrix4x4 probe, Vector3 referencePlane)
        {
            ComputeFrom(probe, referencePlane, Quaternion.Identity, out var proxyPosition, out var proxyRotation, out var referencePositionm, out var referenceRotationm, out var influenceToWorld);

            var proxyMatrix = TransformExtensions.TRS(proxyPosition, proxyRotation,Vector3.One);
            var mirrorPosition = Vector3.Transform(Vector3.Zero, proxyMatrix);
            var referenceRotation = TransformExtensions.QuaternionLookRotation(mirrorPosition - referencePositionm, Vector3.UnitY);
        }

        private static void ComputeFrom(Matrix4x4 probe,Vector3 referencePlane, Quaternion referenceRotation, out Vector3 proxyPosition, out Quaternion proxyRotation, out Vector3 referencePosition, out Quaternion referenceRotationOut, out Matrix4x4 influenceToWorld)
        {

            referencePosition = Vector3.Transform(referencePlane, probe);

            proxyPosition = probe.Translation;
            if (Vector3.Distance(proxyPosition, referencePosition) < 1e-4f)
            {
                referencePosition += new Vector3(1e-4f, 1e-4f, 1e-4f);
            }

            proxyRotation = referenceRotation;
            referenceRotationOut = proxyRotation;
            influenceToWorld = probe;
        }
    }
}
