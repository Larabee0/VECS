using System;
namespace VECS
{
    public static class Time
    {
        private readonly static DateTime startTime;

        private const double FixedTimeStepDouble = 1.0 / 120.0;
        private const float FixedTimeStep = (float)FixedTimeStepDouble;
        private static DateTime currentTime;
        private static double deltaTime;

        private static double timeAccumulator;
        public static double DeltaTimeAsDouble => deltaTime;
        public static float DeltaTime => (float)deltaTime;
        public static float FixedDeltaTime => FixedTimeStep;
        public static double TimeSinceStartUpAsDouble => (DateTime.UtcNow - startTime).TotalSeconds;
        public static float TimeSinceStartUp => (float)TimeSinceStartUpAsDouble;
        public static float InterpolationWeight { get; private set; }

        internal static Action FixedTimeStepCallback;

        static Time()
        {
            startTime = DateTime.UtcNow;
            currentTime = DateTime.UtcNow;
        }

        internal static void Update()
        {
            var newTime = DateTime.UtcNow;
            deltaTime = (newTime - currentTime).TotalSeconds;
            currentTime = newTime;
        }

        internal static void UpdateFixedTimeStep()
        {
            timeAccumulator += deltaTime;
            while (timeAccumulator >= FixedTimeStepDouble)
            {
                FixedTimeStepCallback?.Invoke();
                timeAccumulator -= FixedTimeStepDouble;
            }
            InterpolationWeight = (float)timeAccumulator / FixedTimeStep;
        }
    }
}
