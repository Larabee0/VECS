using System;
using System.Collections.Generic;
using System.Linq;
namespace VECS
{
    public static class Time
    {
        private readonly static DateTime startTime;

        private static DateTime currentTime;
        private static double deltaTime;

        public static double DeltaTimeAsDouble => deltaTime;
        public static float DeltaTime => (float)deltaTime;
        public static double TimeSinceStartUpAsDouble => (DateTime.UtcNow - startTime).TotalSeconds;
        public static float TimeSinceStartUp => (float)TimeSinceStartUpAsDouble;

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
    }
}
