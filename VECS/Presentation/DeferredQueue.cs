using System;
using System.Collections.Generic;
using System.Threading;

namespace VECS
{
    internal class DeferredQueue : QueueBase
    {
        private readonly HashSet<ulong> DeferredMats = [];

        public DeferredQueue(string name) : base(name)
        {
            AssetDataBase<Material>.AddOnAddedListener(OnMaterialAdded);
            AssetDataBase<Material>.AddOnRemovedListener(OnMaterialRemoved);
        }

        private void OnMaterialAdded(Material newMaterial)
        {
            if (newMaterial.Pipeline.PipelineType != PipelineType.Deferred) return;
            DeferredMats.Add(newMaterial.CombinedHash);
        }

        private void OnMaterialRemoved(Material oldMaterial)
        {
            DeferredMats.Remove(oldMaterial.CombinedHash);
        }


        internal override void IncrementQueueCount(in MaterialProviderFrozen entityData)
        {
            if (DeferredMats.Contains(entityData.ColourHash))
            {
                Interlocked.Increment(ref _queueCount);
            }
        }

        internal override void AddToQueue(in MaterialProviderFrozen entityData)
        {
            if (DeferredMats.Contains(entityData.ColourHash))
            {
                var index = Interlocked.Increment(ref _queueIndexer) - 1;
                _queue[index] = entityData;
            }
        }

        internal override void SortQueuePhaseOne()
        {
            if (_queueCount > 0)
            {
                Array.Sort(_queue, new SortByColour());
                CountColourIslands();
            }
        }
    }
}