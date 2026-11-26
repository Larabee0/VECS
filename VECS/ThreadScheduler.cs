using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using BepuUtilities.Memory;

namespace VECS.Threading
{
    /// <summary>
    /// This is a crude threadsafe reentrant IThreadDispatcher for bepu. it uses a slightly modified BepuUtilities ThreadDispatcher which is non-reentrant 
    /// ThreadScheduler controls access to the ThreadDispatcher using AutoResetEvents and a concurrent queue to allow multiple threads to queue up work for the ThreadDispatcher
    /// The ThreadScheduler thread waits for a trigger signal when the queue is empty, once triggered it will execute the queued work synchronously until the queue is empty
    /// 
    /// Threads can schedule work that will blocked them until the ThreadScheduler has completed their scheduled WorkUint.
    /// Threads can schedule work that returns an AutoResetEvent allowing them to continue and wait for it later or never wait.
    /// </summary>
    public class ThreadScheduler : BepuUtilities.IThreadDispatcher, IDisposable
    {
        public const int DEFAULT_WAIT_TIMEOUT = 60000; // 60000 Milieseconds == 60 seconds

        private class WorkUnit
        {
            public int MaximumWorkerCount;
            public Action<int> WorkerBody;
            public AutoResetEvent CallbackUnblock;

            public WorkUnit(int maximumWorkerCount, Action<int> workerBody,  AutoResetEvent callbackUnblock)
            {
                MaximumWorkerCount = maximumWorkerCount;
                WorkerBody = workerBody;
                CallbackUnblock = callbackUnblock;
            }
        }

        private readonly Thread _threadScheduler;

        private readonly CancellationTokenSource _schedulerCancel;

        private readonly ConcurrentQueue<WorkUnit> _tasksToSchedule = new();
        private readonly ConcurrentDictionary<int, AutoResetEvent> _callbackCache = new();

        private readonly AutoResetEvent _trigger = new(false);

        private readonly ThreadDispatcher _threadDispatcher;
        public int ThreadCount => _threadDispatcher.ThreadCount;

        public ThreadScheduler()
        {
             _threadDispatcher = new(int.Max(1, Environment.ProcessorCount > 4 ? Environment.ProcessorCount - 2 : Environment.ProcessorCount - 1));
            _schedulerCancel = new();
            _threadScheduler = new(SchedulerProcess)
            {
                IsBackground = true,
                Name = "VECS Thread Scheduler"
            };
            _threadScheduler.Start(_schedulerCancel);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _schedulerCancel.Cancel();
            _trigger.Set();
            _threadScheduler.Join();
            _trigger.Dispose();
            foreach (var item in _callbackCache)
            {
                item.Value.Dispose();
            }
            _callbackCache.Clear();
            _threadDispatcher.Dispose();
            GC.ReRegisterForFinalize(this);
        }

        private void SchedulerProcess(object cancellationToken)
        {
            CancellationTokenSource cancel = (CancellationTokenSource)cancellationToken;
            WorkUnit workUnit;
            while (!cancel.IsCancellationRequested)
            {
                if(!_tasksToSchedule.TryDequeue(out workUnit))
                {
                    _trigger.WaitOne();
                    continue;
                }
                _threadDispatcher.DispatchWorkers(workUnit.WorkerBody, workUnit.MaximumWorkerCount);
                workUnit.CallbackUnblock.Set();
            }
        }

        public void ParallelForWaitNow(int count, Action<int> action)
        {
            int bepuCounter = -1;
            DispatchWorkers((workIndex) =>
            {
                int claimedIndex;
                while ((claimedIndex = Interlocked.Increment(ref bepuCounter)) < count)
                {
                    action?.Invoke(claimedIndex);
                }
            });
        }

        public AutoResetEvent ParallelForWaitLater(int count, Action<int> action)
        {
            int bepuCounter = -1;
            return DispatchWorkersWaitLater((workIndex) =>
            {
                int claimedIndex;
                while ((claimedIndex = Interlocked.Increment(ref bepuCounter)) < count)
                {
                    action?.Invoke(claimedIndex);
                }
            });
        }

        public void ParallelForWaitNow(int count, Action<int, int> action)
        {
            int bepuCounter = -1;
            DispatchWorkers((workIndex) =>
            {
                int claimedIndex;
                while ((claimedIndex = Interlocked.Increment(ref bepuCounter)) < count)
                {
                    action?.Invoke(workIndex, claimedIndex);
                }
            });
        }

        public AutoResetEvent ParallelForWaitLater(int count, Action<int, int> action)
        {
            int bepuCounter = -1;
            return DispatchWorkersWaitLater((workIndex) =>
            {
                int claimedIndex;
                while ((claimedIndex = Interlocked.Increment(ref bepuCounter)) < count)
                {
                    action?.Invoke(workIndex, claimedIndex);
                }
            });
        }

        public void DispatchWorkers(Action<int> workerBody, int maximumWorkerCount = int.MaxValue)
        {
            if(!DispatchWorkersWaitLater(workerBody, maximumWorkerCount).WaitOne(DEFAULT_WAIT_TIMEOUT))
            {
                throw new TimeoutException("Possible hang in the ThreadScheduler detected!");
            }
        }


