using System;
using System.Collections.Generic;
using VECS.ECS;
using VECS.ECS.Presentation;

namespace VECS
{
    public struct MaterialProviderComponent : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public int Value;
    }


    public static class MaterialProviderDataBase
    {
        private static Entity[] _entities;
        private static MaterialProviderFrozen[] _materialInfo;
        private static DirectSubMeshIndex[] _meshInfo;

        private static MaterialProviderFrozen[] _fowardQueue;
        private static MaterialProviderFrozen[] _depthOnlyQueue;
        private static MaterialProviderFrozen[] _deferredQueue;
        private static MaterialProviderFrozen[] _transparentQueue;

        private static int _entityCount;
        private static int _forwardCount;
        private static int _depthOnlyCount;
        private static int _deferredCount;
        private static int _transparentCount;


        public static void RebuildStructure(EntityManager entityManager, List<Entity> entities)
        {
            _entityCount = entities.Count;

            Array.Resize(ref _entities, _entityCount);
            Array.Resize(ref _materialInfo, _entityCount);
            _forwardCount = 0;
            _depthOnlyCount = 0;
            _deferredCount = 0;
            _transparentCount = 0;

            for (int i = 0; i < _entityCount; i++)
            {
                var entity = entities[i];
                var frozenData = AssetDataBase<MaterialProvider>.GetHashed(entityManager.GetComponent<MaterialProviderComponent>(entity).Value).GetFrozen(i);
                var meshData = entityManager.GetComponent<DirectSubMeshIndex>(entity);
                _meshInfo[i] = meshData;
                _entities[i] = entity;
                _materialInfo[i] = frozenData;

                if (frozenData.IsTransparent)
                {
                    _transparentCount++;
                }
                else if (frozenData.IsDeferred)
                {
                    _deferredCount++;
                    _depthOnlyCount++;
                }
                else if (frozenData.IsDepthOnly)
                {
                    _depthOnlyCount++;
                }
                else if (frozenData.IsForward)
                {
                    _forwardCount++;
                    _depthOnlyCount++;
                }
            }

            Array.Resize(ref _fowardQueue, _forwardCount);
            Array.Resize(ref _depthOnlyQueue, _depthOnlyCount);
            Array.Resize(ref _deferredQueue, _deferredCount);
            Array.Resize(ref _transparentQueue, _transparentCount);

            var _forwardIndexer = 0;
            var _depthOnlyIndexer = 0;
            var _deferredIndexer = 0;
            var _transparentIndexer = 0;
            for (int i = 0; i < _entityCount; i++)
            {
                var frozenData =_materialInfo[i];
                if (frozenData.IsTransparent)
                {
                    _transparentQueue[_transparentIndexer] = frozenData;
                    _transparentIndexer++;
                }
                else if (frozenData.IsDeferred)
                {
                    _deferredQueue[_deferredIndexer] = frozenData;
                    _depthOnlyQueue[_depthOnlyIndexer] = frozenData;
                    _deferredIndexer++;
                    _depthOnlyIndexer++;
                }
                else if (frozenData.IsDepthOnly)
                {
                    _depthOnlyQueue[_depthOnlyIndexer] = frozenData;
                    _depthOnlyIndexer++;
                }
                else if (frozenData.IsForward)
                {
                    _fowardQueue[_forwardIndexer] = frozenData;
                    _depthOnlyQueue[_depthOnlyIndexer] = frozenData;
                    _forwardIndexer++;
                    _depthOnlyIndexer++;
                }
            }

            if(_depthOnlyCount > 0)
            {
                Array.Sort(_depthOnlyQueue, new SortByDepthPipeline());
                Array.Sort(_depthOnlyQueue, new SortByDepthMaterial());
            }

            if(_deferredCount > 0)
            {
                Array.Sort(_deferredQueue, new SortByDeferredPipeline());
                Array.Sort(_deferredQueue, new SortByDeferredMaterial());
            }

            if(_forwardCount > 0)
            {
                Array.Sort(_fowardQueue, new SortByForwardPipeline());
                Array.Sort(_fowardQueue, new SortByForwardMaterial());
            }

            if(_transparentCount > 0)
            {
                Array.Sort(_transparentQueue, new SortByForwardPipeline());
                Array.Sort(_transparentQueue, new SortByForwardMaterial());
            }
        }
    }
}