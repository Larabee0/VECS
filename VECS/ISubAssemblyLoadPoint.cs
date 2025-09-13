using VECS.ECS;

namespace VECS
{
    internal interface ISubAssemblyLoadPoint
    {
        /// <summary>
        /// Called after the loaded assembly passes IsUseable
        /// </summary>
        public void OnAssemblyLoad();

        /// <summary>
        /// Called on all loaded assembies after assembly loading phase is complete
        /// </summary>
        public void OnAllAssemblyLoaded();

        /// <summary>
        /// Called before the <see cref="Application"/> instance is created
        /// </summary>
        public void PreApplicationConstruction();

        /// <summary>
        /// Called before <see cref="Application.Start"/>
        /// </summary>
        public void PreApplicationStart();

        /// <summary>
        /// Called as part of <see cref="Application.Start"/> before <see cref="World.OnCreate()"/>
        /// </summary>
        public void PreDefaultWorldCreation();

        /// <summary>
        /// Called as part of <see cref="Application.Start"/> after <see cref="World.OnCreate()"/>
        /// </summary>
        public void PostDefaultWorldCreation();

        /// <summary>
        /// Called as part of <see cref="Application.Destroy"/> before <see cref="World.OnDestroy()"/>
        /// </summary>
        public void PreWorldDestroy();

        /// <summary>
        /// Called as part of <see cref="Application.Destroy"/> after <see cref="World.OnDestroy()"/>
        /// </summary>
        public void PostWorldDestroy();

        /// <summary>
        /// This occurs first in <see cref="Application.Dispose"/> before any other disposal operations"/>
        /// </summary>
        public void PreApplicationDispose();
    }
}
