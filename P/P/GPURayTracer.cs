using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        int bruteFirstHitProgram;
        int shadingProgram;
        int[] raySSBOs = { -1, -1 };
        int shadowRaySSBO = -1;
        int[] rayCounterBOs = { -1, -1, -1 };
        int width = 1;
        int height = 1;
        int textureHandle = -1;
        Vector3 cameraOrigin = new Vector3(0.0f, -1f, 11f);

        public GPURayTracer ()
        {
            generateProgram = Shader.CreateComputeShaderProgram("../../generate.shader");
            bruteFirstHitProgram = Shader.CreateComputeShaderProgram("../../bruteFirstHit.shader");
            shadingProgram = Shader.CreateComputeShaderProgram("../../shading.shader");

            SetupBuffers(width, height);
        }

        public void updateCamera(Vector3 newPosition)
        {
            cameraOrigin += newPosition;
        }

        void SetupBuffers(int width, int height)
        {
            GL.UseProgram(bruteFirstHitProgram);

            for (int i = 0; i < 2; i++)
            {
                if (raySSBOs[i] != -1) { GL.DeleteBuffer(raySSBOs[i]); }
                raySSBOs[i] = GL.GenBuffer();

                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, raySSBOs[i]);
                GL.BufferData(BufferTarget.ShaderStorageBuffer,
                    (1 + width * height) * (Vector4.SizeInBytes * 30), IntPtr.Zero, BufferUsageHint.StaticDraw);
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1 + i, raySSBOs[i]);
            }
            if (shadowRaySSBO != -1) { GL.DeleteBuffer(shadowRaySSBO); }
            shadowRaySSBO = GL.GenBuffer();

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, shadowRaySSBO);
            GL.BufferData(BufferTarget.ShaderStorageBuffer,
               (1 + width * height) * (Vector4.SizeInBytes * 30), IntPtr.Zero, BufferUsageHint.StaticDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 3, shadowRaySSBO);

            GL.UseProgram(shadingProgram);

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
            GL.BindImageTexture(0, textureHandle, 0, false, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba32f);

            SetupAtomics();
        }

        void SetupAtomics()
        {
            for(int i = 0; i < 3; i++)
            if (rayCounterBOs[i] == -1)
            {
                rayCounterBOs[i] = GL.GenBuffer();
                GL.UseProgram(generateProgram);
                uint[] test = { 0 };
                GL.BindBuffer(BufferTarget.AtomicCounterBuffer, rayCounterBOs[i]);
                GL.BufferData(BufferTarget.AtomicCounterBuffer, sizeof(uint), test, BufferUsageHint.StaticDraw);
                GL.BindBufferBase(BufferRangeTarget.AtomicCounterBuffer, 4 + i, rayCounterBOs[i]);
                
                GL.UseProgram(bruteFirstHitProgram);
                GL.BindBuffer(BufferTarget.AtomicCounterBuffer, rayCounterBOs[i]);
                GL.BufferData(BufferTarget.AtomicCounterBuffer, sizeof(uint), test, BufferUsageHint.StaticDraw);
                GL.BindBufferBase(BufferRangeTarget.AtomicCounterBuffer, 4 + i, rayCounterBOs[i]);
            }
        }

        public void GenTex(int width, int height)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            if (width != this.width || height != this.height)
            {
                SetupBuffers(width, height);
                this.width = width;
                this.height = height;
            }
            GL.BindTexture(TextureTarget.Texture2D, textureHandle);
            GL.ClearTexImage(textureHandle, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            GL.UseProgram(generateProgram);
            //Console.WriteLine("Elapsed 1: " + sw.ElapsedMilliseconds);
            GL.Uniform3(GL.GetUniformLocation(generateProgram, "cameraOrigin"), cameraOrigin);
            //Console.WriteLine("Elapsed 2: " + sw.ElapsedMilliseconds);


            GL.UseProgram(generateProgram);
            for (int i = 0; i < 3; i++)
            {
                GL.BindBuffer(BufferTarget.AtomicCounterBuffer, rayCounterBOs[i]);
                GL.BindBufferBase(BufferRangeTarget.AtomicCounterBuffer, 4 + i, rayCounterBOs[i]);
                GL.BufferData(BufferTarget.AtomicCounterBuffer, sizeof(uint), new uint[] { 0 }, BufferUsageHint.StaticDraw);
            }
            GL.UseProgram(bruteFirstHitProgram);
            for (int i = 0; i < 3; i++)
            {
                GL.BindBuffer(BufferTarget.AtomicCounterBuffer, rayCounterBOs[i]);
                GL.BindBufferBase(BufferRangeTarget.AtomicCounterBuffer, 4 + i, rayCounterBOs[i]);
                GL.BufferData(BufferTarget.AtomicCounterBuffer, sizeof(uint), new uint[] { 0 }, BufferUsageHint.StaticDraw);
            }
            GL.UseProgram(shadingProgram);
            for (int i = 0; i < 3; i++)
            {
                GL.BindBuffer(BufferTarget.AtomicCounterBuffer, rayCounterBOs[i]);
                GL.BindBufferBase(BufferRangeTarget.AtomicCounterBuffer, 4 + i, rayCounterBOs[i]);
                GL.BufferData(BufferTarget.AtomicCounterBuffer, sizeof(uint), new uint[] { 0 }, BufferUsageHint.StaticDraw);
            }

            int currentInBuffer = 0;
            //Console.WriteLine("Elapsed 3: " + sw.ElapsedMilliseconds);

            GL.UseProgram(generateProgram);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, raySSBOs[currentInBuffer]);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, raySSBOs[currentInBuffer]);

            GL.DispatchCompute(width / 1, height, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);

            //Console.WriteLine("Elapsed 4: " + sw.ElapsedMilliseconds);

            int maximumBounces = 20;
            for (int i = 0; i < maximumBounces; i++)
            {
                GL.UseProgram(shadingProgram);
                GL.BindBuffer(BufferTarget.AtomicCounterBuffer, rayCounterBOs[2]);
                GL.BindBufferBase(BufferRangeTarget.AtomicCounterBuffer, 6, rayCounterBOs[2]);
                GL.BufferData(BufferTarget.AtomicCounterBuffer, sizeof(uint), new uint[] { 0 }, BufferUsageHint.StaticDraw);

                GL.UseProgram(bruteFirstHitProgram);
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, raySSBOs[currentInBuffer]);
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, raySSBOs[currentInBuffer]);

                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, raySSBOs[1 - currentInBuffer]);
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 2, raySSBOs[1 - currentInBuffer]);

                GL.BindBuffer(BufferTarget.AtomicCounterBuffer, rayCounterBOs[currentInBuffer]);
                GL.BindBufferBase(BufferRangeTarget.AtomicCounterBuffer, 4, rayCounterBOs[currentInBuffer]);

                GL.BindBuffer(BufferTarget.AtomicCounterBuffer, rayCounterBOs[1 - currentInBuffer]);
                GL.BindBufferBase(BufferRangeTarget.AtomicCounterBuffer, 5, rayCounterBOs[1 - currentInBuffer]);
                GL.BufferData(BufferTarget.AtomicCounterBuffer, sizeof(uint), new uint[] { 0 }, BufferUsageHint.StaticDraw);

                GL.DispatchCompute(262144 / 64, 1, 1);
                GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);

                currentInBuffer = 1 - currentInBuffer;

                GL.UseProgram(shadingProgram);
                GL.DispatchCompute(262144 / 64, 1, 1);
                GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);
            }
            //Console.WriteLine("Elapsed 5: " + sw.ElapsedMilliseconds);

            sw.Stop();
            //Console.WriteLine("Elapsed final: " + sw.ElapsedMilliseconds);
        }

        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                GL.DeleteProgram(generateProgram);
                GL.DeleteProgram(bruteFirstHitProgram);
                GL.DeleteProgram(shadingProgram);

                disposedValue = true;
            }
        }

        ~GPURayTracer()
        {
            GL.DeleteProgram(generateProgram);
            GL.DeleteProgram(bruteFirstHitProgram);
            GL.DeleteProgram(shadingProgram);
        }

        public void Dispose()
        {
            for (int i = 0; i < 2; i++)
            {
                GL.DeleteBuffer(raySSBOs[i]);
            }
            GL.DeleteBuffer(shadowRaySSBO);
            for (int i = 0; i < 3; i++)
            {
                GL.DeleteBuffer(rayCounterBOs[i]);
            }
            GL.DeleteTexture(textureHandle);
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
