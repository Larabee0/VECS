using BepuPhysics.CollisionDetection;
using BepuUtilities.Collections;
using BepuUtilities.Memory;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VECS.ECS.Physics
{
    public sealed partial class PhysicsWorld
    {
        public const int MAX_BATCHED_RAYS = 1024;
        public const int MAX_UNBATCHED_RAYS = 1024;
        private struct RayJob
        {
            public int Start;
            public int End;
        }
        public Buffer<RaycastHit> SingleTimeResults;
        private RayHitHandler rayHitHandler;
        private QuickList<RaycastInput> _batchedRayInputs;
        private Buffer<RayJob> jobs;

        private IntersectionAlgorithm _rayBatcherAlgorithm;

        private void InitRayCasting()
        {
            BufferPool.Take(MAX_UNBATCHED_RAYS, out SingleTimeResults);
            rayHitHandler = new() { Hits = SingleTimeResults };
            _rayBatcherAlgorithm = new("Batched", BatchedWorker, BufferPool, MAX_BATCHED_RAYS);
            _batchedRayInputs = new QuickList<RaycastInput>(MAX_BATCHED_RAYS, BufferPool);
        }

        /// <summary>
        /// Returns the first hit object
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="dir"></param>
        /// <param name="maxDst"></param>
        /// <param name="hit"></param>
        /// <returns></returns>
        public bool Raycast(Vector3 origin, Vector3 dir, float maxDst, out RaycastHit hit)
        {
            return Raycast(new(origin, dir, maxDst), out hit);
        }

        /// <summary>
        /// returns the first hit object
        /// </summary>
        /// <param name="raycastInput"></param>
        /// <param name="hit"></param>
        /// <returns></returns>
        public bool Raycast(RaycastInput raycastInput, out RaycastHit hit)
        {
            hit = Raycast(raycastInput);
            return hit.Hit;
        }

        /// <summary>
        /// Returns the first hit object
        /// </summary>
        /// <param name="raycastInput"></param>
        /// <returns></returns>
        public unsafe RaycastHit Raycast(RaycastInput raycastInput)
        {
            Debug.Assert(raycastInput.Valid, string.Format("Raycast input invalid MaxDst = {0}, Direction = {1}", raycastInput.MaxDst, raycastInput.Direction));
            int intersections = 0;
            rayHitHandler.IntersectionCount = &intersections;
            rayHitHandler.ClearOne();
            Simulation.RayCast(raycastInput.Origin, raycastInput.Direction, raycastInput.MaxDst, ref rayHitHandler);
            return rayHitHandler.Hits[0];
        }

        /// <summary>
        /// Returns all hit object
        /// </summary>
        /// <param name="raycastInput"></param>
        /// <returns></returns>
        public unsafe RaycastHit[] RaycastAll(RaycastInput raycastInput)
        {
            Debug.Assert(raycastInput.Valid, string.Format("Raycast input invalid MaxDst = {0}, Direction = {1}", raycastInput.MaxDst, raycastInput.Direction));
            int intersections = 0;
            rayHitHandler.IntersectionCount = &intersections;
            rayHitHandler.ClearAll();

            Simulation.RayCast(raycastInput.Origin, raycastInput.Direction, raycastInput.MaxDst, ref rayHitHandler);
            if (intersections == 0)
            {
                return [];
            }

            RaycastHit[] hits = new RaycastHit[intersections];

            rayHitHandler.Hits.CopyTo(0, hits, 0, intersections);
            return hits;
        }

        /// <summary>
        /// Performs multiple fisrt hit ray casts in a single batch executed on multiple threads
        /// </summary>
        /// <param name="raycasts"></param>
        /// <returns></returns>
        public RaycastHit[] RaycastBatch(params RaycastInput[] raycasts)
        {
            Debug.Assert(raycasts.Length > 0,"Batched Raycast inputs must be at least length 1");
            Debug.Assert(raycasts.Length <= MAX_BATCHED_RAYS, "Batched raycast inputs must not exceed hits MAX_BATCHED_RAYS");
            _batchedRayInputs.Clear();
            _batchedRayInputs.AddRangeUnsafely(raycasts);
            _rayBatcherAlgorithm.Execute(ref _batchedRayInputs, ThreadDispatcher);
            RaycastHit[] hits = new RaycastHit[_rayBatcherAlgorithm.IntersectionCount];
            _rayBatcherAlgorithm.Results.CopyTo(0, hits, 0, _rayBatcherAlgorithm.IntersectionCount);
            return hits;
        }

        private unsafe int BatchedWorker(int workerIndex, IntersectionAlgorithm algorithm)
        {
            int intersectionCount = 0;
            var hitHandler = new RayHitHandler { Hits = algorithm.Results, IntersectionCount = &intersectionCount };
            var batcher = new SimulationRayBatcher<RayHitHandler>(ThreadDispatcher.GetThreadMemoryPool(workerIndex), Simulation, hitHandler, 2048);
            int claimedIndex;
            while ((claimedIndex = Interlocked.Increment(ref algorithm.JobIndex)) < jobs.Length)
            {
                ref var job = ref jobs[claimedIndex];
                for (int i = job.Start; i < job.End; ++i)
                {
                    ref var ray = ref _batchedRayInputs[i];
                    Debug.Assert(ray.Valid, string.Format("Raycast input Worker Index = {2} Job Index = {3} invalid MaxDst = {0}, Direction = {1}", ray.MaxDst, ray.Direction, workerIndex, i));
                    batcher.Add(ref ray.Origin, ref ray.Direction, ray.MaxDst, i);
                }
            }
            batcher.Flush();
            batcher.Dispose();
            return intersectionCount;
        }
    }
}
