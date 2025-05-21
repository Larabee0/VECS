using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using VECS;
using VECS.ECS;
using VECS.ECS.Physics;
using VECS.ECS.Presentation;
using VECS.ECS.Transforms;

namespace Planets
{
    public struct ShipStatsMS : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public float Thrust;
        public Vector3 TurnTorque;
        public float ForceMult;

        public float Sensitivity;
        public float AggressiveTurnAngle;
    }

    public struct ShipControlInputMS : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public float Throttle;
        public Vector3 Stick;
        public Entity Engines;
    }

    public struct MouseFlightController : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public float TPScamSmoothSpeed;
        public float MouseSensitivity;
        public float ThrottleSenstivity;
        public bool IsMouseAimFrozen;
        public Vector3 frozenDirection;

        public Entity MouseAim;
        public Entity CameraEntity;
        public Entity CameraRig;
    }

    public class MouseFlightShipMover : SystemBase
    {
        private EntityQuery _shipQuery;
        private EntityQuery _flightRigQuery;

        public override void OnCreate(EntityManager entityManager)
        {
            _shipQuery = new EntityQuery(entityManager)
                .WithAll(typeof(ShipStatsMS), typeof(ShipControlInputMS), typeof(DynamicBodyTag), typeof(DynamicHandleComp))
                .WithNone(typeof(Prefab))
                .Build();
            _flightRigQuery = new EntityQuery(entityManager)
                .WithAll(typeof(MouseFlightController), typeof(LocalToWorld))
                .WithNone(typeof(Prefab))
                .Build();
        }

        public override void OnUpdate(EntityManager entityManager)
        {
            if (_shipQuery.HasEntities && _flightRigQuery.HasEntities)
            {
                var shipEntity = _shipQuery.GetEntities()[0];
                var FlightRig = _flightRigQuery.GetEntities()[0];
                var msc = entityManager.GetComponent<MouseFlightController>(FlightRig);
                var input = entityManager.GetComponent<ShipControlInputMS>(shipEntity);
                var mouseAimTransform = entityManager.GetComponent<LocalToWorld>(msc.MouseAim).Value;
                var shipTransform = entityManager.GetComponent<LocalToWorld>(shipEntity).Value;
                var shipStats = entityManager.GetComponent<ShipStatsMS>(shipEntity);

                entityManager.SetComponent(FlightRig, new Translation() { Value = shipTransform.Translation });

                input.Throttle = ThrottleInput(input.Throttle, msc.ThrottleSenstivity);

                var enginesColour = entityManager.GetComponent<RenderMesh>(input.Engines);

                enginesColour.Colour.W = MathF.Abs(input.Throttle);

                entityManager.SetComponent(input.Engines, enginesColour);

                var stickIn = GetStickOverrides();

                bool rollOverride = false;
                bool pitchOverride = false;
                bool yawOverride = false;
                // roll (x)
                switch (Math.Abs(stickIn.X))
                {
                    case > 0.25f:
                        rollOverride = true;
                        break;
                }

                // pitch (y)
                switch (Math.Abs(stickIn.Y))
                {
                    case > 0.25f:
                        yawOverride = true;
                        pitchOverride = true;
                        rollOverride = true;
                        break;
                }
                // yaw (z)
                switch (Math.Abs(stickIn.Z))
                {
                    case > 0.25f:
                        yawOverride = true;
                        pitchOverride = true;
                        rollOverride = true;
                        break;
                }

                Vector3 mouseAimPos = mouseAimTransform.Translation + (-mouseAimTransform.Forward() * 500f);

                RunAutopilot(mouseAimPos, shipTransform, shipStats.Sensitivity, shipStats.AggressiveTurnAngle, out float autoYaw, out float autoPitch, out float autoRoll);

                input.Stick.X = rollOverride ? stickIn.X : autoRoll;
                input.Stick.Y = pitchOverride ? stickIn.Y : autoPitch;
                input.Stick.Z = yawOverride ? stickIn.Z : autoYaw;
                //input.Stick = stickIn;
                entityManager.SetComponent(shipEntity, input);
                RotateRig(entityManager,
                    msc.MouseSensitivity,
                    msc.TPScamSmoothSpeed,
                    msc.CameraEntity,
                    entityManager.GetComponent<LocalToWorld>(msc.CameraEntity).Value,
                    msc.MouseAim,
                    mouseAimTransform,
                    msc.CameraRig,
                    entityManager.GetComponent<LocalToWorld>(msc.CameraRig).Value);
            }
        }

        public override void OnFixedUpdate(EntityManager entityManager)
        {
            if (_shipQuery.HasEntities)
            {
                var entities = _shipQuery.GetEntities();

                entities.ForEach(e =>
                {
                    var wtl = entityManager.GetComponent<LocalToWorld>(e).Value;
                    //wtl = wtl.Invert();

                    var bodyHandle = entityManager.GetComponent<DynamicHandleComp>(e).Value;
                    var stats = entityManager.GetComponent<ShipStatsMS>(e);
                    var input = entityManager.GetComponent<ShipControlInputMS>(e);

                    if (World.Simulation.Simulation.Bodies.BodyExists(bodyHandle))
                    {
                        Vector3 turnTorque = stats.TurnTorque;
                        var rigid = World.Simulation.Simulation.Bodies.GetBodyReference(bodyHandle);
                        var forceVector = stats.ForceMult * input.Throttle * stats.Thrust * Vector3.UnitZ;
                        var torqueVector = stats.ForceMult *
                            new Vector3(turnTorque.X * input.Stick.Y,
                            -stats.TurnTorque.Y * input.Stick.Z,
                            stats.TurnTorque.Z * input.Stick.X);

                        forceVector = Vector3.Transform(forceVector, wtl);
                        torqueVector = Vector3.Transform(torqueVector, wtl);
                        rigid.Awake = true;

                        if (forceVector.LengthSquared() > Vector3.Epsilon.LengthSquared())
                        {

                            rigid.Velocity.Linear = forceVector * rigid.LocalInertia.InverseMass;
                        }
                        //rigid.ApplyImpulse(forceVector, Vector3.Zero);

                        //Console.WriteLine(new Vector4(input.Stick, input.Throttle));
                        //rigid.ApplyAngularImpulse(torqueVector);
                        if (torqueVector.LengthSquared() > Vector3.Epsilon.LengthSquared())
                        {
                            rigid.Velocity.Angular = torqueVector * rigid.LocalInertia.InverseMass;
                        }
                    }
                });
            }
        }

        private static float ThrottleInput(float current, float sensitivity)
        {
            float axisValue = 0;
            if (InputManager.Instance.GetKey(SDL3.SDL_Keycode.LeftControl))
            {
                axisValue = -1f;
            }
            else if (InputManager.Instance.GetKey(SDL3.SDL_Keycode.LeftShift))
            {
                axisValue = 1f;
            }
            bool reverse = InputManager.Instance.GetKey(SDL3.SDL_Keycode.B);
            axisValue = axisValue != 0 ? Math.Clamp(current + (axisValue * (sensitivity * Time.DeltaTime)), 0f, 1f) : current;
            axisValue = axisValue > 0 && reverse ? 0 : axisValue;
            axisValue -= reverse ? sensitivity * Time.DeltaTime : 0;
            axisValue = Math.Clamp(axisValue, -0.25f, 1f);
            return axisValue;
        }

        private static Vector3 GetStickOverrides()
        {
            Vector3 stickInput = Vector3.Zero;
            if (InputManager.Instance.GetKey(SDL3.SDL_Keycode.A))
            {
                stickInput.X = -1;
            }
            else if (InputManager.Instance.GetKey(SDL3.SDL_Keycode.D))
            {
                stickInput.X = 1;
            }

            if (InputManager.Instance.GetKey(SDL3.SDL_Keycode.W))
            {
                stickInput.Y = 1;
            }
            else if (InputManager.Instance.GetKey(SDL3.SDL_Keycode.S))
            {
                stickInput.Y = -1;
            }

            if (InputManager.Instance.GetKey(SDL3.SDL_Keycode.Q))
            {
                stickInput.Z = -1;
            }
            else if (InputManager.Instance.GetKey(SDL3.SDL_Keycode.E))
            {
                stickInput.Z = 1;
            }

            return stickInput;
        }


        private static void RunAutopilot(Vector3 flyTarget, Matrix4x4 shipLocalToWorld, float sensitivity, float aggressiveTurnAngle, out float yaw, out float pitch, out float roll)
        {
            var shipWorldToLocal = shipLocalToWorld.Invert();
            Vector3 localFlyTarget = Vector3.Normalize(Vector3.Transform(flyTarget, shipWorldToLocal)) * sensitivity;

            float angleOffTarget = TransformExtensions.Angle(-shipLocalToWorld.Forward(), flyTarget - shipLocalToWorld.Translation);

            yaw = Math.Clamp(localFlyTarget.X, -1f, 1f);
            pitch = Math.Clamp(localFlyTarget.Y, -1f, 1f);

            float agressiveRoll = Math.Clamp(localFlyTarget.X, -1f, 1f);

            float wingsLevelRoll = -shipLocalToWorld.Right().Y;
            float wingsLevelInfluence = TransformExtensions.InverseLerp(0f, aggressiveTurnAngle, angleOffTarget);
            roll = TransformExtensions.Lerp(wingsLevelRoll, agressiveRoll, wingsLevelInfluence);
            //Console.WriteLine(angleOffTarget);

        }

        private static void RotateRig(EntityManager entityManager, float sensitivity, float smoothSpeed, Entity cameraEntity, Matrix4x4 cameraTransform, Entity mouseAim, Matrix4x4 mouseAimTransform, Entity cameraRig, Matrix4x4 cameraRigTransform)
        {
            var rawAxis = InputManager.Instance.MouseDelta;

            float mouseX = float.DegreesToRadians(-rawAxis.X * sensitivity);
            float mouseY = float.DegreesToRadians(rawAxis.Y * sensitivity);

            Matrix4x4.Decompose(mouseAimTransform, out _, out Quaternion mouseAimRot, out _);
            Quaternion qY = Quaternion.CreateFromAxisAngle(cameraTransform.Right(), mouseY);
            Quaternion qX = Quaternion.CreateFromAxisAngle(cameraTransform.Up(), mouseX);

            mouseAimRot = Quaternion.Concatenate(mouseAimRot, Quaternion.Concatenate(qY, qX));

            entityManager.SetComponent(mouseAim, new Rotation() { Value = mouseAimRot });

            Vector3 upVec = (Math.Abs(mouseAimTransform.Forward().Y) > 0.9f) ? cameraRigTransform.Up() : Vector3.UnitY;

            Matrix4x4.Decompose(cameraRigTransform, out _, out Quaternion cameraRigRotation, out _);
            var cameraRigRot = DampCamera(cameraRigRotation,
                TransformExtensions.QuaternionLookRotation(mouseAimTransform.Forward(), upVec),
                smoothSpeed,
                Time.DeltaTime);
            entityManager.SetComponent(cameraRig, new Rotation() { Value = cameraRigRot });
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Quaternion DampCamera(Quaternion a, Quaternion b, float lambda, float dt)
        {
            return Quaternion.Slerp(a, b, 1 - MathF.Exp(-lambda * dt));
        }
    }
}
