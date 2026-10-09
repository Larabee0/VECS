using VECS;

namespace VECSStabilityTester
{
    [TestClass]
    public sealed class VECSStabilityTester
    {
        [TestMethod]
        public void TestMethod1()
        {
            Thread.Sleep(2500);
            var exitCode = Bootstrap.Main(null);
            GC.Collect();
            Thread.SpinWait(100000);
            Thread.Sleep(2500);
            Assert.AreEqual(0, exitCode,"Exit code indicates crash");
        }


    }
}
