using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VECS
{
    public class ShaderModuleMetaFile : AssetMetaFile
    {
        [JsonIgnore]
        public string SrcFileName;
        
        [JsonIgnore]
        public string MetaFileName => string.Format("{0}.meta", SrcFileName);
        [JsonIgnore]
        public ShaderModule TargetInstance;

        public ShaderModuleMetaFile(string srcFile, ShaderModule targetInstance)
        {
            GUID = Guid.NewGuid();
            Version = 0;
            Type = typeof(ShaderModuleMetaFile).FullName;
            SrcFileName = srcFile;
            TargetInstance = targetInstance;
            Debug.Assert(File.Exists(srcFile));
        }

        public override void SaveMetaFile()
        {
            var serialized = JsonSerializer.Serialize(this);
            File.WriteAllText(MetaFileName, serialized);
        }
        
    }
}