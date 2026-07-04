using Noesis;
using NoesisApp;
using System;
using System.IO;

namespace VECS.UI
{
    public class NoesisXamlProvider : LocalXamlProvider
    {
        private string _basePath;

        public NoesisXamlProvider()
            : this("")
        {
        }

        public NoesisXamlProvider(string basePath)
        {
            _basePath = basePath;
        }

        public override Stream LoadXaml(Uri uri)
        {
            string path = System.IO.Path.Combine(_basePath, uri.GetPath());
            if (File.Exists(path))
            {
                return File.OpenRead(path);
            }

            return null;
        }
    }
}