        public AutoResetEvent DispatchWorkersWaitLater(Action<int> workerBody, int maximumWorkerCount = int.MaxValue)
        {
            int threadId = Environment.CurrentManagedThreadId;

#if DEBUG
            if (_threadDispatcher.ThreadIds.Contains(threadId))
            {
                throw new InvalidOperationException($"Attempting to dispatch workers from a thread in the worker pool will lead to a deadlock in the ThreadScheduler");
            }
#endif

            if (!_callbackCache.TryGetValue(threadId, out var waiter))
            {
                waiter = new(false);
                _callbackCache.TryAdd(threadId, waiter);
            }
            else
            {
                waiter.Reset();
            }

            _tasksToSchedule.Enqueue(new(maximumWorkerCount, workerBody, waiter));
            _trigger.Set();
            return waiter;
            
        }

        public BufferPool GetThreadMemoryPool(int workerIndex)
        {
            return _threadDispatcher.GetThreadMemoryPool(workerIndex);
        }
    }

    //
    // Summary:
    //     Provides a BepuUtilities.IThreadDispatcher implementation. Not reentrant.
    // added thread names & thread ids hashset for debugging
    public class ThreadDispatcher : BepuUtilities.IThreadDispatcher, IDisposable
    {
        private struct Worker
        {
            public Thread Thread;

            public AutoResetEvent Signal;
        }

        private readonly int _threadCount;

        private readonly Worker[] _workers;

        private readonly AutoResetEvent _finished;

        private readonly BufferPool[] _bufferPools;
#if DEBUG
        public HashSet<int> ThreadIds = [];
#endif
        private volatile Action<int> _workerBody;

        private int _remainingWorkerCounter;

        private volatile bool _disposed;

        //
        // Summary:
        //     Gets the number of threads to dispatch work on.
        public int ThreadCount => _threadCount;

        //
        // Summary:
        //     Creates a new thread dispatcher with the given number of threads.
        //
        // Parameters:
        //   threadCount:
        //     Number of threads to dispatch on each invocation.
        //
        //   threadPoolBlockAllocationSize:
        //     Size of memory blocks to allocate for thread pools.
        public ThreadDispatcher(int threadCount, int threadPoolBlockAllocationSize = 16384)
        {
            _threadCount = threadCount;
            _workers = new Worker[threadCount - 1];
            for (int i = 0; i < _workers.Length; i++)
            {
                _workers[i] = new Worker
                {
                    Thread = new Thread(WorkerLoop) { 
                        IsBackground = true,
                        Name = $"Worker Thead: {i}"
                    },
                    Signal = new AutoResetEvent(initialState: false)
                };
                _workers[i].Thread.Start((_workers[i].Signal, i + 1));
#if DEBUG
                ThreadIds.Add(_workers[i].Thread.ManagedThreadId);
#endif
            }

#if DEBUG
            Debug.Assert(ThreadIds.Count == _workers.Length);
#endif
            _finished = new AutoResetEvent(initialState: false);
            _bufferPools = new BufferPool[threadCount];
            for (int j = 0; j < _bufferPools.Length; j++)
            {
                _bufferPools[j] = new BufferPool(threadPoolBlockAllocationSize);
            }
        }

        private void DispatchThread(int workerIndex)
        {
            _workerBody(workerIndex);
            if (Interlocked.Decrement(ref _remainingWorkerCounter) == -1)
            {
                _finished.Set();
            }
        }

        private void WorkerLoop(object untypedSignal)
        {
            var (autoResetEvent, workerIndex) = ((AutoResetEvent, int))untypedSignal;
            while (true)
            {
                autoResetEvent.WaitOne();
                if (_disposed)
                {
                    break;
                }

                DispatchThread(workerIndex);
            }
        }

        private void SignalThreads(int maximumWorkerCount)
        {
            int num = maximumWorkerCount - 1;
            int num2 = (_remainingWorkerCounter = ((num < _workers.Length) ? num : _workers.Length));
            for (int i = 0; i < num2; i++)
            {
                _workers[i].Signal.Set();
            }
        }

        public void DispatchWorkers(Action<int> workerBody, int maximumWorkerCount = int.MaxValue)
        {
            if (maximumWorkerCount > 1)
            {
                _workerBody = workerBody;
                SignalThreads(maximumWorkerCount);
                DispatchThread(0);
                _finished.WaitOne();
                _workerBody = null;
            }
            else if (maximumWorkerCount == 1)
            {
                workerBody(0);
            }
        }

        //
        // Summary:
        //     Waits for all pending work to complete and then disposes all workers.
        public void Dispose()
        {
            if (_disposed) return;
            GC.SuppressFinalize(this);
            _disposed = true;
            SignalThreads(_threadCount);
            for (int i = 0; i < _bufferPools.Length; i++)
            {
                _bufferPools[i].Clear();
            }

            Worker[] array = _workers;
            for (int j = 0; j < array.Length; j++)
            {
                Worker worker = array[j];
                worker.Thread.Join();
                worker.Signal.Dispose();
            }
            GC.ReRegisterForFinalize(this);
        }

        public BufferPool GetThreadMemoryPool(int workerIndex)
        {
            return _bufferPools[workerIndex];
        }
    }

}