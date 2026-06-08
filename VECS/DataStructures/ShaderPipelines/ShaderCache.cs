using System;
using System.IO;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public static class ShaderCache
    {
        public const string SHADER_CACHE_FILE_NAME = "ShaderCache.bin";

        public static string ShaderCacheFileDirectory => Path.Combine(Application.PersistentDataPath, SHADER_CACHE_FILE_NAME);

        private static readonly VkPipelineCache _cache;
        public static VkPipelineCache Cache => _cache;

        static unsafe ShaderCache()
        {
            var createInfo = new VkPipelineCacheCreateInfo();
            VkPipelineCacheHeaderVersionOne header = default;
            uint loadedDriverVersion = default;
            VkVersion loadedAPIVersion = default;
            int loadedHash = int.MaxValue;
            int calculatedHash = int.MaxValue;
            byte[] shaderCache = null;
            try
            {
                if (File.Exists(ShaderCacheFileDirectory))
                {
                    shaderCache = File.ReadAllBytes(ShaderCacheFileDirectory);

                    uint additionalHeaderSize = sizeof(uint) + (uint)sizeof(VkVersion) + sizeof(int);
                    uint cacheSize = (uint)shaderCache.Length - additionalHeaderSize;
                    createInfo.initialDataSize = cacheSize;

                    fixed (byte* pCache = shaderCache)
                    {
                        Buffer.MemoryCopy(pCache, &header, sizeof(VkPipelineCacheHeaderVersionOne), sizeof(VkPipelineCacheHeaderVersionOne));

                        Buffer.MemoryCopy(pCache + cacheSize, &loadedDriverVersion, sizeof(uint), sizeof(uint));
                        Buffer.MemoryCopy(pCache + cacheSize + sizeof(uint), &loadedAPIVersion, sizeof(VkVersion), sizeof(VkVersion));
                        Buffer.MemoryCopy(pCache + cacheSize + sizeof(uint) + sizeof(VkVersion), &loadedHash, sizeof(int), sizeof(int));

                        calculatedHash = ShaderProperties.Hash(pCache, cacheSize + sizeof(uint) + (uint)sizeof(VkVersion));
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error loading shader cache: {0}", ex.Message);
                Console.WriteLine(ex.StackTrace);
            }

            if (shaderCache != null)
            {
                if (loadedHash == int.MaxValue || calculatedHash == int.MaxValue) { shaderCache = null; }

                if (loadedDriverVersion != GraphicsDevice.PropertiesVK10.driverVersion) { shaderCache = null; }

                if (loadedAPIVersion != GraphicsDevice.PropertiesVK10.apiVersion) { shaderCache = null; }

                if(header.deviceID != GraphicsDevice.PropertiesVK10.deviceID) { shaderCache = null; }

                if (header.vendorID != GraphicsDevice.PropertiesVK10.vendorID) { shaderCache = null; }

                if(header.headerSize == 0) {  shaderCache = null; }

                if (loadedHash != calculatedHash) { shaderCache = null; }
            }

            if (shaderCache != null)
            {
                fixed (void* pShaderCache = shaderCache)
                {
                    createInfo.pInitialData = pShaderCache;
                    GraphicsDevice.DeviceAPI.vkCreatePipelineCache(createInfo, null, out _cache);
                }

                return;
            }
            else
            {
                createInfo.initialDataSize = 0;
            }

            GraphicsDevice.DeviceAPI.vkCreatePipelineCache(createInfo, null, out _cache);

        }

        public  static unsafe void Dispose()
        {
            try
            {
                if (File.Exists(ShaderCacheFileDirectory))
                {
                    File.Delete(ShaderCacheFileDirectory);
                }
                nuint cacheSize = 0;
                uint additionalHeaderSize = sizeof(uint) + (uint)sizeof(VkVersion) + sizeof(int);
                GraphicsDevice.DeviceAPI.vkGetPipelineCacheData(_cache, &cacheSize, null);
                uint totalSize = additionalHeaderSize + (uint)cacheSize;
                byte[] cache = new byte[totalSize];
                uint driverVersion = GraphicsDevice.PropertiesVK10.driverVersion;
                VkVersion apiVersion = GraphicsDevice.PropertiesVK10.apiVersion;
                int cacheHash = int.MaxValue;

                fixed (byte* pCache = cache)
                {
                    GraphicsDevice.DeviceAPI.vkGetPipelineCacheData(_cache, &cacheSize, pCache);
                    
                    Buffer.MemoryCopy(&driverVersion, pCache + (uint)cacheSize, additionalHeaderSize, sizeof(uint));
                    Buffer.MemoryCopy(&apiVersion, pCache + (uint)cacheSize + sizeof(uint), additionalHeaderSize - sizeof(uint), sizeof(VkVersion));

                    cacheHash = ShaderProperties.Hash(pCache, (uint)cacheSize + sizeof(uint) + (uint)sizeof(VkVersion));


                    Buffer.MemoryCopy(&cacheHash, pCache + (totalSize - sizeof(int)), sizeof(int), sizeof(int));
                }



                File.WriteAllBytes(ShaderCacheFileDirectory, cache);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error saving shader cache: {0}", ex.Message);
                Console.WriteLine(ex.StackTrace);
            }

            GraphicsDevice.DeviceAPI.vkDestroyPipelineCache(_cache);
        }

    }
}