using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace VECS
{
    /// <summary>
    /// Globally accessible uniform buffer
    /// This holds things like the camera data and lights
    /// Point lights are defined up to a max lights value
    /// </summary>
    public unsafe class GlobalUbo
    {
        public unsafe static int SizeInBytes => sizeof(WriteableUBO) + (sizeof(PointLight) * Presenter.MAX_LIGHTS);

        public Matrix4x4 Projection;
        public Matrix4x4 View;
        public Matrix4x4 InverseView;
        public Vector4 AmbientLightColour;
        public int NumLights;

        public  PointLight[] PointLights;

        public GlobalUbo()
        {
            PointLights = new PointLight[Presenter.MAX_LIGHTS];
        }


        public void WriteToBuffer(GPUBuffer targetBuffer)
        {
            WriteableUBO.Write(this, targetBuffer);
        }

        public void WriteToSwapChainBuffer(SwapChainBuffer targetBuffer)
        {
            WriteableUBO.Write(this,targetBuffer);
        }

        [StructLayout(LayoutKind.Sequential, Size = 212)]
        public struct WriteableUBO
        {
            public unsafe static int SizeInBytes => sizeof(WriteableUBO);
            public Matrix4x4 Projection;
            public Matrix4x4 View;
            public Matrix4x4 InverseView;
            public Vector4 AmbientLightColour;
            public int NumLights;

            public WriteableUBO(GlobalUbo source)
            {
                Projection = source.Projection;
                View = source.View;
                InverseView = source.InverseView;
                AmbientLightColour = source.AmbientLightColour;
                NumLights = source.NumLights;

            }

            public static void Write(GlobalUbo source, GPUBuffer buffer)
            {
                WriteableUBO writeable = new(source);
                PointLight* pPointLights = stackalloc PointLight[Presenter.MAX_LIGHTS];

                for (int i = 0; i < Presenter.MAX_LIGHTS; i++)
                {
                    pPointLights[i] = source.PointLights[i];
                }
                buffer.WriteToBuffer(&writeable);

                ulong offset =  (ulong)SizeInBytes;

                buffer.WriteToBuffer(pPointLights, (ulong)sizeof(PointLight) * Presenter.MAX_LIGHTS, offset+12);
                //buffer.Flush();
            }

            public static void Write(GlobalUbo source, SwapChainBuffer buffer)
            {
                WriteableUBO writeable = new(source);
                PointLight* pPointLights = stackalloc PointLight[Presenter.MAX_LIGHTS];

                for (int i = 0; i < Presenter.MAX_LIGHTS; i++)
                {
                    pPointLights[i] = source.PointLights[i];
                }

                NativeMemory.Copy(&writeable, buffer.HostPtr, (uint)SizeInBytes);

                var hostPtr = (IntPtr)buffer.HostPtr;

                hostPtr = IntPtr.Add(hostPtr, SizeInBytes+12);

                NativeMemory.Copy(pPointLights, (void*)hostPtr, (uint)sizeof(PointLight) * Presenter.MAX_LIGHTS);
                buffer.SetBuffersDirty(true);
            }
        }
    }
}
