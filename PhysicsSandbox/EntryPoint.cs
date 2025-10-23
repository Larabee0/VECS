using System.Diagnostics;
using VECS;
using VECS.ECS;
using VECS.ECS.Presentation;

namespace PhysicsSandbox
{
    public class SandboxEntryPoint : ISubAssemblyEntryPoint
    {
        public int Main(string[] args)
        {
            try
            {
                Application app = new();

                //World.DefaultWorld.EntityManager.
                //World.DefaultWorld.GetSystem<DebugDrawUtilities>().

                //app.PreOnCreate +=
                //app.OnDestroy +=
                app.Run();
                //app.PreOnCreate -=
                //app.OnDestroy -=
                app.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("{0},\n{1}", ex.Message, ex.StackTrace));
                Console.ReadLine();
                return 1;
            }
            return 0;
        }
    }
}
