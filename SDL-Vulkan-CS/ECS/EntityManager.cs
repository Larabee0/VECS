using System;
using System.Collections.Generic;
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

        private readonly Dictionary<uint, Entity> _entities = [];
        
        private readonly Dictionary<uint, HashSet<int>> _entityToComponents = [];

        private readonly Dictionary<int, HashSet<Entity>> _componentToEntities = [];

        private readonly Dictionary<int, IComponent> _componentReference = [];

        private readonly Dictionary<Guid, int> _componentTypeLookup = [];

        private readonly List<HashSet<int>> _entityArchetypes = [];
        private readonly List<int> _entityArchetypesSums = [];


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
                components[i].GetField(nameof(IComponent.ComponentId)).SetValue(null, i);
                _componentTypeLookup[components[i].GUID] = i;
            }
            _totalComponentTypes = components.Count;

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
                if(_componentToEntities.TryGetValue(compId,out var entities))
                {
                    entities.Add(entity);
                }
                else
                {
                    _componentToEntities.Add(compId, [entity]);
                }

                _entityToComponents[entity.Id].Add(compId);

                _componentReference.Add(GetEntityComponentSigature<T>(entity), comp);

                return comp;
            }
        }

        public void RemoveComponent<T>(Entity entity) where T : IComponent
        {
            if (HasComponent<T>(entity, out int signature))
            {
                _entityToComponents[entity.Id].Remove(GetComponentId<T>());
                _componentToEntities.Remove(signature);
                _componentReference.Remove(signature);
            }
        }

        public bool GetComponent<T>(Entity entity, out T component) where T : IComponent
        {
            int signature = GetEntityComponentSigature<T>(entity);
            bool hasComponent = _componentReference.TryGetValue(signature, out IComponent comp);
            component = hasComponent ? (T)comp : default;
            return hasComponent;
        }

        public bool SetComponent<T>(Entity entity, T component) where T : IComponent
        {
            if (HasComponent<T>(entity, out int signature))
            {
                _componentReference[signature] = component;
                return true;
            }
            return false;
        }

        public bool HasComponent<T>(Entity entity) where T : IComponent
        {
            return HasComponent<T>(entity, out _);
        }

        public bool HasComponent<T>(Entity entity, out int signature) where T : IComponent
        {
            signature = GetEntityComponentSigature<T>(entity);
            return _componentReference.ContainsKey(signature);
        }

        public int GetEntityComponentSigature<T>(Entity entity) where T : IComponent
        {
            return HashCode.Combine(entity.GetHashCode(), GetComponentId<T>());
        }

        public int GetComponentId<T>() where T : IComponent
        {
            return _componentTypeLookup[typeof(T).GUID];
        }

        public List<Entity> GetAllEntitiesWithComponent<T>() where T : IComponent
        {
            int compId = GetComponentId<T>();

            if(_componentToEntities.TryGetValue(compId, out var entitiesSet))
            {
                return new(entitiesSet);
            }

            return null;
        }

        public List<Entity> GetAllEntitiesWithComponents(params Type[] components)
        {
            List<int> componentIds = new (components.Length);

            for (int i = 0; i < components.Length; i++)
            {
                if (_componentTypeLookup.TryGetValue(components[i].GUID, out int compId))
                {
                    componentIds.Add(compId);
                }
            }

            HashSet<Entity> allEntities = new(_entities.Values);

            componentIds.ForEach(comp => allEntities.IntersectWith(_componentToEntities[comp]));

            return new(allEntities);
        }

        public Entity CreateEntity()
        {

            uint id = GetNextId(out int version);
            var newEntity = new Entity(id, version);

            _entities.Add(id, newEntity);
            _entityIds.Add(id);
            _entityToComponents.Add(id, []);
            return newEntity;
        }

        public bool DestroyEntity(Entity entity)
        {
            if (_entityIds.Remove(entity.Id))
            {
                _idsToRecyle.Enqueue(entity);
                _entities.Remove(entity.Id);
                return true;
            }
            return false;
        }

        public bool DestroyEntity(uint id)
        {
            return DestroyEntity(_entities[id]);
        }

        private uint GetNextId(out int version)
        {
            bool idIsAvaliable;
            uint id = _nextMaxEntityId;
            version = 0;
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
    }
}
