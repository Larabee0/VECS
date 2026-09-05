#define VERY_LOW_FRAME_RATES
using BepuUtilities;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using VECS.ECS;
using VECS.LowLevel;
using VECS.UI;
using Vortice.Vulkan;

namespace VECS
{
    public sealed class Application : IDisposable
    {
        public readonly static int Width = 1280;
        public readonly static int Height = 720;

        public static Application Instance { get; private set; }
        private static bool running = true;

        private static uint _targetFrameRate = uint.MaxValue; //20;//  
        private static double _targetFrameTime;

        public static uint TargetFrameRate
        {
            get => _targetFrameRate;
            set
            {
#if DEBUG
                value = Math.Max(value,1);
#else
                value = Math.Max(value, 10);
#endif
                value = value > 20000 ? uint.MaxValue : value;
                _targetFrameRate = value;
                _targetFrameTime = 1000 / (double)_targetFrameRate;
            }
        }

        private readonly SDL3Window _mainAppWindow;

        private readonly Presenter _presenter;

        private static World _mainWorld;
        private static ThreadDispatcher _threadDispatcher;

        public static ThreadDispatcher ThreadDispatcher => _threadDispatcher;

        public Action PreOnCreate;
        public Action PostOnCreate;
        public Action OnDestroy;
        public Action UpdateCallback;
        public static IWindow MainWindow => Instance._mainAppWindow;

        private static string _persistentDataPath;

        public static string ExecutingDirectory => AppDomain.CurrentDomain.BaseDirectory;
        public static string ProjectName => Bootstrap.ProjectName;
        public static string PersistentDataPath => _persistentDataPath;

        public static NoesisDriver NoesisDriver => NoesisHandler.NoesisDriver;

        public Application()
        {
            _persistentDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _persistentDataPath = System.IO.Path.Combine(_persistentDataPath, Bootstrap.ProjectName);
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
            SDL3WindowManager.Init();
            _mainAppWindow = SDL3WindowManager.CreateNewWindow("VECS", Width, Height);
            GraphicsDevice.Initialise(_mainAppWindow);
            SDL3WindowManager.CheckLoadedPresentMode();
            ShaderModule.LoadAllShaders();
            //SDL3WindowManager.CreateNewEditorWindow("VECS-Editor", Width, Height);
            //_presenter = new Presenter<ForwardRenderer>();
            _presenter = new Presenter<DeferredRenderer>();

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
                running = !SDL3WindowManager.UpdateWindowEvents();
                if (!running)
                {
                    break;
                }
                Time.Update();
                frameStart = Time.TimeSinceStartUpAsDouble * 1000.0;
                Time.UpdateFixedTimeStep();
                Update();
                Presentation();
                SDL3WindowManager.LateInputUpdate();
                TextureLoader.UpdateCompression();
                TargetFrameRateUpdate();
            }
            SwapChain.FinishTimelineWorkers(false);
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
            AssetManager.FileWatcherStart();
            Bootstrap.subAssemblyLoadPoints.ForEach(loadPoint => loadPoint.PreDefaultWorldCreation());
            _mainWorld = new World();

            _presenter.Start(); // presenter depends on the main entity world existing right away
            NoesisHandler.Init();
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

