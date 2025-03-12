using System;
using System.Collections.Generic;

namespace VECS.ECS
{
    /// <summary>
    /// Defines an entity query to get entities from the entity world with certain components
    /// With all rqeuires an entity to have all the given components
    /// With none requires an entity to not have the given the components
    /// With any requires the entity to have at least one of the given components
    /// </summary>
    public class EntityQuery
    {
        private List<int> _withAll = []; // must have all of these
        private List<int> _withNone = []; // cannot have any of these
        private List<int> _withAny = []; // must have one of these

        // hashsets of the above lists for O(n) comparison hashset to hashset instead of O(n+m)
        private readonly HashSet<int> _withAllSet = [];
        private readonly HashSet<int> _withNoneSet = [];
        private readonly HashSet<int> _withAnySet = [];

        private readonly EntityManager _entityManager; // probably want to eliminate this reference.
        private readonly List<Entity> entities = [];
        private bool _built = false; // indicate the query has been built so can be used
        private bool _stale = true; // indicates the query should be updated, _hasEnitities will be invalid
        private bool _hasEnitities = false;
        public bool Built => _built;

        public bool HasEntities
        {  
            // if the query is stale, this updates the query
            get
            {
                if (!Built)
                {
                    throw new InvalidOperationException("Cannot check enities in unbuilt EntityQuery");
                }
                if (Stale)
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
            entityManager.AddQuery(this);
        }

        /// <summary>
        /// Adds the given component types to the query's WithAll list.
        /// If a given component type exists in WithNone an exception is raised.
        /// Removes any overlaps with WithAny from the With Any list
        /// </summary>
        /// <param name="componentTypes"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public EntityQuery WithAll(params Type[] componentTypes)
        {
            if (_built)
            {
                return this;
            }
            HashSet<int> all = [.. _withAll];
            all.UnionWith(_entityManager.GetComponentIds(componentTypes));
            _withAll = [.. all];


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
                HashSet<int> any = [.. _withAny];
                any.ExceptWith(all);
                _withAny = [.. any];
            }
            return this;
        }

        /// <summary>
        /// Adds the given component types to the query's WithNone list.
        /// If a given component type exists WithAny or WithAll, an exception is raised.
        /// </summary>
        /// <param name="componentTypes"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public EntityQuery WithNone(params Type[] componentTypes)
        {
            if (_built)
            {
                return this;
            }
            HashSet<int> none = [.. _withNone];
            none.UnionWith(_entityManager.GetComponentIds(componentTypes));
            _withNone = [.. none];

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

        /// <summary>
        /// Adds the given component types to the query's WithAny list.
        /// If a given component type exists in WithNone an exception is raised.
        /// Does not add components to WithAny that are present in WithAll
        /// </summary>
        /// <param name="componentTypes"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public EntityQuery WithAny(params Type[] componentTypes)
        {
            if (_built)
            {
                return this;
            }
            HashSet<int> any = [.. _withAny];
            any.UnionWith(_entityManager.GetComponentIds(componentTypes));

            any.ExceptWith(_withAll);

            _withAny = [.. any];

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

            if (_withAll.Count > 0)
            {
                HashSet<int> all = [.. _withAll];
                any.ExceptWith(all);
                _withAny = [.. any];
            }

            return this;
        }

        /// <summary>
        /// Marks the query as built (no longer editable)
        /// This initalises the hashsets
        /// </summary>
        /// <returns></returns>
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

        internal void AutoStale(int componentId)
        {
            if (_stale)
            {
                return;
            }
            else if (_withAllSet.Contains(componentId))
            {
                MarkStale();
            }
            else if (_withAnySet.Contains(componentId))
            {
                MarkStale();
            }
            else if (_withNoneSet.Contains(componentId))
            {
                MarkStale();
            }
        }

        /// <summary>
        /// Marks the query as stale
        /// </summary>
        public void MarkStale()
        {
            //entities.Clear();
            _stale = true;
        }

        /// <summary>
        /// Should be a very fast way of checking if the query has any entities right now.
        /// Hashset to hashset overlap/supersetof as this is hashset to hashset comparison all should be O(n) where n = elements in each query set.
        /// This will be parallel with high number of archetypes
        /// 
        /// This does not allocate any memory
        /// </summary>
        /// <returns></returns>
        public bool AnyEntities()
        {   
            bool any = false;


            foreach (var entitySet in _entityManager._archetypeIdsToComponentIds.Values)
            {
                if ((entitySet.Overlaps(_withAnySet) || _withAnySet.Count == 0)
                && (entitySet.IsSupersetOf(_withAllSet) || _withAllSet.Count == 0)
                && (!entitySet.Overlaps(_withNoneSet) || _withNoneSet.Count == 0))
                {
                    any = true;
                    break;
                }
            }

            // this was MUCH slower than serial for some reason
            // Parallel.ForEach(_entityManager._archetypeIdsToComponentIds.Values, (HashSet<int> entitySet, ParallelLoopState state) =>
            // {
            //     if ((entitySet.Overlaps(_withAnySet)||_withAnySet.Count == 0)
            //     && (entitySet.IsSupersetOf(_withAllSet) || _withAllSet.Count == 0)
            //     && (!entitySet.Overlaps(_withNoneSet)||_withNoneSet.Count == 0))
            //     {
            //         any = true;
            //         state.Break();
            //     }
            // });

            _stale = false;
            _hasEnitities = any;

            return any;
        }

        /// <summary>
        /// Getting the entities from the query has memory allocation overhead
        /// The WithAny alogirthim seems quite bad to me
        /// </summary>
        /// <returns></returns>
        public List<Entity> GetEntities()
        {
            if (Stale)
            {
                this.entities.Clear();
            }
            if(this.entities.Count != 0)
            {
                return this.entities;
            }
            HashSet<Entity> entitiesSet = [];

            _withAll.ForEach(compId =>
            {
                if(_entityManager.GetAllEntitiesWithComponent(compId,out var entities))
                {
                    entitiesSet.UnionWith(entities);
                }
            });

            _withAll.ForEach(compId =>
            {
                if (_entityManager.GetAllEntitiesWithoutComponent(compId, out var entities))
                {
                    entitiesSet.ExceptWith(entities);
                }
            });

            _withNone.ForEach(compId =>
            {
                if (_entityManager.GetAllEntitiesWithoutComponent(compId, out var entities))
                {
                    if(entitiesSet.Count > 0)
                    {
                        entitiesSet.IntersectWith(entities);

                    }
                    else
                    {
                        entitiesSet.UnionWith(entities);
                    }
                }
            });

            _withNone.ForEach(compId =>
            {
                if (_entityManager.GetAllEntitiesWithComponent(compId, out var entities))
                {
                    entitiesSet.ExceptWith(entities);
                }
            });

            var entities = new List<Entity>(entitiesSet);
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

            }
            this.entities.AddRange(entities);
            return this.entities;
        }
    }
}
