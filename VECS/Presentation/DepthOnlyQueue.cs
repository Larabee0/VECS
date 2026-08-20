using System;
using System.Threading;

namespace VECS
{
    internal class DepthOnlyQueue : QueueBase
    {
        public DepthOnlyQueue(string name) : base(name)
        {

        }

        internal override void IncrementQueueCount(in MaterialProviderFrozen entityData)
        {
            if (entityData.HasDepthOnly)
            {
                Interlocked.Increment(ref _queueCount);
            }
        }

        internal override void AddToQueue(in MaterialProviderFrozen entityData)
        {
            if (entityData.HasDepthOnly)
            {
                var index = Interlocked.Increment(ref _queueIndexer) - 1;
                _queue[index] = entityData;
            }
        }

        internal override void SortQueuePhaseOne()
        {
            if (_queueCount > 0)
            {
                Array.Sort(_queue, new SortByDepthOnly());

                CountDepthIslands();

            }
        }
    }
}