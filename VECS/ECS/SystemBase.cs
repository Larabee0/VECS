using System;
using System.Collections.Generic;

namespace VECS.ECS
{
    /// <summary>
    /// Base system implemnetation not quite finished.
    /// Updating behaviour is suppsed to be dependant on the Always update flag or if any of the queries in the last have entities.
    /// </summary>
    public class SystemBase
    {
        internal static int systemIdAllocator;
        public World World { get; set; }
        public bool AlwaysUpdate;
        public int SystemID;
        public int SystemTypeIndex;

        public List<EntityQuery> Queries = [];

        public SystemBase()
        {
            SystemTypeIndex = TypeManager.GetSystemTypeIndex(GetType());
            SystemID = ++systemIdAllocator;
        }

        public virtual void OnCreate(EntityManager entityManager)
        {

        }

        public virtual void OnDestroy(EntityManager entityManager)
        {

        }

        public virtual void OnFixedUpdate(EntityManager entityManager)
        {
        }

        public virtual void OnPostFixedUpdate(EntityManager entityManager)
        {
        }

        public virtual void OnUpdate(EntityManager entityManager)
        {

        }

        public virtual void OnPostUpdate(EntityManager entityManager)
        {

        }

        public virtual void OnPrePresent(EntityManager entityManager)
        {

        }
    }
}
