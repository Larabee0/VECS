using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDL_Vulkan_CS.ECS
{
    public class EntityQuery
    {
        private List<int> _withAll = []; // must have all of these
        private List<int> _withNone = []; // cannot have any of these
        private List<int> _withAny = []; // must have one of these

        private readonly HashSet<int> _withAllSet = [];
        private readonly HashSet<int> _withNoneSet = [];
        private readonly HashSet<int> _withAnySet = [];

        private readonly EntityManager _entityManager;

        private bool _built = false;
        private bool _stale = true;
        private bool _hasEnitities = false;
        public bool Built => _built;

        public bool HasEntities { 
            get
            {
                if (!_built)
                {
                    throw new InvalidOperationException("Cannot check enities in unbuilt EntityQuery");
                }
                if (_stale)
                {
                    AnyEntities();
                }
                return _hasEnitities;
            }
        }

        public bool Stale
        {
            get => _stale;
        }

        public EntityQuery(EntityManager entityManager)
        {
            _entityManager = entityManager;
        }

        public EntityQuery WithAll(params Type[] componentTypes)
        {
            if (_built)
            {
                return this;
            }
            HashSet<int> all = new(_withAll);
            all.UnionWith(_entityManager.GetComponentIds(componentTypes));
            _withAll = new(all);


            if (all.Overlaps(_withNone))
            {
                all.IntersectWith(_withNone);
                string invalidTypes = "";
                foreach (var invalid in all)
                {
                    invalidTypes = string.Format("{0}, {1}", invalidTypes, _entityManager.GetComponentName(invalid));
                }

                throw new InvalidOperationException(string.Format("WithAll query may not contain component types present in _withNone!\nComponent Type mistmatch {0}", invalidTypes));
            }

            if (_withAny.Count > 0)
            {
                HashSet<int> any = new(_withAny);
                any.ExceptWith(all);
                _withAny = new(any);
            }
            return this;
        }

        public EntityQuery WithNone(params Type[] componentTypes)
        {
            if (_built)
            {
                return this;
            }
            HashSet<int> none = new(_withNone);
            none.UnionWith(_entityManager.GetComponentIds(componentTypes));
            _withNone = new(none);

            if(none.Overlaps(_withAll))
            {
                none.IntersectWith(_withAll);
                string invalidTypes = "";
                foreach (var invalid in none)
                {
                    invalidTypes = string.Format("{0}, {1}", invalidTypes, _entityManager.GetComponentName(invalid));
                }

                throw new InvalidOperationException(string.Format("WithNone query may not contain component types present in _withAll\nComponent Type mistmatch {0}", invalidTypes));
            }
            if (none.Overlaps(_withAny))
            {
                none.IntersectWith(_withAny);
                string invalidTypes = "";
                foreach (var invalid in none)
                {
                    invalidTypes = string.Format("{0}, {1}", invalidTypes, _entityManager.GetComponentName(invalid));
                }

                throw new InvalidOperationException(string.Format("WithNone query may not contain component types present in _withAny!\nComponent Type mistmatch {0}", invalidTypes));
            }

            return this;
        }

        public EntityQuery WithAny(params Type[] componentTypes)
        {
            if (_built)
            {
                return this;
            }
            HashSet<int> any = new(_withAny);
            any.UnionWith(_entityManager.GetComponentIds(componentTypes));

            any.ExceptWith(_withAll);

            _withAny = new(any);


            if (any.Overlaps(_withNone))
            {
                any.IntersectWith(_withNone);
                string invalidTypes = "";
                foreach (var invalid in any)
                {
                    invalidTypes = string.Format("{0}, {1}", invalidTypes, _entityManager.GetComponentName(invalid));
                }

                throw new InvalidOperationException(string.Format("WithAny query may not contain component types present in _withNone!\nComponent Type mistmatch {0}", invalidTypes));
            }



            return this;
        }

        public EntityQuery Build()
        {
            if (!_built)
            {
                _withAllSet.UnionWith(_withAll);
                _withNoneSet.UnionWith(_withNone);
                _withAnySet.UnionWith(_withAny);
                _built = true;
            }
            return this;
        }

        public void MarkStale()
        {
            _stale = true;
        }

        public bool AnyEntities()
        {   
            bool any = false;
            Parallel.ForEach(_entityManager._archetypeIdsToComponentIds.Values, (HashSet<int> entitySet, ParallelLoopState state) =>
            {
                if (entitySet.Overlaps(_withAnySet) && entitySet.IsSupersetOf(_withAllSet) && !entitySet.Overlaps(_withNoneSet))
                {
                    any = true;
                    state.Break();
                }
            });
            _stale = false;
            _hasEnitities = any;

            return any;
        }

        public List<Entity> GetEntities()
        {
            HashSet<Entity> entitiesSet = [];

            _withAll.ForEach(compId =>
            {
                if(_entityManager.GetAllEntitiesWithComponent(compId,out var entities))
                {
                    entitiesSet.UnionWith(entities);
                }
            });

            _withNone.ForEach(compId =>
            {
                if(_entityManager.GetAllEntitiesWithComponent(compId,out var entities))
                {
                    entitiesSet.ExceptWith(entities);
                }
            });
            
            List<Entity> entities = new(entitiesSet);
            if (_withAny.Count > 0 && entities.Count > 0)
            {
                for (int i = entities.Count - 1; i >= 0; i--)
                {
                    Entity entity = entities[i];
                    bool hasAny = false;
                    for (int j = 0; j < _withAny.Count; j++)
                    {
                        if (_entityManager.HasComponent(entity, _withAny[j]))
                        {
                            hasAny = true;
                            break;
                        }
                    }
                    if (!hasAny)
                    {
                        entities.RemoveAt(i);
                    }
                }

                entities.TrimExcess();
            }

            return entities;
        }


    }
}
