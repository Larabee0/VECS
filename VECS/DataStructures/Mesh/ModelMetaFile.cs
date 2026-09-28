using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace VECS
{
    public class ModelMetaFile : AssetMetaFile
    {
        [JsonIgnore]
        public string SrcFileName;

        [JsonIgnore]
        public string MetaFileName => string.Format("{0}.meta", SrcFileName);
        [JsonIgnore]
        public DirectMesh TargetInstance;
        [JsonIgnore]
        public MaterialInfo[] MaterialInfo;
        [JsonIgnore]
        HashSet<MaterialInfo.TextureInfo> AllTextures;
        public MaterialSet MaterialSet {  get; set; }

        public bool LoadOnStart { get; set; } = true;
        public bool AutoLoadTextures { get; set; } = true;

        [JsonIgnore]
        public bool IsLoaded => TargetInstance != null;
        [JsonIgnore]
        public uint IndexCount;

        public ModelMetaFile() { }

        public ModelMetaFile(string srcFile, DirectMesh targetInstance, MaterialSet materialSet = null)
        {
            GUID = Guid.NewGuid();
            Version = 0;
            Type = typeof(ModelMetaFile).FullName;
            SrcFileName = srcFile;
            TargetInstance = targetInstance;
            MaterialSet = materialSet;
            PostLoad();
            Debug.Assert(File.Exists(srcFile));
        }

        public override void PostLoad()
        {
            AllTextures = [];
            if (MaterialSet != null)
            {
                MaterialInfo = new MaterialInfo[MaterialSet.Materials.Length];
                string filename = Path.GetFileNameWithoutExtension(SrcFileName);
                for (int i = 0; i < MaterialSet.Materials.Length; i++)
                {
                    MaterialInfo[i] = new(MaterialSet.Materials[i], filename);
                    var tex = MaterialInfo[i].TextureInfos;
                    for (int j = 0; j < tex.Length; j++)
                    {
                        if (tex[j].TextureFile != null)
                        {
                            AllTextures.Add(tex[j]);
                        }
                        
                    }
                }

            }
        }

        public void TryTexturesLoaded()
        {
            if(AllTextures != null && AllTextures.Count > 0)
            {
                foreach (var item in AllTextures)
                {
                    TextureLoader.GetOrLoad2D(item.TextureFile, item.FormatHint);
                }
            }
        }

        public override void SaveMetaFile()
        {
            var serialized = JsonSerializer.Serialize(this);
            File.WriteAllText(MetaFileName, serialized);
        }
    }
}
