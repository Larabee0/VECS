using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using VECS;
using VECS.ECS.Transforms;

public static class ThreadingTesting
{
    private static int iterations = 6;
    private static int waitIterations = 500;
    public static void Test()
    {
        Console.WriteLine("Testing threading overhead");
        Console.WriteLine("{0}x{1}", iterations, waitIterations);
        Thread.SpinWait(waitIterations);
        BepuFor();
        BepuFor();
        BepuFor();
        Thread.SpinWait(waitIterations);
        BepuFor();
        BepuFor();
        BepuFor();
        Thread.SpinWait(waitIterations);
        For();
        For();
        For();
        Thread.SpinWait(waitIterations);
        For();
        For();
        For();
        Thread.SpinWait(waitIterations);
        ParallelFor();
        ParallelFor();
        ParallelFor();
        Thread.SpinWait(waitIterations);
        ParallelFor();
        ParallelFor();
        ParallelFor();
        //Debugger.Break();
    }

    public static void For()
    {
        Stopwatch stopwatch = new();
        stopwatch.Start();
        for (int i = 0; i < iterations; i++)
        {
            Matrix4x4[] martixArray = new Matrix4x4[waitIterations];
            for (int j = 0; j < waitIterations; j++)
            {
                martixArray[j] = TransformExtensions.TRS(Vector3.One, Quaternion.Identity, Vector3.One);
            }
        }
        stopwatch.Stop();

        Console.WriteLine("Serial For time: {0} ticks", stopwatch.ElapsedTicks);
    }

    public static void ParallelFor()
    {
        Stopwatch stopwatch = new();
        stopwatch.Start();
        Parallel.For(0, iterations, (i) =>
        {
            Matrix4x4[] martixArray = new Matrix4x4[waitIterations];
            for (int j = 0; j < waitIterations; j++)
            {
                martixArray[j] = TransformExtensions.TRS(Vector3.One, Quaternion.Identity, Vector3.One);
            }
        });
        stopwatch.Stop();

        Console.WriteLine("Parallel For time: {0} ticks", stopwatch.ElapsedTicks);
    }

    public static void BepuFor()
    {
        Stopwatch stopwatch = new();
        stopwatch.Start();
        bepuCounter = -1;
        Application.ThreadDispatcher.DispatchWorkers(ExecuteWorker);
        stopwatch.Stop();
        Console.WriteLine("Bepu Therad Dispatcher time: {0} ticks", stopwatch.ElapsedTicks);
    }

    private static int bepuCounter = -1;
    private static void ExecuteWorker(int workerIndex)
    {
        int claimedIndex;
        while ((claimedIndex = Interlocked.Increment(ref bepuCounter)) < iterations)
        {
            Matrix4x4[] martixArray = new Matrix4x4[waitIterations];
            for (int j = 0; j < waitIterations; j++)
            {
                martixArray[j] = TransformExtensions.TRS(Vector3.One, Quaternion.Identity, Vector3.One);
            }
        }
    }

}