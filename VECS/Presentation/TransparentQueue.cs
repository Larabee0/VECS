using System;
using System.Collections.Generic;
using System.Threading;

namespace VECS
{
    internal class TransparentQueue : QueueBase
    {
        private readonly HashSet<ulong> TransparentMats = [];

        public TransparentQueue(string name) : base(name)
        {
            AssetDataBase<Material>.AddOnAddedListener(OnMaterialAdded);
            AssetDataBase<Material>.AddOnRemovedListener(OnMaterialRemoved);
        }

        private void OnMaterialAdded(Material newMaterial)
        {
            if (newMaterial.Pipeline.PipelineType != PipelineType.Transparent) return;
            TransparentMats.Add(newMaterial.CombinedHash);
        }

        private void OnMaterialRemoved(Material oldMaterial)
        {
            TransparentMats.Remove(oldMaterial.CombinedHash);
        }

        internal override void IncrementQueueCount(in MaterialProviderFrozen entityData)
        {
            if (TransparentMats.Contains( entityData.ColourHash))
            {
                Interlocked.Increment(ref _queueCount);
            }
        }

        internal override void AddToQueue(in MaterialProviderFrozen entityData)
        {
            if (TransparentMats.Contains(entityData.ColourHash))
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