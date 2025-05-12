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
        public bool AlwaysUpdate;

        public List<EntityQuery> Queries = [];


        public virtual void OnCreate(EntityManager entityManager)
        {

        }

        public virtual void OnDestroy(EntityManager entityManager)
        {

        }

        internal void OnFixedUpdate(EntityManager entityManager)
        {
        }

        internal void OnPostFixedUpdate(EntityManager entityManager)
        {
        }

        public virtual void OnUpdate(EntityManager entityManager)
        {

        }

        public virtual void OnPostUpdate(EntityManager entityManager)
        {

        }

    }
}
