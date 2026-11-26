using SDL3;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public struct CommandBufferSubmitInfo
    {
        public string Label;
        public VkCommandBuffer CommandBuffer;

        public uint Present;
        public ulong PresentSemaphoreValue;

        public uint WaitSemaphores;
        public uint SignalSemaphores;

        public VkSemaphoreSubmitInfo WaitSemaphore0;
        public VkSemaphoreSubmitInfo WaitSemaphore1;
        public VkSemaphoreSubmitInfo WaitSemaphore2;
        public VkSemaphoreSubmitInfo WaitSemaphore3;


        public VkSemaphoreSubmitInfo SignalSemaphore0;
        public VkSemaphoreSubmitInfo SignalSemaphore1;
        public VkSemaphoreSubmitInfo SignalSemaphore2;
        public VkSemaphoreSubmitInfo SignalSemaphore3;

        public CommandBufferSubmitInfo()
        {
            Present = uint.MaxValue;
        }

        public CommandBufferSubmitInfo(VkCommandBuffer commandBuffer,
            uint waitSemaphores,
            VkSemaphoreSubmitInfo wait0, VkSemaphoreSubmitInfo wait1, VkSemaphoreSubmitInfo wait2, VkSemaphoreSubmitInfo wait3,
            uint signalSemaphores,
            VkSemaphoreSubmitInfo signal0, VkSemaphoreSubmitInfo signal1, VkSemaphoreSubmitInfo signal2, VkSemaphoreSubmitInfo signal3)
        {
            CommandBuffer = commandBuffer;
            WaitSemaphores = waitSemaphores;
            SignalSemaphores = signalSemaphores;
            WaitSemaphore0 = wait0;
            WaitSemaphore1 = wait1;
            WaitSemaphore2 = wait2;
            WaitSemaphore3 = wait3;
            SignalSemaphore0 = signal0;
            SignalSemaphore1 = signal1;
            SignalSemaphore2 = signal2;
            SignalSemaphore3 = signal3;
            Present = uint.MaxValue;
        }

        public static CommandBufferSubmitInfo Wait(VkCommandBuffer commandBuffer, VkSemaphoreSubmitInfo wait)
        {
            return new(commandBuffer, 1, wait, default, default, default, 0, default, default, default, default);
        }

        public static CommandBufferSubmitInfo Signal(VkCommandBuffer commandBuffer, VkSemaphoreSubmitInfo signal)
        {
            return new(commandBuffer, 0, default, default, default, default, 1, signal, default, default, default);
        }

        public static CommandBufferSubmitInfo Wait(VkCommandBuffer commandBuffer, VkSemaphoreSubmitInfo wait0, VkSemaphoreSubmitInfo wait1)
        {
            return new(commandBuffer, 2, wait0, wait1, default, default, 0, default, default, default, default);
        }

        public static CommandBufferSubmitInfo Signal(VkCommandBuffer commandBuffer, VkSemaphoreSubmitInfo signal0, VkSemaphoreSubmitInfo signal1)
        {
            return new(commandBuffer, 0, default, default, default, default, 2, signal0, signal1, default, default);
        }

        public static CommandBufferSubmitInfo Wait(VkCommandBuffer commandBuffer, VkSemaphoreSubmitInfo wait0, VkSemaphoreSubmitInfo wait1, VkSemaphoreSubmitInfo wait2)
        {
            return new(commandBuffer, 3, wait0, wait1, wait2, default, 0, default, default, default, default);
        }

        public static CommandBufferSubmitInfo Signal(VkCommandBuffer commandBuffer, VkSemaphoreSubmitInfo signal0, VkSemaphoreSubmitInfo signal1, VkSemaphoreSubmitInfo signal2)
        {
            return new(commandBuffer, 0, default, default, default, default, 3, signal0, signal1, signal2, default);
        }

        public static CommandBufferSubmitInfo Wait(VkCommandBuffer commandBuffer, VkSemaphoreSubmitInfo wait0, VkSemaphoreSubmitInfo wait1, VkSemaphoreSubmitInfo wait2, VkSemaphoreSubmitInfo wait3)
        {
            return new(commandBuffer, 4, wait0, wait1, wait2, wait3, 0, default, default, default, default);
        }

        public static CommandBufferSubmitInfo Signal(VkCommandBuffer commandBuffer, VkSemaphoreSubmitInfo signal0, VkSemaphoreSubmitInfo signal1, VkSemaphoreSubmitInfo signal2, VkSemaphoreSubmitInfo signal3)
        {
            return new(commandBuffer, 0, default, default, default, default, 4, signal0, signal1, signal2, signal3);
        }

        public static CommandBufferSubmitInfo WaitSignal(VkCommandBuffer commandBuffer, VkSemaphoreSubmitInfo wait, VkSemaphoreSubmitInfo signal)
        {
            return new(commandBuffer, 1, wait, default, default, default, 1, signal, default, default, default);
        }

        public static CommandBufferSubmitInfo WaitSignal(VkCommandBuffer commandBuffer, VkSemaphoreSubmitInfo wait0, VkSemaphoreSubmitInfo wait1, VkSemaphoreSubmitInfo signal0, VkSemaphoreSubmitInfo signal1)
        {
            return new(commandBuffer, 2, wait0, wait1, default, default, 2, signal0, signal1, default, default);
        }

        public static CommandBufferSubmitInfo WaitSignal(VkCommandBuffer commandBuffer, VkSemaphoreSubmitInfo wait0, VkSemaphoreSubmitInfo wait1, VkSemaphoreSubmitInfo wait2, VkSemaphoreSubmitInfo signal0, VkSemaphoreSubmitInfo signal1, VkSemaphoreSubmitInfo signal2)
        {
            return new(commandBuffer, 3, wait0, wait1, wait2, default, 3, signal0, signal1, signal2, default);
        }

        public static CommandBufferSubmitInfo WaitSignal(VkCommandBuffer commandBuffer, VkSemaphoreSubmitInfo wait0, VkSemaphoreSubmitInfo wait1, VkSemaphoreSubmitInfo wait2, VkSemaphoreSubmitInfo wait3, VkSemaphoreSubmitInfo signal0, VkSemaphoreSubmitInfo signal1, VkSemaphoreSubmitInfo signal2, VkSemaphoreSubmitInfo signal3)
        {
            return new(commandBuffer, 4, wait0, wait1, wait2, wait3, 4, signal0, signal1, signal2, signal3);
        }

        public static CommandBufferSubmitInfo WaitSignal(VkSemaphoreSubmitInfo wait0, VkCommandBuffer commandBuffer, VkSemaphoreSubmitInfo signal0, VkSemaphoreSubmitInfo signal1, VkSemaphoreSubmitInfo signal2, VkSemaphoreSubmitInfo signal3)
        {
            return new(commandBuffer, 1, wait0, default, default, default, 4, signal0, signal1, signal2, signal3);
        }

        public static CommandBufferSubmitInfo WaitSignal(VkSemaphoreSubmitInfo wait0, VkCommandBuffer commandBuffer, VkSemaphoreSubmitInfo signal0, VkSemaphoreSubmitInfo signal1, VkSemaphoreSubmitInfo signal2)
        {
            return new(commandBuffer, 1, wait0, default, default, default, 3, signal0, signal1, signal2, default);
        }

        public static CommandBufferSubmitInfo WaitSignal(VkSemaphoreSubmitInfo wait0, VkCommandBuffer commandBuffer, VkSemaphoreSubmitInfo signal0, VkSemaphoreSubmitInfo signal1)
        {
            return new(commandBuffer, 1, wait0, default, default, default, 2, signal0, signal1, default, default);
        }

        public static CommandBufferSubmitInfo WaitSignal( VkSemaphoreSubmitInfo wait0, VkSemaphoreSubmitInfo wait1, VkSemaphoreSubmitInfo wait2, VkCommandBuffer commandBuffer, VkSemaphoreSubmitInfo signal0, VkSemaphoreSubmitInfo signal1, VkSemaphoreSubmitInfo signal2, VkSemaphoreSubmitInfo signal3)
        {
            return new(commandBuffer, 3, wait0, wait1, wait2, default, 4, signal0, signal1, signal2, signal3);
        }

        public static CommandBufferSubmitInfo WaitSignal(VkSemaphoreSubmitInfo wait0, VkSemaphoreSubmitInfo wait1, VkCommandBuffer commandBuffer, VkSemaphoreSubmitInfo signal0, VkSemaphoreSubmitInfo signal1, VkSemaphoreSubmitInfo signal2, VkSemaphoreSubmitInfo signal3)
        {
            return new(commandBuffer, 2, wait0, wait1, default, default, 4, signal0, signal1, signal2, signal3);
        }

        public readonly void AddWaits(Span<VkSemaphoreSubmitInfo> waits)
        {
            switch (WaitSemaphores)
            {
                case 1:
                    waits[0] = WaitSemaphore0;
                    break;
                case 2:
                    waits[0] = WaitSemaphore0;
                    waits[1] = WaitSemaphore1;
                    break;
                case 3:
                    waits[0] = WaitSemaphore0;
                    waits[1] = WaitSemaphore1;
                    waits[2] = WaitSemaphore2;
                    break;
                case 4:
                    waits[0] = WaitSemaphore0;
                    waits[1] = WaitSemaphore1;
                    waits[2] = WaitSemaphore2;
                    waits[3] = WaitSemaphore3;
                    break;
                default:
                    return;

            }
        }

        public readonly void AddSignals(Span<VkSemaphoreSubmitInfo> signals)
        {
            switch (SignalSemaphores)
            {
                case 1:
                    signals[0] = SignalSemaphore0;
                    break;
                case 2:
                    signals[0] = SignalSemaphore0;
                    signals[1] = SignalSemaphore1;
                    break;
                case 3:
                    signals[0] = SignalSemaphore0;
                    signals[1] = SignalSemaphore1;
                    signals[2] = SignalSemaphore2;
                    break;
                case 4:
                    signals[0] = SignalSemaphore0;
                    signals[1] = SignalSemaphore1;
                    signals[2] = SignalSemaphore2;
                    signals[3] = SignalSemaphore3;
                    break;
                default:
                    return;

            }
        }

        public readonly VkSemaphoreSubmitInfo GetWait(int index)
        {
            return index switch
            {
                0 => WaitSemaphore0,
                1 => WaitSemaphore1,
                2 => WaitSemaphore2,
                3 => WaitSemaphore3,
                _ => default,
            };
        }


        public readonly VkSemaphoreSubmitInfo GetSignal(int index)
        {
            return index switch
            {
                0 => SignalSemaphore0,
                1 => SignalSemaphore1,
                2 => SignalSemaphore2,
                3 => SignalSemaphore3,
                _ => default,
            };
        }

        public readonly ulong GetTimelineWaitValue(VkSemaphore timelineSemaphore)
        {
            for (int i = 0; i < WaitSemaphores; i++)
            {
                var wait = GetWait(i);
                if(wait.semaphore == timelineSemaphore)
                {
                    return wait.value;
                }
            }
            return ulong.MaxValue;
        }

        public readonly ulong GetTimelineSignalValue(VkSemaphore timelineSemaphore)
        {
            for (int i = 0; i < SignalSemaphores; i++)
            {
                var signal = GetSignal(i);
                if (signal.semaphore == timelineSemaphore)
                {
                    return signal.value;
                }
            }
            return ulong.MaxValue;
        }

        public override string ToString()
        {
            return Label;
        }
    }

    public enum TimelineGuide : ulong
    {
        RelativeSwapChainPrepare = 990,
        RelativeAuxiliaryDispatch = 980,

        StartQueue = 000,

        AuxiliaryDispatch = 1980,
        SwapChainPrepare = 1990,
        BeginPresent = 2000,
        MAX = 2000
    }

    /// <summary>
    /// Three main threads are used for generating a frame
    /// The main thead is responsible for the application update loop and generating command buffers either directily or by scheduling other threads to generate command buffers.
    /// Command buffers can be submitted via <see cref="EnqueueCommandBuffer"/> from any thread at any time
    /// 
    /// The <see cref="_submitterThread"/> which runs <see cref="SubmissionThread(object)"/>, dequeues and submits command buffers whenever 
    /// it has items in <see cref="_commandBuffersToSubmit"/> AND isn't current waiting for <see cref="_submitTrigger"/> to be set
    /// Currently <see cref="_submitTrigger"/> is set whenever anything is enqueued via <see cref="EnqueueCommandBuffer"/> but this may not always be the case
    /// 
    /// The <see cref="_swapChainSyncThread"/> runs <see cref="SwapChainSyncThread(object)"/>, schroninses to the swapchain and by extension the gpu main queue.
    /// Only the main thread will be blocked by this thread, as such it aims to release it as soon as possible after incrementing the frame and "reference" timeline semaphore values.
    /// Afterwards it queues up the swapchain present, beings acquision of the next swapchain image then waits for the frame just presented to finish rendering (but not presenting) before incrementing
    /// the "true" semaphore value and signaling the gpu to start executing work once again
    /// 
    /// </summary>
    public static class AdvancedSubmission
    {
        private static int CurrentFrame => SwapChain.FrameIndex;

        private static TimelineSemaphore _timelineSemaphore;
        private static Thread _swapChainSyncThread;
        private static Thread _submitterThread;

        private static CancellationTokenSource _frameSyncCancel;
        private static CancellationTokenSource _submitCancel;

        private static AutoResetEvent _mainThreadControlled;
        private static AutoResetEvent _swapChainSyncThreadControlled;
        private static AutoResetEvent _submitTrigger;

        private static bool _anyFrameAcquired;

        private readonly static ConcurrentQueue<CommandBufferSubmitInfo> _commandBuffersToSubmit = new();
        private readonly static List<CommandBufferSubmitInfo> _signalAfterWaitToSubmits = [];
        private static ulong _signalTrigger;

        public static VkSemaphore MainTimelineSemaphore => _timelineSemaphore.Semaphore;

        public static void EnqueueCommandBuffer(CommandBufferSubmitInfo submitInfo, string label)
        {
            submitInfo.Label = label;
            _commandBuffersToSubmit.Enqueue(submitInfo);
            _submitTrigger.Set();
        }

        public static void StartSubmission()
        {
            _commandBuffersToSubmit.Clear();
            _timelineSemaphore = new(0);
            GraphicsDevice.SetObjectName(VkObjectType.Semaphore, _timelineSemaphore.Semaphore.Handle, "Main Queue Timeline Semaphore");
            SwapChain._currentSemaphoreValue = 0;
            _signalTrigger = 0;

            _frameSyncCancel = new();
            _submitCancel = new();
            _mainThreadControlled = new(false);
            _swapChainSyncThreadControlled = new(false);
            _submitTrigger = new(false);
            _swapChainSyncThread = new(SwapChainSyncThread)
            {
                Name = "Swap Chain Syncroniser",
                IsBackground = true
            };
            _submitterThread = new(SubmissionThread)
            {
                Name = "Queue Submitter",
                IsBackground = true
            };
            _anyFrameAcquired = false;
            _submitterThread.Start(_submitCancel);
            _swapChainSyncThread.Start(_frameSyncCancel);
        }

        public static void StopSubmission()
        {
            _frameSyncCancel.Cancel();
            _submitCancel.Cancel();
            _mainThreadControlled.Set();
            _submitTrigger.Set();
            _swapChainSyncThread.Join();
            _submitterThread.Join();
            _mainThreadControlled.Dispose();
            _swapChainSyncThreadControlled.Dispose();
            _submitTrigger.Dispose();
            _commandBuffersToSubmit.Clear();

            GraphicsDevice.DeviceAPI.vkQueueWaitIdle(GraphicsDevice._mainQueue);
            _timelineSemaphore.Dispose();
            SwapChain._currentSemaphoreValue = 0;
            _signalTrigger = 0;
            Debug.Assert(_signalAfterWaitToSubmits.Count == 0);
        }

        public static unsafe void AcquireFrame(SwapChainData swapChain, int frameIndex)
        {
            uint imageIndex = _anyFrameAcquired ? (*swapChain.CurrentImageIndex + 1 ) % SwapChain.SWAP_CHAIN_IMAGE_COUNT_UINT : 0;
            _anyFrameAcquired = true;
            swapChain.AcquireSemaphoreIndex[0] = imageIndex;
            VkAcquireNextImageInfoKHR acquireInfo = new()
            {
                swapchain = swapChain.SwapChain,
                timeout = SwapChain.SEMAPHORE_TIME_OUT,
                semaphore = swapChain.AcquiredImageReadySemaBINARY[imageIndex],
                deviceMask = 0 | (1 << /* 1st subdevice index*/0)
            };

            var result = GraphicsDevice.DeviceAPI.vkAcquireNextImage2KHR(&acquireInfo, swapChain.CurrentImageIndex);

            if (result == VkResult.ErrorOutOfDateKHR)
            {
                SwapChain.RecreateSwapChain = true;
            }
            else if (result != VkResult.Success && result != VkResult.SuboptimalKHR)
            {
                result.CheckResult("Failed to acquire next swap chain image!");
                SwapChain.RecreateSwapChain = true;
            }
        }


        public unsafe static void TriggerFrameFinished()
        {
            SwapChain.BuildGraphicsCommands(CurrentFrame, 1, SwapChain.MainSwapChainData.CurrentImageIndex);
            _mainThreadControlled.Set();
            _swapChainSyncThreadControlled.WaitOne();
        }

        private static void SubmissionThread(object cancellationToken)
        {
            CancellationTokenSource cancel = (CancellationTokenSource)cancellationToken;
            while (!cancel.IsCancellationRequested)
            {
                if (!_commandBuffersToSubmit.TryDequeue(out var submission))
                {
                    _submitTrigger.WaitOne();
                    continue;
                }
                CheckAndSubmit(submission);

            }
        }

        private static void CheckAndSubmit(CommandBufferSubmitInfo submission)
        {
            ulong currentSignal = Math.Max(_signalTrigger, _timelineSemaphore.GetCounterValue());
            ulong wait = submission.GetTimelineWaitValue(_timelineSemaphore.Semaphore);
            ulong signal = submission.GetTimelineSignalValue(_timelineSemaphore.Semaphore);

            if (currentSignal >= wait || wait == ulong.MaxValue)
            {
                _signalTrigger = Math.Max(_signalTrigger, signal);
                SignalAfterWaitToSubmits(submission);
            }
            else
            {
                _signalAfterWaitToSubmits.Add(submission);
            }
        }

        private static void SwapChainSyncThread(object cancellationToken)
        {
            CancellationTokenSource cancel = (CancellationTokenSource)cancellationToken;
            // acquire frame 0
            AcquireFrame(SwapChain.MainSwapChainData, 0);

            while (!cancel.IsCancellationRequested)
            {
                // wait for main thread
                _mainThreadControlled.WaitOne();
                if (cancel.IsCancellationRequested) return;
                SubmitAndSignal(SwapChain.FrameIndex);
            }
        }

        public static unsafe void SubmitAndSignal(int frameIndex)
        {
            uint currentImageIndex = *SwapChain.MainSwapChainData.CurrentImageIndex;
            uint currentAcquireSemaphoreIndex = *SwapChain.MainSwapChainData.AcquireSemaphoreIndex;
            int nextFrameToAcquire = SwapChain.NextFrame;


            Interlocked.Exchange(ref SwapChain._currentFrame, nextFrameToAcquire);
            Interlocked.Increment(ref SwapChain._currentSemaphoreValue);

            // unblock main thread as early as possible when the frame is consdiered "submitted"
            _swapChainSyncThreadControlled.Set();

            // preapre swapchains for present by copying the output textures to them
            PrepareAndPresentSwapChainImages(frameIndex, _timelineSemaphore.SemaphoreValue, currentImageIndex, currentAcquireSemaphoreIndex);

            // get the next frame to present
            AcquireFrame(SwapChain.MainSwapChainData, nextFrameToAcquire);

            _timelineSemaphore.Wait(_timelineSemaphore.GetTimelineStageValue((ulong)TimelineGuide.BeginPresent));

            _timelineSemaphore.SemaphoreValue++;
        }

        private unsafe static void PrepareAndPresentSwapChainImages(int frameIndex, ulong semaphoreValue, uint imageIndex, uint acquireImageSemaphoreIndex)
        {
            VkCommandBuffer commandBuffer = GraphicsDevice.GetPrimaryCommandBuffer(frameIndex);
            GraphicsDevice.BeginPrimaryCommandBuffer(commandBuffer);
            GraphicsDevice.BeginLabelCmd(commandBuffer, "SwapChain Present Prepare");

            SwapChain.SetSwapChainImageLayoutTransferDST(commandBuffer, frameIndex, 1, &imageIndex);
            Presenter.Instance.CopyFromOutputToSwapChainFull(commandBuffer, Presenter.Display0Src, 0, (int)imageIndex);
            SwapChain.SetSwapChainLayoutPresent(commandBuffer, frameIndex, 1, &imageIndex);

            GraphicsDevice.EndLabelCmd(commandBuffer);
            GraphicsDevice.DeviceAPI.vkEndCommandBuffer(commandBuffer);
            var submitInfo = CommandBufferSubmitInfo.WaitSignal
            (
                commandBuffer,
                // wait
                new()
                {
                    semaphore = _timelineSemaphore.Semaphore,
                    value = TimelineSemaphore.GetTimelineStageValue((ulong)TimelineGuide.SwapChainPrepare, semaphoreValue),
                    stageMask = VkPipelineStageFlags2.AllCommands
                },
                new()
                {
                    semaphore = SwapChain.MainSwapChainData.AcquiredImageReadySemaBINARY[acquireImageSemaphoreIndex],
                    stageMask = VkPipelineStageFlags2.AllCommands
                },
                // signal,
                new()
                {
                    semaphore = _timelineSemaphore.Semaphore,
                    value = TimelineSemaphore.GetTimelineStageValue((ulong)TimelineGuide.BeginPresent, semaphoreValue),
                    stageMask = VkPipelineStageFlags2.AllCommands
                },
                new()
                {
                    semaphore = SwapChain._renderCompleteSemaBINARY[imageIndex],
                    stageMask = VkPipelineStageFlags2.AllCommands
                }
            );
            submitInfo.Present = imageIndex;
            submitInfo.PresentSemaphoreValue = semaphoreValue;
            EnqueueCommandBuffer(submitInfo,"SwapChain Prepare");
        }

        private static void SignalAfterWaitToSubmits(CommandBufferSubmitInfo submission)
        {
            SubmitQueue(submission);
            uint present = submission.Present;
            if (_signalAfterWaitToSubmits.Count > 0)
            {
                _signalAfterWaitToSubmits.Sort((a, b) =>
                {
                    var aWait = a.GetTimelineWaitValue(_timelineSemaphore.Semaphore);
                    var bWait = b.GetTimelineWaitValue(_timelineSemaphore.Semaphore);
                    aWait = aWait == ulong.MaxValue ? 0 : aWait;
                    bWait = bWait == ulong.MaxValue ? 0 : bWait;
                    var compare = bWait.CompareTo(aWait);
                    if (compare != 0) return compare;
                    var aSignal = a.GetTimelineSignalValue(_timelineSemaphore.Semaphore);
                    var bSignal = b.GetTimelineSignalValue(_timelineSemaphore.Semaphore);
                    return bSignal.CompareTo(aSignal);

                });

                int submitUpTo = -1;
                for (int i = _signalAfterWaitToSubmits.Count - 1; i >= 0; i--)
                {
                    var cur = _signalAfterWaitToSubmits[i];
                    ulong currentSignal = _signalTrigger;
                    ulong wait = cur.GetTimelineWaitValue(_timelineSemaphore.Semaphore);
                    ulong signal = cur.GetTimelineSignalValue(_timelineSemaphore.Semaphore);
                    if (wait >= signal)
                    {
                        break;
                    }
                    _signalTrigger = signal != ulong.MaxValue ? Math.Max(_signalTrigger, signal) : _signalTrigger;
                    submitUpTo = i;
                    present = Math.Min(cur.Present, present);
                    SubmitQueue(cur);
                    _signalAfterWaitToSubmits.RemoveAt(i);
                }
            }

            if (present != uint.MaxValue)
            {
                QueuePresent(SwapChain.MainSwapChainData, present);
            }
        }

        private static void SubmitQueue(CommandBufferSubmitInfo submission)
        {
            uint waits = submission.WaitSemaphores;
            uint signals = submission.SignalSemaphores;
            VkCommandBufferSubmitInfo bufferSubmitInfo = new() { commandBuffer = submission.CommandBuffer };

            Span<VkSemaphoreSubmitInfo> semaphoreSubmitInfos = stackalloc VkSemaphoreSubmitInfo[(int)(waits + signals)];
            submission.AddWaits(semaphoreSubmitInfos);
            submission.AddSignals(semaphoreSubmitInfos.Slice((int)waits, (int)signals));

            VkSubmitInfo2 submitInfo = new()
            {
                commandBufferInfoCount = 1,
                waitSemaphoreInfoCount = waits,
                signalSemaphoreInfoCount = signals,
            };
            // Console.WriteLine("Queue submitted at: {0}", Time.TimeSinceStartUp);
            // for (int i = 0; i < waits; i++)
            // {
            //     Console.WriteLine("Queue wait semahpore value {0}", semaphoreSubmitInfos[i].value);
            // }
            // for (int i = (int)waits; i < waits + signals; i++)
            // {
            //     Console.WriteLine("Queue signal semahpore value {0}", semaphoreSubmitInfos[i].value);
            // }
            unsafe
            {
                fixed (VkSemaphoreSubmitInfo* pSemaSubInfo = semaphoreSubmitInfos)
                {
                    submitInfo.pCommandBufferInfos = &bufferSubmitInfo;
                    submitInfo.pWaitSemaphoreInfos = pSemaSubInfo;
                    submitInfo.pSignalSemaphoreInfos = pSemaSubInfo + waits;
                    GraphicsDevice.DeviceAPI.vkQueueSubmit2KHR(GraphicsDevice.MainQueue, 1, &submitInfo, VkFence.Null).CheckResult("Failed to submit graphics queue!");
                }
            }
        }

        public static void SubmitGraphicsQueue(VkCommandBuffer commandBuffer, ulong semaphoreValue, ulong waitOffset, ulong signalOffset)
        {
            Debug.Assert(waitOffset < signalOffset, "Relative Signal cannot be less than relative wait!");
            var submitInfo = CommandBufferSubmitInfo.WaitSignal(
                commandBuffer,
                new()
                {
                    value = TimelineSemaphore.GetTimelineStageValue(waitOffset, semaphoreValue),
                    semaphore = _timelineSemaphore.Semaphore,
                    stageMask = VkPipelineStageFlags2.AllCommands
                },
                new()
                {
                    value = TimelineSemaphore.GetTimelineStageValue(signalOffset, semaphoreValue),
                    semaphore = _timelineSemaphore.Semaphore,
                    stageMask = VkPipelineStageFlags2.AllCommands
                }
            );

            EnqueueCommandBuffer(submitInfo,"Graphics Queue Submit");
        }

        public unsafe static bool QueuePresent(SwapChainData swapChain, uint imageIndex)
        {
            SwapChainData swapChainData = swapChain;
            VkSwapchainKHR swapchains = swapChainData.SwapChain;
            VkSemaphore waitSemaphore = SwapChain._renderCompleteSemaBINARY[imageIndex];
            VkPresentInfoKHR presentInfo = new()
            {
                swapchainCount = 1,
                pSwapchains = &swapchains,
                waitSemaphoreCount = 1,
                pWaitSemaphores = &waitSemaphore,
                pImageIndices = &imageIndex
            };

            var result = GraphicsDevice.DeviceAPI.vkQueuePresentKHR(GraphicsDevice.MainQueue, &presentInfo);

            if (result == VkResult.ErrorOutOfDateKHR || result == VkResult.SuboptimalKHR)
            {
                Console.WriteLine("Cancel on Present current image");
                SwapChain.RecreateSwapChain = true;
                return false;
            }

            result.CheckResult("Could not present the image to the swapchain!");
            return true;
        }
    }
}
