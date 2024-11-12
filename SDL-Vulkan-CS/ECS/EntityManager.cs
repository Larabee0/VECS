using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SDL_Vulkan_CS.ECS
{
    public class EntityManager
    {
        private int _totalComponentTypes = 0;
        private uint _nextMaxEntityId = 0;
        private readonly Queue<Entity> _idsToRecyle = [];
        private readonly HashSet<uint> _entityIds = [];

        private readonly Dictionary<uint, Entity> _entityIdToEntity = [];
        private readonly Dictionary<int, IComponent> _compSignatureToCompReference = [];

        private readonly Dictionary<int, HashSet<Entity>> _archetypeIdsToEntities = [];
        public readonly Dictionary<int, HashSet<int>> _archetypeIdsToComponentIds = [];
        private readonly Dictionary<uint, int> _entityIdToArchetypeIdLookup = [];

        private readonly Dictionary<uint, HashSet<int>> _entityToComponentIds = [];
        private readonly Dictionary<int, HashSet<Entity>> _componentIdToEntities = [];

        private readonly Dictionary<Guid, int> _componentTypeToIdLookup = [];
        private readonly Dictionary<int, Type> _componentIdToTypeLookup = [];


        public EntityManager()
        {
            var executingAssembly = Assembly.GetExecutingAssembly();
            Type[] allTypes = executingAssembly.GetTypes();
            List<Type> components = [];
            Type icomp = typeof(IComponent);
            for (int i = 0; i < allTypes.Length; i++)
            {
                if (icomp != allTypes[i] && icomp.IsAssignableFrom(allTypes[i]))
                {
                    components.Add(allTypes[i]);
                }
            }
            for (int i = 0; i < components.Count; i++)
            {
                components[i].GetProperty(nameof(IComponent.ComponentId)).SetValue(null, i);
                _componentTypeToIdLookup[components[i].GUID] = i;
                _componentIdToTypeLookup.Add(i, components[i]);
            }
            _totalComponentTypes = components.Count;
            _archetypeIdsToEntities.Add(0, []);
            _archetypeIdsToComponentIds.Add(0, []);
        }

        public void AddComponent<T>(Entity entity, T component) where T : IComponent
        {
            AddComponent<T>(entity);
            SetComponent(entity, component);
        }

        public T AddComponent<T>(Entity entity) where T : IComponent
        {
            if(GetComponent(entity, out T comp))
            {
                return comp;
            }
            else
            {
                comp = default;
                int compId = GetComponentId<T>();
                if (_componentIdToEntities.TryGetValue(compId, out var entities))
                {
                    entities.Add(entity);
                }
                else
                {
                    _componentIdToEntities.Add(compId, [entity]);
                }

                _entityToComponentIds[entity.Id].Add(compId);

                _compSignatureToCompReference.Add(GetEntityComponentSigature<T>(entity), comp);
                UpdateEntityArchetype(entity);

                return comp;
            }
        }

        public void RemoveComponent<T>(Entity entity) where T : IComponent
        {
            if (HasComponent<T>(entity, out int signature))
            {
                _entityToComponentIds[entity.Id].Remove(GetComponentId<T>());
                _componentIdToEntities.Remove(signature);
                _compSignatureToCompReference.Remove(signature);

                UpdateEntityArchetype(entity);
            }
        }

        public bool GetComponent<T>(Entity entity, out T component) where T : IComponent
        {
            int signature = GetEntityComponentSigature<T>(entity);
            bool hasComponent = _compSignatureToCompReference.TryGetValue(signature, out IComponent comp);
            component = hasComponent ? (T)comp : default;
            return hasComponent;
        }

        public T GetComponent<T>(Entity entity) where T : IComponent
        {
            int signature = GetEntityComponentSigature<T>(entity);
            return (T)_compSignatureToCompReference[signature];
        }

        public T GetComponent<T>(int signature) where T : IComponent
        {
            return (T)_compSignatureToCompReference[signature];
        }

        public bool SetComponent<T>(Entity entity, T component) where T : IComponent
        {
            if (HasComponent<T>(entity, out int signature))
            {
                _compSignatureToCompReference[signature] = component;
                return true;
            }
            return false;
        }

        private void UpdateEntityArchetype(Entity entity)
        {
            if (_entityIdToArchetypeIdLookup.TryGetValue(entity.Id, out int oldArcetype))
            {
                _archetypeIdsToEntities[oldArcetype].Remove(entity);

                _archetypeIdsToComponentIds.Remove(oldArcetype);
            }

            int archetype = ComputeArchetypeHash(entity);
            _entityIdToArchetypeIdLookup[entity.Id] = archetype;
            if (_archetypeIdsToEntities.TryGetValue(archetype, out HashSet<Entity> entities))
            {
                entities.Add(entity);
            }
            else
            {
                _archetypeIdsToEntities[archetype] = [entity];
                _archetypeIdsToComponentIds.Add(archetype, _entityToComponentIds[entity.Id]);
            }
        }

        public bool HasComponent<T>(Entity entity) where T : IComponent
        {
            return HasComponent<T>(entity, out _);
        }

        public bool HasComponent<T>(Entity entity, out int signature) where T : IComponent
        {
            signature = GetEntityComponentSigature<T>(entity);
            return _compSignatureToCompReference.ContainsKey(signature);
        }

        public bool HasComponent(Entity entity, int compId)
        {
            return _compSignatureToCompReference.ContainsKey(GetEntityComponentSigature(entity, compId));
        }

        public int GetEntityComponentSigature<T>(Entity entity) where T : IComponent
        {
            return HashCode.Combine(entity.GetHashCode(), GetComponentId<T>());
        }

        public int GetEntityComponentSigature(Entity entity, int compId)
        {
            return HashCode.Combine(entity.GetHashCode(), compId);
        }

        public int GetComponentId<T>() where T : IComponent
        {
            return _componentTypeToIdLookup[typeof(T).GUID];
        }

        public List<Entity> GetAllEntitiesWithComponent<T>() where T : IComponent
        {
            int compId = GetComponentId<T>();
            return GetAllEntitiesWithComponent(compId);
        }

        public List<Entity> GetAllEntitiesWithComponent(int compId)
        {
            if (_componentIdToEntities.TryGetValue(compId, out var entitiesSet))
            {
                return new(entitiesSet);
            }

            return null;
        }

        public bool GetAllEntitiesWithComponent(int compId, out HashSet<Entity> entities)
        {
            return _componentIdToEntities.TryGetValue(compId, out entities);
        }

        public List<Entity> GetAllEntitiesWithComponents(params Type[] components)
        {
            List<int> componentIds = new (components.Length);

            for (int i = 0; i < components.Length; i++)
            {
                if (_componentTypeToIdLookup.TryGetValue(components[i].GUID, out int compId))
                {
                    componentIds.Add(compId);
                }
            }

            HashSet<Entity> allEntities = new(_entityIdToEntity.Values);

            componentIds.ForEach(comp => allEntities.IntersectWith(_componentIdToEntities[comp]));

            return new(allEntities);
        }

        public int GetArchetypeSigature(params Type[] componentsTypes)
        {

            return GetArchetypeHash(GetComponentIds(componentsTypes));
        }

        public HashSet<int> GetComponentIds(params Type[] componentsTypes)
        {
            HashSet<int> componentIds = new(componentsTypes.Length);

            for (int i = 0; i < componentsTypes.Length; i++)
            {
                if (_componentTypeToIdLookup.TryGetValue(componentsTypes[i].GUID, out int compId))
                {
                    componentIds.Add(compId);
                }
            }
            componentIds.TrimExcess();
            return componentIds;
            
        }

        public Entity CreateEntity()
        {

            uint id = GetNextId(out int version);
            var newEntity = new Entity(id, version);

            _entityIdToEntity.Add(id, newEntity);
            _entityIds.Add(id);
            _entityToComponentIds.Add(id, []);
            _entityIdToArchetypeIdLookup.Add(id,0);
            return newEntity;
        }

        public bool DestroyEntity(Entity entity)
        {
            if (_entityIds.Remove(entity.Id))
            {
                int archetype = ComputeArchetypeHash(entity);

                if(_archetypeIdsToEntities.TryGetValue(archetype,out var entities))
                {
                    entities.Remove(entity);
                }

                _entityIdToArchetypeIdLookup.Remove(entity.Id);

                _idsToRecyle.Enqueue(entity);
                _entityIdToEntity.Remove(entity.Id);
                return true;
            }
            return false;
        }

        public bool DestroyEntity(uint id)
        {
            return DestroyEntity(_entityIdToEntity[id]);
        }

        private uint GetNextId(out int version)
        {
            bool idIsAvaliable;
            uint id = _nextMaxEntityId;
            version = 1;
            if (_nextMaxEntityId == 0)
            {
                _nextMaxEntityId++;
            }

            if (_idsToRecyle.Count > 0)
            {
                Entity toRecycle = _idsToRecyle.Dequeue();
                id = toRecycle.Id;
                idIsAvaliable = !_entityIds.Contains(id);
                while (_idsToRecyle.Count > 0 && !idIsAvaliable)
                {
                    toRecycle = _idsToRecyle.Dequeue();
                    id = toRecycle.Id;
                    idIsAvaliable = !_entityIds.Contains(id);
                }
                version = toRecycle.Version++;
            }

            idIsAvaliable = !_entityIds.Contains(id);

            if (!idIsAvaliable)
            {
                id = _nextMaxEntityId;
                _nextMaxEntityId++;
                idIsAvaliable = !_entityIds.Contains(id);
                while (!idIsAvaliable)
                {
                    _nextMaxEntityId++;
                    idIsAvaliable = !_entityIds.Contains(id);
                }
            }

            return id;
        }

        public int ComputeArchetypeHash(Entity entity)
        {
            return GetArchetypeHash(_entityToComponentIds[entity.Id]);
        }

        private static int GetArchetypeHash(HashSet<int> componentIds)
        {
            int[] unsorted = [.. componentIds];
            if (unsorted.Length > 0)
            {
                Array.Sort(unsorted);
                int hash = unsorted[0];
                for (int i = 1; i < unsorted.Length; i++)
                {
                    hash = HashCode.Combine(hash, unsorted[i]);
                }
                return hash;
            }
            return 0;
        }

        public bool AnyEntitiesWith(int compId)
        {
            return _componentIdToEntities.TryGetValue(compId, out var value) && value.Count > 0;
        }

        public bool AnyEntitiesWithout(int compId)
        {
            return !_componentIdToEntities.TryGetValue(compId, out var value) || value.Count < _entityIdToEntity.Count;
        }

        public string GetComponentName(int compId)
        {
            if(_componentIdToTypeLookup.TryGetValue(compId, out var value))
            {
                return value.Name;
            }
            return null;
        }

        public bool SingletonEntity<T>(out Entity entity) where T : IComponent
        {
            int id = GetComponentId<T>();
            entity = Entity.Null;
            if( _componentIdToEntities.TryGetValue(id, out HashSet<Entity> entities) && entities.Count == 1)
            {
                entity = new List<Entity>(entities)[0];
                return true;
            }
            return false;
        }
        public bool Singleton<T>(out T component) where T : IComponent
        {
            int id = GetComponentId<T>();
            component = default;
            return _componentIdToEntities.TryGetValue(id, out HashSet<Entity> entities) && entities.Count == 1 && GetComponent(new List<Entity>(entities)[0], out component);
        }
    }
}
