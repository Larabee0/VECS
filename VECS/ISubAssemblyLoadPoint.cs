using VECS.ECS;

namespace VECS
{
    public interface ISubAssemblyLoadPoint
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
        /// Called at the top of <see cref="Application"/> constructor
        /// </summary>
        public void PreApplicationConstruction();

        /// <summary>
        /// Called before <see cref="Application.Start"/>
        /// </summary>
        public void PreApplicationStart();

        /// <summary>
        /// Called as part of <see cref="Application.Start"/> before <see cref="World.OnCreate()"/>
        /// This is called before <see cref="Presenter.Start"/> and <see cref="Application.PreOnCreate"/>
        /// </summary>
        public void PreDefaultWorldCreation();

        /// <summary>
        /// Called as part of <see cref="Application.Start"/> after <see cref="World.OnCreate()"/>
        /// This is called before <see cref="Application.PostOnCreate"/> but after <see cref="Application.PreOnCreate"/> and <see cref="World.OnCreate"/>
        /// </summary>
        public void PostDefaultWorldCreation();

        /// <summary>
        /// Called as part of <see cref="Application.Destroy"/> before <see cref="World.OnDestroy()"/>
        /// </summary>
        public void PreDefaultWorldDestroy();

        /// <summary>
        /// Called as part of <see cref="Application.Destroy"/> after <see cref="World.OnDestroy()"/>
        /// </summary>
        public void PostDefaultWorldDestroy();

        /// <summary>
        /// Called at the top of <see cref="Application.Dispose"/>"/>
        /// This is called beforethe default world is disposed
        /// </summary>
        public void PreApplicationDispose();
    }
}
