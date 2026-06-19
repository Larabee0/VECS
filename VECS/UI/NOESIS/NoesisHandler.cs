using Noesis;
using NoesisApp;
using VECS.ECS;

namespace VECS.UI
{
    public static class NoesisHandler
    {
        private static NoesisDriver _noesisDriver;

        public static NoesisDriver NoesisDriver => _noesisDriver;
        internal static void Init()
        {

            Log.SetLogCallback(NoesisDriver.LoggerCallback);
            Error.SetUnhandledCallback(NoesisDriver.ErrorCallback);

            GUI.Init();
            GUI.RegisterType(typeof(VectorField));
            var xamlProvider = new LocalXamlProvider(System.IO.Path.Combine(Asset.AssetsPath, "GUI"));
            GUI.SetXamlProvider(xamlProvider);
            var fontProvider = new LocalFontProvider(System.IO.Path.Combine(Asset.AssetsPath, "GUI"));
            GUI.SetFontProvider(fontProvider);
            var textureProvider = new NoesisTextureProvider();
            GUI.SetTextureProvider(textureProvider);

            GUI.SetSoftwareKeyboardCallback(KeyboardCallback);
            string[] fonts = ["Fonts/PT Root UI_Regular", "Arial", "Segoe UI Emoji"];
            GUI.SetFontFallbacks(fonts);
            GUI.SetFontDefaultProperties(15.0f, FontWeight.Normal, FontStretch.Normal, FontStyle.Normal);
            NoesisApp.Application.SetThemeProviders(xamlProvider, fontProvider, textureProvider);
            GUI.LoadApplicationResources("Theme/NoesisTheme.DarkBlue.xaml");
            _noesisDriver = new NoesisDriver();

            World.DefaultWorld.CreateSystem<NoesisEditorView>();
        }

        private static void KeyboardCallback(UIElement focused, bool open)
        {
            
        }
        internal static void Dispose()
        {
            _noesisDriver?.CleanUpMeshData();
            _noesisDriver = null;
            GUI.Shutdown();
        }
    }
}
