using BepuUtilities;
using BepuUtilities.Collections;
using BepuUtilities.Memory;
using System;
using System.Threading;

namespace VECS.Physics
{
    unsafe class IntersectionAlgorithm
    {
        public string Name;
        public int IntersectionCount;
        public Buffer<RaycastHit> Results;

        private readonly Func<int, IntersectionAlgorithm, int> worker;
        private readonly Action<int> internalWorker;
        public int JobIndex;

        public IntersectionAlgorithm(string name, Func<int, IntersectionAlgorithm, int> worker,
            BufferPool pool, int largestRayCount)
        {
            Name = name;
            this.worker = worker;
            internalWorker = ExecuteWorker;
            pool.Take(largestRayCount, out Results);
        }

        void ExecuteWorker(int workerIndex)
        {
            var intersectionCount = worker(workerIndex, this);
            Interlocked.Add(ref IntersectionCount, intersectionCount);
        }

        public void Execute(ref QuickList<RaycastInput> rays, IThreadDispatcher dispatcher)
        {
            for (int i = 0; i < rays.Count; ++i)
            {
                Results[i].T = float.MaxValue;
                Results[i].Hit = false;
            }
            JobIndex = -1;
            IntersectionCount = 0;
            if (dispatcher != null)
            {
                dispatcher.DispatchWorkers(internalWorker);
            }
            else
            {
                internalWorker(0);
            }
        }
    }
}
