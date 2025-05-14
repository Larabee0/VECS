using BepuPhysics.CollisionDetection;
using BepuUtilities.Collections;
using BepuUtilities.Memory;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VECS.Physics
{
    public sealed partial class PhysicsWorld
    {
        public const int MAX_BATCHED_RAYS = 1024;
        private struct RayJob
        {
            public int Start;
            public int End;
        }
        private QuickList<RaycastInput> _batchedRayInputs;
        private Buffer<RayJob> jobs;

        private IntersectionAlgorithm _rayBatcherAlgorithm;

        private void InitRayCasting()
        {
            _rayBatcherAlgorithm = new("Batched", BatchedWorker, BufferPool, MAX_BATCHED_RAYS);
            _batchedRayInputs = new QuickList<RaycastInput>(MAX_BATCHED_RAYS, BufferPool);
        }

        public bool Raycast(Vector3 origin, Vector3 dir, float maxDst, out RaycastHit hit)
        {
            hit = Raycast(origin, dir, maxDst);
            return hit.Hit;
        }

        public RaycastHit Raycast(Vector3 origin, Vector3 dir, float maxDst)
        {
            Simulation.RayCast(origin, dir, maxDst, ref rayHitHandler);
            return rayHitHandler.Hits.Length == 0 ? new RaycastHit() : rayHitHandler.Hits[0];
        }

        public RaycastHit[] RaycastAll(Vector3 origin, Vector3 dir, float maxDst)
        {
            Simulation.RayCast(origin, dir, maxDst, ref rayHitHandler);
            if (rayHitHandler.Hits.Length == 0)
            {
                return null;
            }

            RaycastHit[] hits = new RaycastHit[rayHitHandler.Hits.Length];
            rayHitHandler.Hits.CopyTo(0, hits, 0, rayHitHandler.Hits.Length);
            return hits;
        }

        public RaycastHit[] RaycastBatch(params RaycastInput[] raycasts)
        {
            Debug.Assert(raycasts.Length > 0,"Batched Raycast inputs must be at least length 1");
            Debug.Assert(raycasts.Length <= MAX_BATCHED_RAYS, "Batched raycast inputs must not exceed hits MAX_BATCHED_RAYS");
            _batchedRayInputs.Clear();
            _batchedRayInputs.AddRangeUnsafely(raycasts);
            _rayBatcherAlgorithm.Execute(ref _batchedRayInputs, ThreadDispatcher);
            RaycastHit[] hits = new RaycastHit[_rayBatcherAlgorithm.IntersectionCount];
            _rayBatcherAlgorithm.Results.CopyTo(0, hits, 0, hits.Length);
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
                    batcher.Add(ref ray.Origin, ref ray.Direction, ray.MaximumT, i);
                }
            }
            batcher.Flush();
            batcher.Dispose();
            return intersectionCount;
        }
    }
}
