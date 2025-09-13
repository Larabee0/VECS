using System;
using VECS.ECS;
using VECS.LowLevel;
using VECS.ECS.Physics;
using System.Runtime.InteropServices.Marshalling;
using BepuUtilities;
using Vortice.Vulkan;
using System.Runtime.CompilerServices;
using System.Threading;

namespace VECS
{
    public sealed class Application : IDisposable
    {
        public readonly static int Width = 1280;
        public readonly static int Height = 720;
        private static bool running = true;

        private readonly SDL3Window _appWindow;
        private readonly Presenter _presenter;

        private static World _mainWorld;
        private static ThreadDispatcher _threadDispatcher;

        public static ThreadDispatcher ThreadDispatcher => _threadDispatcher;

        public Action PreOnCreate;
        public Action PostOnCreate;
        public Action OnDestroy;

        public static string ExecutingDirectory => AppDomain.CurrentDomain.BaseDirectory;

        public Application()
        {
            _appWindow = new(Width, Height, "VECS");
            GraphicsDevice.Initialise(_appWindow);
            ShaderModule.LoadAllShaders();
            _presenter = new(_appWindow);
            

            var targetThreadCount = int.Max(1, Environment.ProcessorCount > 4 ? Environment.ProcessorCount - 2 : Environment.ProcessorCount - 1);
            _threadDispatcher = new ThreadDispatcher(targetThreadCount);
            Time.FixedTimeStepCallback += FixedUpdate;
        }

        /// <summary>
        /// Main application loop
        /// </summary>
        public void Run()
        {
            Start();
            while (running)
            {
                running = !_appWindow.UpdateWindowEvents();
                if (!running)
                {
                    break;
                }
                Time.Update();
                Time.UpdateFixedTimeStep();
                Update();
                Presentation();
                InputManager.Instance.LateUpdate();
            }
            SwapChain.Instance.FinishTimelineWorkers(false);
            GraphicsDevice.DeviceWaitIdle();
            Destroy();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Exit()
        {
            running = false;
        }

        /// <summary>
        /// called before the first frame
        /// Sets up the entity world, presenter and artifact.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Start()
        {
            running = true;
            _mainWorld = new World();

            _presenter.Start(); // presenter depends on the main entity world existing right away
            PreOnCreate?.Invoke();

            World.OnCreate();

            PostOnCreate?.Invoke();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private  void FixedUpdate()
        {
            _mainWorld.OnFixedUpdate();
            _mainWorld.OnPostFixedUpdate();
        }

        /// <summary>
        /// Game logic loop
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Update()
        {
            _mainWorld.OnUpdate();
            _mainWorld.OnPostUpdate();
        }

        /// <summary>
        /// Frame presentation/render loop
        /// Render management is handled by the <see cref="Presenter"/> class this just calls begin & end.
        /// 
        /// The order here is begin present, which creates a command buffer nad generates the frame info for the current frame.
        /// 
        /// The main entity world will then update all the presentation systems, parsing this frame info, so render commands can be recorded.
        /// 
        /// Then EndPresent is called, which submits the render commands and starts the graphics queue.
        /// 
        /// Finally PostPresentationSystemUpdate is called on all presentation systems in the main world
        /// 
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Presentation()
        {
            _presenter.Present();
        }

        /// <summary>
        /// Called after a quit command is registered
        /// Called after the graphics device is idle
        /// Called before <see cref="Dispose"/>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Destroy()
        {
            _mainWorld.OnDestroy();
            OnDestroy?.Invoke();
            //_artifact.Destroy();
        }

        public static void ParallelFor(int count, Action<int> action)
        {

            int bepuCounter = -1;
            ThreadDispatcher.DispatchWorkers((workIndex) =>
            {
                int claimedIndex;
                while ((claimedIndex = Interlocked.Increment(ref bepuCounter)) < count)
                {
                    action?.Invoke(claimedIndex);
                }
            });
        }
        
        public static void ParallelFor(int count, Action<int,int> action)
        {

            int bepuCounter = -1;
            ThreadDispatcher.DispatchWorkers((workIndex) =>
            {
                int claimedIndex;
                while ((claimedIndex = Interlocked.Increment(ref bepuCounter)) < count)
                {
                    action?.Invoke(workIndex,claimedIndex);
                }
            });
        }
        /// <summary>
        /// Order of dispoal matters here.
        /// </summary>
        public void Dispose()
        {
            _mainWorld?.Dispose();
            Time.FixedTimeStepCallback -= FixedUpdate;
            _presenter.Dispose();
            GraphicsDevice.Dispose();
            _appWindow.Dispose();
        }
    }
}
