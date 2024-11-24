using System;

namespace SDL_Vulkan_CS
{
    /// <summary>
    /// Main entry point for the program.
    /// Only initalises and catches excpetions.
    /// </summary>
    public class Program
    {
        static int Main(string[] args)
        {
            try
            {
                Application app = new();
                app.Run();
                app.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("{0},\n{1}", ex.Message, ex.StackTrace));
                return 1;
            }
            return 0;
        }
    }
}
