using SDL3;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS
{
    internal class Program
    {
        static int Main(string[] args)
        {
            try
            {
                Application app = new();
                app.Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return 1;
            }
            return 0;
        }
    }
}
