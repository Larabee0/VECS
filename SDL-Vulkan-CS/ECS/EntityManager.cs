using SDL_Vulkan_CS.ECS.Presentation;
using SDL_Vulkan_CS.VulkanBackend;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace SDL_Vulkan_CS.ECS
{
    /// <summary>
    /// Managers the entities in a world.
    /// 
    /// Components have a static id in their type. this id is an int and increments from 0 to n component types.
    /// 
    /// Entities have a uint id and an int representing their version.
    /// When an entity is destroy it is added to a queue to be recycled.
    /// When an entity is recycled, its version number is incremented by one.
    /// 
    /// A component instance can have an instance ID associated by hashing its id with the entity hashcode. This is referred to as the component signature.
    /// component signature =  HashCode.Combine(entity.GetHashCode(), componentId);
    /// This signature can be used to quickly look up if the entity has a component as well just given the component id.
    /// 
    /// Archetypes are entities with a set of components. An Archetype may consist of one or many entities.
    /// Archetypes can be uniquely identified by hashing all their component ids in order (lowest to highest id)
    /// Archetype id = 0 contains entities that have no components.
    /// 
    /// </summary>
    public class EntityManager
    {
        private readonly int _totalComponentTypes = 0;
        private uint _nextMaxEntityId = 0;
        private readonly Queue<Entity> _idsToRecyle = [];
        private readonly HashSet<uint> _entityIds = [];

        public int TotalComponentTypes => _totalComponentTypes;

        private readonly Dictionary<uint, Entity> _entityIdToEntity = []; // quick entity look up just given an entity id.
        private readonly Dictionary<int, IComponent> _compSignatureToCompReference = []; // component storage, keys are component sigantures.

        private readonly Dictionary<int, HashSet<Entity>> _archetypeIdsToEntities = []; // archetype ids to a hashset of entities that are members of archetype.
        public readonly Dictionary<int, HashSet<int>> _archetypeIdsToComponentIds = []; // archetype ids to a hashset of component ids that comprises the archetype.
        private readonly Dictionary<uint, int> _entityIdToArchetypeIdLookup = []; // entity id to the archetype id that the entity belongs to.

        private readonly Dictionary<uint, HashSet<int>> _entityToComponentIds = []; // entity id to the unique set of component ids it has attached to the entity.
        private readonly Dictionary<int, HashSet<Entity>> _componentIdToEntities = []; // component id to the unique set of entities that have that component attached.

        private readonly Dictionary<Guid, int> _componentTypeToIdLookup = []; // look up for the component type guid to the smaller component id
        private readonly Dictionary<int, Type> _componentIdToTypeLookup = []; // look up for a component id to the component type

        /// <summary>
        /// Generates ids for all the components present in the executing assembly,
        /// then tracks them in <see cref="_componentIdToTypeLookup"/> and <see cref="_componentTypeToIdLookup"/>
        /// </summary>
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

        /// <summary>
        /// Add component overload that adds then sets a component on an entity.
        /// If the entity already has the component, will still work to set it.
        /// </summary>
        /// <typeparam name="T">Component type</typeparam>
        /// <param name="entity"></param>
        /// <param name="component">Component copy</param>
        public void AddComponent<T>(Entity entity, T component) where T : IComponent
        {
            AddComponent<T>(entity);
            SetComponent(entity, component);
        }

        /// <summary>
        /// Adds a component of type T to the entity, then returns it.
        /// If the entity already has the component, it will return the current value of it.
        /// </summary>
        /// <typeparam name="T">Component type</typeparam>
        /// <param name="entity"></param>
        /// <returns></returns>
        public T AddComponent<T>(Entity entity) where T : IComponent
        {
            if (GetComponent(entity, out T comp))
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

        public void AddComponentToHierarchy<T>(Entity entity, T component) where T : IComponent
        {
            if (HasComponent<Children>(entity, out int signature))
            {
                var children = (Children)_compSignatureToCompReference[signature];
                var childCount = children.Value != null ? children.Value.Length : 0;
                for (int i = 0; i < childCount; i++)
                {
                    AddComponentToHierarchy(children.Value[i], component);
                }
            }
            AddComponent(entity, component);
        }


        public void AddComponentToHierarchy<T>(Entity entity) where T : IComponent
        {
            if (HasComponent<Children>(entity, out int signature))
            {
                var children = (Children)_compSignatureToCompReference[signature];
                var childCount = children.Value != null ? children.Value.Length : 0;
                for (int i = 0; i < childCount; i++)
                {
                    AddComponentToHierarchy<T>(children.Value[i]);
                }
            }
            AddComponent<T>(entity);
        }


        private void AddComponentById(Entity entity, int compId,bool archetypeRefresh = true)
        {

            if (_componentIdToEntities.TryGetValue(compId, out var entities))
            {
                entities.Add(entity);
            }
            else
            {
                _componentIdToEntities.Add(compId, [entity]);
            }
            _entityToComponentIds[entity.Id].Add(compId);
            IComponent component = (IComponent)Activator.CreateInstance(_componentIdToTypeLookup[compId]);
            _compSignatureToCompReference.Add(GetEntityComponentSigature(entity, compId), component);
            if (archetypeRefresh)
            {
                UpdateEntityArchetype(entity);
            }
        }

        /// <summary>
        /// Removes a component from an entity only if the netity has the component.
        /// </summary>
        /// <typeparam name="T">Component type</typeparam>
        /// <param name="entity"></param>
        /// <param name="archetypeRefresh">Auto update the archetypes or not</param>
        public void RemoveComponent<T>(Entity entity, bool archetypeRefresh = true) where T : IComponent
        {
            if (HasComponent<T>(entity, out int signature))
            {
                int compId = GetComponentId<T>();
                _entityToComponentIds[entity.Id].Remove(compId);
                _componentIdToEntities[compId].Remove(entity);
                _compSignatureToCompReference.Remove(signature);

                if (archetypeRefresh)
                {
                    UpdateEntityArchetype(entity);
                }
            }
        }


        public void RemoveComponentFromHierarchy<T>(Entity entity) where T : IComponent
        {
            if (HasComponent<Children>(entity, out int signature))
            {
                var children = (Children)_compSignatureToCompReference[signature];
                var childCount = children.Value != null ? children.Value.Length : 0;
                for (int i = 0; i < childCount; i++)
                {
                    RemoveComponentFromHierarchy<T>(children.Value[i]);
                }
            }
            RemoveComponent<T>(entity);
        }


        /// <summary>
        /// Remove the component id from the entity if the entity has the component
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="compId">component id</param>
        /// <param name="archetypeRefresh">Auto update the archetypes or not</param>
        public void RemoveComponent(Entity entity, int compId, bool archetypeRefresh = true)
        {
            if (HasComponent(entity, compId, out int signature))
            {
                _entityToComponentIds[entity.Id].Remove(compId);
                _componentIdToEntities[compId].Remove(entity);
                _compSignatureToCompReference.Remove(signature);

                if (archetypeRefresh)
                {
                    UpdateEntityArchetype(entity);
                }
            }
        }


        /// <summary>
        /// Updates the arcehtype dictionaries after an entity has had a component added or removed.
        /// </summary>
        /// <param name="entity">entity that has changed</param>
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

        /// <summary>
        /// Simple overload to get just a bool of if a component is attached to the entity.
        /// </summary>
        /// <typeparam name="T">Component type</typeparam>
        /// <param name="entity"></param>
        /// <returns></returns>
        public bool HasComponent<T>(Entity entity) where T : IComponent
        {
            return HasComponent<T>(entity, out _);
        }

        /// <summary>
        /// Given a component id and entity checks if the component is attched to the entity.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="compId">Component id</param>
        /// <returns></returns>
        public bool HasComponent(Entity entity, int compId)
        {
            int signature = GetEntityComponentSigature(entity, compId);
            return _compSignatureToCompReference.ContainsKey(signature);
        }

        /// <summary>
        /// Has compoennt overload to return the component instance signature as well
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="compId">Component id</param>
        /// <param name="signature">component instance signature</param>
        /// <returns></returns>
        public bool HasComponent(Entity entity, int compId, out int signature)
        {
            signature = GetEntityComponentSigature(entity, compId);
            return _compSignatureToCompReference.ContainsKey(signature);
        }


        /// <summary>
        /// Given a component type and entity checks if the component is attched to the entity and set the component instance sigature as an out parameter
        /// </summary>
        /// <typeparam name="T">Component type</typeparam>
        /// <param name="entity"></param>
        /// <param name="signature">Component instance signature</param>
        /// <returns></returns>
        public bool HasComponent<T>(Entity entity, out int signature) where T : IComponent
        {
            signature = GetEntityComponentSigature<T>(entity);
            return _compSignatureToCompReference.ContainsKey(signature);
        }

        /// <summary>
        /// Safe get component overload that looks up a component on an entity.
        /// </summary>
        /// <typeparam name="T">Component type</typeparam>
        /// <param name="entity"></param>
        /// <param name="component">Component copy</param>
        /// <returns></returns>
        public bool GetComponent<T>(Entity entity, out T component) where T : IComponent
        {
            int signature = GetEntityComponentSigature<T>(entity);
            bool hasComponent = _compSignatureToCompReference.TryGetValue(signature, out IComponent comp);
            component = hasComponent ? (T)comp : default;
            return hasComponent;
        }

        private IComponent GetComponent(Entity entity, int compId)
        {
            int signature = GetEntityComponentSigature(entity,compId);
            _compSignatureToCompReference.TryGetValue(signature, out IComponent comp);
            return comp;
        }

        /// <summary>
        /// Slightly unsafe get component overload that directly looks up a component sigature.
        /// If the signature is not present in <see cref="_compSignatureToCompReference"/>, an exception will be raised
        /// so this should probably be guarded by <see cref="HasComponent"/>
        /// </summary>
        /// <typeparam name="T">Component type</typeparam>
        /// <param name="entity"></param>
        /// <returns></returns>
        public T GetComponent<T>(Entity entity) where T : IComponent
        {
            int signature = GetEntityComponentSigature<T>(entity);
            return GetComponent<T>(signature);
        }

        public T[] GetComponentsInHierarchy<T>(Entity entity) where T : IComponent
        {
            T[] components = [];
            
            if (HasComponent<T>(entity, out int signature))
            {
                components = [(T)_compSignatureToCompReference[signature]];
            }

            if (HasComponent<Children>(entity,out signature))
            {
                var children = (Children)_compSignatureToCompReference[signature];
                var childCount = children.Value != null ? children.Value.Length : 0;
                for (int i = 0; i < childCount; i++)
                {
                    components = [.. components, .. GetComponentsInHierarchy<T>(children.Value[i])];
                }
            }

            return components;
        }

        /// <summary>
        /// Slightly unsafe get component overload that directly looks up a component sigature.
        /// If the signature is not present in <see cref="_compSignatureToCompReference"/>, an exception will be raised
        /// so this should probably be guarded by <see cref="HasComponent"/>
        /// </summary>
        /// <typeparam name="T">Component type</typeparam>
        /// <param name="signature"></param>
        /// <returns></returns>
        public T GetComponent<T>(int signature) where T : IComponent
        {
            return (T)_compSignatureToCompReference[signature];
        }

        /// <summary>
        /// Safe way to set a component of an entity.
        /// </summary>
        /// <typeparam name="T">Component type</typeparam>
        /// <param name="entity"></param>
        /// <param name="component"></param>
        /// <returns>True if the component was set</returns>
        public bool SetComponent<T>(Entity entity, T component) where T : IComponent
        {
            if (HasComponent<T>(entity, out int signature))
            {
                _compSignatureToCompReference[signature] = component;
                return true;
            }
            return false;
        }

        public bool CopyComponent(Entity destEntity, IComponent source)
        {
            if (HasComponent(destEntity, source.Id, out int signature))
            {
                _compSignatureToCompReference[signature] = source;
            }

            return false;
        }

        /// <summary>
        /// Returns the siganture of a component instance if the given entity had the component.
        /// </summary>
        /// <typeparam name="T">Component type</typeparam>
        /// <param name="entity"></param>
        /// <returns></returns>
        public static int GetEntityComponentSigature<T>(Entity entity) where T : IComponent
        {
            //return HashCode.Combine(entity.GetHashCode(), GetComponentId<T>());
            return HashCode.Combine(entity.GetHashCode(), GetComponentId<T>());
        }

        /// <summary>
        /// Returns the siganture of a component instance if the given entity had the component.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="compId">Component id</param>
        /// <returns></returns>
        public static int GetEntityComponentSigature(Entity entity, int compId)
        {
            return HashCode.Combine(entity.GetHashCode(), compId);
        }

        /// <summary>
        /// Looks up a component id from the type guid
        /// </summary>
        /// <typeparam name="T">Component type</typeparam>
        /// <returns></returns>
        public static int GetComponentId<T>() where T : IComponent
        {
            return default(T).Id;
        }

        /// <summary>
        /// Looks up an component id from the type guid then gets all entities with the component attached.
        /// </summary>
        /// <typeparam name="T">Component type</typeparam>
        /// <returns>list of entities with the attached component</returns>
        public List<Entity> GetAllEntitiesWithComponent<T>() where T : IComponent
        {
            int compId = GetComponentId<T>();
            return GetAllEntitiesWithComponent(compId);
        }

        /// <summary>
        /// Looks up an component id from the type guid then gets all entities with the component attached.
        /// </summary>
        /// <param name="compId">Component id</param>
        /// <returns>list of entities with the attached component</returns>
        public List<Entity> GetAllEntitiesWithComponent(int compId)
        {
            if (_componentIdToEntities.TryGetValue(compId, out var entitiesSet))
            {
                return new(entitiesSet);
            }

            return null;
        }

        /// <summary>
        /// gets all entities with the component attached.
        /// This returns a true false for if the compoennt has any entities
        /// and will output a hashset of entities if true
        /// </summary>
        /// <param name="compId">component id</param>
        /// <param name="entities">hashset of all entities that have the component</param>
        /// <returns>true if the component has entities</returns>
        public bool GetAllEntitiesWithComponent(int compId, out HashSet<Entity> entities)
        {
            return _componentIdToEntities.TryGetValue(compId, out entities);
        }

        /// <summary>
        /// Gets a hashset of entities which do not have the given componentId
        /// </summary>
        /// <param name="compId"></param>
        /// <param name="entities"></param>
        /// <returns></returns>
        public bool GetAllEntitiesWithoutComponent(int compId, out HashSet<Entity> entities)
        {
            entities = [];
            foreach (var pair in _entityToComponentIds)
            {
                if (!pair.Value.Contains(compId))
                {
                    entities.Add(_entityIdToEntity[pair.Key]);
                }
            }

            return entities.Count > 0;
        }

        /// <summary>
        /// Get all entities with the given component types
        /// Converts component types to a list of component ids then
        /// creates a hashset of all the entities
        /// Iterates the list to remove all entities that don't match the required components
        /// </summary>
        /// <param name="components">component types we want he entities to have</param>
        /// <returns>list of entities with all the given components attached</returns>
        public List<Entity> GetAllEntitiesWithComponents(params Type[] components)
        {
            List<int> componentIds = new(components.Length);

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

        /// <summary>
        /// Computes the archetype signature of the given set of components.
        /// </summary>
        /// <param name="componentsTypes">component types</param>
        /// <returns>archetype signature</returns>
        public int GetArchetypeSigature(params Type[] componentsTypes)
        {

            return GetArchetypeHash(GetComponentIds(componentsTypes));
        }

        /// <summary>
        /// Computes the arcetype sigature of a given entity's component set.
        /// </summary>
        /// <param name="entity"></param>
        /// <returns>archetype signature</returns>
        public int ComputeArchetypeHash(Entity entity)
        {
            return GetArchetypeHash(_entityToComponentIds[entity.Id]);
        }

        /// <summary>
        /// Computes the archetype signature of a given set of component ids
        /// This is allocated to an array, sorted then combined.
        /// If there are no component ids, 0 is returned.
        /// </summary>
        /// <param name="componentIds"></param>
        /// <returns></returns>
        private static int GetArchetypeHash(HashSet<int> componentIds)
        {
            if (componentIds.Count > 0)
            {
                int[] unsorted = [.. componentIds];
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

        /// <summary>
        /// Gets the component ids from the set of component types
        /// </summary>
        /// <param name="componentsTypes">component types</param>
        /// <returns>hashset of component ids</returns>
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

        /// <summary>
        /// Creates a new entity
        /// </summary>
        /// <returns>copy of the entity</returns>
        public Entity CreateEntity()
        {

            uint id = GetNextId(out int version);
            var newEntity = new Entity(id, version);

            _entityIdToEntity.Add(id, newEntity);
            _entityIds.Add(id);
            _entityToComponentIds.Add(id, []);
            _entityIdToArchetypeIdLookup.Add(id, 0);
            return newEntity;
        }

        /// <summary>
        /// Destroys an entity and removes all its components.
        /// Removes the entity from the archetype tables
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public bool DestroyEntity(Entity entity)
        {
            if (_entityIds.Remove(entity.Id))
            {
                List<int> componentsToRemove = new(_entityToComponentIds[entity.Id]);

                componentsToRemove.ForEach(comp => RemoveComponent(entity, comp, false));

                UpdateEntityArchetype(entity);

                int archetype = ComputeArchetypeHash(entity);

                if (_archetypeIdsToEntities.TryGetValue(archetype, out var entities))
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

        /// <summary>
        /// Destroy an entity given just an id.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public bool DestroyEntity(uint id)
        {
            return DestroyEntity(_entityIdToEntity[id]);
        }

        /// <summary>
        /// Gets the next entity id and version number.
        /// If there are elements in the recycle queue we reuse those ids
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
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
            else
            {
                _nextMaxEntityId++;
            }

            idIsAvaliable = !_entityIds.Contains(id);

            if (!idIsAvaliable)
            {
                idIsAvaliable = !_entityIds.Contains(id);
                while (!idIsAvaliable)
                {
                    id = _nextMaxEntityId;
                    idIsAvaliable = !_entityIds.Contains(id);
                    _nextMaxEntityId++;
                }
            }

            return id;
        }

        /// <summary>
        /// Checks if any entities of the given id exist
        /// </summary>
        /// <param name="compId"></param>
        /// <returns></returns>
        public bool AnyEntitiesWith(int compId)
        {
            return _componentIdToEntities.TryGetValue(compId, out var value) && value.Count > 0;
        }

        /// <summary>
        /// Checks if the given component id has no entities
        /// </summary>
        /// <param name="compId"></param>
        /// <returns></returns>
        public bool AnyEntitiesWithout(int compId)
        {
            return !_componentIdToEntities.TryGetValue(compId, out var value) || value.Count < _entityIdToEntity.Count;
        }

        /// <summary>
        /// gets the type name of a component id
        /// </summary>
        /// <param name="compId"></param>
        /// <returns></returns>
        public string GetComponentName(int compId)
        {
            if (_componentIdToTypeLookup.TryGetValue(compId, out var value))
            {
                return value.Name;
            }
            return null;
        }

        /// <summary>
        /// looks up a singleton entity instance assuming a singleton component instance.
        /// If there is no singleton instance, this method will return false. (either no instance or more thna one instance)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entity"></param>
        /// <returns></returns>
        public bool SingletonEntity<T>(out Entity entity) where T : IComponent
        {
            int id = GetComponentId<T>();
            entity = Entity.Null;
            if (_componentIdToEntities.TryGetValue(id, out HashSet<Entity> entities) && entities.Count == 1)
            {
                entity = new List<Entity>(entities)[0];
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets a singleton component instance
        /// If there is no singleton instance, this method will return false. (either no instance or more thna one instance)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="component"></param>
        /// <returns></returns>
        public bool SingletonComponent<T>(out T component) where T : IComponent
        {
            int id = GetComponentId<T>();
            component = default;
            return _componentIdToEntities.TryGetValue(id, out HashSet<Entity> entities) && entities.Count == 1 && GetComponent(new List<Entity>(entities)[0], out component);
        }

        /// <summary>
        /// Components using mangaged data structures, other than <see cref="Children"/>, will have the same shared instance.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="instantiateNewMeshes"></param>
        /// <param name="parentEntity"></param>
        /// <returns></returns>
        public Entity Instantiate(Entity entity, bool instantiateNewMeshes = false, Entity parentEntity = default)
        {
            HashSet<int> components = new(_entityToComponentIds[entity.Id]);
            components.Remove(GetComponentId<Prefab>());

            Entity instance = CreateEntity();
            
            if (components.Remove(GetComponentId<Children>()))
            {
                InstantiateChildren(entity, instantiateNewMeshes, instance);
            }

            if (components.Remove(GetComponentId<Parent>()) || parentEntity != Entity.Null)
            {
                AddComponent<Parent>(instance, new() { Value = parentEntity });
            }

            if (instantiateNewMeshes && components.Remove(GetComponentId<MeshIndex>()))
            {
                InstantiateMeshes(entity, instance);
            }

            foreach (var compId in components)
            {
                IComponent sourceInstance = GetComponent(entity, compId);   
                AddComponentById(instance, compId,false);
                CopyComponent(instance, sourceInstance);
            }

            UpdateEntityArchetype(instance);

            return instance;
        }

        private void InstantiateChildren(Entity entity, bool instantiateNewMeshes, Entity instance)
        {
            Children children = GetComponent<Children>(entity);
            Children instanceChildren = new() { Value = new Entity[children.Value.Length] };
            for (int i = 0; i < children.Value.Length; i++)
            {
                instanceChildren.Value[i] = Instantiate(children.Value[i], instantiateNewMeshes, instance);
            }
            AddComponent(instance, instanceChildren);
        }

        private void InstantiateMeshes(Entity entity, Entity instance)
        {
            MeshIndex mesh = GetComponent<MeshIndex>(entity);
            Mesh instanceMesh = new(Mesh.GetMeshAtIndex(mesh.Value, false));
            AddComponent(instance, new MeshIndex() { Value = Mesh.GetIndexOfMesh(instanceMesh) });
        }
    }
}
