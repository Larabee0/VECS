using Noesis;
using System;
using System.IO;

namespace VECS.UI
{
    public class NoesisXamlProvider : XamlProvider
    {
        public override Stream LoadXaml(Uri uri)
        {
            if (uri.IsAbsoluteUri && File.OpenRead(uri.AbsolutePath) is Stream stream)
            {
                return stream;
            }

            if (File.OpenRead(uri.OriginalString) is Stream originalStream)
            {
                return originalStream;
            }

            throw new FileNotFoundException("File not found", uri.OriginalString);
        }
    }
}
