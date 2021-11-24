using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK;
using OpenTK.Graphics.OpenGL;

namespace P
{
    class GPURayTracer
    {
        int generateProgram;
        int firstHitProgram;
        int shadingProgram;
        int raySSBO = -1;
        int shadowRaySSBO = -1;
        int rayCounterBO = -1;
        int width = 1;
        int height = 1;
        int textureHandle = -1;
        public GPURayTracer ()
        {
            generateProgram = Shader.CreateComputeShaderProgram("../../generate.shader");
            firstHitProgram = Shader.CreateComputeShaderProgram("../../bruteFirstHit.shader");
            shadingProgram = Shader.CreateComputeShaderProgram("../../shading.shader");

            SetupBuffers(width, height);
        }

        bool st = true;
        void SetupBuffers(int width, int height)
        {
            if(raySSBO != -1) { GL.DeleteBuffer(raySSBO); }
            raySSBO = GL.GenBuffer();
            if (shadowRaySSBO != -1) { GL.DeleteBuffer(shadowRaySSBO); }
            shadowRaySSBO = GL.GenBuffer();
            if (rayCounterBO == -1) { 
                rayCounterBO = GL.GenBuffer();
            }

            GL.UseProgram(generateProgram);

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, raySSBO);
            GL.BufferData(BufferTarget.ShaderStorageBuffer,
               (1 + width * height) * (Vector4.SizeInBytes * 5), IntPtr.Zero, BufferUsageHint.StaticDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, raySSBO);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

            
            if (st)
            {
                uint[] test = { 3 };
                GL.BindBuffer(BufferTarget.AtomicCounterBuffer, rayCounterBO);
                GL.BufferData(BufferTarget.AtomicCounterBuffer, sizeof(uint), test, BufferUsageHint.StaticDraw);
                //GL.BufferSubData(BufferTarget.AtomicCounterBuffer, IntPtr.Zero, sizeof(uint), test);
                GL.BindBufferBase(BufferRangeTarget.AtomicCounterBuffer, 2, rayCounterBO);
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
                st = false;
            }

            unsafe
            {
                GL.BindBuffer(BufferTarget.AtomicCounterBuffer, rayCounterBO);
                uint* userCounters;
                userCounters = (uint*)GL.MapBuffer(BufferTarget.AtomicCounterBuffer, BufferAccess.ReadWrite);
                //userCounters = (uint*)GL.MapBufferRange(BufferTarget.AtomicCounterBuffer, IntPtr.Zero, sizeof(uint),
                //BufferAccessMask.MapWriteBit | BufferAccessMask.MapInvalidateBufferBit | BufferAccessMask.MapUnsynchronizedBit);
                Console.WriteLine("user counters 0: " + userCounters[0]);
                userCounters[0] = 0;
                Console.WriteLine("user counters 0 agane: " + userCounters[0]);
                Console.WriteLine("unmap returns: " + GL.UnmapBuffer(BufferTarget.AtomicCounterBuffer));
                uint uc2 = 69;
                uint* uc2ptr = &uc2;
                GL.GetBufferSubData(BufferTarget.AtomicCounterBuffer, IntPtr.Zero, sizeof(uint), (IntPtr)uc2ptr);
                Console.WriteLine("agane agane: " + uc2ptr[0]);
            }

            GL.UseProgram(firstHitProgram);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, raySSBO);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, raySSBO);

            GL.BindBuffer(BufferTarget.AtomicCounterBuffer, rayCounterBO);
            GL.BindBufferBase(BufferRangeTarget.AtomicCounterBuffer, 2, rayCounterBO);

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, shadowRaySSBO);
            GL.BufferData(BufferTarget.ShaderStorageBuffer,
               (1 + width * height) * (Vector4.SizeInBytes * 6), IntPtr.Zero, BufferUsageHint.StaticDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 3, shadowRaySSBO);

            GL.UseProgram(shadingProgram);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, raySSBO);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, raySSBO);

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, shadowRaySSBO);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 3, shadowRaySSBO);

            if (textureHandle != -1) { GL.DeleteTexture(textureHandle); } //We already had a texture, so delete it.
            textureHandle = GL.GenTexture();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, textureHandle);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba32f, width, height, 0, PixelFormat.Rgba, PixelType.Float, (IntPtr)null);
            GL.BindImageTexture(0, textureHandle, 0, false, 0, TextureAccess.WriteOnly, SizedInternalFormat.Rgba32f);
        }

        public void GenTex(int width, int height)
        {
            if(width != this.width || height != this.height)
            {
                SetupBuffers(width, height);
                this.width = width;
                this.height = height;
            }

            GL.BindTexture(TextureTarget.Texture2D, textureHandle);
            GL.UseProgram(generateProgram);

            //trying to set ray counter buffer to be 0???
            GL.BindBuffer(BufferTarget.AtomicCounterBuffer, rayCounterBO);
            GL.BufferData(BufferTarget.AtomicCounterBuffer, sizeof(uint), new uint[] { 0 }, BufferUsageHint.StaticDraw);

            GL.DispatchCompute(width, height, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);

            GL.UseProgram(firstHitProgram);
            GL.DispatchCompute(width, height, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);

            GL.UseProgram(shadingProgram);
            GL.DispatchCompute(width, height, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);
        }

        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                GL.DeleteProgram(generateProgram);
                GL.DeleteProgram(firstHitProgram);
                GL.DeleteProgram(shadingProgram);

                disposedValue = true;
            }
        }

        ~GPURayTracer()
        {
            GL.DeleteProgram(generateProgram);
            GL.DeleteProgram(firstHitProgram);
            GL.DeleteProgram(shadingProgram);
        }

        public void Dispose()
        {
            GL.DeleteBuffer(raySSBO);
            GL.DeleteBuffer(shadowRaySSBO);
            GL.DeleteBuffer(rayCounterBO);
            GL.DeleteTexture(textureHandle);
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
