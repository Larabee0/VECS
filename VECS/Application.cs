using System;
using VECS.ECS;
using VECS.LowLevel;
using BepuUtilities;
using System.Runtime.CompilerServices;
using System.Threading;
using VECS.UI;
using System.Diagnostics;
using System.IO;

namespace VECS
{
    public sealed class Application : IDisposable
    {
        public readonly static int Width = 1280;
        public readonly static int Height = 720;

        public static Application Instance { get; private set; }
        private static bool running = true;

        private static uint _targetFrameRate = uint.MaxValue;
        private static double _targetFrameTime;

        public static uint TargetFrameRate
        {
            get => _targetFrameRate;
            set
            {
                value = Math.Max(value, 10);
                value = value > 20000 ? uint.MaxValue : value;
                _targetFrameRate = value;
                _targetFrameTime = 1000 / (double)_targetFrameRate;
            }
        }

        private readonly SDL3Window _appWindow;
        private readonly Presenter _presenter;

        private static World _mainWorld;
        private static ThreadDispatcher _threadDispatcher;

        public static ThreadDispatcher ThreadDispatcher => _threadDispatcher;

        public Action PreOnCreate;
        public Action PostOnCreate;
        public Action OnDestroy;

        private static string _persistentDataPath;

        public static string ExecutingDirectory => AppDomain.CurrentDomain.BaseDirectory;
        public static string ProjectName => Bootstrap.ProjectName;
        public static string PersistentDataPath => _persistentDataPath;

        public Application()
        {
            _persistentDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _persistentDataPath = Path.Combine(_persistentDataPath, Bootstrap.ProjectName);
            if (!Directory.Exists(_persistentDataPath))
            {
                Directory.CreateDirectory(_persistentDataPath);
            }
            Console.WriteLine("PersistentDataPath: {0}", PersistentDataPath);
            var sw = Stopwatch.StartNew();
            Instance = this;
            var targetThreadCount = int.Max(1, Environment.ProcessorCount > 4 ? Environment.ProcessorCount - 2 : Environment.ProcessorCount - 1);
            _threadDispatcher = new ThreadDispatcher(targetThreadCount);

            Bootstrap.subAssemblyLoadPoints.ForEach(loadPoint => loadPoint.PreApplicationConstruction());
            _appWindow = new(Width, Height, "VECS");
            GraphicsDevice.Initialise(_appWindow);
            ShaderModule.LoadAllShaders();
            _presenter = new(_appWindow);
            ULUI.Initialise();

            Time.FixedTimeStepCallback += FixedUpdate;
            sw.Stop();
            Console.WriteLine("Application.Constructor time: {0}ms", sw.ElapsedMilliseconds);
            TargetFrameRate = _targetFrameRate;
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
                frameStart = Time.TimeSinceStartUpAsDouble * 1000.0;
                Time.UpdateFixedTimeStep();
                Update();
                ULUI.UpdateUI();
                Presentation();
                InputManager.Instance.LateUpdate();
                //Thread.Sleep(1000);
                TargetFrameRateUpdate();
            }
            SwapChain.Instance.FinishTimelineWorkers(false);
            GraphicsDevice.DeviceWaitIdle();
            Destroy();
        }
        private double frameStart;
        private void TargetFrameRateUpdate()
        {
            if (_targetFrameRate == uint.MaxValue) return;
            double frameEnd = Time.TimeSinceStartUpAsDouble * 1000.0;

            double duration = frameEnd - frameStart;
            double remaining = _targetFrameTime - duration;

            while (remaining > 0)
            {
                Thread.SpinWait(5);
                frameEnd = Time.TimeSinceStartUpAsDouble * 1000.0;
                duration = frameEnd - frameStart;
                remaining = _targetFrameTime - duration;
            }
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
            Stopwatch sw = Stopwatch.StartNew();
            Bootstrap.subAssemblyLoadPoints.ForEach(loadPoint => loadPoint.PreApplicationStart());
            running = true;

            Bootstrap.subAssemblyLoadPoints.ForEach(loadPoint => loadPoint.PreDefaultWorldCreation());
            _mainWorld = new World();

            _presenter.Start(); // presenter depends on the main entity world existing right away
            PreOnCreate?.Invoke();

            World.OnCreate();

            Bootstrap.subAssemblyLoadPoints.ForEach(loadPoint => loadPoint.PostDefaultWorldCreation());
            PostOnCreate?.Invoke();
            if (Bootstrap.LogAssetDataBaseCountsOnStart)
            {
                LogAssetCounts();
            }
            
            DisposableAsset.RemoveDisposedFromAssetDataBase();

            if (Bootstrap.LogAssetDataBaseCountsOnStart)
            {
                Console.WriteLine("Purging Disposed Assets...");
                LogAssetCounts();
            }
            sw.Stop();
            Console.WriteLine("Application.Start time: {0}ms", sw.ElapsedMilliseconds);
            Console.WriteLine("Start completed, Engine is Running!");
        }

        private static void LogAssetCounts()
        {
            Console.WriteLine("Logging Assets Counts...");
            foreach (var assetType in typeof(Asset).AllSubclassesNonAbstract())
            {
                var assetCount = (int)GenericExtensions.GetStaticPropertyOnGenericType(typeof(AssetDataBase<>), assetType, "AssetCount");
                Console.WriteLine("{0}: {1}", assetType.Name, assetCount);
            }
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
            Bootstrap.subAssemblyLoadPoints.ForEach(loadPoint => loadPoint.PreDefaultWorldDestroy());
            _mainWorld.OnDestroy();
            Bootstrap.subAssemblyLoadPoints.ForEach(loadPoint => loadPoint.PostDefaultWorldDestroy());
            OnDestroy?.Invoke();
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
            try
            {
                Bootstrap.subAssemblyLoadPoints.ForEach(loadPoint => loadPoint.PreApplicationDispose());
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Concat(new object[]
                {
                    "ERROR: Exception In PreApplicationDispose from sub assembly: ",
                    ex
                }));
            }
            _mainWorld?.Dispose();
            Time.FixedTimeStepCallback -= FixedUpdate;
            ULUI.CleanUp();
            _presenter.Dispose();
            GPUBufferExtensions.Reset();
            TextureExtensions.Reset();
            ShaderCache.Dispose();
            GraphicsDevice.Dispose();
            _appWindow.Dispose();
        }
    }
}