        private static unsafe void LogMemoryUsage()
        {
            VmaBudget* budgets = stackalloc VmaBudget[(int)Vulkan.VK_MAX_MEMORY_HEAPS];

            Vma.vmaGetHeapBudgets(GraphicsDevice.VmaAllocator, budgets);
            Console.WriteLine("\nLogging Vulkan Memory Usage");

            Vma.vmaCalculateStatistics(GraphicsDevice.VmaAllocator, out VmaTotalStatistics stats);
            Console.WriteLine("Totaly Bytes: {0}", stats.total.statistics.allocationBytes);
            Console.WriteLine("Unused Bytes: {0} Min {1} Max", stats.total.unusedRangeSizeMin, stats.total.unusedRangeSizeMax);
            Console.WriteLine("\nLogging Per Asset Memory Usage");
            GetTextureMemoryUsage<Texture2D>();
            GetTextureMemoryUsage<Texture2DArray>();
            GetTextureMemoryUsage<Texture3D>();
            GetTextureMemoryUsage<Cubemap>();
            GetTextureMemoryUsage<CubemapArray>();

            ulong meshBytes = 0;

            for (int i = 0; i < AssetDataBase<DirectMesh>.AllAssetsListForReading.Count; i++)
            {
                var mesh = AssetDataBase<DirectMesh>.AllAssetsListForReading[i];
                foreach (var vertexBuffer in mesh._vertexBuffers)
                {
                    meshBytes += vertexBuffer.Value.VkBufferSize;
                }
                meshBytes += mesh.IndexBuffer.VkBufferSize;
            }
            Console.WriteLine("Mesh memory usage {0} bytes", meshBytes);

            ulong computePipe = 0;

            for (int i = 0; i < AssetDataBase<ComputePipeline>.AllAssetsListForReading.Count; i++)
            {
                var pipe = AssetDataBase<ComputePipeline>.AllAssetsListForReading[i];
                computePipe += pipe.UniformBufferSize * SwapChain.MAX_CONCURRENT_FRAMES_UINT;

                for (int j = 0; j < pipe.DescriptorSetInfos.Length; j++)
                {
                    var setInfo = pipe.DescriptorSetInfos[j];
                    for (int k = 0; k < setInfo.DescriptorBuffers.Length; k++)
                    {
                        computePipe += setInfo.DescriptorBuffers[k].AllocationSize;
                    }
                    if (setInfo.StorageBuffers == null) continue;
                    for (int k = 0; k < setInfo.StorageBuffers.Length; k++)
                    {
                        if (setInfo.IsStorageBufferOwnerBufferIndex(k))
                        {
                            computePipe += setInfo.StorageBuffers[k].VkBufferSize * SwapChain.MAX_CONCURRENT_FRAMES_UINT;
                        }
                    }
                }
            }

            Console.WriteLine("Compute Pipeline memory usage {0} bytes", computePipe);

            ulong graphicsPipes = 0;

            for (int i = 0; i < AssetDataBase<GraphicsPipeline>.AllAssetsListForReading.Count; i++)
            {
                var pipe = AssetDataBase<GraphicsPipeline>.AllAssetsListForReading[i];
                graphicsPipes += pipe.UniformBufferSize * SwapChain.MAX_CONCURRENT_FRAMES_UINT;

                for (int j = 0; j < pipe.DescriptorSetInfos.Length; j++)
                {
                    var setInfo = pipe.DescriptorSetInfos[j];
                    for (int k = 0; k < setInfo.DescriptorBuffers.Length; k++)
                    {
                        if (setInfo.DescriptorBuffers[k] == null) continue;
                        graphicsPipes += setInfo.DescriptorBuffers[k].AllocationSize;
                    }
                    if (setInfo.StorageBuffers == null) continue;
                    for (int k = 0; k < setInfo.StorageBuffers.Length; k++)
                    {
                        if (setInfo.IsStorageBufferOwnerBufferIndex(k))
                        {
                            graphicsPipes += setInfo.StorageBuffers[k].VkBufferSize * SwapChain.MAX_CONCURRENT_FRAMES_UINT;
                        }
                    }
                }
            }

            Console.WriteLine("Graphics Pipeline memory usage {0} bytes", graphicsPipes);

            ulong engineBuffers = 0;
            foreach (var item in EngineBuffers._engineBuffers)
            {
                engineBuffers += item.Value.VkBufferSize * SwapChain.MAX_CONCURRENT_FRAMES_UINT;
            }

            Console.WriteLine("Engine Buffer memory usage {0} bytes", graphicsPipes);
        }

        private static void GetTextureMemoryUsage<T>() where T :Texture
        {
            ulong sizeBytes = 0;

            for (int i = 0; i < AssetDataBase<T>.AllAssetsListForReading.Count; i++)
            {
                var texture = AssetDataBase<T>.AllAssetsListForReading[i];

                sizeBytes += texture._vkBufferSizeRequirement;

                if(texture._hostBuffer  != null)
                {
                    sizeBytes += texture._hostBuffer.VkBufferSize;
                }
            }

            Console.WriteLine("{0} memory usage {1} bytes", typeof(T).Name, sizeBytes);
        }

        private static void LogAssetCounts()
        {
            Console.WriteLine("Logging Assets Counts...");
            foreach (var assetType in typeof(Asset).AllSubclassesNonAbstract())
            {
                var assetCount = (int)GenericExtensions.GetStaticPropertyOnGenericType(typeof(AssetDataBase<>), assetType, "AssetCount");
                Console.WriteLine("{0}: {1}", assetType.Name, assetCount);
            }
            LogMemoryUsage();
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
            Instance.UpdateCallback?.Invoke();
            _mainWorld.OnUpdate();
            _mainWorld.OnPostUpdate();
            if (InputManager.Instance.GetKeyUp(SDL3.SDL_Keycode.Y))
            {
                LogMemoryUsage();
            }
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
            AssetManager.CleanUp();
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
            NoesisHandler.Dispose();
            _presenter.Dispose();
            SDL3WindowManager.DestroyAllWindows();
            GPUBufferExtensions.Reset();
            TextureExtensions.Reset();
            ShaderModule.CleanUp();
            ShaderCache.Dispose();
            AuxiliaryCommandBufferManager.CleanUp();
            GraphicsDevice.Dispose();
            SDL3WindowManager.CleanUp();
        }
    }
}
